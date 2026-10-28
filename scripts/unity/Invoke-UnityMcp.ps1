[CmdletBinding()]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$McpArguments)
$ErrorActionPreference = 'Stop'
$env:PYTHONIOENCODING = 'utf-8'
$env:PYTHONUTF8 = '1'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$python = Join-Path $projectRoot '.tools/unity-mcp/Scripts/python.exe'
if (-not (Test-Path -LiteralPath $python)) { throw 'Run Setup-UnityAutomation.ps1 first.' }
# The runner starts once, bounds readiness/operation waits, and preserves stdout JSON.
& $python -B (Join-Path $PSScriptRoot 'unity_mcp_runner.py') @McpArguments
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) { throw "Unity MCP failed with exit code $exitCode" }
