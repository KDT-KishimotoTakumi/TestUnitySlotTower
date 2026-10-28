[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new()
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$failures = [System.Collections.Generic.List[string]]::new()
$passes = [System.Collections.Generic.List[string]]::new()

function Add-CheckResult {
    param([bool]$Passed, [string]$Message)
    if ($Passed) { $script:passes.Add($Message) } else { $script:failures.Add($Message) }
}

Push-Location $projectRoot
try {
    $requiredFiles = @(
        'AGENTS.md',
        'docs/ai/PROJECT_CONTEXT.md',
        'CLAUDE.md',
        'docs/ai/HANDOFF.md',
        'docs/ai/UNITY_CHECKS.md',
        'docs/ai/UNITY_AUTOMATION.md',
        'ProjectSettings/ProjectVersion.txt',
        'Packages/manifest.json',
        'Packages/packages-lock.json'
    )
    $missingRequired = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
    Add-CheckResult ($missingRequired.Count -eq 0) $(if ($missingRequired.Count -eq 0) { '必須ファイル' } else { "必須ファイル不足: $($missingRequired -join ', ')" })

    foreach ($jsonPath in @('Packages/manifest.json', 'Packages/packages-lock.json', '.claude/settings.json')) {
        try {
            $null = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
            Add-CheckResult $true "JSON: $jsonPath"
        } catch {
            Add-CheckResult $false "JSON不正: $jsonPath ($($_.Exception.Message))"
        }
    }

    $unityVersion = (Get-Content -LiteralPath 'ProjectSettings/ProjectVersion.txt' | Select-String '^m_EditorVersion:\s*(.+)$').Matches.Groups[1].Value
    Add-CheckResult ($unityVersion -eq '6000.6.7f1') "Unityバージョン: $unityVersion"

    $assetEntries = @(Get-ChildItem -LiteralPath 'Assets' -Recurse -Force | Where-Object {
        $_.Name -notlike '*.meta' -and $_.FullName -notmatch '[\\/]\.gitkeep$'
    })
    $missingMeta = @($assetEntries | Where-Object { -not (Test-Path -LiteralPath ($_.FullName + '.meta')) } | ForEach-Object {
        [IO.Path]::GetRelativePath($projectRoot, $_.FullName)
    })
    Add-CheckResult ($missingMeta.Count -eq 0) $(if ($missingMeta.Count -eq 0) { 'Unity .meta対応' } else { "対応する.metaなし: $($missingMeta -join ', ')" })

    # Unity serializes YAML with trailing spaces; check hand-written text only.
    $unityYaml = @(':(exclude)*.asset', ':(exclude)*.unity', ':(exclude)*.prefab', ':(exclude)*.mat', ':(exclude)*.anim', ':(exclude)*.controller')
    $diffCheck = (& git -c core.safecrlf=false diff --check -- . @unityYaml 2>&1 | Out-String).Trim()
    $diffCheckExit = $LASTEXITCODE
    $untrackedTextFiles = @(& git ls-files --others --exclude-standard | Where-Object {
        $_ -match '\.(asmdef|cs|json|md|ps1|py|shader|toml|txt|xml|ya?ml)$' -and
        (Test-Path -LiteralPath $_ -PathType Leaf) -and
        (Get-Item -LiteralPath $_).Length -le 1MB
    })
    $untrackedWhitespace = @($untrackedTextFiles | ForEach-Object {
        $path = $_
        Select-String -LiteralPath $path -Pattern '[ \t]+$' | ForEach-Object { "${path}:$($_.LineNumber)" }
    })
    $whitespacePassed = $diffCheckExit -eq 0 -and $untrackedWhitespace.Count -eq 0
    $whitespaceDetails = @($diffCheck, $untrackedWhitespace) | Where-Object { $_ }
    Add-CheckResult $whitespacePassed $(if ($whitespacePassed) { 'Git差分の空白エラー' } else { "Git差分の空白エラー: $($whitespaceDetails -join ', ')" })

    $secretPatterns = @(
        '-----BEGIN (RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----',
        'sk-[A-Za-z0-9_-]{20,}',
        'gh[pousr]_[A-Za-z0-9]{20,}',
        'AKIA[0-9A-Z]{16}',
        'xox[baprs]-[A-Za-z0-9-]{20,}'
    )
    $candidateFiles = @(& git ls-files --cached --others --exclude-standard | Where-Object {
        $_ -match '\.(asmdef|asset|cs|json|md|ps1|py|shader|toml|txt|xml|ya?ml)$' -and
        (Test-Path -LiteralPath $_ -PathType Leaf) -and
        (Get-Item -LiteralPath $_).Length -le 1MB
    })
    $secretHits = [System.Collections.Generic.List[string]]::new()
    foreach ($file in $candidateFiles) {
        foreach ($pattern in $secretPatterns) {
            $matches = @(Select-String -LiteralPath $file -Pattern $pattern -AllMatches)
            foreach ($match in $matches) {
                $secretHits.Add("${file}:$($match.LineNumber)")
            }
        }
    }
    $uniqueSecretHits = @($secretHits | Sort-Object -Unique)
    Add-CheckResult ($uniqueSecretHits.Count -eq 0) $(if ($uniqueSecretHits.Count -eq 0) { '代表的な秘密情報' } else { "秘密情報の可能性: $($uniqueSecretHits -join ', ')" })
} finally {
    Pop-Location
}

foreach ($item in $passes) { Write-Host "[PASS] $item" }
foreach ($item in $failures) { Write-Host "[FAIL] $item" -ForegroundColor Red }
Write-Host "結果: $($passes.Count)件成功 / $($failures.Count)件失敗"

if ($failures.Count -gt 0) { exit 1 }
