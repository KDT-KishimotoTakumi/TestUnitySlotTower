using NUnit.Framework;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Tests
{
    public class MapTests
    {
        private const string GridFile = "maps/w1.csv";
        private const string GroundFirstRow = "w1,g1,0,0,1,ground";

        private static TableSet LoadWith(string file, string from, string to)
        {
            return TableSet.Load(f => f == file ? TableTests.ReadFile(f).Replace(from, to) : TableTests.ReadFile(f));
        }

        /// <summary>map_paths.csv を、見出しと指定の行だけに差し替える。</summary>
        private static TableSet WithPaths(string rows)
        {
            return TableSet.Load(f => f == TableSet.MapPathsFile
                ? "map_id,path_id,order,x,y,kind\n" + rows
                : TableTests.ReadFile(f));
        }

        private static CsvException ValidateFails(TableSet tables) =>
            Assert.Throws<CsvException>(() => TableValidator.Validate(tables));

        [Test]
        public void W1_LoadsGridSpawnGoalAndPaths()
        {
            var map = TableSet.Load(TableTests.ReadFile).Maps[0];
            Assert.AreEqual("w1", map.Id);
            Assert.AreEqual(9, map.Width);
            Assert.AreEqual(6, map.Height);
            Assert.AreEqual(CellType.Spawn, map.GetCell(0, 1));
            Assert.AreEqual(new GridPoint(0, 1), map.Spawns["S1"]);
            Assert.AreEqual(1, map.Goals.Count);
            Assert.AreEqual(CellType.Goal, map.GetCell(map.Goals[0]));
            Assert.AreEqual(CellType.High, map.GetCell(0, 0));
            Assert.AreEqual(CellType.Blocked, map.GetCell(4, 0));

            var ground = map.FindPath("g1");
            var air = map.FindPath("a1");
            Assert.IsFalse(ground.IsAir);
            Assert.IsTrue(air.IsAir);
            Assert.AreEqual(12, ground.Cells.Count);
            Assert.AreEqual(map.Spawns["S1"].ToString(), ground.Cells[0].ToString());
            Assert.AreEqual(map.Goals[0].ToString(), air.Cells[air.Cells.Count - 1].ToString());
        }

        [Test]
        public void W1_AirPathCrossesBlockedCell()
        {
            var map = TableSet.Load(TableTests.ReadFile).Maps[0];
            var crossed = false;
            foreach (var point in map.FindPath("a1").Cells)
                crossed |= map.GetCell(point) == CellType.Blocked;
            Assert.IsTrue(crossed);
        }

        [Test]
        public void Validate_FailsOnUnknownSymbol()
        {
            var ex = ValidateFails(LoadWith(GridFile, "S1,G,G,G,G", "S1,G,Q,G,G"));
            Assert.AreEqual(GridFile, ex.FileName);
            Assert.AreEqual(3, ex.Line);
            Assert.AreEqual("x2", ex.Column);
        }

        [Test]
        public void Validate_FailsWithoutSpawn()
        {
            var ex = ValidateFails(LoadWith(GridFile, "S1,G,G,G,G", "G,G,G,G,G"));
            Assert.AreEqual(GridFile, ex.FileName);
        }

        [Test]
        public void Validate_FailsWithoutGoal()
        {
            var ex = ValidateFails(LoadWith(GridFile, "G,G,G,G,E", "G,G,G,G,G"));
            Assert.AreEqual(GridFile, ex.FileName);
        }

        [Test]
        public void Validate_FailsWhenPathDoesNotStartAtSpawn()
        {
            var ex = ValidateFails(WithPaths("w1,g1,0,7,4,ground\nw1,g1,1,8,4,ground\n"));
            Assert.AreEqual(TableSet.MapPathsFile, ex.FileName);
            StringAssert.Contains("出現口から始まっていません", ex.Message);
        }

        [Test]
        public void Validate_FailsWhenPathDoesNotEndAtGoal()
        {
            var ex = ValidateFails(WithPaths("w1,g1,0,0,1,ground\nw1,g1,1,1,1,ground\n"));
            Assert.AreEqual(TableSet.MapPathsFile, ex.FileName);
            StringAssert.Contains("ゴールで終わっていません", ex.Message);
        }

        [Test]
        public void Validate_FailsWhenPathJumpsOverCells()
        {
            var ex = ValidateFails(LoadWith(TableSet.MapPathsFile, "w1,g1,2,2,1,ground", "w1,g1,2,3,1,ground"));
            Assert.AreEqual(TableSet.MapPathsFile, ex.FileName);
            Assert.AreEqual(4, ex.Line);
            Assert.AreEqual("x", ex.Column);
        }

        [Test]
        public void Validate_FailsWhenGroundPathCrossesBlockedCell()
        {
            // (4,3) を通行不可にする。空中経路は通れるが地上経路は通れない
            var tables = LoadWith(GridFile, "H,H,X,H,G,H,X,H,H", "H,H,X,H,X,H,X,H,H");
            var ex = ValidateFails(tables);
            Assert.AreEqual(TableSet.MapPathsFile, ex.FileName);
            StringAssert.Contains("通行不可", ex.Message);
        }

        [Test]
        public void Validate_FailsWhenGroundPathCrossesHighCell()
        {
            var ex = ValidateFails(LoadWith(GridFile, "H,H,H,H,G,H,H,H,H", "H,H,H,H,H,H,H,H,H"));
            Assert.AreEqual(TableSet.MapPathsFile, ex.FileName);
            StringAssert.Contains("高台", ex.Message);
        }

        [Test]
        public void Validate_FailsOnBrokenOrderKindAndMapId()
        {
            var order = ValidateFails(LoadWith(TableSet.MapPathsFile, "w1,g1,1,1,1,ground", "w1,g1,5,1,1,ground"));
            Assert.AreEqual("order", order.Column);

            var kind = ValidateFails(LoadWith(TableSet.MapPathsFile, GroundFirstRow, "w1,g1,0,0,1,walk"));
            Assert.AreEqual("kind", kind.Column);

            var mapId = ValidateFails(TableSet.Load(f => f == TableSet.MapPathsFile
                ? TableTests.ReadFile(f) + "w9,g1,0,0,1,ground\n"
                : TableTests.ReadFile(f)));
            Assert.AreEqual("map_id", mapId.Column);
        }

        [Test]
        public void Validate_FailsOnOutOfRangePoint()
        {
            var ex = ValidateFails(LoadWith(TableSet.MapPathsFile, "w1,a1,3,1,3,air", "w1,a1,3,1,9,air"));
            Assert.AreEqual(TableSet.MapPathsFile, ex.FileName);
        }
    }
}
