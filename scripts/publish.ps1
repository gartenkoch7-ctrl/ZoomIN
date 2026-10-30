#Requires -Version 5.1
<#
.SYNOPSIS
    Builds a release of ZoomIN (ADR-0009): self-contained win-x64 folder, ZIP and – if Inno Setup
    is installed – the per-user installer.
.EXAMPLE
    ./scripts/publish.ps1 -Version 1.0.0-rc1
#>
param(
    [string]$Version = '1.0.0-rc1',
    [string]$Output = 'artifacts'
)

$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel).Trim()
$out = Join-Path $root $Output
$publish = Join-Path $out 'publish\ZoomIN'

if (Test-Path $publish) {
    Remove-Item -Recurse -Force $publish
}

dotnet publish (Join-Path $root 'src\ZoomIN.App\ZoomIN.App.csproj') `
    -c Release -r win-x64 --self-contained -p:Platform=x64 -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed ($LASTEXITCODE)."
}

$zip = Join-Path $out "ZoomIN-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -Force
Write-Host "ZIP: $zip" -ForegroundColor Green

# Inno Setup is optional locally; the release workflow installs it.
$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    Write-Host 'Inno Setup not found: installer skipped (ZIP only).' -ForegroundColor Yellow
    exit 0
}

& $iscc "/DAppVersion=$Version" "/DSourceDir=$publish" "/DOutputDir=$out" (Join-Path $root 'installer\ZoomIN.iss')
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed ($LASTEXITCODE)."
}

Write-Host "Installer: $(Join-Path $out "ZoomIN-$Version-setup.exe")" -ForegroundColor Green
