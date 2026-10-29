using UnityEngine;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 戦闘フィールドの仮表示（グレーボックス）。マップ CSV のグリッドと経路を、マス種別ごとの色の直方体で並べる。T201。
    /// ウェーブごとのマップへは <see cref="ShowMap"/> で作り直す（T305）。マスと経路の実体は専用の子（<see cref="CellsName"/>）にまとめ、味方・敵の表示とは分ける。
    /// </summary>
    public sealed class BattleFieldView : MonoBehaviour
    {
        private const float CellFootprint = 0.96f;
        private const float GroundHeight = 0.1f;
        private const float HighHeight = 0.5f;
        private const float BlockedHeight = 0.9f;
        private const float GroundPathSize = 0.4f;
        private const float GroundPathThickness = 0.04f;
        private const float AirPathSize = 0.25f;
        public const float AirPathHeight = 1.3f;
        private const float PillarRadius = 0.5f;
        private const float PillarHeight = 0.8f;
        private const float CylinderHeightPerScale = 2f;
        private const float Half = 0.5f;
        private const string CellsName = "Cells";

        // 手前（高い面）から順に調べる
        private static readonly float[] PickHeights = { BlockedHeight, HighHeight, GroundHeight };

        [SerializeField] private PresentationCatalog catalog;
        [SerializeField] private string mapId = "w1";
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material highMaterial;
        [SerializeField] private Material blockedMaterial;
        [SerializeField] private Material spawnMaterial;
        [SerializeField] private Material goalMaterial;
        [SerializeField] private Material groundPathMaterial;
        [SerializeField] private Material airPathMaterial;

        public MapData Map { get; private set; }

        private Transform cellsRoot;

        /// <summary>マス座標とフィールドのローカル座標の変換（Map ができた後に使う）。</summary>
        public FieldGeometry Geometry => new FieldGeometry(Map.Width, Map.Height);

        /// <summary>マスの上面の高さ（その上に立つ味方・敵の足元）。</summary>
        public float SurfaceHeight(GridPoint cell) => CellHeight(Map.GetCell(cell));

        /// <summary>
        /// ワールド座標の光線が指すマス（T208 のドラッグ・クリックの位置→マス変換）。各マスの上面の高さの水平面との交点で調べ、
        /// 手前（高い面）から順に見る。確保なし。マップ外・どのマスにも当たらないときは false。
        /// </summary>
        public bool TryPickCell(Vector3 worldOrigin, Vector3 worldDirection, out GridPoint cell)
        {
            cell = default;
            if (Map == null) return false;
            var origin = transform.InverseTransformPoint(worldOrigin);
            var direction = transform.InverseTransformDirection(worldDirection);
            var geometry = Geometry;
            for (var i = 0; i < PickHeights.Length; i++)
            {
                if (!geometry.TryRayToCell(origin, direction, PickHeights[i], out var candidate)) continue;
                if (!Map.Contains(candidate)) continue;
                if (Mathf.Approximately(SurfaceHeight(candidate), PickHeights[i]))
                {
                    cell = candidate;
                    return true;
                }
            }
            return false;
        }

        private void Awake()
        {
            foreach (var map in catalog.LoadTables().Maps)
                if (map.Id == mapId) ShowMap(map);
            if (Map == null) Debug.LogError($"マップ '{mapId}' が見つかりません", this);
        }

        /// <summary>
        /// 表示するマップを map に替える（前のマスと経路は捨てる）。BattleView と PlacementMarkerView が持つ座標変換は作り直さないので、
        /// 先に表示したマップと同じ大きさに限る（全マップ 9×6、EditMode テストで確認）。
        /// </summary>
        public void ShowMap(MapData map)
        {
            if (map == null) throw new System.ArgumentNullException(nameof(map));
            if (Map != null && (Map.Width != map.Width || Map.Height != map.Height))
                throw new System.ArgumentException($"マップ '{map.Id}' の大きさが表示中のマップ '{Map.Id}' と違います", nameof(map));
            if (cellsRoot != null) Destroy(cellsRoot.gameObject);
            cellsRoot = new GameObject(CellsName).transform;
            cellsRoot.SetParent(transform, false);
            Map = map;
            BuildCells();
            BuildPaths();
        }

        private void BuildCells()
        {
            for (var y = 0; y < Map.Height; y++)
            {
                for (var x = 0; x < Map.Width; x++)
                {
                    var point = new GridPoint(x, y);
                    var type = Map.GetCell(point);
                    var height = CellHeight(type);
                    CreatePart(PrimitiveType.Cube, $"Cell{x}_{y}", LocalCenter(point, height * Half),
                        new Vector3(CellFootprint, height, CellFootprint), CellMaterial(type));
                    if (type == CellType.Spawn || type == CellType.Goal)
                    {
                        var material = type == CellType.Spawn ? spawnMaterial : goalMaterial;
                        CreatePart(PrimitiveType.Cylinder, type == CellType.Spawn ? $"Spawn{x}_{y}" : $"Goal{x}_{y}",
                            LocalCenter(point, GroundHeight + PillarHeight * Half),
                            new Vector3(PillarRadius, PillarHeight / CylinderHeightPerScale, PillarRadius), material);
                    }
                }
            }
        }

        private void BuildPaths()
        {
            foreach (var path in Map.Paths)
            {
                for (var i = 0; i < path.Cells.Count; i++)
                {
                    var point = path.Cells[i];
                    if (path.IsAir)
                        CreatePart(PrimitiveType.Cube, $"Air{path.Id}_{i}", LocalCenter(point, AirPathHeight),
                            Vector3.one * AirPathSize, airPathMaterial);
                    else
                        CreatePart(PrimitiveType.Cube, $"Path{path.Id}_{i}",
                            LocalCenter(point, CellHeight(Map.GetCell(point)) + GroundPathThickness * Half),
                            new Vector3(GroundPathSize, GroundPathThickness, GroundPathSize), groundPathMaterial);
                }
            }
        }

        /// <summary>マスの中心（フィールドの中央が原点）。y=0 の行が奥（画面の上）に来る。</summary>
        private Vector3 LocalCenter(GridPoint point, float height) => Geometry.ToLocal(point, height);

        private static float CellHeight(CellType type)
        {
            switch (type)
            {
                case CellType.High: return HighHeight;
                case CellType.Blocked: return BlockedHeight;
                default: return GroundHeight;
            }
        }

        private Material CellMaterial(CellType type)
        {
            switch (type)
            {
                case CellType.High: return highMaterial;
                case CellType.Blocked: return blockedMaterial;
                case CellType.Spawn: return spawnMaterial;
                case CellType.Goal: return goalMaterial;
                default: return groundMaterial;
            }
        }

        private void CreatePart(PrimitiveType shape, string partName, Vector3 localPosition, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            Destroy(part.GetComponent<Collider>());
            var partTransform = part.transform;
            partTransform.SetParent(cellsRoot, false);
            partTransform.localPosition = localPosition;
            partTransform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
