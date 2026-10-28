[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$marker = Join-Path $projectRoot 'UserSettings/SlotTowerAutomation.enabled'
$request = Join-Path $projectRoot 'UserSettings/SlotTowerAutomation.disconnect'
$acknowledgement = Join-Path $projectRoot 'UserSettings/SlotTowerAutomation.disconnected'
$pidFile = Join-Path $projectRoot 'Logs/Automation/server.pid'
if (-not (Test-Path -LiteralPath $pidFile)) {
    if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
    Write-Output 'No managed Unity MCP server PID. Unity Editor remains open.'
    return
}
$recordedPid = 0
if (-not [int]::TryParse((Get-Content -LiteralPath $pidFile -Raw).Trim(), [ref]$recordedPid) -or $recordedPid -le 0) {
    throw "Invalid server PID file: $pidFile"
}
$server = [IO.Path]::GetFullPath((Join-Path $projectRoot '.tools/unity-mcp/Scripts/mcp-for-unity.exe'))
$process = Get-CimInstance Win32_Process -Filter "ProcessId=$recordedPid"
if (-not $process) {
    if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
    Remove-Item -LiteralPath $pidFile
    Write-Output 'Managed Unity MCP server has already exited. Unity Editor remains open.'
    return
}
if (-not $process.ExecutablePath -or
    -not $process.ExecutablePath.Equals($server, [StringComparison]::OrdinalIgnoreCase) -or
    $process.CreationDate -gt (Get-Item -LiteralPath $pidFile).LastWriteTime.AddSeconds(2) -or
    $process.CommandLine -notmatch '--transport\s+http' -or
    $process.CommandLine -notmatch '--http-url\s+http://127\.0\.0\.1:8080' -or
    $process.CommandLine -notmatch '--default-instance\s+UnitySlotTower') {
    throw "PID $recordedPid does not match this project's Unity MCP server; left it running."
}
if (Test-Path -LiteralPath $acknowledgement) { Remove-Item -LiteralPath $acknowledgement }
New-Item -ItemType File -Path $request -Force | Out-Null
if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
try {
    $editor = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        $_.CommandLine -match '-projectPath\s+(?:"([^\"]+)"|(\S+))' -and
        [IO.Path]::GetFullPath($(if ($Matches[1]) { $Matches[1] } else { $Matches[2] })).TrimEnd('\','/').Equals($projectRoot, [StringComparison]::OrdinalIgnoreCase) -and
        $_.CommandLine -notmatch '-batchMode'
    }
    if ($editor) {
        $acknowledged = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            if (Test-Path -LiteralPath $acknowledgement) { $acknowledged = $true; break }
            Start-Sleep -Seconds 1
        }
        if (-not $acknowledged) { throw 'Unity Editor did not acknowledge disconnection within 30 seconds; server left running. Retry after compilation finishes.' }
    }
} finally {
    if (Test-Path -LiteralPath $request) { Remove-Item -LiteralPath $request }
    if (Test-Path -LiteralPath $acknowledgement) { Remove-Item -LiteralPath $acknowledgement }
}
Stop-Process -Id $recordedPid -ErrorAction Stop
Remove-Item -LiteralPath $pidFile
Write-Output 'Stopped managed Unity MCP server. Unity Editor remains open.'
