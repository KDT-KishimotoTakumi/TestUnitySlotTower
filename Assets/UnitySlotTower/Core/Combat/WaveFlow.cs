using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Core.Combat
{
    /// <summary>
    /// ウェーブの進行状態。最終ウェーブを終えると Cleared ではなく Victory になる。
    /// 最終ウェーブの終了条件は他と同じ（出現を消化 + 場の敵ゼロ）で、最終ボスは最後に出るので、撃破と呼び出された雑魚の全滅がクリアの条件になる（T306）。
    /// </summary>
    public enum WaveState
    {
        /// <summary>準備中。戦闘は進まず、味方の召喚に待ち時間がない。StartWave で Running になる。</summary>
        Preparing,
        Running,
        /// <summary>ウェーブ終了の処理が済んだ。次のウェーブは Prepare で用意する。</summary>
        Cleared,
        Victory,
    }

    /// <summary>
    /// ウェーブ進行（FR-010, FR-014, FR-015, FR-016、T304）。Preparing → Running → Cleared（最終ウェーブは Victory）。
    /// ウェーブ終了 = 出現予定をすべて出した かつ 場の敵がいない。終了すると次の順で処理する。
    ///  1. 残りライフ × life_bonus_per_life をコインに加算（返還金のような他の加算とは別）
    ///  2. 配置中の味方を種類ごとに数えて無料ストックにし（前のウェーブの使い残しは捨てる）、全員を盤面から撤退させる（返還金なし）
    ///  3. ライフを満タンに戻す
    /// 強化の段階（UnitUpgrades）とコイン（Wallet）はこの外にあり、そのまま引き継がれる。
    /// 盤面・敵・戦闘は 1 ウェーブごとに作り直して Prepare で渡す。召喚役・ライフ・財布は作り直さず引き継ぐ。
    /// </summary>
    public sealed class WaveFlow : IUnitStock
    {
        /// <summary>ゲームのウェーブ数（FR-010）。</summary>
        public const int DefaultWaveCount = 5;

        private readonly UnitSummoner summoner;
        private readonly PlayerLife life;
        // ライフボーナスはスロットコイン（slotWallet、省くと wallet。002 FR-105）。引き継ぎは両方とも財布側で保たれる（FR-106）
        private readonly Wallet wallet;
        private readonly Wallet slotWallet;
        private readonly int lifeBonusPerLife;
        private readonly PauseState pause;
        private readonly Dictionary<string, int> stock = new Dictionary<string, int>();
        private readonly List<PlacedUnit> retreatBuffer = new List<PlacedUnit>();

        private PlacementBoard board;
        private EnemyField enemies;
        private WaveSchedule schedule;
        private BattleSimulation simulation;

        public WaveState State { get; private set; } = WaveState.Cleared;
        /// <summary>いまのウェーブの番号（1 始まり）。Prepare を呼ぶまでは 0（State は最初のウェーブを準備できる Cleared）。</summary>
        public int WaveNumber { get; private set; }
        public int WaveCount { get; }

        /// <summary>ウェーブが終わり、後始末が済んだ（最終ウェーブでは State は Victory）。引数はウェーブ番号とライフボーナスのコイン。</summary>
        public event Action<int, int> WaveCleared;

        /// <summary>最終ウェーブが終わった。WaveCleared の後に 1 回だけ呼ぶ。</summary>
        public event Action AllWavesCleared;

        public WaveFlow(UnitSummoner summoner, PlayerLife life, Wallet wallet, int lifeBonusPerLife, int waveCount = DefaultWaveCount,
            PauseState pause = null, Wallet slotWallet = null)
        {
            this.pause = pause;
            this.slotWallet = slotWallet;
            this.summoner = summoner ?? throw new ArgumentNullException(nameof(summoner));
            this.life = life ?? throw new ArgumentNullException(nameof(life));
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            if (lifeBonusPerLife < 0) throw new ArgumentOutOfRangeException(nameof(lifeBonusPerLife));
            if (waveCount < 1) throw new ArgumentOutOfRangeException(nameof(waveCount));
            this.lifeBonusPerLife = lifeBonusPerLife;
            WaveCount = waveCount;
        }

        /// <summary>種類 unitId の無料ストックの数。</summary>
        public int GetStock(string unitId) => stock.TryGetValue(unitId, out var count) ? count : 0;

        /// <summary>
        /// 次のウェーブを準備中にする。待ち時間をなくし、敵は進めない。最初のウェーブか、Cleared の後にだけ呼べる。
        /// 引数は新しいウェーブ用の盤面・敵・出現予定・戦闘（simulation はこれらから作ったもの）。
        /// </summary>
        public void Prepare(int wave, PlacementBoard newBoard, EnemyField newEnemies, WaveSchedule newSchedule, BattleSimulation newSimulation)
        {
            if (State != WaveState.Cleared)
                throw new InvalidOperationException($"いまはウェーブを準備できません: {State}");
            if (wave < 1 || wave > WaveCount) throw new ArgumentOutOfRangeException(nameof(wave));
            board = newBoard ?? throw new ArgumentNullException(nameof(newBoard));
            enemies = newEnemies ?? throw new ArgumentNullException(nameof(newEnemies));
            schedule = newSchedule ?? throw new ArgumentNullException(nameof(newSchedule));
            simulation = newSimulation ?? throw new ArgumentNullException(nameof(newSimulation));
            WaveNumber = wave;
            State = WaveState.Preparing;
            summoner.IgnoreCooldown = true;
        }

        /// <summary>準備中なら戦闘を始める（待ち時間が元に戻る）。準備中でなければ何もせず false。</summary>
        public bool StartWave()
        {
            if (State != WaveState.Preparing) return false;
            State = WaveState.Running;
            summoner.IgnoreCooldown = false;
            return true;
        }

        /// <summary>
        /// 固定ステップから呼ぶ。Running の間だけ戦闘を進め、終了条件を満たしたらウェーブ終了の処理をする。
        /// 準備中・終了後は何も進めない。ライフが 0（ゲームオーバー）のときは終了にしない。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (State != WaveState.Running) return;
            if (pause != null && pause.IsPaused) return;
            simulation.Step(deltaSeconds);
            if (life.IsGameOver) return;
            if (schedule.IsFinished && enemies.Active.Count == 0) FinishWave();
        }

        /// <summary>
        /// 無料ストックから種類 unitId を置く（FR-016）。コイン・待ち時間は不要で、HP 全快の新規個体になる。配置上限は守る。
        /// 成功するとストックが 1 減る。失敗時は何も変わらない。準備中と戦闘中だけ使える。
        /// </summary>
        public SummonResult TrySummonFromStock(string unitId, PlacementBoard targetBoard, GridPoint cell, Facing facing, out PlacedUnit placed)
        {
            placed = null;
            if (pause != null && pause.IsPaused) return SummonResult.Paused;
            if (State != WaveState.Preparing && State != WaveState.Running) return SummonResult.WaveNotActive;
            if (GetStock(unitId) <= 0) return SummonResult.NoStock;
            var result = summoner.TrySummonFree(unitId, targetBoard, cell, facing, out placed);
            if (result == SummonResult.Success) stock[unitId]--;
            return result;
        }

        private void FinishWave()
        {
            var bonus = life.Current * lifeBonusPerLife;
            if (bonus > 0) (slotWallet ?? wallet).Add(bonus);

            stock.Clear();
            retreatBuffer.Clear();
            var units = board.Units;
            for (var i = 0; i < units.Count; i++) retreatBuffer.Add(units[i]);
            for (var i = 0; i < retreatBuffer.Count; i++)
            {
                var id = retreatBuffer[i].Def.Id;
                stock[id] = GetStock(id) + 1;
                simulation.RemoveUnit(retreatBuffer[i]);
            }

            life.RestoreFull();
            summoner.IgnoreCooldown = true;
            var finalWave = WaveNumber >= WaveCount;
            State = finalWave ? WaveState.Victory : WaveState.Cleared;
            WaveCleared?.Invoke(WaveNumber, bonus);
            if (finalWave) AllWavesCleared?.Invoke();
        }
    }
}
