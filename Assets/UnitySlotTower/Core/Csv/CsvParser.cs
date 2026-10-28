using System.Collections.Generic;
using System.Text;

namespace UnitySlotTower.Core.Csv
{
    /// <summary>UTF-8（BOM 可）、引用符、引用符内の改行、空行に対応する CSV パーサー。</summary>
    public static class CsvParser
    {
        private const char Bom = '﻿';

        public static CsvTable Parse(string fileName, string text)
        {
            var records = ReadRecords(fileName, text ?? string.Empty);
            if (records.Count == 0)
                throw new CsvException(fileName, 0, null, "見出し行がありません");

            var header = records[0].Fields;
            var seen = new HashSet<string>();
            for (var i = 0; i < header.Count; i++)
            {
                header[i] = header[i].Trim();
                if (header[i].Length == 0)
                    throw new CsvException(fileName, records[0].Line, $"#{i + 1}", "見出しが空です");
                if (!seen.Add(header[i]))
                    throw new CsvException(fileName, records[0].Line, header[i], "見出しが重複しています");
            }

            var rows = new List<CsvRow>(records.Count - 1);
            for (var r = 1; r < records.Count; r++)
            {
                var record = records[r];
                if (record.Fields.Count != header.Count)
                    throw new CsvException(fileName, record.Line, null,
                        $"列数が見出し（{header.Count}）と一致しません（{record.Fields.Count}）");
                rows.Add(new CsvRow(fileName, record.Line, header, record.Fields));
            }
            return new CsvTable(fileName, header, rows);
        }

        private struct Record
        {
            public int Line;
            public List<string> Fields;
        }

        private static List<Record> ReadRecords(string fileName, string text)
        {
            var records = new List<Record>();
            var fields = new List<string>();
            var field = new StringBuilder();
            var line = 1;
            var recordLine = 1;
            var inQuotes = false;
            var fieldWasQuoted = false;
            var i = text.Length > 0 && text[0] == Bom ? 1 : 0;

            void EndField()
            {
                fields.Add(fieldWasQuoted ? field.ToString() : field.ToString().Trim());
                field.Clear();
                fieldWasQuoted = false;
            }

            void EndRecord()
            {
                EndField();
                var blank = fields.Count == 1 && fields[0].Length == 0;
                if (!blank) records.Add(new Record { Line = recordLine, Fields = fields });
                fields = new List<string>();
            }

            for (; i < text.Length; i++)
            {
                var c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else
                    {
                        if (c == '\n') line++;
                        field.Append(c);
                    }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        if (field.ToString().Trim().Length != 0)
                            throw new CsvException(fileName, line, null, "引用符が欄の途中に現れました");
                        field.Clear();
                        inQuotes = true;
                        fieldWasQuoted = true;
                        break;
                    case ',':
                        EndField();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        EndRecord();
                        line++;
                        recordLine = line;
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (inQuotes)
                throw new CsvException(fileName, recordLine, null, "引用符が閉じていません");
            if (field.Length > 0 || fields.Count > 0) EndRecord();
            return records;
        }
    }
}
