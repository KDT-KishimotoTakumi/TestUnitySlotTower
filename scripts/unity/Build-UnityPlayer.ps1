[CmdletBinding()]
param([int]$TimeoutSeconds = 1200)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$invokeMcp = Join-Path $PSScriptRoot 'Invoke-UnityMcp.ps1'
$runDirectory = Join-Path $projectRoot ('Artifacts/Automation/Build/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$buildPath = Join-Path $projectRoot 'Builds/Windows/UnitySlotTower.exe'
$parameters = @{action='build'; target='windows64'; output_path=$buildPath; development=$true; options=@('strict_mode','detailed_report')} | ConvertTo-Json -Compress
$envelope = (& $invokeMcp raw manage_build $parameters) -join "`n" | ConvertFrom-Json
$response = $envelope.result
if ($envelope.status -ne 'success' -or -not $response) { throw ($envelope | ConvertTo-Json -Depth 8 -Compress) }
$response | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $runDirectory 'request-result.json') -Encoding UTF8
if ($response.success -eq $false) { throw ($response | ConvertTo-Json -Depth 8 -Compress) }
$jobId = $response.data.job_id
if (-not $jobId) { throw "Build did not return a job id. See $runDirectory" }
Write-Host "Build started: $jobId"
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 10
    try {
        $statusParameters = @{action='status'; job_id=$jobId} | ConvertTo-Json -Compress
        $envelope = (& $invokeMcp raw manage_build $statusParameters) -join "`n" | ConvertFrom-Json
        if ($envelope.status -ne 'success' -or -not $envelope.result) { throw 'Build status is unavailable.' }
        $status = $envelope.result
    } catch {
        # Unity's main thread may be unavailable while BuildPipeline runs.
        Write-Host 'Waiting for Unity to finish the build...'
        continue
    }
    $status | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $runDirectory 'build-result.json') -Encoding UTF8
    if ($status.data.result -eq 'succeeded') {
        if (-not (Test-Path -LiteralPath $buildPath)) { throw 'Build reported success but the executable is missing.' }
        $status | ConvertTo-Json -Depth 12
        return
    }
    if ($status.data.result -in @('failed','cancelled','skipped') -or $status.success -eq $false) {
        throw "Build failed. See $runDirectory/build-result.json"
    }
}
throw "Build status timed out. The Editor was not terminated; inspect job $jobId before retrying. Results: $runDirectory"
