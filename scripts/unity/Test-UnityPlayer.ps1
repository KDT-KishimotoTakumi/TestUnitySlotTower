[CmdletBinding()]
param([string]$PlayerPath = 'Builds/Windows/UnitySlotTower.exe', [int]$TimeoutSeconds = 90, [switch]$ShowWindow)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not [IO.Path]::IsPathRooted($PlayerPath)) { $PlayerPath = Join-Path $projectRoot $PlayerPath }
if (-not (Test-Path -LiteralPath $PlayerPath)) { throw "Player does not exist: $PlayerPath" }
$runDirectory = Join-Path $projectRoot ('Artifacts/Automation/Player/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$screenshot = Join-Path $runDirectory 'player.png'
$playerLog = Join-Path $runDirectory 'player.log'
$windowStyle = if ($ShowWindow) { 'Normal' } else { 'Hidden' }
$player = Start-Process -FilePath $PlayerPath -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"' + $playerLog + '"'),'-automationScreenshot',('"' + $screenshot + '"')) -WorkingDirectory (Split-Path $PlayerPath) -WindowStyle $windowStyle -PassThru
$completed = $player.WaitForExit($TimeoutSeconds * 1000)
if (-not $completed) {
    # Only stop the process started by this verification run.
    $player.Kill()
    $player.WaitForExit()
}
$pngValid = $false
if (Test-Path -LiteralPath $screenshot) {
    $bytes = [IO.File]::ReadAllBytes($screenshot)
    $pngValid = $bytes.Length -gt 24 -and [BitConverter]::ToString($bytes,0,8) -eq '89-50-4E-47-0D-0A-1A-0A'
}
$result = [ordered]@{ completed=$completed; exitCode=$player.ExitCode; pngValid=$pngValid; screenshot=$screenshot; log=$playerLog; player=$PlayerPath }
$result | ConvertTo-Json | Set-Content (Join-Path $runDirectory 'result.json') -Encoding UTF8
$result | ConvertTo-Json
if (-not $completed -or $player.ExitCode -ne 0 -or -not $pngValid) { throw "Player verification failed. See $runDirectory" }
