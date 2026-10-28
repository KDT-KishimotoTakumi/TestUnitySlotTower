using UnityEngine;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 配置中のフィールド上の印（T208）。候補マスの板（置ける＝緑、置けない＝赤）と、向き選択の 4 つの矢印（選んでいる向きだけ明るく大きい）。
    /// 表示専用で、状態は <see cref="PlacementInput"/> が渡す。実体は <see cref="Bind"/> で 1 度だけ作り、以後は位置・材質を変えるだけ。
    /// </summary>
    public sealed class PlacementMarkerView : MonoBehaviour
    {
        private const string TileName = "PlacementTile";
        private const string ArrowName = "PlacementArrow";
        private const float TileFootprint = 0.96f;
        private const float TileThickness = 0.04f;
        private const float TileLift = 0.12f;
        private const float ArrowDistance = 0.75f;
        private const float ArrowHeight = 0.9f;
        private const float ArrowSize = 0.26f;
        private const float ArrowSelectedSize = 0.4f;
        private const float ArrowThickness = 0.08f;
        private const float DiamondYaw = 45f;
        private const float Half = 0.5f;
        private static readonly int FacingCount = System.Enum.GetValues(typeof(Facing)).Length;

        [SerializeField] private Material validMaterial;
        [SerializeField] private Material invalidMaterial;
        [SerializeField] private Material arrowMaterial;
        [SerializeField] private Material arrowSelectedMaterial;

        private BattleFieldView field;
        private FieldGeometry geometry;
        private Transform tile;
        private MeshRenderer tileRenderer;
        private Transform[] arrows;
        private MeshRenderer[] arrowRenderers;

        private bool tileShown;
        private GridPoint tileCell;
        private bool tileValid;
        private bool arrowsShown;
        private GridPoint arrowCell;
        private Facing arrowFacing;

        /// <summary>フィールドにつなぐ。BattleFieldView.Map ができた後に 1 度だけ呼ぶ。</summary>
        public void Bind(BattleFieldView fieldView)
        {
            field = fieldView;
            geometry = field.Geometry;

            tile = CreateCube(TileName, out tileRenderer);
            tile.localScale = new Vector3(TileFootprint, TileThickness, TileFootprint);
            arrows = new Transform[FacingCount];
            arrowRenderers = new MeshRenderer[FacingCount];
            for (var i = 0; i < FacingCount; i++)
            {
                arrows[i] = CreateCube(ArrowName + i, out arrowRenderers[i]);
                arrows[i].localRotation = Quaternion.Euler(0f, DiamondYaw, 0f);
            }
        }

        /// <summary>候補マスの板を出す（置ける＝緑、置けない＝赤）。同じ内容なら何もしない。</summary>
        public void ShowTile(GridPoint cell, bool valid)
        {
            if (tileShown && tileValid == valid && tileCell.X == cell.X && tileCell.Y == cell.Y) return;
            tileShown = true;
            tileCell = cell;
            tileValid = valid;
            tile.localPosition = geometry.ToLocal(cell, field.SurfaceHeight(cell) + TileLift + TileThickness * Half);
            tileRenderer.sharedMaterial = valid ? validMaterial : invalidMaterial;
            tile.gameObject.SetActive(true);
        }

        public void HideTile()
        {
            if (!tileShown || tile == null) return;
            tileShown = false;
            tile.gameObject.SetActive(false);
        }

        /// <summary>マス cell の四方に向きの矢印を出し、facing を強調する。同じ内容なら何もしない。</summary>
        public void ShowArrows(GridPoint cell, Facing facing)
        {
            if (arrowsShown && arrowFacing == facing && arrowCell.X == cell.X && arrowCell.Y == cell.Y) return;
            arrowsShown = true;
            arrowCell = cell;
            arrowFacing = facing;
            var height = field.SurfaceHeight(cell) + ArrowHeight;
            for (var i = 0; i < FacingCount; i++)
            {
                var direction = (Facing)i;
                var offset = direction.Rotate(new GridPoint(1, 0));
                var selected = direction == facing;
                var size = selected ? ArrowSelectedSize : ArrowSize;
                arrows[i].localPosition = geometry.ToLocal(cell.X + offset.X * ArrowDistance, cell.Y + offset.Y * ArrowDistance, height);
                arrows[i].localScale = new Vector3(size, ArrowThickness, size);
                arrowRenderers[i].sharedMaterial = selected ? arrowSelectedMaterial : arrowMaterial;
                arrows[i].gameObject.SetActive(true);
            }
        }

        public void HideArrows()
        {
            if (!arrowsShown || arrows[0] == null) return;
            arrowsShown = false;
            for (var i = 0; i < FacingCount; i++) arrows[i].gameObject.SetActive(false);
        }

        public void HideAll()
        {
            HideTile();
            HideArrows();
        }

        private Transform CreateCube(string cubeName, out MeshRenderer meshRenderer)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = cubeName;
            Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(field.transform, false);
            cube.SetActive(false);
            meshRenderer = cube.GetComponent<MeshRenderer>();
            return cube.transform;
        }
    }
}
