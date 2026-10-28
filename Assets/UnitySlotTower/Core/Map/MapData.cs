using System.Collections.Generic;
using UnitySlotTower.Core.Csv;

namespace UnitySlotTower.Core.Map
{
    /// <summary>maps/*.csv（グリッド）と map_paths.csv（経路）から作る読み取り専用のマップ。</summary>
    public sealed class MapData
    {
        public const string PathMapIdColumn = "map_id";
        public const string PathIdColumn = "path_id";
        public const string PathOrderColumn = "order";
        public const string PathXColumn = "x";
        public const string PathYColumn = "y";
        public const string PathKindColumn = "kind";
        public const string GroundKind = "ground";
        public const string AirKind = "air";

        private const string ColumnPrefix = "x";
        private const char GroundSymbol = 'G';
        private const char HighSymbol = 'H';
        private const char BlockedSymbol = 'X';
        private const char SpawnSymbol = 'S';
        private const char GoalSymbol = 'E';

        private readonly CellType[,] cells;
        private readonly List<MapPath> paths = new List<MapPath>();

        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>出現口の記号（"S1" など）と座標。</summary>
        public IReadOnlyDictionary<string, GridPoint> Spawns { get; }
        public IReadOnlyList<GridPoint> Goals { get; }
        public IReadOnlyList<MapPath> Paths => paths;

        private MapData(string id, CellType[,] cells, Dictionary<string, GridPoint> spawns, List<GridPoint> goals)
        {
            Id = id;
            this.cells = cells;
            Width = cells.GetLength(0);
            Height = cells.GetLength(1);
            Spawns = spawns;
            Goals = goals;
        }

        public CellType GetCell(int x, int y) => cells[x, y];

        public CellType GetCell(GridPoint point) => cells[point.X, point.Y];

        public bool Contains(GridPoint point) => point.X >= 0 && point.Y >= 0 && point.X < Width && point.Y < Height;

        public MapPath FindPath(string pathId)
        {
            foreach (var path in paths)
                if (path.Id == pathId) return path;
            return null;
        }

        /// <summary>グリッドの CSV を読む。見出しは x0,x1,...、各行が 1 行分のマス。</summary>
        public static MapData FromGridTable(string id, CsvTable table)
        {
            var width = table.Header.Count;
            for (var x = 0; x < width; x++)
                if (table.Header[x] != ColumnPrefix + x)
                    throw new CsvException(table.FileName, 1, table.Header[x], $"見出しは '{ColumnPrefix}{x}' の並びにしてください");
            if (table.Rows.Count == 0)
                throw new CsvException(table.FileName, 0, null, "マスの行がありません");

            var grid = new CellType[width, table.Rows.Count];
            var spawns = new Dictionary<string, GridPoint>();
            var goals = new List<GridPoint>();
            for (var y = 0; y < table.Rows.Count; y++)
            {
                var row = table.Rows[y];
                for (var x = 0; x < width; x++)
                {
                    var column = ColumnPrefix + x;
                    var symbol = row.GetString(column);
                    grid[x, y] = ParseSymbol(row, column, symbol);
                    if (grid[x, y] == CellType.Spawn)
                    {
                        if (spawns.ContainsKey(symbol)) throw row.Error(column, $"出現口 '{symbol}' が重複しています");
                        spawns[symbol] = new GridPoint(x, y);
                    }
                    else if (grid[x, y] == CellType.Goal)
                    {
                        goals.Add(new GridPoint(x, y));
                    }
                }
            }

            if (spawns.Count == 0) throw new CsvException(table.FileName, 0, null, "出現口（S1 など）がありません");
            if (goals.Count == 0) throw new CsvException(table.FileName, 0, null, "ゴール（E）がありません");
            return new MapData(id, grid, spawns, goals);
        }

        /// <summary>map_paths.csv の、このマップの行を読んで経路を作る。</summary>
        public void AddPaths(CsvTable table)
        {
            var points = new Dictionary<string, List<GridPoint>>();
            var airFlags = new Dictionary<string, bool>();
            var firstRows = new Dictionary<string, CsvRow>();
            var pathOrder = new List<string>();
            foreach (var row in table.Rows)
            {
                if (row.GetString(PathMapIdColumn) != Id) continue;
                var pathId = row.GetString(PathIdColumn);
                var kind = row.GetString(PathKindColumn);
                if (kind != GroundKind && kind != AirKind)
                    throw row.Error(PathKindColumn, $"'{GroundKind}' か '{AirKind}' を指定してください: '{kind}'");
                var isAir = kind == AirKind;

                if (!points.TryGetValue(pathId, out var list))
                {
                    list = new List<GridPoint>();
                    points[pathId] = list;
                    airFlags[pathId] = isAir;
                    firstRows[pathId] = row;
                    pathOrder.Add(pathId);
                }
                else if (airFlags[pathId] != isAir)
                {
                    throw row.Error(PathKindColumn, $"経路 '{pathId}' の種別が行ごとに違います");
                }

                if (row.GetInt(PathOrderColumn) != list.Count)
                    throw row.Error(PathOrderColumn, $"order は 0 から 1 ずつ増やしてください（ここは {list.Count}）");

                var point = new GridPoint(row.GetInt(PathXColumn), row.GetInt(PathYColumn));
                if (!Contains(point)) throw row.Error(PathXColumn, $"マップの外です: {point}");
                if (list.Count > 0 && !list[list.Count - 1].IsAdjacentTo(point))
                    throw row.Error(PathXColumn, $"前のマス {list[list.Count - 1]} と隣り合っていません: {point}");
                if (!isAir && GetCell(point) == CellType.Blocked)
                    throw row.Error(PathXColumn, $"地上経路が通行不可のマスを通っています: {point}");
                if (!isAir && GetCell(point) == CellType.High)
                    throw row.Error(PathXColumn, $"地上経路が高台のマスを通っています: {point}");
                list.Add(point);
            }

            foreach (var pathId in pathOrder)
            {
                var list = points[pathId];
                var first = firstRows[pathId];
                if (GetCell(list[0]) != CellType.Spawn)
                    throw first.Error(PathXColumn, $"経路 '{pathId}' が出現口から始まっていません: {list[0]}");
                if (GetCell(list[list.Count - 1]) != CellType.Goal)
                    throw first.Error(PathXColumn, $"経路 '{pathId}' がゴールで終わっていません: {list[list.Count - 1]}");
                paths.Add(new MapPath(pathId, airFlags[pathId], list));
            }
        }

        private static CellType ParseSymbol(CsvRow row, string column, string symbol)
        {
            if (symbol.Length == 1)
            {
                switch (symbol[0])
                {
                    case GroundSymbol: return CellType.Ground;
                    case HighSymbol: return CellType.High;
                    case BlockedSymbol: return CellType.Blocked;
                    case GoalSymbol: return CellType.Goal;
                }
            }
            else if (symbol.Length == 2 && symbol[0] == SpawnSymbol && symbol[1] >= '1' && symbol[1] <= '9')
            {
                return CellType.Spawn;
            }
            throw row.Error(column, $"マスの記号が不明です: '{symbol}'");
        }
    }
}
