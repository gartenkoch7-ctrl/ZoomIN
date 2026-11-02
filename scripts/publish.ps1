#Requires -Version 5.1
<#
.SYNOPSIS
    Builds a release of ZoomIN (ADR-0009): self-contained win-x64 folder, ZIP and – if Inno Setup
    is installed – the per-user installer.
.DESCRIPTION
    With -UiAccess (ADR-0012) it builds the installer variant instead: uiAccess manifest, ZoomIN.exe
    signed with the certificate from scripts/setup-signing.ps1, installed to Program Files (asks for
    admin rights once). Only this variant draws the zoom above Start, quick settings and the
    notification center. No ZIP for it: a uiAccess exe does not start outside Program Files.
.EXAMPLE
    ./scripts/publish.ps1 -Version 1.0.0-rc1
    ./scripts/publish.ps1 -Version 1.0.0-rc1 -UiAccess
#>
param(
    [string]$Version = '1.0.0-rc1',
    [string]$Output = 'artifacts',
    [switch]$UiAccess,
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

$uiAccessFlag = if ($UiAccess) { 'true' } else { 'false' }
dotnet publish (Join-Path $root 'src\ZoomIN.App\ZoomIN.App.csproj') `
    -c Release -r win-x64 --self-contained -p:Platform=x64 -p:Version=$Version `
    -p:ZoomInUiAccess=$uiAccessFlag -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed ($LASTEXITCODE)."
}

if ($UiAccess) {
    # Windows checks the signature of the exe itself before granting UIAccess.
    $exe = Join-Path $publish 'ZoomIN.exe'
    $signature = Set-AuthenticodeSignature -FilePath $exe -Certificate (Get-SigningCertificate) -HashAlgorithm SHA256
    if ($signature.Status -ne 'Valid') {
        throw "Signing ZoomIN.exe failed ($($signature.Status): $($signature.StatusMessage)). Is the certificate trusted (scripts/setup-signing.ps1)?"
    }
    Write-Host "Signed: $exe" -ForegroundColor Green
}
else {
    $zip = Join-Path $out "ZoomIN-$Version-win-x64.zip"
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -Force
    Write-Host "ZIP: $zip" -ForegroundColor Green
}

# Inno Setup is optional locally; the release workflow installs it.
$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    if ($UiAccess) {
        # The exe only starts from Program Files; install-uiaccess.ps1 copies it there.
        Write-Host 'Inno Setup not found: installer skipped. Next, as administrator: ./scripts/install-uiaccess.ps1' -ForegroundColor Yellow
        exit 0
    }
    Write-Host 'Inno Setup not found: installer skipped (ZIP only).' -ForegroundColor Yellow
    exit 0
}

$isccArgs = @("/DAppVersion=$Version", "/DSourceDir=$publish", "/DOutputDir=$out")
if ($UiAccess) {
    $isccArgs += '/DUiAccess=1'
}
& $iscc @isccArgs (Join-Path $root 'installer\ZoomIN.iss')
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed ($LASTEXITCODE)."
}

$setupName = if ($UiAccess) { "ZoomIN-$Version-setup-uiaccess.exe" } else { "ZoomIN-$Version-setup.exe" }
Write-Host "Installer: $(Join-Path $out $setupName)" -ForegroundColor Green
