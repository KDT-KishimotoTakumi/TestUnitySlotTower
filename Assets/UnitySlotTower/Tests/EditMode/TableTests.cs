using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnitySlotTower.Core.Csv;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    public class TableTests
    {
        private static string TablesDirectory => Path.Combine(Application.dataPath, "UnitySlotTower/Data/Tables");

        private static string ReadFile(string file)
        {
            var path = Path.Combine(TablesDirectory, file);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        [Test]
        public void RealTables_LoadAndValidate()
        {
            var tables = TableSet.Load(ReadFile);
            Assert.DoesNotThrow(() => TableValidator.Validate(tables));
        }

        [Test]
        public void GameConfig_ReadsInitialValues()
        {
            var config = TableSet.Load(ReadFile).GameConfig;
            Assert.AreEqual(30, config.InitialCoins);
            Assert.AreEqual(3, config.Bet);
            Assert.AreEqual(10, config.StartingLife);
            Assert.AreEqual(8, config.MaxDeployed);
            Assert.AreEqual(0.5f, config.RefundRate, 1e-6f);
        }

        [Test]
        public void Load_MissingFileReportsFileName()
        {
            var ex = Assert.Throws<CsvException>(() => TableSet.Load(f => f == TableSet.UnitsFile ? null : ReadFile(f)));
            Assert.AreEqual(TableSet.UnitsFile, ex.FileName);
        }

        [Test]
        public void Validate_FailsOnBrokenColumnAndValue()
        {
            var noColumn = TableSet.Load(f => f == TableSet.WavesFile ? "wave,time\n" : ReadFile(f));
            var ex = Assert.Throws<CsvException>(() => TableValidator.Validate(noColumn));
            Assert.AreEqual(TableSet.WavesFile, ex.FileName);

            var badValue = TableSet.Load(f => f == TableSet.GameConfigFile
                ? ReadFile(f).Replace("refund_rate,0.5", "refund_rate,2")
                : ReadFile(f));
            ex = Assert.Throws<CsvException>(() => { var c = badValue.GameConfig; });
            Assert.AreEqual("value", ex.Column);
        }

        [Test]
        public void PresentationCatalog_ResolvesTablesByName()
        {
            var catalog = ScriptableObject.CreateInstance<PresentationCatalog>();
            var asset = new TextAsset("key,value\n") { name = "game_config" };
            try
            {
                Assert.IsNull(catalog.ReadTable(TableSet.GameConfigFile));

                var serialized = new UnityEditor.SerializedObject(catalog);
                var list = serialized.FindProperty("tables");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = asset;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreEqual("key,value\n", catalog.ReadTable(TableSet.GameConfigFile));
                Assert.IsNull(catalog.ReadTable(TableSet.UnitsFile));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
