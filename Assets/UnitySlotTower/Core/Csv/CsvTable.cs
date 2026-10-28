using System.Collections.Generic;
using System.Globalization;

namespace UnitySlotTower.Core.Csv
{
    public sealed class CsvTable
    {
        public string FileName { get; }
        public IReadOnlyList<string> Header { get; }
        public IReadOnlyList<CsvRow> Rows { get; }

        public CsvTable(string fileName, IReadOnlyList<string> header, IReadOnlyList<CsvRow> rows)
        {
            FileName = fileName;
            Header = header;
            Rows = rows;
        }
    }

    public sealed class CsvRow
    {
        private readonly IReadOnlyList<string> header;
        private readonly IReadOnlyList<string> fields;

        public string FileName { get; }
        public int Line { get; }

        public CsvRow(string fileName, int line, IReadOnlyList<string> header, IReadOnlyList<string> fields)
        {
            FileName = fileName;
            Line = line;
            this.header = header;
            this.fields = fields;
        }

        public bool Has(string column) => IndexOf(column) >= 0;

        public string GetString(string column) => fields[RequireIndex(column)];

        public int GetInt(string column)
        {
            var text = GetString(column);
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw Error(column, $"整数として読めません: '{text}'");
            return value;
        }

        public float GetFloat(string column)
        {
            var text = GetString(column);
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw Error(column, $"数値として読めません: '{text}'");
            return value;
        }

        public bool GetBool(string column)
        {
            var text = GetString(column);
            switch (text.ToLowerInvariant())
            {
                case "1": case "true": return true;
                case "0": case "false": return false;
                default: throw Error(column, $"真偽値として読めません: '{text}'");
            }
        }

        /// <summary>確率。小数（0.25）か分母表記（1/7.3）を受け付ける。0〜1 の範囲外は不可。</summary>
        public double GetProbability(string column)
        {
            var text = GetString(column);
            double value;
            var slash = text.IndexOf('/');
            if (slash >= 0)
            {
                if (!TryParseDouble(text.Substring(0, slash), out var numerator)
                    || !TryParseDouble(text.Substring(slash + 1), out var denominator)
                    || denominator <= 0)
                    throw Error(column, $"分母表記として読めません: '{text}'");
                value = numerator / denominator;
            }
            else if (!TryParseDouble(text, out value))
            {
                throw Error(column, $"確率として読めません: '{text}'");
            }
            if (value < 0 || value > 1)
                throw Error(column, $"確率が 0〜1 の範囲外です: '{text}'");
            return value;
        }

        public CsvException Error(string column, string message) => new CsvException(FileName, Line, column, message);

        private static bool TryParseDouble(string text, out double value) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private int IndexOf(string column)
        {
            for (var i = 0; i < header.Count; i++)
                if (header[i] == column) return i;
            return -1;
        }

        private int RequireIndex(string column)
        {
            var index = IndexOf(column);
            if (index < 0) throw Error(column, "列がありません");
            return index;
        }
    }
}
