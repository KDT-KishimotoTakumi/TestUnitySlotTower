# タスク: コインの2種類化とBGM

仕様: [`spec.md`](spec.md) / 計画: [`plan.md`](plan.md)

- [x] T601 (FR-101) `game_config.csv` に `initial_slot_coins`、`GameConfig.InitialSlotCoins`、検証の更新 — 検証: EditMode `DualCoinTests.Config_*`・`TableTests`
- [x] T602 (FR-102, FR-103) `SlotMachine`: 掛け金=スロットコイン、払い出し=キャラクターコイン — 検証: EditMode `DualCoinTests.Slot_*`（3件）
- [x] T603 (FR-104) 召喚・強化・撤退の返還はキャラクターコインのみ — 検証: EditMode `DualCoinTests.SummonAndRefund_*`・`Summon_Refused*`
- [x] T604 (FR-105, FR-106) ドロップ・ライフボーナスをスロットコインへ、ウェーブ間の引き継ぎ — 検証: EditMode `DualCoinTests.EnemyDrop_*`・`LifeBonus_*`
- [x] T605 (FR-107) 2種類の表示（金の丸・銀の四角）、筐体はスロットコイン — 検証: Editor 画像、`TextRuleTests`
- [x] T606 (FR-110, FR-112) `BgmPlayer`、`ToneSynth.CreateLoop`、一時停止連動、効果音と別の AudioSource — 検証: PlayMode `BgmPlayModeTests`
- [x] T607 (FR-111) `BgmVolume`（0〜5、既定 3）、スピーカーのクリックとキー（`=`/`-`/テンキー）、数字表示 — 検証: PlayMode `BgmPlayModeTests`、`InputBindingTests`、Editor 画像
- [ ] T608 BGM の聴感・音量の手動確認、経済バランス（T309 に含む） — 検証: 手動プレイ
