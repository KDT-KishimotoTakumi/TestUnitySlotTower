[CmdletBinding()]
param([switch]$ServerOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$server = Join-Path $projectRoot '.tools/unity-mcp/Scripts/mcp-for-unity.exe'
if (-not (Test-Path -LiteralPath $server)) { throw 'Run Setup-UnityAutomation.ps1 first.' }
$logDirectory = Join-Path $projectRoot 'Logs/Automation'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$stdinFile = Join-Path $logDirectory 'empty.stdin'
if (-not (Test-Path -LiteralPath $stdinFile)) { New-Item -ItemType File -Path $stdinFile | Out-Null }
try { $health = Invoke-RestMethod 'http://127.0.0.1:8080/health' -TimeoutSec 3 } catch { $health = $null }
if (-not $health) {
    if (Get-NetTCPConnection -State Listen -LocalPort 8080 -ErrorAction SilentlyContinue) {
        throw 'Port 8080 is occupied by a service that did not pass the MCP health check.'
    }
    $env:UNITY_MCP_TELEMETRY_ENABLED = 'false'
    $serverProcess = Start-Process -FilePath $server -ArgumentList @('--transport','http','--http-url','http://127.0.0.1:8080','--default-instance','UnitySlotTower') -WorkingDirectory $projectRoot -WindowStyle Hidden -RedirectStandardInput $stdinFile -RedirectStandardOutput (Join-Path $logDirectory 'server.stdout.log') -RedirectStandardError (Join-Path $logDirectory 'server.stderr.log') -PassThru
    $serverProcess.Id | Set-Content (Join-Path $logDirectory 'server.pid')
    $ready = $false
    for ($attempt = 0; $attempt -lt 15; $attempt++) {
        Start-Sleep -Seconds 1
        try { $health = Invoke-RestMethod 'http://127.0.0.1:8080/health' -TimeoutSec 1; $ready = $true; break } catch { }
    }
    if (-not $ready) { throw 'MCP server did not start. Check Logs/Automation/server.stderr.log.' }
}
# The Editor only connects while this project has explicitly requested automation.
$settings = Join-Path $projectRoot 'UserSettings'
if (-not (Test-Path -LiteralPath $settings)) { New-Item -ItemType Directory -Path $settings -Force | Out-Null }
foreach ($name in @('SlotTowerAutomation.disconnect', 'SlotTowerAutomation.disconnected')) {
    $staleRequest = Join-Path $settings $name
    if (Test-Path -LiteralPath $staleRequest) { Remove-Item -LiteralPath $staleRequest }
}
New-Item -ItemType File -Path (Join-Path $settings 'SlotTowerAutomation.enabled') -Force | Out-Null
if (-not $ServerOnly) {
    $versionLine = Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }
    $version = ($versionLine -split ': ', 2)[1]
    $editor = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
    if (-not (Test-Path -LiteralPath $editor)) { throw "Unity $version was not found at $editor" }
    $running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        if ($_.CommandLine -match '-projectPath\s+(?:"([^"]+)"|(\S+))') {
            $openedPath = if ($Matches[1]) { $Matches[1] } else { $Matches[2] }
            [IO.Path]::GetFullPath($openedPath).TrimEnd('\','/').Equals($projectRoot, [StringComparison]::OrdinalIgnoreCase)
        } else { $false }
    }
    if (-not $running) {
        # Unity must not inherit the caller's output pipes: otherwise an automation
        # command can stay open after this script has printed its final message.
        Start-Process -FilePath $editor -ArgumentList @('-projectPath', ('"' + $projectRoot + '"'), '-logFile', ('"' + (Join-Path $logDirectory 'editor.log') + '"')) -WindowStyle Hidden -RedirectStandardInput $stdinFile -RedirectStandardOutput (Join-Path $logDirectory 'editor.stdout.log') -RedirectStandardError (Join-Path $logDirectory 'editor.stderr.log') | Out-Null
    }
}
Write-Output 'MCP server healthy at http://127.0.0.1:8080/mcp. Unity connection is not yet verified; check the instance list separately with a timeout.'
