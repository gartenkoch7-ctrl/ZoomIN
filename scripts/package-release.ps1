#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the release assets the setup and the in-app updater expect (ADR-0014).
.DESCRIPTION
    Input: the UIAccess folder from publish.ps1 -UiAccess (signed locally or by SignPath).
    Output in the artifacts folder:
      ZoomIN-<version>-win-x64-uiaccess.zip   package the setup installs to Program Files
      checksums.txt                           SHA-256 of every release asset (sha256sum format)
    The names must match ZoomIN.Core.Updates.ReleaseAssets, otherwise the release feed skips the
    release. An unsigned ZoomIN.exe is refused: Windows would not start it with UIAccess.
.EXAMPLE
    ./scripts/publish.ps1 -Version 1.0.0-dev.42 -UiAccess
    ./scripts/package-release.ps1 -Version 1.0.0-dev.42
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Output = 'artifacts',
    # Folder with the signed UIAccess build; defaults to publish.ps1's output.
    [string]$Source,
    # Local packaging only: publish-setup.ps1 does not sign; CI gets the setup signed by SignPath.
    [switch]$AllowUnsignedSetup
)

$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel).Trim()
$out = Join-Path $root $Output
if (-not $Source) {
    $Source = Join-Path $out 'publish\ZoomIN-uiaccess'
}

$exe = Join-Path $Source 'ZoomIN.exe'
if (-not (Test-Path $exe)) {
    throw "No UIAccess build in $Source. Run ./scripts/publish.ps1 -UiAccess first."
}

$signature = Get-AuthenticodeSignature $exe
if ($signature.Status -ne 'Valid') {
    throw "ZoomIN.exe is not validly signed ($($signature.Status)); the package would never start."
}

# The app's "install signed version" button refuses an unsigned setup (ADR-0017), and SmartScreen warns.
$setup = Join-Path $out 'ZoomIN-Setup.exe'
if ((Test-Path $setup) -and -not $AllowUnsignedSetup -and (Get-AuthenticodeSignature $setup).Status -ne 'Valid') {
    throw "ZoomIN-Setup.exe is not validly signed. Sign it, or pass -AllowUnsignedSetup for a local package."
}

$package = Join-Path $out "ZoomIN-$Version-win-x64-uiaccess.zip"
if (Test-Path $package) {
    Remove-Item $package -Force
}
Compress-Archive -Path (Join-Path $Source '*') -DestinationPath $package
Write-Host "Package: $package" -ForegroundColor Green

# Every downloadable asset gets a line, so users can also check the portable ZIP by hand.
$assets = Get-ChildItem $out -File | Where-Object { $_.Extension -in '.zip', '.exe' } | Sort-Object Name
$lines = foreach ($asset in $assets) {
    $hash = (Get-FileHash $asset.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($asset.Name)"
}

# No BOM and LF endings: the format of sha256sum, which ChecksumList reads.
$checksums = Join-Path $out 'checksums.txt'
[IO.File]::WriteAllText($checksums, (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
Write-Host "Checksums: $checksums ($($assets.Count) files)" -ForegroundColor Green
