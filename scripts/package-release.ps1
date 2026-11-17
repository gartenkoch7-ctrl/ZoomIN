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
    release. Signed by SignPath or locally; with -AllowUnsigned (no public certificate, ADR-0031)
    the setup signs ZoomIN on each PC, so an unsigned ZoomIN.exe and setup are packaged too.
.EXAMPLE
    ./scripts/publish.ps1 -Version 1.0.0-dev.42 -UiAccess
    ./scripts/package-release.ps1 -Version 1.0.0-dev.42
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Output = 'artifacts',
    # Folder with the UIAccess build (signed if a certificate was available); defaults to publish.ps1's output.
    [string]$Source,
    # Local packaging only: publish-setup.ps1 does not sign; CI gets the setup signed by SignPath.
    [switch]$AllowUnsignedSetup,
    # Release without a public certificate (ADR-0031): ZoomIN.exe and the setup stay unsigned.
    [switch]$AllowUnsigned
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
if ($signature.Status -ne 'Valid' -and -not ($AllowUnsigned -and $signature.Status -eq 'NotSigned')) {
    throw "ZoomIN.exe is not validly signed ($($signature.Status)); the package would never start."
}

# Unsigned setups make SmartScreen warn; a broken signature would make every install refuse it.
$setup = Join-Path $out 'ZoomIN-Setup.exe'
if (Test-Path $setup) {
    $setupStatus = (Get-AuthenticodeSignature $setup).Status
    $unsignedAllowed = ($AllowUnsignedSetup -or $AllowUnsigned) -and $setupStatus -eq 'NotSigned'
    if ($setupStatus -ne 'Valid' -and -not $unsignedAllowed) {
        throw "ZoomIN-Setup.exe is not validly signed ($setupStatus). Sign it, or pass -AllowUnsigned / -AllowUnsignedSetup for an unsigned one."
    }
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
