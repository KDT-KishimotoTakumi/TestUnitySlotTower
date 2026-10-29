using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Tests
{
    /// <summary>T305: 全マップ・全ウェーブの検証（FR-010）。実データ（Data/Tables）を読む。</summary>
    public class MapWaveTests
    {
        private const int ExpectedWidth = 9;
        private const int ExpectedHeight = 6;
        private const float EndOfWave = WaveTable.MaxSpawnTime;
        private const string MeleeId = "melee_grunt";
        private const string RangedId = "ranged_grunt";
        private const string AirId = "air_grunt";
        private const int SecondWave = 2;
        private const int ThirdWave = 3;
        private const int FourthWave = 4;
        private const int FifthWave = 5;
        private const int FourthWaveSpawnCount = 2;

        private static TableSet Tables => TableSet.Load(TableTests.ReadFile);

        private static List<WaveEntry> EntriesOf(TableSet tables, int wave)
        {
            var entries = new List<WaveEntry>();
            tables.Waves.CreateSchedule(wave).Advance(EndOfWave, entries);
            return entries;
        }

        private static List<string> EnemyIdsOf(int wave)
        {
            var tables = Tables;
            var ids = new List<string>();
            foreach (var entry in EntriesOf(tables, wave)) ids.Add(entry.Enemy.Id);
            return ids;
        }

        [Test]
        public void MapFiles_MatchWaveMapIds_InOrder()
        {
            var maps = Tables.Maps;
            Assert.AreEqual(TableSet.MapFiles.Count, maps.Count);
            Assert.AreEqual(WaveFlow.DefaultWaveCount, maps.Count);
            for (var wave = 1; wave <= WaveFlow.DefaultWaveCount; wave++)
            {
                Assert.AreEqual(WaveTable.MapIdOf(wave), maps[wave - 1].Id, $"MapFiles の {wave} 番目");
                StringAssert.EndsWith(WaveTable.MapIdOf(wave) + ".csv", TableSet.MapFiles[wave - 1]);
            }
        }

        [Test]
        public void TableFiles_HaveUniqueFileNames()
        {
            // PresentationCatalog.ReadTable はファイル名だけで照合する
            var names = new HashSet<string>();
            foreach (var file in TableSet.MapFiles) Assert.IsTrue(names.Add(Path.GetFileName(file)), file);
            foreach (var file in TableSet.AllFiles) Assert.IsTrue(names.Add(Path.GetFileName(file)), file);
        }

        [Test]
        public void AllMaps_ShareGridSize()
        {
            // BattleView / PlacementMarkerView は座標変換を作り直さない（BattleFieldView.ShowMap の前提）
            foreach (var map in Tables.Maps)
            {
                Assert.AreEqual(ExpectedWidth, map.Width, map.Id);
                Assert.AreEqual(ExpectedHeight, map.Height, map.Id);
            }
        }

        [Test]
        public void AllMaps_PathsAreContinuousAndRunSpawnToGoal()
        {
            foreach (var map in Tables.Maps)
            {
                Assert.GreaterOrEqual(map.Paths.Count, 1, map.Id);
                foreach (var path in map.Paths)
                {
                    var label = $"{map.Id}/{path.Id}";
                    Assert.GreaterOrEqual(path.Cells.Count, 2, label);
                    Assert.AreEqual(CellType.Spawn, map.GetCell(path.Cells[0]), label);
                    Assert.AreEqual(CellType.Goal, map.GetCell(path.Cells[path.Cells.Count - 1]), label);
                    for (var i = 1; i < path.Cells.Count; i++)
                        Assert.IsTrue(path.Cells[i - 1].IsAdjacentTo(path.Cells[i]), $"{label} の {i} 番目");
                    if (path.IsAir) continue;
                    foreach (var cell in path.Cells)
                    {
                        var type = map.GetCell(cell);
                        Assert.AreNotEqual(CellType.Blocked, type, $"{label} {cell}");
                        Assert.AreNotEqual(CellType.High, type, $"{label} {cell}");
                    }
                }
            }
        }

        [Test]
        public void AllMaps_HaveHighGroundForRangedAllies()
        {
            foreach (var map in Tables.Maps)
            {
                var high = 0;
                for (var y = 0; y < map.Height; y++)
                    for (var x = 0; x < map.Width; x++)
                        if (map.GetCell(x, y) == CellType.High) high++;
                Assert.Greater(high, 0, $"{map.Id} に高台がありません");
            }
        }

        [Test]
        public void AllMaps_HaveDistinctLayouts()
        {
            var maps = Tables.Maps;
            for (var a = 0; a < maps.Count; a++)
            {
                for (var b = a + 1; b < maps.Count; b++)
                {
                    var same = true;
                    for (var y = 0; y < ExpectedHeight && same; y++)
                        for (var x = 0; x < ExpectedWidth && same; x++)
                            same = maps[a].GetCell(x, y) == maps[b].GetCell(x, y);
                    Assert.IsFalse(same, $"{maps[a].Id} と {maps[b].Id} のグリッドが同じです");
                }
            }
        }

        [Test]
        public void EveryWave_HasEntries_WithValidSpawnAndPathReferences()
        {
            var tables = Tables;
            var maps = tables.Maps;
            var waves = tables.Waves;
            for (var wave = 1; wave <= WaveFlow.DefaultWaveCount; wave++)
            {
                Assert.IsTrue(waves.HasWave(wave), $"waves.csv にウェーブ {wave} がありません");
                var map = maps[wave - 1];
                var entries = EntriesOf(tables, wave);
                Assert.Greater(entries.Count, 0, $"ウェーブ {wave}");
                foreach (var entry in entries)
                {
                    var label = $"ウェーブ {wave} t={entry.Time} {entry.Enemy.Id}";
                    Assert.IsTrue(map.Spawns.TryGetValue(entry.Spawn, out var spawnPoint), $"{label}: 出現口 {entry.Spawn}");
                    var path = map.FindPath(entry.PathId);
                    Assert.IsNotNull(path, $"{label}: 経路 {entry.PathId}");
                    Assert.AreEqual(entry.Enemy.IsAir, path.IsAir, $"{label}: 空中と地上の経路");
                    Assert.AreEqual(spawnPoint.ToString(), path.Cells[0].ToString(), $"{label}: 経路の始点");
                }
            }
        }

        [Test]
        public void EveryWave_HasOneBossAtTheEnd_FinalBossOnlyInWave5()
        {
            // ボスの中身（ステータス・固有行動・出現順）は BossTests (T306)
            var tables = Tables;
            for (var wave = 1; wave <= FifthWave; wave++)
            {
                var bosses = 0;
                foreach (var entry in EntriesOf(tables, wave))
                {
                    if (!entry.IsBoss) continue;
                    bosses++;
                    Assert.AreEqual(wave == FifthWave ? EnemyKind.FinalBoss : EnemyKind.MidBoss, entry.Enemy.Kind, $"ウェーブ {wave}");
                }
                Assert.AreEqual(1, bosses, $"ウェーブ {wave}");
            }
        }

        [Test]
        public void Wave2_AddsRangedGrunts_WithoutAir()
        {
            var ids = EnemyIdsOf(SecondWave);
            CollectionAssert.Contains(ids, MeleeId);
            CollectionAssert.Contains(ids, RangedId);
            CollectionAssert.DoesNotContain(ids, AirId);
        }

        [Test]
        public void Wave3_IntroducesAirGrunts()
        {
            CollectionAssert.Contains(EnemyIdsOf(ThirdWave), AirId);
        }

        [Test]
        public void Wave4_UsesTwoSpawns()
        {
            var tables = Tables;
            Assert.AreEqual(FourthWaveSpawnCount, tables.Maps[FourthWave - 1].Spawns.Count);
            var used = new HashSet<string>();
            foreach (var entry in EntriesOf(tables, FourthWave)) used.Add(entry.Spawn);
            Assert.AreEqual(FourthWaveSpawnCount, used.Count);
        }

        [Test]
        public void Wave5_MixesAllEnemyKinds()
        {
            var ids = EnemyIdsOf(FifthWave);
            CollectionAssert.Contains(ids, MeleeId);
            CollectionAssert.Contains(ids, RangedId);
            CollectionAssert.Contains(ids, AirId);
        }
    }
}
