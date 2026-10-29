using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Enemies
{
    /// <summary>
    /// waves.csv の読み取り専用データ。ウェーブ N のマップは ID が "w" + N のマップ（maps/wN.csv）。
    /// </summary>
    public sealed class WaveTable
    {
        public const string MapIdPrefix = "w";
        /// <summary>出現時刻の上限（秒）。入力ミスの検出用で、ウェーブの長さの目安（2〜3分）より十分大きくとる。</summary>
        public const float MaxSpawnTime = 600f;

        private readonly Dictionary<int, List<WaveEntry>> byWave;

        private WaveTable(Dictionary<int, List<WaveEntry>> byWave)
        {
            this.byWave = byWave;
        }

        public bool HasWave(int wave) => byWave.ContainsKey(wave);

        /// <summary>ウェーブの出現予定。呼ぶたびに新しい WaveSchedule を作る。</summary>
        public WaveSchedule CreateSchedule(int wave)
        {
            if (!byWave.TryGetValue(wave, out var entries))
                throw new ArgumentException($"waves.csv にないウェーブ: {wave}", nameof(wave));
            return new WaveSchedule(entries);
        }

        public static string MapIdOf(int wave) => MapIdPrefix + wave;

        public static WaveTable FromTable(CsvTable table, EnemyTable enemies, IReadOnlyList<MapData> maps)
        {
            var byWave = new Dictionary<int, List<WaveEntry>>();
            var rowsByWave = new Dictionary<int, List<CsvRow>>();
            foreach (var row in table.Rows)
            {
                var wave = row.GetInt("wave");
                if (wave < 1) throw row.Error("wave", $"wave は 1 以上にしてください: {wave}");
                var map = FindMap(maps, MapIdOf(wave));
                if (map == null) throw row.Error("wave", $"ウェーブ {wave} のマップ '{MapIdOf(wave)}' がありません");

                var time = row.GetFloat("time");
                if (!(time >= 0f && time <= MaxSpawnTime))
                    throw row.Error("time", $"time は 0〜{MaxSpawnTime} 秒にしてください: {row.GetString("time")}");

                var enemyId = row.GetString("enemy_id");
                if (!enemies.TryGet(enemyId, out var enemy)) throw row.Error("enemy_id", $"enemies.csv にない敵: '{enemyId}'");

                var spawn = row.GetString("spawn");
                if (!map.Spawns.TryGetValue(spawn, out var spawnPoint))
                    throw row.Error("spawn", $"マップ '{map.Id}' にない出現口: '{spawn}'");

                var pathId = row.GetString("path_id");
                var path = map.FindPath(pathId);
                if (path == null) throw row.Error("path_id", $"マップ '{map.Id}' にない経路: '{pathId}'");
                if (path.IsAir != enemy.IsAir)
                    throw row.Error("path_id", $"経路 '{pathId}' は{(path.IsAir ? "空中" : "地上")}経路ですが、敵 '{enemyId}' は{(enemy.IsAir ? "空中" : "地上")}の敵です");
                var start = path.Cells[0];
                if (start.X != spawnPoint.X || start.Y != spawnPoint.Y)
                    throw row.Error("path_id", $"経路 '{pathId}' は出現口 '{spawn}' {spawnPoint} から始まっていません: {start}");

                var isBoss = row.GetBool("is_boss");
                var bossKind = enemy.Kind == EnemyKind.MidBoss || enemy.Kind == EnemyKind.FinalBoss;
                if (isBoss != bossKind)
                    throw row.Error("is_boss", $"is_boss は敵 '{enemyId}' の種類（ボスかどうか）と合わせてください");

                if (!byWave.TryGetValue(wave, out var list))
                {
                    list = new List<WaveEntry>();
                    byWave[wave] = list;
                }
                list.Add(new WaveEntry(wave, time, enemy, spawn, pathId, isBoss));
                rowsByWave.TryGetValue(wave, out var rows);
                if (rows == null)
                {
                    rows = new List<CsvRow>();
                    rowsByWave[wave] = rows;
                }
                rows.Add(row);
            }
            ValidateBosses(byWave, rowsByWave);
            return new WaveTable(byWave);
        }

        // ボスの並び（FR-010）: 各ウェーブにボスが 1 体で、そのウェーブの最後に出る。
        // 最終ボスは最大のウェーブ番号にだけ置き、最大のウェーブのボスは必ず最終ボス（それ以外のウェーブは中ボス）。
        private static void ValidateBosses(Dictionary<int, List<WaveEntry>> byWave, Dictionary<int, List<CsvRow>> rowsByWave)
        {
            var finalWave = 0;
            foreach (var wave in byWave.Keys) finalWave = Math.Max(finalWave, wave);

            foreach (var pair in byWave)
            {
                var wave = pair.Key;
                var entries = pair.Value;
                var rows = rowsByWave[wave];
                var bossIndex = -1;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (!entries[i].IsBoss) continue;
                    if (bossIndex >= 0) throw rows[i].Error("is_boss", $"ウェーブ {wave} のボスは 1 体だけにしてください");
                    bossIndex = i;
                }
                if (bossIndex < 0) throw rows[rows.Count - 1].Error("is_boss", $"ウェーブ {wave} にボス（is_boss=1）がありません");

                var boss = entries[bossIndex];
                for (var i = 0; i < entries.Count; i++)
                    if (i != bossIndex && entries[i].Time >= boss.Time)
                        throw rows[bossIndex].Error("time", $"ウェーブ {wave} のボスは最後に出してください（ボスより遅いか同時の出現があります）");

                var expected = wave == finalWave ? EnemyKind.FinalBoss : EnemyKind.MidBoss;
                if (boss.Enemy.Kind != expected)
                    throw rows[bossIndex].Error("enemy_id", wave == finalWave
                        ? $"最終ウェーブ {wave} のボスは final_boss にしてください"
                        : $"final_boss は最終ウェーブ {finalWave} だけに置けます（ウェーブ {wave} は mid_boss）");
            }
        }

        private static MapData FindMap(IReadOnlyList<MapData> maps, string id)
        {
            for (var i = 0; i < maps.Count; i++)
                if (maps[i].Id == id) return maps[i];
            return null;
        }
    }
}
