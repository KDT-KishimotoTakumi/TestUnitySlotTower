---
name: unity-editor
description: Unity Editorの操作（シーン・GameObject・Prefab・マテリアル・Play・スクリーンショット・テスト・ビルド）をMCP for Unity CLIラッパーで行うとき。C#の編集だけなら不要。
---

# Unity Editor操作

正本は `docs/ai/UNITY_AUTOMATION.md`。初回はそれを読み、以下の要点で動く。

- 入口は `pwsh -NoProfile -File scripts/unity/Invoke-UnityMcp.ps1 <group> <command> ...`。サーバーとEditorの起動・待機は自動。Startを別に呼ばない。
- コマンド一覧は `Invoke-UnityMcp.ps1 --help` / `<group> --help` で必要な分だけ確認する（ヘルプは起動しない）。生のツール呼び出しは `raw <tool> '<json>'`。
- 操作前に `scene active` で対象sceneを確認する。Play・リコンパイル後は再確認する。
- 変更系（作成・削除・ビルド）がタイムアウトしても未実行とは限らない。状態とジョブを確認し、同じ変更を再送しない。
- シーン・Prefabの変更は保存（`scene save` 等）まで確認する。.metaとGUIDを壊す手作業のYAML編集はしない。
- 画像は `camera screenshot --output-folder Artifacts/Automation/Editor` で保存し、PNGを開いて確認する。
- 一連のUnity操作と非同期ジョブが終わったら `Stop-UnityAutomation.ps1` で停止し、8080が閉じたことを確認する。CookingShotの自動化と同時に動かさない。
