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
            }
            return new WaveTable(byWave);
        }

        private static MapData FindMap(IReadOnlyList<MapData> maps, string id)
        {
            for (var i = 0; i < maps.Count; i++)
                if (maps[i].Id == id) return maps[i];
            return null;
        }
    }
}
