#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    One-time setup on THIS PC for the UIAccess installer build (ADR-0012): creates a self-signed
    code-signing certificate and makes Windows trust it.
.DESCRIPTION
    Windows only grants UIAccess (overlay above Start, quick settings and notification center)
    to exes signed with a certificate the machine trusts. This script
      1. creates "CN=ZoomIN Local Code Signing" in CurrentUser\My (reused if it already exists),
      2. adds its public part to LocalMachine\Root and LocalMachine\TrustedPublisher.
    Step 2 changes what the PC trusts, hence administrator rights. The private key never leaves
    CurrentUser\My; only exes signed by you on this PC become trusted.
    Undo: run with -Remove.
.EXAMPLE
    ./scripts/setup-signing.ps1
    ./scripts/publish.ps1 -Version 1.0.0-rc1 -UiAccess
#>
param(
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$subject = 'CN=ZoomIN Local Code Signing'
$trustStores = @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPublisher')

function Find-SigningCertificate {
    Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

if ($Remove) {
    foreach ($store in $trustStores + @('Cert:\CurrentUser\My')) {
        Get-ChildItem $store | Where-Object { $_.Subject -eq $subject } | Remove-Item
    }
    Write-Host 'ZoomIN signing certificate removed from this PC.' -ForegroundColor Green
    exit 0
}

$cert = Find-SigningCertificate
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
        -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy NonExportable `
        -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears(5)
    Write-Host "Certificate created: $($cert.Thumbprint)" -ForegroundColor Green
}
else {
    Write-Host "Certificate already present: $($cert.Thumbprint)"
}

# Only the public certificate goes into the machine stores.
$cer = Join-Path ([IO.Path]::GetTempPath()) 'ZoomIN-signing.cer'
Export-Certificate -Cert $cert -FilePath $cer | Out-Null
try {
    foreach ($store in $trustStores) {
        if (-not (Get-ChildItem $store | Where-Object { $_.Thumbprint -eq $cert.Thumbprint })) {
            Import-Certificate -FilePath $cer -CertStoreLocation $store | Out-Null
            Write-Host "Trusted in $store"
        }
    }
}
finally {
    Remove-Item $cer -ErrorAction SilentlyContinue
}

Write-Host 'Done. Next: ./scripts/publish.ps1 -UiAccess' -ForegroundColor Green
