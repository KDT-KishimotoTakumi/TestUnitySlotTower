using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Core.Enemies
{
    /// <summary>
    /// 戦闘中の敵の集合。Tick(dt) で、出現予定の確認 → 移動 → 攻撃の順に進める。
    /// ゴールに着いた敵は一覧から外して ReachedGoal で知らせる（ライフ減少は T207）。
    /// </summary>
    public sealed class EnemyField
    {
        private readonly PlacementBoard board;
        private readonly WaveSchedule schedule;
        private readonly List<EnemyInstance> active = new List<EnemyInstance>();
        private readonly List<WaveEntry> spawnBuffer = new List<WaveEntry>();

        /// <summary>ゴールに着いた敵。Tick の中で、後から出た敵から順に呼ぶ。</summary>
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

            for (var i = active.Count - 1; i >= 0; i--)
            {
                var enemy = active[i];
                enemy.Move(deltaSeconds);
                if (enemy.ReachedGoal)
                {
                    active.RemoveAt(i);
                    ReachedGoal?.Invoke(enemy);
                    continue;
                }
                enemy.TryAttack(deltaSeconds, board);
            }
        }

        private void SpawnFromEntry(WaveEntry entry)
        {
            var path = board.Map.FindPath(entry.PathId);
            if (path == null) throw new InvalidOperationException($"マップ '{board.Map.Id}' にない経路: '{entry.PathId}'");
            Spawn(entry.Enemy, path);
        }
    }
}
