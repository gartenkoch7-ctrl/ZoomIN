#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs the signed UIAccess build (ADR-0012) to Program Files on THIS PC without Inno Setup.
.DESCRIPTION
    Windows grants UIAccess only to a signed exe in a protected folder. Only then does the overlay
    lie above quick settings (sound, network, Bluetooth), Start and the notification center.
    Order:
      1. ./scripts/setup-signing.ps1            (once, as administrator)
      2. ./scripts/publish.ps1 -UiAccess         (as normal user, signs ZoomIN.exe)
      3. ./scripts/install-uiaccess.ps1          (as administrator, this script)
    The script stops a running ZoomIN, copies the build, adds a Start menu entry and starts
    ZoomIN again as the normal user (never elevated). Undo: run with -Remove.
#>
param(
    [string]$Source,
    [string]$Destination = (Join-Path $env:ProgramFiles 'ZoomIN'),
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$shortcut = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\ZoomIN.lnk'

function Stop-ZoomIN {
    # App and watchdog share the image name; stopping both prevents a restart mid-copy.
    Get-Process ZoomIN -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

if ($Remove) {
    Stop-ZoomIN
    Remove-Item $Destination -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $shortcut -Force -ErrorAction SilentlyContinue
    Write-Host "ZoomIN removed from $Destination." -ForegroundColor Green
    exit 0
}

if (-not $Source) {
    $Source = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\publish\ZoomIN-uiaccess'
}

$sourceExe = Join-Path $Source 'ZoomIN.exe'
if (-not (Test-Path $sourceExe)) {
    throw "No UIAccess build in $Source. Run ./scripts/publish.ps1 -UiAccess first."
}

# Without a valid signature Windows refuses to start the exe ("A referral was returned").
$signature = Get-AuthenticodeSignature $sourceExe
if ($signature.Status -ne 'Valid') {
    throw "ZoomIN.exe is not validly signed ($($signature.Status)). Run setup-signing.ps1, then publish.ps1 -UiAccess."
}

Stop-ZoomIN
if (Test-Path $Destination) {
    Remove-Item $Destination -Recurse -Force
}
Copy-Item $Source $Destination -Recurse
Write-Host "Installed to $Destination" -ForegroundColor Green

$exe = Join-Path $Destination 'ZoomIN.exe'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $Destination
$link.Save()
Write-Host "Start menu entry: $shortcut"

# Started through Explorer so ZoomIN runs as the signed-in user, not elevated like this script.
Start-Process explorer.exe -ArgumentList "`"$exe`""
Write-Host 'ZoomIN started (UIAccess).' -ForegroundColor Green
