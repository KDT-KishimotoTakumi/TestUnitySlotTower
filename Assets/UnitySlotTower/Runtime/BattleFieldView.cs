using UnityEngine;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Runtime
{
    /// <summary>戦闘フィールドの仮表示（グレーボックス）。マップ CSV のグリッドと経路を、マス種別ごとの色の直方体で並べる。T201。</summary>
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

        /// <summary>マス座標とフィールドのローカル座標の変換（Map ができた後に使う）。</summary>
        public FieldGeometry Geometry => new FieldGeometry(Map.Width, Map.Height);

        /// <summary>マスの上面の高さ（その上に立つ味方・敵の足元）。</summary>
        public float SurfaceHeight(GridPoint cell) => CellHeight(Map.GetCell(cell));

        private void Awake()
        {
            foreach (var map in catalog.LoadTables().Maps)
                if (map.Id == mapId) Map = map;
            if (Map == null)
            {
                Debug.LogError($"マップ '{mapId}' が見つかりません", this);
                return;
            }
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
            partTransform.SetParent(transform, false);
            partTransform.localPosition = localPosition;
            partTransform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
