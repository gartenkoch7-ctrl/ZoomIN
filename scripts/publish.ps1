#Requires -Version 5.1
<#
.SYNOPSIS
    Builds ZoomIN (ADR-0014): self-contained win-x64 folder and the portable ZIP.
.DESCRIPTION
    With -UiAccess (ADR-0012) it builds the installed variant instead: uiAccess manifest, ZoomIN.exe
    signed with the certificate from scripts/setup-signing.ps1. Only this variant draws the zoom
    above Start, quick settings and the notification center, and only from Program Files, so there
    is no ZIP for it: ZoomIN-Setup.exe (publish-setup.ps1) or install-uiaccess.ps1 install it.
.EXAMPLE
    ./scripts/publish.ps1 -Version 1.0.0-rc1
    ./scripts/publish.ps1 -Version 1.0.0-rc1 -UiAccess
    ./scripts/publish.ps1 -Version 1.0.0-dev.42 -UiAccess -Unsigned   # CI, before SignPath
    Then ./scripts/package-release.ps1 builds the release assets (ADR-0014).
#>
param(
    [string]$Version = '1.0.0-rc1',
    [string]$Output = 'artifacts',
    [switch]$UiAccess,
    # With -UiAccess: skip local signing, for CI where SignPath signs afterwards (ADR-0014).
    [switch]$Unsigned,
    # Defaults to the certificate created by scripts/setup-signing.ps1.
    [string]$CertificateSubject = 'CN=ZoomIN Local Code Signing'
)

$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel).Trim()
$out = Join-Path $root $Output
$publish = Join-Path $out ($(if ($UiAccess) { 'publish\ZoomIN-uiaccess' } else { 'publish\ZoomIN' }))

function Get-SigningCertificate {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.Subject -eq $CertificateSubject -and $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
    if (-not $cert) {
        throw "No code-signing certificate '$CertificateSubject'. Run scripts/setup-signing.ps1 as administrator first."
    }
    return $cert
}

if (Test-Path $publish) {
    Remove-Item -Recurse -Force $publish
}

function Publish-App([string]$Flag, [string]$Folder) {
    dotnet publish (Join-Path $root 'src\ZoomIN.App\ZoomIN.App.csproj') `
        -c Release -r win-x64 --self-contained -p:Platform=x64 -p:Version=$Version `
        -p:ZoomInUiAccess=$Flag -o $Folder
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed ($LASTEXITCODE)."
    }
}

# ZoomIN.Mcp.exe (ADR-0027): the same program without the uiAccess manifest. An MCP client starts
# it with redirected stdin/stdout, and CreateProcess refuses UIAccess exes (ERROR_ELEVATION_REQUIRED,
# ADR-0012). The app host only names ZoomIN.dll, so a renamed copy of the plain host runs the same app.
$plainHost = Join-Path $out 'publish\ZoomIN-mcp-host'
if ($UiAccess) {
    if (Test-Path $plainHost) {
        Remove-Item -Recurse -Force $plainHost
    }

    Publish-App 'false' $plainHost
    Publish-App 'true' $publish
    Copy-Item (Join-Path $plainHost 'ZoomIN.exe') (Join-Path $publish 'ZoomIN.Mcp.exe')
    Remove-Item -Recurse -Force $plainHost
}
else {
    Publish-App 'false' $publish
    Copy-Item (Join-Path $publish 'ZoomIN.exe') (Join-Path $publish 'ZoomIN.Mcp.exe')
}

if ($UiAccess -and $Unsigned) {
    # CI: the folder goes to SignPath first; package-release.ps1 zips it once it comes back signed.
    Write-Host "Unsigned UIAccess build (does not start until signed): $publish" -ForegroundColor Yellow
    exit 0
}
elseif ($UiAccess) {
    # Windows checks the signature of the exe itself before granting UIAccess. ZoomIN.Mcp.exe
    # needs none for that, but is signed so it carries the same publisher.
    $certificate = Get-SigningCertificate
    foreach ($name in 'ZoomIN.exe', 'ZoomIN.Mcp.exe') {
        $exe = Join-Path $publish $name
        $signature = Set-AuthenticodeSignature -FilePath $exe -Certificate $certificate -HashAlgorithm SHA256
        if ($signature.Status -ne 'Valid') {
            throw "Signing $name failed ($($signature.Status): $($signature.StatusMessage)). Is the certificate trusted (scripts/setup-signing.ps1)?"
        }
        Write-Host "Signed: $exe" -ForegroundColor Green
    }
}
else {
    $zip = Join-Path $out "ZoomIN-$Version-win-x64.zip"
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -Force
    Write-Host "ZIP: $zip" -ForegroundColor Green
}

if ($UiAccess) {
    # The exe only starts from Program Files; install-uiaccess.ps1 copies it there.
    Write-Host "Next, as administrator: ./scripts/install-uiaccess.ps1" -ForegroundColor Yellow
}
