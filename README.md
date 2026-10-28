# UnitySlotTower

Unity 6.6 (6000.6.7f1) / URP 3D のゲームプロジェクト。

## セットアップ（新しいPC・クローン直後）

```powershell
# Unity 6000.6.7f1 を Unity Hub でインストール済みであること
pwsh -NoProfile -File scripts/unity/Setup-UnityAutomation.ps1   # Python 3.12 で .tools/unity-mcp を作成（-PythonPath で指定可）
pwsh -NoProfile -File scripts/unity/Test-UnityConnection.ps1    # Editor起動・接続確認・停止
pwsh -NoProfile -File scripts/harness/Test-ProjectHarness.ps1   # 構成チェック
```

## AIエージェント

- ルール: `AGENTS.md`（正本）、`CLAUDE.md`（Claude Code用、AGENTSを読込）
- スキル: `unity-verify` / `unity-editor` / `spec` / `handoff`（`.agents/skills/`、Claude用入口は `.claude/skills/`）
- 専用エージェント: `claude --agent unity-check`（読み取り専用の接続確認）、`claude --agent code-task`（既知パスの小修正）
- 資料: `docs/ai/`（PROJECT_CONTEXT, HANDOFF, UNITY_AUTOMATION, UNITY_CHECKS, SPEC_WORKFLOW）
