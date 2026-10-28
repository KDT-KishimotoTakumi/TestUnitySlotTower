[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$invoke = Join-Path $PSScriptRoot 'Invoke-UnityMcp.ps1'
$result = [ordered]@{ success = $false; instances = $null; scene = $null; console = $null; stopped = $false }
try {
    # scene active waits for readiness; instance list alone can legitimately be empty during startup.
    $scene = (& $invoke scene active) -join "`n" | ConvertFrom-Json
    $result.scene = $scene.result
    $result.instances = (& $invoke instance list) -join "`n"
    $console = (& $invoke editor console --type error --count 10) -join "`n" | ConvertFrom-Json
    $result.console = $console.result
    $result.success = $true
} finally {
    & (Join-Path $PSScriptRoot 'Stop-UnityAutomation.ps1') | Out-Host
    if (Get-NetTCPConnection -State Listen -LocalPort 8080 -ErrorAction SilentlyContinue) {
        throw 'Port 8080 remains open; shutdown is not verified. An external server is not terminated by this check.'
    }
    $result.stopped = $true
}
$result | ConvertTo-Json -Depth 12
