# 計画: コインの2種類化とBGM

仕様: [`spec.md`](spec.md)（承認済み 2026-10-31）。要求は ID（FR-101 等）で参照する。

## 方針
- 既存の `Wallet` を2つ持つ。キャラクターコイン=従来の `Wallet`、スロットコイン=新しい `slotWallet`。クラスは増やさない。
- 掛け金・ドロップ・ライフボーナスだけ行き先を `slotWallet` に替える。`SlotMachine` / `BattleSimulation` / `WaveFlow` は末尾の省略可引数 `slotWallet` を受け取り、省略時は従来どおり1つの財布（既存テストを壊さないため）。本番の組み立て（`SlotCabinetView`・`BattleRunner`）は必ず渡す。
- BGM は `Core/Audio/BgmVolume`（0〜5 の段階。Unity 非依存）と `Runtime/BgmPlayer`（再生・一時停止連動・スピーカー・キー）。音源は `ToneSynth.CreateLoop` の簡易ループ（Am-F-C-G）。

## 影響するファイル
| 区分 | ファイル |
| --- | --- |
| データ | `Data/Tables/game_config.csv`（`initial_coins`=250、`initial_slot_coins`=250）、`Core/Data/GameConfig.cs`、`TableValidator.cs`（掛け金とスロットコインの検査） |
| Core | `Core/Slot/SlotMachine.cs`、`Core/Combat/BattleSimulation.cs`・`WaveFlow.cs`、`Core/Audio/BgmVolume.cs`（新規） |
| Runtime | `SlotCabinetView.cs`（`SlotWallet`、表示はスロットコイン、銀色）、`BattleRunner.cs`（`SlotWallet`）、`BgmPlayer.cs`（新規）、`ToneSynth.cs`（`CreateLoop`） |
| Editor | `GameSceneBuilder.cs`（スピーカーと音量の数字、配置数・▶❚❚ の段の再配置）、`Game.unity` を再生成 |
| 入力 | `InputSystem_Actions.inputactions`（Game マップに `BgmVolumeUp`=`=`/テンキー+、`BgmVolumeDown`=`-`/テンキー-） |

## 表示（FR-107）
- 左下の操作バー: キャラクターコイン（金の丸、250 から）。スロット筐体の信用表示: スロットコイン（銀の四角、250 から）。払い出し表示はキャラクターコインへの加算額。

## リスク
- 収入がスロットの払い出しに偏る（経済バランスは仮置き。T309 で調整）。
- スロットコインが尽きると回せない。補充はドロップとライフボーナスのみ（仕様どおり）。
- キー割り当ては US 配列の位置名（JIS では `=` の位置が `^`）。
- Editor の再生ではオーディオデバイスの有無で `isPlaying` が変わる可能性があり、聴感は手動確認。

## 検証
EditMode（`DualCoinTests`、`TableTests`、`InputBindingTests`、`TextRuleTests`）、PlayMode（`BgmPlayModeTests`、`PausePlayModeTests`、`OperationBarPlayModeTests`）、Editor 画像（`Artifacts/Automation/Editor/dual-coin-bgm2.png`）、聴感は手動。
