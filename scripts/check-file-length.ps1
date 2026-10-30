#Requires -Version 5.1
<#
.SYNOPSIS
    Enforces the architecture rule: code and test files have at most 300 lines (see AGENTS.md 2.1).
.EXAMPLE
    ./scripts/check-file-length.ps1
#>
param(
    [int]$MaxLines = 300
)

$ErrorActionPreference = 'Stop'

$extensions = @('.cs', '.xaml', '.hlsl', '.hlsli', '.ps1')
# Generated code is exempt: it is not written or reviewed by hand.
$excludePatterns = @('\.g\.cs$', '\.g\.i\.cs$', '\.Designer\.cs$', '(^|/)(bin|obj)/')

$root = (git rev-parse --show-toplevel).Trim()
$files = git -C $root ls-files --cached --others --exclude-standard

$violations = @()
foreach ($file in $files) {
    $ext = [IO.Path]::GetExtension($file).ToLowerInvariant()
    if ($extensions -notcontains $ext) { continue }
    if ($excludePatterns | Where-Object { $file -match $_ }) { continue }

    $path = Join-Path $root $file
    if (-not (Test-Path -LiteralPath $path)) { continue }

    # ReadAllLines counts blank lines too, unlike Measure-Object -Line.
    $count = [IO.File]::ReadAllLines($path).Length
    if ($count -gt $MaxLines) {
        $violations += [pscustomobject]@{ File = $file; Lines = $count }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Files exceed $MaxLines lines (AGENTS.md 2.1) - split them by responsibility:" -ForegroundColor Red
    $violations | Sort-Object Lines -Descending | Format-Table -AutoSize | Out-String | Write-Host
    exit 1
}

Write-Host "OK: all code and test files have <= $MaxLines lines." -ForegroundColor Green
exit 0
