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
    Layout (ADR-0015), the same the in-app updater maintains:
      Program Files\ZoomIN\app-<version>\   one folder per version
      Program Files\ZoomIN\current          directory link to the active version
    The script stops a running ZoomIN, copies the build into its version folder, points "current"
    at it, adds a Start menu entry and starts ZoomIN again as the normal user (never elevated).
    An old flat installation (ZoomIN.exe directly in Program Files\ZoomIN) is replaced.
    Undo: run with -Remove.
#>
param(
    [string]$Source,
    [string]$Destination = (Join-Path $env:ProgramFiles 'ZoomIN'),
    # Defaults to the product version stamped into ZoomIN.exe by publish.ps1 -Version.
    [string]$Version,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$shortcut = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\ZoomIN.lnk'
$current = Join-Path $Destination 'current'

function Stop-ZoomIN {
    # App and watchdog share the image name; stopping both prevents a restart mid-copy.
    Get-Process ZoomIN -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

function Remove-Link([string]$Path) {
    # Delete() on a directory link removes the link only, never the folder it points to.
    $item = Get-Item $Path -Force -ErrorAction SilentlyContinue
    if ($item) { $item.Delete() }
}

if ($Remove) {
    Stop-ZoomIN
    Remove-Link $current
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

if (-not $Version) {
    # "1.0.0-dev.99+commit" -> "1.0.0-dev.99", the name the updater gives the folder.
    $Version = ((Get-Item $sourceExe).VersionInfo.ProductVersion -split '\+')[0]
}

Stop-ZoomIN
if (Test-Path (Join-Path $Destination 'ZoomIN.exe')) {
    Write-Host 'Replacing the old flat installation.'
    Remove-Item $Destination -Recurse -Force
}

$target = Join-Path $Destination "app-$Version"
New-Item -ItemType Directory -Force $Destination | Out-Null
if (Test-Path $target) {
    Remove-Item $target -Recurse -Force
}
Copy-Item $Source $target -Recurse
Remove-Link $current
New-Item -ItemType SymbolicLink -Path $current -Target $target | Out-Null
Write-Host "Installed $Version to $target (current -> app-$Version)" -ForegroundColor Green

$exe = Join-Path $current 'ZoomIN.exe'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $current
$link.Save()
Write-Host "Start menu entry: $shortcut"

# Started through Explorer so ZoomIN runs as the signed-in user, not elevated like this script.
Start-Process explorer.exe -ArgumentList "`"$exe`""
Write-Host 'ZoomIN started (UIAccess).' -ForegroundColor Green
