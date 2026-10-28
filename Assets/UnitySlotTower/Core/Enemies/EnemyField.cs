using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Core.Enemies
{
    /// <summary>
    /// 戦闘中の敵の集合。Tick(dt) で、出現予定の確認 → ブロック判定・移動 → 攻撃の順に進める。
    /// ゴールに着いた敵は一覧から外して ReachedGoal で知らせる（ライフ減少は T207）。
    /// </summary>
    public sealed class EnemyField
    {
        private readonly PlacementBoard board;
        private readonly WaveSchedule schedule;
        private readonly List<EnemyInstance> active = new List<EnemyInstance>();
        private readonly List<WaveEntry> spawnBuffer = new List<WaveEntry>();

        /// <summary>ゴールに着いた敵。Tick の中で、先に出た敵から順に呼ぶ。</summary>
        public event Action<EnemyInstance> ReachedGoal;

        public IReadOnlyList<EnemyInstance> Active => active;

        /// <summary>schedule は省略できる（Spawn で直接出すだけの使い方）。</summary>
        public EnemyField(PlacementBoard board, WaveSchedule schedule = null)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.schedule = schedule;
        }

        /// <summary>経路を指定して敵を出現口に出す。</summary>
        public EnemyInstance Spawn(EnemyDef def, MapPath path)
        {
            var enemy = new EnemyInstance(def, path);
            active.Add(enemy);
            return enemy;
        }

        public void Tick(float deltaSeconds)
        {
            if (schedule != null)
            {
                spawnBuffer.Clear();
                schedule.Advance(deltaSeconds, spawnBuffer);
                for (var i = 0; i < spawnBuffer.Count; i++) SpawnFromEntry(spawnBuffer[i]);
            }

            // 先に出た敵から順に処理する。ブロックの空きは先に着いた敵が取る（同じステップで同じ味方に着いたときも出現順）。
            for (var i = 0; i < active.Count; i++)
            {
                var enemy = active[i];
                if (!enemy.IsBlocked && !TryBlock(enemy, deltaSeconds)) enemy.Move(deltaSeconds);
                if (enemy.ReachedGoal)
                {
                    active.RemoveAt(i);
                    i--;
                    ReachedGoal?.Invoke(enemy);
                    continue;
                }
                enemy.TryAttack(deltaSeconds, board);
            }
        }

        /// <summary>敵を一覧から外す（撃破用の窓口、T206）。ブロックしていた味方のブロック数も戻す。一覧になければ false。</summary>
        public bool Remove(EnemyInstance enemy)
        {
            var index = active.IndexOf(enemy);
            if (index < 0) return false;
            active.RemoveAt(index);
            enemy.ClearBlock();
            return true;
        }

        /// <summary>
        /// 味方が消えるとき（撤退・消滅）に呼ぶ。その味方がブロックしていた敵を解放し、使っていたブロック数を戻す。
        /// 解放した敵は次の Tick からまた進む。PlacementBoard.Remove と UnitSummoner.ReleaseSlot は呼び出し側が行う。
        /// </summary>
        public int ReleaseBlocksOf(PlacedUnit unit)
        {
            var released = 0;
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].BlockedBy != unit) continue;
                active[i].ClearBlock();
                released++;
            }
            return released;
        }

        /// <summary>
        /// この Tick の移動で通り過ぎるマスのうち、最初にブロックできる味方のいるマスで止める（空中敵はブロックされない）。
        /// 空きが足りない味方のマスは素通りする。止めたら true。
        /// </summary>
        private bool TryBlock(EnemyInstance enemy, float deltaSeconds)
        {
            var def = enemy.Def;
            if (def.IsAir || def.BlockCost <= 0) return false;
            var cells = enemy.Path.Cells;
            var first = (int)enemy.Progress + 1;
            var reach = enemy.ProgressAfter(deltaSeconds);
            var last = cells.Count - 1;   // ゴールのマスには味方を置けない
            for (var k = first; k < last && k <= reach; k++)
            {
                var unit = board.GetAt(cells[k]);
                if (unit == null || unit.Hp <= 0f || !unit.TryReserveBlock(def.BlockCost)) continue;
                enemy.BlockAt(k, unit);
                return true;
            }
            return false;
        }

        private void SpawnFromEntry(WaveEntry entry)
        {
            var path = board.Map.FindPath(entry.PathId);
            if (path == null) throw new InvalidOperationException($"マップ '{board.Map.Id}' にない経路: '{entry.PathId}'");
            Spawn(entry.Enemy, path);
        }
    }
}
