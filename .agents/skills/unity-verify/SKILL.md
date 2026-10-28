---
name: unity-verify
description: UnitySlotTowerのコード・アセット・パッケージ・設定・エージェント設定の変更を、変更内容に合う最小限の方法で検証する。文章だけの修正には使わない。
---

# UnitySlotTowerの検証

成功・失敗・未実行を区別して報告する。

1. コード・アセット・依存関係・設定・エージェント設定を変更したら `pwsh -NoProfile -File scripts/harness/Test-ProjectHarness.ps1` を実行する。失敗は原因を直すか、既存のユーザー変更によるものと確認して明記する。
2. UnityのC#、アセット、`Packages/`、`ProjectSettings/` を変更した場合だけ `docs/ai/UNITY_CHECKS.md` の該当節を読む。
3. コンパイル・Console・テスト・シーン確認が必要なら `docs/ai/UNITY_AUTOMATION.md` に従い、`scripts/unity/Invoke-UnityMcp.ps1` で対象インスタンスとsceneを確認してから行う。
   - コンパイル確認: `editor console --type error --count 10`（リコンパイル後に再接続を待つ）
   - テスト: `editor tests --mode EditMode --async` → `editor poll-test <job_id> --wait 5`。`summary.total/passed/failed` を報告し、0件を機能検証と呼ばない。
   - 表示に関わる変更は実画像を保存して開く。成功メッセージだけで判断しない。
4. Windowsプレイヤーの挙動に関わる場合だけ `Build-UnityPlayer.ps1` → `Test-UnityPlayer.ps1 -ShowWindow` を使い、ジョブ最終状態・終了コード・画像を確認する。
5. Unity操作が終わったら結果と保存を確認し `Stop-UnityAutomation.ps1` で停止、8080が閉じたことを確認する。

検査を増やす前に、その検査が今回の変更で起こりうる失敗を検出するか考える。Editorが使えない場合は静的確認を行い、残りを未実行として報告する。関連検査が成功し受け入れ条件を満たしたら、同じ検査を繰り返さず完了する。
