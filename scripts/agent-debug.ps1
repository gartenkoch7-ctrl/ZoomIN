<#
.SYNOPSIS
    Calls one KI-Debug tool of the running ZoomIN through ZoomIN.Mcp.exe (ADR-0027), without an MCP client.
.DESCRIPTION
    Starts the MCP role, sends initialize + tools/call over stdin and prints the text of the answer.
    -Arguments is a JSON object. Images are saved as PNG files into -Out. Needs "Allow KI-Debug" on in the running ZoomIN.
.EXAMPLE
    ./scripts/agent-debug.ps1 get_state
    ./scripts/agent-debug.ps1 take_snapshot -Arguments '{"images":"all","max_width":1280}' -Out TestResults/snap
    ./scripts/agent-debug.ps1 run_shortcut -Arguments '{"shortcut":"toggle_filter"}'
#>
param(
    [Parameter(Mandatory)][string]$Tool,
    [string]$Arguments = '{}',
    [string]$Out = 'TestResults/agent-debug',
    # Never the installed ZoomIN.exe: it has UIAccess, and Windows refuses to start it with redirected stdio.
    [string]$Exe = 'C:\Program Files\ZoomIN\current\ZoomIN.Mcp.exe'
)

$ErrorActionPreference = 'Stop'
$info = [System.Diagnostics.ProcessStartInfo]::new($Exe, '--mcp')
$info.RedirectStandardInput = $true
$info.RedirectStandardOutput = $true
$info.UseShellExecute = $false
$info.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
$process = [System.Diagnostics.Process]::Start($info)

function Send($message) {
    $process.StandardInput.WriteLine(($message | ConvertTo-Json -Depth 10 -Compress))
    $process.StandardInput.Flush()
}

function Receive($id) {
    while ($true) {
        $line = $process.StandardOutput.ReadLine()
        if ($null -eq $line) { throw 'The MCP role ended without an answer.' }
        $message = $line | ConvertFrom-Json
        if ($message.id -eq $id) { return $message }
    }
}

try {
    Send @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{ protocolVersion = '2025-06-18'; capabilities = @{}; clientInfo = @{ name = 'agent-debug.ps1'; version = '1' } } }
    $null = Receive 1
    Send @{ jsonrpc = '2.0'; method = 'notifications/initialized' }
    Send @{ jsonrpc = '2.0'; id = 2; method = 'tools/call'; params = @{ name = $Tool; arguments = ($Arguments | ConvertFrom-Json) } }
    $answer = Receive 2
    if ($answer.error) { throw "MCP error: $($answer.error.message)" }

    $name = 'image'
    foreach ($block in $answer.result.content) {
        if ($block.type -eq 'text') {
            Write-Output $block.text
            $name = $block.text
        }
        elseif ($block.type -eq 'image') {
            New-Item -ItemType Directory -Force $Out | Out-Null
            $file = Join-Path $Out (($name -replace '[^A-Za-z0-9_-]', '_') + '.png')
            [System.IO.File]::WriteAllBytes((Join-Path (Get-Location) $file), [Convert]::FromBase64String($block.data))
            Write-Output "saved $file"
        }
    }

    if ($answer.result.isError) { exit 1 }
}
finally {
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(3000)) { $process.Kill() }
}
