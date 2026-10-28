using System;

namespace UnitySlotTower.Core.Csv
{
    /// <summary>CSV の読み取り・検証エラー。ファイル名・行・列を含める（FR-050）。</summary>
    public sealed class CsvException : Exception
    {
        public string FileName { get; }
        public int Line { get; }
        public string Column { get; }

        public CsvException(string fileName, int line, string column, string message)
            : base(Format(fileName, line, column, message))
        {
            FileName = fileName;
            Line = line;
            Column = column;
        }

        private static string Format(string fileName, int line, string column, string message)
        {
            var location = fileName;
            if (line > 0) location += $" 行{line}";
            if (!string.IsNullOrEmpty(column)) location += $" 列'{column}'";
            return $"{location}: {message}";
        }
    }
}
