# Resume state

## 現在の作業

更新: 2026-10-28 Asia/Tokyo / 状態: M0 完了、M1 の T101〜T105 完了（T104 までコミット済み `40210ca`、T105 は未コミット）。次は T106

- 仕様: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（承認済み）/ [`plan.md`](../../specs/001-slot-tower-defense/plan.md) / [`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)。ユーザーが 2026-10-28 に実装開始を依頼済み。
- 完了: asmdef 5つ、CSV パーサー、`SimClock`/`GameRandom`、`TableSet`/`GameConfig`/`TableValidator`、`PresentationCatalog`（クラスのみ）、`Data/Tables/` の雛形。EditMode 17件成功、ハーネス8件成功（2026-10-28）。
- T006: `Tools/UnitySlotTower/Build Game Scene`（`Editor/GameSceneBuilder.cs`）が `Scenes/Game.unity`、`Data/PresentationCatalog.asset`、ウィンドウ設定を再生成する（ビルド設定は Game のみ）。レイアウトは Canvas 内の `PlayArea`（9:16）を割合アンカーで 38%/5%/残り に分け、`BattleViewport` が戦闘カメラの Viewport Rect を合わせる。定数は `Runtime/GameLayout.cs`。
- T007: 文字は `Core/Text/AllowedText.cs` の数字・記号だけ。フォントは `Art/Fonts/DigitsSDF.asset`（`Tools/UnitySlotTower/Fonts/*` で再生成、元は TMP Essentials の Liberation Sans / OFL）。UI の文字は必ず TextMeshPro でこのフォントを使う（旧 `Text` は不可）。`TextRuleScanner` と `TextRuleTests` が Title 以外のシーン・プレハブを検査する。`Assets/TextMesh Pro/` はベンダー製なので編集しない。
- T008: `Assets/InputSystem_Actions.inputactions` の `Game` マップ（テンプレートの Player/UI は残す）。キーボードは plan 3章どおり、マウスは Point/Press/Cancel。アイコン・レバー・ストップボタンは uGUI クリック（UI マップ）。Esc は Cancel と Pause の両方に割り当て済みで、配置操作中かどうかの切り分けは実装側で行う。Shift+数字（強化）と数字（召喚選択）は同じキーを使うので、実装時に Shift 押下中は召喚選択を無視する。C# ラッパーは未生成（`generateWrapperCode: 0`）。
- T101（スロットのデータ）: `Core/Slot/` に図柄・ライン（T/M/B/D1/D2）・リール・配当表・ルール。リール配列は上から下の順、停止位置 s は中段の添字（上段 s-1、下段 s+1、21で循環）。左リールは [BELL REPLAY WATERMELON CHERRY]×5＋SEVEN、中・右は3種サイクル×5＋7/BAR 6個（`slot_reels.csv` は `Gen-Reels` 相当のスクリプトで作ったが、以後は CSV を直接編集してよい）。ベル・リプレイ・スイカは各リールで間隔5以内、左のチェリーも5以内＝中段1ラインだけで必ず引き込める設計。7 は目押しが必要な間隔。配当・確率は仮値。
- T102（内部抽選）: `slot_probabilities.csv`（role,setting,state,probability。state は normal/kakuhen/bonus、role は `+` で重複当選、ハズレ `NONE` は書かず残り確率）。`SlotProbabilityTable.Draw(setting, state, GameRandom)` が `SlotFlag`（`Bonus`/`Small`）を返す。生成元スクリプトは一時的なもので、以後は CSV を直接編集してよい。ボーナス持ち越し中の抽選スキップは T105 で扱う。
- T103（リール制御）: `SlotReelController`。`Evaluate(s0,s1,s2)` が揃った役のビット（payouts の並び順）、`Decide(flag, s0,s1,s2, reel, press)` が停止位置（回転中は `Unstopped`=-1）。停止位置は press から最大 `MaxSlip` コマ先。小役・リプレイは必ず揃え、非成立役は揃えず、ボーナスは目押しが合うときだけ（狙える手を優先）。リール配列を変えたら `SlotReelControlTests` の全数検査（約1分）で再確認する。配列が安全でないときは `Tools/UnitySlotTower/Slot/Search Reel Layout`（`SlotReelOptimizer`、結果は `Artifacts/Automation/reels-candidate.csv`、Editor は探索中も応答する）で入れ替え探索ができる。現在の `slot_reels.csv` は探索結果で、左リールは [BELL REPLAY WATERMELON CHERRY]×5＋SEVEN のまま、中・右は並びを入れ替えた（T101 の「3種サイクル」ではなくなった）。
- T103 の設計判断: 小役との重複当選（WATERMELON+BIG など）は、同じ窓に両方の図柄が入らない限りボーナスが同時には揃わない＝ボーナスは持ち越しになる（T105 で扱う）。
- T104（1ゲームの進行）: `Core/Economy/Wallet`（スロットと戦闘で共有する財布。T301 で統合）と `Core/Slot/SlotMachine`。`PullLever()`（`LeverResult`: Started/NotEnoughCoins/Waiting/Busy。掛け金は `game_config.csv` の `bet` を自動投入、リプレイ後は 0）、`Tick(dt)`（固定ステップから呼ぶ。回転 28 コマ/秒、ウェイト `wait_seconds`）、`PressStop(reel)`（押した瞬間の `ReelPosition` を `SlotReelController.Decide` に渡す）。全停止で払い出しを財布に加算し `LastResult`（`SlotGameResult`: Flag/Stops/RealizedMask/BetPaid/Payout/Replay）を更新。`State` は Normal 固定で、確変・ボーナス中への遷移は T105〜T107。注意: `TableSet.SlotPayouts` などのプロパティは呼ぶたびに表を作り直す（参照比較に使わない、ループ内で呼ばない）。
- T105（持ち越しとボーナス開始）: `SlotMachine.CarriedBonus`（抽選で当たったボーナスを揃うまで保持。持ち越し中の抽選は小役だけ採用して「小役＋持ち越し」を `Flag` にする）、揃ったら `State=Bonus`・`ActiveBonus` に BIG/REG・`CarriedBonus=null`、`SlotGameResult.BonusStarted`。ボーナス図柄そのものの払い出しは 0。plan 5章の「持ち越し中は小役より優先」は採らず、小役の引き込みは必須のまま、ボーナスは狙える手を優先するだけにした（T103 の制御の性質。小役の払い出しを確実にするため）。
- 次の一手: T106（ボーナス中の抽選と、上限枚数に達するまでの払い出し・終了）。`State==Bonus` の間は `slot_probabilities.csv` の bonus 状態の表で抽選済み（BIG/REG は出ない）。終了条件は `slot_rules.csv` の `bonus_big_max_payout`/`bonus_reg_max_payout`（ボーナス中の払い出しの合計）。終了時に `State` を Normal に戻し `ActiveBonus` を null にする（BIG 後の確変突入は T107）。AC-040。戦闘フィールドは仮の平面（`FieldPlaceholder`）で、T201 で置き換える。
- テスト実行: `Invoke-UnityMcp.ps1 editor tests --mode EditMode --async` → `poll-test <job_id>`。スクリプトを変えたら先に `editor refresh`（下記の注意）。

## 注意（環境）

- Unity のインスタンス名はフォルダ名（`TestUnitySlotTower`）。`unity_mcp_runner.py` は `ROOT.name` で選ぶよう修正済み。
- `Invoke-UnityMcp.ps1 editor refresh` は CLI が JSON の前に文字を出すため、実行は成功しても失敗扱いになる。`.tools/unity-mcp/Scripts/python.exe -B scripts/unity/unity_mcp_runner.py editor refresh` を直接呼ぶ（要 `PYTHONUTF8=1`）。再コンパイルに約30秒かかる。
- `.gitignore` に Unity 生成物・`.tools/`・`Artifacts/`・`Builds/` の除外を追記した（元は C++ 用テンプレート）。
- 作業終了時は `Stop-UnityAutomation.ps1` を実行し、8080 が閉じたことを確認する。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
