using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Tests
{
    /// <summary>002: コインの2種類化。キャラクターコイン（召喚・強化・返還・スロットの払い出し）とスロットコイン（掛け金・ドロップ・ライフボーナス）。AC-101〜AC-104。</summary>
    public class DualCoinTests
    {
        private const int CharacterCoins = 100;
        private const int SlotCoins = 50;
        private const int ReplayWaitSteps = 140;   // wait_seconds を越えるステップ数

        // ---- AC-101 初期値 ----

        [Test]
        public void Config_HasInitialCharacterAndSlotCoins()
        {
            var config = TableSet.Load(TableTests.ReadFile).GameConfig;
            Assert.AreEqual(1000, config.InitialCoins);
            Assert.AreEqual(500, config.InitialSlotCoins);
        }

        // ---- AC-102 スロット ----

        [Test]
        public void Slot_PaysBetFromSlotCoins_AndPayoutGoesToCharacterCoins()
        {
            var tables = SlotMachineTests.Forced("BELL");
            var character = new Wallet(CharacterCoins);
            var slot = new Wallet(SlotCoins);
            var machine = new SlotMachine(tables, character, new GameRandom(1UL), SlotMachineTests.Setting, null, slot);

            var result = SlotMachineTests.PlayGame(machine, new GameRandom(2UL));

            Assert.Greater(result.BetPaid, 0);
            Assert.AreEqual(SlotCoins - result.BetPaid, slot.Coins, "掛け金はスロットコインから");
            Assert.AreEqual(CharacterCoins + result.Payout, character.Coins, "払い出しはキャラクターコインへ");
            Assert.Greater(result.Payout, 0, "前提: ベルは払い出しがある");
        }

        [Test]
        public void Slot_RefusedWhenSlotCoinsShort_EvenWithManyCharacterCoins()
        {
            var tables = SlotMachineTests.Real();
            var bet = tables.GameConfig.Bet;
            var character = new Wallet(100000);
            var slot = new Wallet(bet - 1);
            var machine = new SlotMachine(tables, character, new GameRandom(1UL), SlotMachineTests.Setting, null, slot);

            Assert.AreEqual(LeverResult.NotEnoughCoins, machine.PullLever());
            Assert.AreEqual(SlotPhase.Idle, machine.Phase);
            Assert.AreEqual(bet - 1, slot.Coins);
            Assert.AreEqual(100000, character.Coins);
        }

        [Test]
        public void Slot_ReplayGame_DoesNotSpendSlotCoins()
        {
            var tables = SlotMachineTests.Forced("REPLAY");
            var character = new Wallet(CharacterCoins);
            var slot = new Wallet(SlotCoins);
            var machine = new SlotMachine(tables, character, new GameRandom(1UL), SlotMachineTests.Setting, null, slot);

            SlotMachineTests.PlayGame(machine, new GameRandom(2UL));
            var slotAfterFirst = slot.Coins;
            Assert.IsTrue(machine.ReplayPending);
            SlotMachineTests.Advance(machine, ReplayWaitSteps * SimClock.StepSeconds);

            Assert.AreEqual(LeverResult.Started, machine.PullLever());
            Assert.AreEqual(slotAfterFirst, slot.Coins, "リプレイの次ゲームは掛け金が 0");
        }

        // ---- AC-103 召喚・強化・返還 ----

        [Test]
        public void SummonAndRefund_UseCharacterCoinsOnly()
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            var character = new Wallet(CharacterCoins);
            var slot = new Wallet(SlotCoins);
            var summoner = new UnitSummoner(tables.Units, character, 8);
            var board = new PlacementBoard(tables.Maps[0]);
            var sim = new BattleSimulation(board, new EnemyField(board), summoner, tables.UnitRanges,
                tables.GameConfig.BufferAtkMultiplier, new PlayerLife(10), character, new GameRandom(1UL),
                tables.GameConfig.RefundRate, null, slot);

            var cost = tables.Units.Get("swordsman").Cost;
            Assert.AreEqual(SummonResult.Success, summoner.TrySummon("swordsman", board, new GridPoint(1, 1), Facing.Right, out var unit));
            Assert.AreEqual(CharacterCoins - cost, character.Coins);
            Assert.AreEqual(SlotCoins, slot.Coins, "召喚でスロットコインは動かない");

            Assert.IsTrue(sim.TryRetreat(unit, out var refund));
            Assert.AreEqual(CharacterCoins - cost + refund, character.Coins, "返還はキャラクターコインへ");
            Assert.AreEqual(SlotCoins, slot.Coins);
        }

        [Test]
        public void Summon_RefusedWhenCharacterCoinsShort_EvenWithManySlotCoins()
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            var summoner = new UnitSummoner(tables.Units, new Wallet(0), 8);
            var board = new PlacementBoard(tables.Maps[0]);
            Assert.AreEqual(SummonResult.NotEnoughCoins, summoner.TrySummon("swordsman", board, new GridPoint(1, 1), Facing.Right, out _));
        }

        // ---- AC-104 ドロップ・ライフボーナス・引き継ぎ ----

        [Test]
        public void EnemyDrop_GoesToSlotCoins()
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            var board = new PlacementBoard(tables.Maps[0]);
            var field = new EnemyField(board);
            var character = new Wallet(CharacterCoins);
            var slot = new Wallet(SlotCoins);
            var summoner = new UnitSummoner(tables.Units, character, 8);
            var sim = new BattleSimulation(board, field, summoner, tables.UnitRanges, tables.GameConfig.BufferAtkMultiplier,
                new PlayerLife(10), character, new GameRandom(1UL), tables.GameConfig.RefundRate, null, slot);
            var statue = new EnemyDef("statue", EnemyKind.Melee, 10, 0, 0, 1f, 0f, 0f, false, 1, 1, 1.0, 4, 4);

            field.Spawn(statue, tables.Maps[0].FindPath("g1")).TakeDamage(statue.MaxHp);
            sim.Step(SimClock.StepSeconds);

            Assert.AreEqual(SlotCoins + 4, slot.Coins, "ドロップはスロットコインへ");
            Assert.AreEqual(CharacterCoins, character.Coins);
        }

        [Test]
        public void LifeBonus_GoesToSlotCoins_AndBothCoinsCarryOverToNextWave()
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            var character = new Wallet(CharacterCoins);
            var slot = new Wallet(SlotCoins);
            var summoner = new UnitSummoner(tables.Units, character, 8);
            var life = new PlayerLife(tables.GameConfig.StartingLife);
            var flow = new WaveFlow(summoner, life, character, tables.GameConfig.LifeBonusPerLife, 2, null, slot);
            var board = new PlacementBoard(tables.Maps[0]);
            var schedule = new WaveSchedule(new List<WaveEntry>());
            var field = new EnemyField(board, schedule);
            var sim = new BattleSimulation(board, field, summoner, tables.UnitRanges, tables.GameConfig.BufferAtkMultiplier,
                life, character, new GameRandom(1UL), tables.GameConfig.RefundRate, null, slot);
            flow.Prepare(1, board, field, schedule, sim);

            Assert.IsTrue(flow.StartWave());
            flow.Tick(SimClock.StepSeconds);

            var bonus = life.Max * tables.GameConfig.LifeBonusPerLife;
            Assert.AreEqual(WaveState.Cleared, flow.State);
            Assert.AreEqual(SlotCoins + bonus, slot.Coins, "ライフボーナスはスロットコインへ");
            Assert.AreEqual(CharacterCoins, character.Coins);

            // 次のウェーブへ進んでも、財布は作り直さないので両方残る
            var board2 = new PlacementBoard(tables.Maps[0]);
            var schedule2 = new WaveSchedule(new List<WaveEntry>());
            var field2 = new EnemyField(board2, schedule2);
            var sim2 = new BattleSimulation(board2, field2, summoner, tables.UnitRanges, tables.GameConfig.BufferAtkMultiplier,
                life, character, new GameRandom(1UL), tables.GameConfig.RefundRate, null, slot);
            flow.Prepare(2, board2, field2, schedule2, sim2);
            Assert.AreEqual(SlotCoins + bonus, slot.Coins);
            Assert.AreEqual(CharacterCoins, character.Coins);
        }
    }
}
