using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Units;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>
    /// T307（AC-011）: Game シーンで、スロット操作中も敵が進み、一時停止中は敵が止まって配置・強化・スロット操作が拒否されることを確かめる。
    /// BattleRunner と SlotCabinetView が同じ財布を共有していることも確認する（T301 の残り）。
    /// </summary>
    public class PausePlayModeTests : InputTestFixture
    {
        private const string SceneName = "Game";
        private const int MaxStartupFrames = 30;
        private const int Bet = 3;
        private const float SpawnWaitSeconds = 1f;
        private const int MaxSpawnWaits = 30;
        private const float AdvanceSeconds = 0.5f;
        private const int IdleFrames = 5;
        private const string Swordsman = "swordsman";

        private static readonly GridPoint GroundCell = new GridPoint(1, 1);

        private Keyboard keyboard;
        private BattleRunner runner;
        private SlotCabinetView cabinet;
        private PlacementInput input;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);
            for (var i = 0; i < MaxStartupFrames && (input == null || input.Controller == null); i++)
            {
                yield return null;
                runner = Object.FindFirstObjectByType<BattleRunner>();
                cabinet = Object.FindFirstObjectByType<SlotCabinetView>();
                input = Object.FindFirstObjectByType<PlacementInput>();
            }
            Assert.IsNotNull(runner, "BattleRunner がシーンにありません");
            Assert.IsNotNull(cabinet, "SlotCabinetView がシーンにありません");
            Assert.IsNotNull(input, "PlacementInput がシーンにありません");
            Assert.IsNotNull(input.Controller, "PlacementInput が戦闘につながっていません");
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl control)
        {
            Press(control);
            yield return null;
            Release(control);
            yield return null;
        }

        // 最初の敵が出るまで進める（味方がいないのでライフは毎回満タンに戻す）
        private UnitySlotTower.Core.Enemies.EnemyInstance WaitForEnemy()
        {
            for (var i = 0; i < MaxSpawnWaits && runner.Enemies.Active.Count == 0; i++)
            {
                runner.FastForward(SpawnWaitSeconds);
                runner.Life.RestoreFull();
            }
            Assert.Greater(runner.Enemies.Active.Count, 0, "敵が出ていません");
            return runner.Enemies.Active[0];
        }

        [UnityTest]
        public IEnumerator SlotOperation_DoesNotStopEnemies_AndSharesWalletWithBattle()
        {
            yield return LoadGame();
            Assert.AreSame(cabinet.Wallet, runner.Wallet, "戦闘とスロットは同じ財布");
            var enemy = WaitForEnemy();

            var coins = runner.Wallet.Coins;
            cabinet.HandleLever();
            Assert.AreEqual(SlotPhase.Spinning, cabinet.Machine.Phase, "レバーでリールが回り始める");
            Assert.AreEqual(coins - Bet, runner.Wallet.Coins, "掛け金は戦闘側から見える財布から引かれる");

            var progress = enemy.Progress;
            runner.FastForward(AdvanceSeconds);
            runner.Life.RestoreFull();
            Assert.AreEqual(SlotPhase.Spinning, cabinet.Machine.Phase, "スロットは回転中のまま");
            Assert.Greater(enemy.Progress, progress, "スロット操作中も敵は進む");

            // 召喚のコインもスロットと同じ財布から引かれる
            var before = runner.Wallet.Coins;
            Assert.AreEqual(SummonResult.Success, runner.Summoner.TrySummon(Swordsman, runner.Board, GroundCell, Facing.Right, out _));
            Assert.Less(runner.Wallet.Coins, before);
            Assert.AreEqual(runner.Wallet.Coins, cabinet.Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator Pause_StopsEnemies_AndRejectsSummonUpgradeRetreatAndSlot()
        {
            yield return LoadGame();
            var enemy = WaitForEnemy();
            var board = runner.Board;
            Assert.AreEqual(SummonResult.Success, runner.Summoner.TrySummon(Swordsman, board, GroundCell, Facing.Right, out var ally));
            Assert.IsNotNull(ally);
            // 回転中のリールがある状態で止める
            cabinet.HandleLever();
            Assert.AreEqual(SlotPhase.Spinning, cabinet.Machine.Phase);
            var reelPosition = cabinet.Machine.ReelPosition(0);

            runner.SetPaused(true);
            Assert.IsTrue(runner.IsPaused);
            Assert.AreSame(runner.Pause, cabinet.Pause, "戦闘とスロットは同じ一時停止状態を共有する");

            var progress = enemy.Progress;
            var coins = runner.Wallet.Coins;
            var enemyCount = runner.Enemies.Active.Count;
            runner.FastForward(AdvanceSeconds);
            for (var i = 0; i < IdleFrames; i++) yield return null;
            Assert.AreEqual(progress, enemy.Progress, "一時停止中は敵が進まない");
            Assert.AreEqual(enemyCount, runner.Enemies.Active.Count);
            Assert.AreEqual(reelPosition, cabinet.Machine.ReelPosition(0), "一時停止中はリールが回らない");

            // 操作の拒否
            Assert.AreEqual(SummonResult.Paused, runner.Summoner.TrySummon(Swordsman, board, new GridPoint(2, 1), Facing.Right, out _));
            Assert.AreEqual(UpgradeResult.Paused, runner.Upgrades.TryUpgrade(Swordsman));
            Assert.IsFalse(runner.Simulation.TryRetreat(ally, out _));
            Assert.AreSame(ally, board.GetAt(GroundCell), "撤退は拒否される");
            cabinet.HandleStop(0);
            Assert.IsFalse(cabinet.Machine.IsStopped(0), "ストップは拒否される");
            Assert.AreEqual(StopResult.Paused, cabinet.Machine.PressStop(0));
            Assert.AreEqual(coins, runner.Wallet.Coins, "拒否された操作でコインは減らない");

            // 閲覧のための読み出しは停止中も使える
            Assert.Greater(runner.Units.Units.Count, 0);
            Assert.Greater(runner.Units.Get(Swordsman).MaxHp, 0);
            Assert.Greater(cabinet.Payouts.Count, 0);

            // 再開で続きから進む
            runner.SetPaused(false);
            runner.FastForward(AdvanceSeconds);
            runner.Life.RestoreFull();
            Assert.Greater(enemy.Progress, progress, "再開で敵が続きから進む");
        }

        [UnityTest]
        public IEnumerator Keyboard_EscCancelsPlacementFirst_ThenPauseTogglesAndRejectsInput()
        {
            yield return LoadGame();
            var controller = input.Controller;
            Assert.IsFalse(runner.IsPaused);

            // 配置操作中の Esc は取り消しだけで、一時停止にはならない
            yield return Tap(keyboard.digit1Key);
            Assert.IsTrue(controller.IsActive);
            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(controller.IsActive);
            Assert.IsFalse(runner.IsPaused, "配置操作中の Esc は取り消し優先");

            // 待機中の Esc で一時停止
            yield return Tap(keyboard.escapeKey);
            Assert.IsTrue(runner.IsPaused);

            // 停止中は数字キー（選択）・強化・Space（レバー）が効かない
            var coins = runner.Wallet.Coins;
            yield return Tap(keyboard.digit1Key);
            Assert.IsFalse(controller.IsActive, "停止中は配置の選択ができない");
            Press(keyboard.leftShiftKey);
            yield return Tap(keyboard.digit1Key);
            Release(keyboard.leftShiftKey);
            yield return Tap(keyboard.spaceKey);
            Assert.AreEqual(SlotPhase.Idle, cabinet.Machine.Phase, "停止中はレバーが効かない");
            Assert.AreEqual(coins, runner.Wallet.Coins);
            Assert.AreEqual(1, runner.Upgrades.GetLevel(runner.Units.Units[0].Id), "停止中は強化されない");

            // もう一度 Esc で再開すると操作できる。P でも切り替えられる
            yield return Tap(keyboard.escapeKey);
            Assert.IsFalse(runner.IsPaused);
            yield return Tap(keyboard.spaceKey);
            Assert.AreEqual(SlotPhase.Spinning, cabinet.Machine.Phase, "再開後はレバーが効く");
            yield return Tap(keyboard.pKey);
            Assert.IsTrue(runner.IsPaused, "P でも一時停止できる");
            yield return Tap(keyboard.pKey);
            Assert.IsFalse(runner.IsPaused);
        }

        [UnityTest]
        public IEnumerator Keyboard_PauseDuringPlacement_AbortsPlacementInTheSameFrame()
        {
            yield return LoadGame();
            var controller = input.Controller;
            yield return Tap(keyboard.digit1Key);
            Assert.IsTrue(controller.IsActive);

            Press(keyboard.pKey);
            yield return null;
            Assert.IsTrue(runner.IsPaused);
            Assert.IsFalse(controller.IsActive, "P で停止した同じフレームに配置操作が取りやめられる");
            Release(keyboard.pKey);
            yield return null;
            Assert.IsTrue(runner.IsPaused, "Pause の押下を二重に読まない");
        }
    }
}
