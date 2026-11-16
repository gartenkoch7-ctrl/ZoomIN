#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the setup program as one file: artifacts\ZoomIN-Setup.exe (M9, ADR-0014).
.DESCRIPTION
    Self-contained single file (~67 MB): a fresh PC has neither .NET nor the Windows App SDK.
    The setup downloads the chosen ZoomIN version itself, so it does not contain the app.
    Run before package-release.ps1, which adds it to checksums.txt.
.EXAMPLE
    ./scripts/publish-setup.ps1 -Version 1.0.0-dev.42
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Output = 'artifacts'
)

$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel).Trim()
$out = Join-Path $root $Output
$publish = Join-Path $out 'publish\ZoomIN-Setup'
if (Test-Path $publish) {
    Remove-Item -Recurse -Force $publish
}

dotnet publish (Join-Path $root 'src\ZoomIN.Setup\ZoomIN.Setup.csproj') `
    -c Release -r win-x64 --self-contained -p:Platform=x64 -p:Version=$Version `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed ($LASTEXITCODE)."
}

$setup = Join-Path $out 'ZoomIN-Setup.exe'
Copy-Item (Join-Path $publish 'ZoomIN-Setup.exe') $setup -Force
Write-Host "Setup: $setup" -ForegroundColor Green
