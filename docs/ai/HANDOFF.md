# Resume state

## 現在の作業

更新: 2026-10-28 Asia/Tokyo / 状態: M0 の T001〜T007 完了（T006 までコミット済み `5ab0b30`、T007 は未コミット）、T008 は未着手

- 仕様: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（承認済み）/ [`plan.md`](../../specs/001-slot-tower-defense/plan.md) / [`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)。ユーザーが 2026-10-28 に実装開始を依頼済み。
- 完了: asmdef 5つ、CSV パーサー、`SimClock`/`GameRandom`、`TableSet`/`GameConfig`/`TableValidator`、`PresentationCatalog`（クラスのみ）、`Data/Tables/` の雛形。EditMode 17件成功、ハーネス8件成功（2026-10-28）。
- T006: `Tools/UnitySlotTower/Build Game Scene`（`Editor/GameSceneBuilder.cs`）が `Scenes/Game.unity`、`Data/PresentationCatalog.asset`、ウィンドウ設定を再生成する（ビルド設定は Game のみ）。レイアウトは Canvas 内の `PlayArea`（9:16）を割合アンカーで 38%/5%/残り に分け、`BattleViewport` が戦闘カメラの Viewport Rect を合わせる。定数は `Runtime/GameLayout.cs`。
- T007: 文字は `Core/Text/AllowedText.cs` の数字・記号だけ。フォントは `Art/Fonts/DigitsSDF.asset`（`Tools/UnitySlotTower/Fonts/*` で再生成、元は TMP Essentials の Liberation Sans / OFL）。UI の文字は必ず TextMeshPro でこのフォントを使う（旧 `Text` は不可）。`TextRuleScanner` と `TextRuleTests` が Title 以外のシーン・プレハブを検査する。`Assets/TextMesh Pro/` はベンダー製なので編集しない。
- 次の一手: T008（InputActionAsset に plan 3章の操作をマウス・キーボード両方で定義）。Editor 操作が必要（`unity-editor` スキル）。戦闘フィールドは仮の平面（`FieldPlaceholder`）で、T201 で置き換える。
- テスト実行: `Invoke-UnityMcp.ps1 editor tests --mode EditMode --async` → `poll-test <job_id>`。スクリプトを変えたら先に `editor refresh`（下記の注意）。

## 注意（環境）

- Unity のインスタンス名はフォルダ名（`TestUnitySlotTower`）。`unity_mcp_runner.py` は `ROOT.name` で選ぶよう修正済み。
- `Invoke-UnityMcp.ps1 editor refresh` は CLI が JSON の前に文字を出すため、実行は成功しても失敗扱いになる。`.tools/unity-mcp/Scripts/python.exe -B scripts/unity/unity_mcp_runner.py editor refresh` を直接呼ぶ（要 `PYTHONUTF8=1`）。再コンパイルに約30秒かかる。
- `.gitignore` に Unity 生成物・`.tools/`・`Artifacts/`・`Builds/` の除外を追記した（元は C++ 用テンプレート）。
- 作業終了時は `Stop-UnityAutomation.ps1` を実行し、8080 が閉じたことを確認する。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
