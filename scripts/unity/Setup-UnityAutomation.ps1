[CmdletBinding()]
param([string]$PythonPath)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$environmentPath = Join-Path $projectRoot '.tools/unity-mcp'
$python = Join-Path $environmentPath 'Scripts/python.exe'
if (-not (Test-Path -LiteralPath $python)) {
    if (-not $PythonPath) {
        $bundled = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
        if (Test-Path -LiteralPath $bundled) { $PythonPath = $bundled }
        else {
            $candidate = Get-Command python -ErrorAction SilentlyContinue
            if ($candidate -and $candidate.Source -notlike '*WindowsApps*') { $PythonPath = $candidate.Source }
        }
    }
    if (-not $PythonPath) { throw 'Pass -PythonPath with a Python 3.12 executable.' }
    & $PythonPath -c 'import sys; assert sys.version_info[:2] == (3,12), "Use Python 3.12 for the checked dependency lock"'
    if ($LASTEXITCODE -ne 0) { throw 'Python version check failed.' }
    & $PythonPath -m venv $environmentPath
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the project Python environment.' }
}
& $python -m pip install --disable-pip-version-check -r (Join-Path $PSScriptRoot 'requirements-lock.txt')
if ($LASTEXITCODE -ne 0) { throw 'Unity MCP dependency installation failed.' }
Write-Output 'Installed the pinned Unity MCP server and CLI. Next: Start-UnityAutomation.ps1'
