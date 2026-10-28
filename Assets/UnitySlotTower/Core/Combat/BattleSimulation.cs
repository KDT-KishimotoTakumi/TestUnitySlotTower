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
    /// 戦闘の1ステップ（FR-024, FR-028, FR-031）。味方の攻撃・回復・バフ、HP0 での消滅、撃破された敵の除去をまとめる。
    /// Step(dt) の処理順:
    ///  1. バフの倍率を決め直す（バッファーの範囲内の味方に、最も高い倍率を 1 つだけかける）
    ///  2. EnemyField.Tick（出現 → ブロック・移動 → 敵の攻撃）
    ///  3. 味方が置いた順に攻撃・回復する。この時点では敵も味方もまだ消えない
    ///     ＝ 2 で HP が 0 になった味方も、このステップは攻撃・回復する（敵と味方の攻撃は同時）。
    ///     ただし HP が 0 の味方は回復の対象にならず、味方が先に倒した敵は後ろの味方の対象から外れる
    ///  4. HP が 0 の味方を消し（UnitDefeated）、HP が 0 の敵を一覧から外す（EnemyDefeated）。
    ///     撃破された敵はここでドロップを抽選して財布に加算する（DropGained）
    /// ゴールに着いた敵は 2 の中で life_damage ぶんライフを減らす（ドロップなし）。ライフが 0 になったステップは最後まで進み、次の Step から何もしない。
    /// 攻撃・回復は 1 ステップに 1 回まで（敵と同じ）。攻撃できる状態で対象がいない間は、待ち時間が 0 で止まる（間隔を溜めない）。
    /// </summary>
    public sealed class BattleSimulation
    {
        private readonly PlacementBoard board;
        private readonly EnemyField enemies;
        private readonly UnitSummoner summoner;
        private readonly UnitRangeTable ranges;
        private readonly float bufferMultiplier;
        private readonly List<GridPoint> cellBuffer = new List<GridPoint>();
        private readonly List<PlacedUnit> defeatedUnits = new List<PlacedUnit>();
        private readonly List<EnemyInstance> defeatedEnemies = new List<EnemyInstance>();
        private readonly PlayerLife life;
        private readonly Wallet wallet;
        private readonly EnemyDropRoller dropRoller;

        /// <summary>HP が 0 になって消えた味方。RemoveUnit が済んだ後に呼ぶ。</summary>
        public event Action<PlacedUnit> UnitDefeated;

        /// <summary>味方に倒されて一覧から外れた敵。ブロックの解放が済んだ後に呼ぶ。</summary>
        public event Action<EnemyInstance> EnemyDefeated;

        /// <summary>撃破ドロップで財布に加算した。引数は倒した敵と加算したコイン（0 枚のときは呼ばない）。</summary>
        public event Action<EnemyInstance, int> DropGained;

        /// <param name="bufferMultiplier">バッファーの攻撃力倍率（game_config.csv の buffer_atk_multiplier）。</param>
        /// <param name="life">省略するとゴール到達でライフを減らさない。</param>
        /// <param name="wallet">ドロップの加算先（スロットと共有、T301）。wallet と random は両方渡すか両方省略する（省略するとドロップなし）。</param>
        public BattleSimulation(PlacementBoard board, EnemyField enemies, UnitSummoner summoner, UnitRangeTable ranges,
            float bufferMultiplier, PlayerLife life = null, Wallet wallet = null, GameRandom random = null)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            this.summoner = summoner ?? throw new ArgumentNullException(nameof(summoner));
            this.ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
            if (bufferMultiplier < 1f) throw new ArgumentOutOfRangeException(nameof(bufferMultiplier));
            this.bufferMultiplier = bufferMultiplier;
            if ((wallet == null) != (random == null))
                throw new ArgumentException("wallet と random は両方渡すか、両方省略してください");
            this.life = life;
            this.wallet = wallet;
            if (random != null) dropRoller = new EnemyDropRoller(random);
            if (life != null) enemies.ReachedGoal += OnEnemyReachedGoal;
        }

        public void Step(float deltaSeconds)
        {
            if (life != null && life.IsGameOver) return;
            RecomputeBuffs();
            enemies.Tick(deltaSeconds);

            var units = board.Units;
            for (var i = 0; i < units.Count; i++) UnitAct(units[i], deltaSeconds);

            SweepDefeated();
        }

        /// <summary>
        /// 味方が消えるときの窓口。ブロックしていた敵の解放・盤面から外す・配置数を戻す、の3つを必ず行う（T302 の撤退もここを使う）。
        /// 盤面にいない味方なら何もせず false。
        /// </summary>
        public bool RemoveUnit(PlacedUnit unit)
        {
            if (unit == null || board.GetAt(unit.Cell) != unit) return false;
            enemies.ReleaseBlocksOf(unit);
            board.Remove(unit.Cell);
            summoner.ReleaseSlot();
            RecomputeBuffs();
            return true;
        }

        /// <summary>全員の攻撃力倍率を 1 に戻し、バッファーの範囲内の味方に倍率をかける（重ねがけなし、いちばん高い倍率だけ）。</summary>
        public void RecomputeBuffs()
        {
            var units = board.Units;
            for (var i = 0; i < units.Count; i++) units[i].AttackMultiplier = 1f;
            for (var i = 0; i < units.Count; i++)
            {
                if (units[i].Def.Role != UnitRole.Buffer) continue;
                ranges.GetCells(units[i], board.Map, cellBuffer);
                for (var k = 0; k < cellBuffer.Count; k++)
                {
                    var ally = board.GetAt(cellBuffer[k]);
                    if (ally != null && ally.AttackMultiplier < bufferMultiplier) ally.AttackMultiplier = bufferMultiplier;
                }
            }
        }

        private void UnitAct(PlacedUnit unit, float deltaSeconds)
        {
            var role = unit.Def.Role;
            if (role == UnitRole.Buffer) return;

            unit.AttackTimer -= deltaSeconds;
            if (unit.AttackTimer > 0f) return;

            bool acted;
            if (role == UnitRole.Healer)
            {
                ranges.GetCells(unit, board.Map, cellBuffer);
                acted = TryHeal(unit);
            }
            else
            {
                var target = FindAttackTarget(unit);
                acted = target != null;
                if (acted) target.TakeDamage(DamageFormula.Calculate(unit.Atk, target.Def.Def));
            }
            if (acted) unit.AttackTimer += unit.Def.AttackInterval;
            else unit.AttackTimer = 0f;
        }

        // 範囲内（cellBuffer）でHP割合がいちばん低い、減っている味方を回復する。同率なら先に見つかった方（範囲の並び順）。
        private bool TryHeal(PlacedUnit healer)
        {
            PlacedUnit best = null;
            var bestRatio = float.MaxValue;
            for (var k = 0; k < cellBuffer.Count; k++)
            {
                var ally = board.GetAt(cellBuffer[k]);
                if (ally == null || ally.Hp <= 0f || ally.Hp >= ally.Def.MaxHp) continue;
                var ratio = ally.Hp / ally.Def.MaxHp;
                if (ratio >= bestRatio) continue;
                best = ally;
                bestRatio = ratio;
            }
            if (best == null) return false;
            best.Heal(healer.Atk);
            return true;
        }

        /// <summary>
        /// unit がいま攻撃する敵（plan 5章）。向きに回した範囲内で、ブロック数のある職は自分がブロック中の敵を優先し、
        /// その中（いなければ全体）でゴールにいちばん近い（Progress が大きい）敵。同じ進み具合なら出現が早い方。
        /// 空中の敵は HitsAir の職だけ。ヒーラー・バッファーと、範囲内に対象がいないときは null。
        /// </summary>
        public EnemyInstance FindAttackTarget(PlacedUnit unit)
        {
            if (unit.Def.Role == UnitRole.Healer || unit.Def.Role == UnitRole.Buffer) return null;
            ranges.GetCells(unit, board.Map, cellBuffer);
            var canBlock = unit.Def.Block > 0;
            EnemyInstance best = null;
            var bestBlockedByMe = false;
            var active = enemies.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var enemy = active[i];
                if (enemy.Hp <= 0f || (enemy.Def.IsAir && !unit.Def.HitsAir) || !InRange(enemy.CurrentCell)) continue;
                var blockedByMe = canBlock && enemy.BlockedBy == unit;
                if (best != null)
                {
                    if (bestBlockedByMe && !blockedByMe) continue;
                    if (bestBlockedByMe == blockedByMe && enemy.Progress <= best.Progress) continue;
                }
                best = enemy;
                bestBlockedByMe = blockedByMe;
            }
            return best;
        }

        private bool InRange(GridPoint cell)
        {
            for (var k = 0; k < cellBuffer.Count; k++)
                if (cellBuffer[k].X == cell.X && cellBuffer[k].Y == cell.Y) return true;
            return false;
        }

        private void OnEnemyReachedGoal(EnemyInstance enemy) => life.TakeDamage(enemy.Def.LifeDamage);

        private void SweepDefeated()
        {
            defeatedUnits.Clear();
            var units = board.Units;
            for (var i = 0; i < units.Count; i++)
                if (units[i].Hp <= 0f) defeatedUnits.Add(units[i]);
            for (var i = 0; i < defeatedUnits.Count; i++)
                if (RemoveUnit(defeatedUnits[i])) UnitDefeated?.Invoke(defeatedUnits[i]);

            defeatedEnemies.Clear();
            var active = enemies.Active;
            for (var i = 0; i < active.Count; i++)
                if (active[i].Hp <= 0f) defeatedEnemies.Add(active[i]);
            for (var i = 0; i < defeatedEnemies.Count; i++)
            {
                var enemy = defeatedEnemies[i];
                if (!enemies.Remove(enemy)) continue;
                EnemyDefeated?.Invoke(enemy);
                if (dropRoller == null) continue;
                var coins = dropRoller.Roll(enemy.Def);
                if (coins <= 0) continue;
                wallet.Add(coins);
                DropGained?.Invoke(enemy, coins);
            }
        }
    }
}
