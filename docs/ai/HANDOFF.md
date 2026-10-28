# Resume state

## 現在の作業

更新: 2026-10-28 Asia/Tokyo / 状態: M0 の T001〜T005 完了、T006〜T008 は未着手

- 仕様: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（承認済み）/ [`plan.md`](../../specs/001-slot-tower-defense/plan.md) / [`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)。ユーザーが 2026-10-28 に実装開始を依頼済み。
- 完了: asmdef 5つ、CSV パーサー、`SimClock`/`GameRandom`、`TableSet`/`GameConfig`/`TableValidator`、`PresentationCatalog`（クラスのみ）、`Data/Tables/` の雛形。EditMode 17件成功、ハーネス8件成功（2026-10-28）。
- 次の一手: T006（Game シーンの縦長レイアウト）、T007（数字専用フォントと文字チェックテスト）、T008（InputActionAsset）。いずれも Editor 操作が必要（`unity-editor` スキル）。T006 のときに `PresentationCatalog.asset` を作り、CSV を登録する。
- テスト実行: `Invoke-UnityMcp.ps1 editor tests --mode EditMode --async` → `poll-test <job_id>`。スクリプトを変えたら先に `editor refresh`（下記の注意）。

## 注意（環境）

- Unity のインスタンス名はフォルダ名（`TestUnitySlotTower`）。`unity_mcp_runner.py` は `ROOT.name` で選ぶよう修正済み。
- `Invoke-UnityMcp.ps1 editor refresh` は CLI が JSON の前に文字を出すため、実行は成功しても失敗扱いになる。`.tools/unity-mcp/Scripts/python.exe -B scripts/unity/unity_mcp_runner.py editor refresh` を直接呼ぶ（要 `PYTHONUTF8=1`）。再コンパイルに約30秒かかる。
- `.gitignore` に Unity 生成物・`.tools/`・`Artifacts/`・`Builds/` の除外を追記した（元は C++ 用テンプレート）。
- 作業終了時は `Stop-UnityAutomation.ps1` を実行し、8080 が閉じたことを確認する。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
