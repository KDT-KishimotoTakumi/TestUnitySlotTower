using NUnit.Framework;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Tests
{
    public class CsvParserTests
    {
        [Test]
        public void Parse_ReadsHeaderAndRows()
        {
            var table = CsvParser.Parse("t.csv", "id,hp\na,10\nb,20\n");
            Assert.AreEqual(2, table.Rows.Count);
            Assert.AreEqual("b", table.Rows[1].GetString("id"));
            Assert.AreEqual(20, table.Rows[1].GetInt("hp"));
        }

        [Test]
        public void Parse_SkipsBomBlankLinesAndCrLf()
        {
            var table = CsvParser.Parse("t.csv", "﻿id,hp\r\n\r\na,1\r\n\r\n");
            Assert.AreEqual(1, table.Rows.Count);
            Assert.AreEqual("id", table.Header[0]);
            Assert.AreEqual(3, table.Rows[0].Line);
        }

        [Test]
        public void Parse_HandlesQuotedCommaQuoteAndNewline()
        {
            var table = CsvParser.Parse("t.csv", "id,note\na,\"x,\"\"y\"\"\nz\"\nb,w\n");
            Assert.AreEqual("x,\"y\"\nz", table.Rows[0].GetString("note"));
            Assert.AreEqual(4, table.Rows[1].Line);
        }

        [Test]
        public void GetProbability_AcceptsDenominatorNotation()
        {
            var table = CsvParser.Parse("t.csv", "p\n1/7.3\n0.25\n");
            Assert.AreEqual(1.0 / 7.3, table.Rows[0].GetProbability("p"), 1e-12);
            Assert.AreEqual(0.25, table.Rows[1].GetProbability("p"), 1e-12);
        }

        [Test]
        public void Errors_ReportFileLineAndColumn()
        {
            var table = CsvParser.Parse("units.csv", "id,hp\na,x\n");
            var ex = Assert.Throws<CsvException>(() => table.Rows[0].GetInt("hp"));
            Assert.AreEqual("units.csv", ex.FileName);
            Assert.AreEqual(2, ex.Line);
            Assert.AreEqual("hp", ex.Column);
            StringAssert.Contains("units.csv 行2 列'hp'", ex.Message);
        }

        [Test]
        public void Parse_RejectsBadStructure()
        {
            Assert.Throws<CsvException>(() => CsvParser.Parse("t.csv", ""));
            Assert.Throws<CsvException>(() => CsvParser.Parse("t.csv", "a,a\n1,2\n"));
            Assert.Throws<CsvException>(() => CsvParser.Parse("t.csv", "a,b\n1\n"));
            Assert.Throws<CsvException>(() => CsvParser.Parse("t.csv", "a\n\"open\n"));
        }

        [Test]
        public void GetProbability_RejectsOutOfRange()
        {
            var table = CsvParser.Parse("t.csv", "p\n2\n1/0\n");
            Assert.Throws<CsvException>(() => table.Rows[0].GetProbability("p"));
            Assert.Throws<CsvException>(() => table.Rows[1].GetProbability("p"));
        }
    }
}
