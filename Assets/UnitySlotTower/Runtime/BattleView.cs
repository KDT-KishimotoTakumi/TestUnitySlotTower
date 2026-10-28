using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 戦闘の状態（味方・敵・ライフ）をグレーボックスで描く（T209）。状態は読むだけで、毎フレーム LateUpdate でポーリングする
    /// （イベントを購読しないので解除も要らない）。表示はプールで使い回し、出現・消滅のときだけ生成・無効化する。
    /// 選択中の味方の攻撃範囲は <see cref="ShowRange"/> で強調する（選択そのものは T208）。
    /// </summary>
    public sealed class BattleView : MonoBehaviour
    {
        private const string ActorsName = "Actors";
        private const string RangeCellName = "RangeCell";
        private const string LifePipName = "LifePip";
        private const int EnemyCapacity = 16;
        private const int RangeCapacity = 16;
        private const float Half = 0.5f;
        private const float RangeFootprint = 0.9f;
        private const float RangeThickness = 0.04f;
        private const float RangeLift = 0.03f;
        private const float BlockedBackOffset = 0.4f;
        private const float LifePipSize = 0.28f;
        private const float LifePipSpacing = 0.4f;
        private const float LifePipFrontMargin = 0.55f;

        [SerializeField] private Camera battleCamera;
        [SerializeField] private Material[] unitMaterials = Array.Empty<Material>();
        [SerializeField] private Material[] enemyMaterials = Array.Empty<Material>();
        [SerializeField] private Material facingMaterial;
        [SerializeField] private Material gaugeBackMaterial;
        [SerializeField] private Material allyGaugeMaterial;
        [SerializeField] private Material enemyGaugeMaterial;
        [SerializeField] private Material rangeMaterial;
        [SerializeField] private Material lifeOnMaterial;
        [SerializeField] private Material lifeOffMaterial;

        private readonly List<GridPoint> rangeCells = new List<GridPoint>(RangeCapacity);
        private readonly List<Transform> rangeMarkers = new List<Transform>(RangeCapacity);
        private readonly Mesh[] meshes = new Mesh[Enum.GetValues(typeof(BodyShape)).Length];

        private BattleFieldView field;
        private PlacementBoard board;
        private EnemyField enemyField;
        private UnitRangeTable ranges;
        private PlayerLife life;
        private FieldGeometry geometry;
        private Transform actorsRoot;
        private ViewSet<PlacedUnit, ActorView> unitViews;
        private ViewSet<EnemyInstance, ActorView> enemyViews;
        private MeshRenderer[] lifePips = Array.Empty<MeshRenderer>();
        private PlacedUnit rangeOwner;
        private int shownLife = -1;

        /// <summary>戦闘の状態につなぐ。BattleFieldView.Map ができた後（Start 以降）に 1 回だけ呼ぶ。</summary>
        public void Bind(BattleFieldView fieldView, PlacementBoard placementBoard, EnemyField enemies, UnitRangeTable unitRanges,
            PlayerLife playerLife, int unitCapacity)
        {
            field = fieldView;
            board = placementBoard;
            enemyField = enemies;
            ranges = unitRanges;
            life = playerLife;
            geometry = field.Geometry;

            // CreatePrimitive と同じ大きさの標準メッシュ（組み込みの *.fbx は大きさが違う）
            meshes[(int)BodyShape.Cube] = GetPrimitiveMesh(PrimitiveType.Cube);
            meshes[(int)BodyShape.Cylinder] = GetPrimitiveMesh(PrimitiveType.Cylinder);
            meshes[(int)BodyShape.Sphere] = GetPrimitiveMesh(PrimitiveType.Sphere);
            meshes[(int)BodyShape.Capsule] = GetPrimitiveMesh(PrimitiveType.Capsule);

            actorsRoot = new GameObject(ActorsName).transform;
            actorsRoot.SetParent(field.transform, false);

            unitViews = new ViewSet<PlacedUnit, ActorView>(
                () => ActorView.Create("Unit", actorsRoot, facingMaterial, gaugeBackMaterial, allyGaugeMaterial),
                BindUnit, view => view.Release(), unitCapacity);
            enemyViews = new ViewSet<EnemyInstance, ActorView>(
                () => ActorView.Create("Enemy", actorsRoot, null, gaugeBackMaterial, enemyGaugeMaterial),
                BindEnemy, view => view.Release(), EnemyCapacity);

            for (var i = 0; i < RangeCapacity; i++) rangeMarkers.Add(CreateRangeMarker());
            BuildLifePips();
        }

        /// <summary>unit の攻撃範囲（向きに回したマス）をハイライトする。別の味方を指定すると切り替わる。</summary>
        public void ShowRange(PlacedUnit unit)
        {
            if (unit == null)
            {
                ClearRange();
                return;
            }
            rangeOwner = unit;
            ranges.GetCells(unit, board.Map, rangeCells);
            for (var i = 0; i < rangeCells.Count; i++)
            {
                if (i == rangeMarkers.Count) rangeMarkers.Add(CreateRangeMarker());
                var cell = rangeCells[i];
                var marker = rangeMarkers[i];
                marker.localPosition = geometry.ToLocal(cell, field.SurfaceHeight(cell) + RangeLift + RangeThickness * Half);
                marker.gameObject.SetActive(true);
            }
            for (var i = rangeCells.Count; i < rangeMarkers.Count; i++) rangeMarkers[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// まだ置いていない味方（配置前のプレビュー）の攻撃範囲を強調する。盤上にいないので、倒れた味方の範囲を消す判定の対象にしない。
        /// ClearRange か別の ShowRange で切り替わる。
        /// </summary>
        public void ShowRangePreview(PlacedUnit hypothetical)
        {
            ShowRange(hypothetical);
            rangeOwner = null;
        }

        public void ClearRange()
        {
            rangeOwner = null;
            for (var i = 0; i < rangeMarkers.Count; i++)
                if (rangeMarkers[i] != null) rangeMarkers[i].gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (board == null) return;
            var cameraRotation = battleCamera.transform.rotation;

            unitViews.BeginFrame();
            var units = board.Units;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                unitViews.Get(unit).Refresh(HpGauge.Ratio(unit.Hp, unit.MaxHp), cameraRotation);
            }
            unitViews.EndFrame();

            enemyViews.BeginFrame();
            var active = enemyField.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var enemy = active[i];
                var view = enemyViews.Get(enemy);
                view.SetFootPosition(EnemyFoot(enemy));
                view.Refresh(HpGauge.Ratio(enemy.Hp, enemy.Def.MaxHp), cameraRotation);
            }
            enemyViews.EndFrame();

            // 倒れた味方の範囲は消す
            if (rangeOwner != null && board.GetAt(rangeOwner.Cell) != rangeOwner) ClearRange();
            RefreshLife();
        }

        private void BindUnit(ActorView view, PlacedUnit unit)
        {
            var style = BattleVisuals.ForUnit(unit.Def.Role);
            view.Bind(style, meshes[(int)style.Shape], unitMaterials[(int)unit.Def.Role],
                geometry.ToLocal(unit.Cell, field.SurfaceHeight(unit.Cell)), FieldGeometry.YawDegrees(unit.Facing));
        }

        private void BindEnemy(ActorView view, EnemyInstance enemy)
        {
            var style = BattleVisuals.ForEnemy(enemy.Def.Kind);
            view.Bind(style, meshes[(int)style.Shape], enemyMaterials[(int)enemy.Def.Kind], EnemyFoot(enemy), 0f);
        }

        /// <summary>
        /// 敵の足元（ローカル座標）。経路上の連続位置から求め、空中敵は浮かせる。
        /// ブロック中はブロッカーと同じマスの中心で止まるので、重ならないよう来た側へ少し下げて描く。
        /// </summary>
        private Vector3 EnemyFoot(EnemyInstance enemy)
        {
            var x = enemy.PositionX;
            var y = enemy.PositionY;
            if (enemy.IsBlocked)
            {
                var cells = enemy.Path.Cells;
                var index = (int)(enemy.Progress + Half);
                if (index > 0)
                {
                    x += (cells[index - 1].X - cells[index].X) * BlockedBackOffset;
                    y += (cells[index - 1].Y - cells[index].Y) * BlockedBackOffset;
                }
            }
            var height = enemy.Def.IsAir
                ? BattleFieldView.AirPathHeight - BattleVisuals.ForEnemy(enemy.Def.Kind).Height * Half
                : field.SurfaceHeight(enemy.CurrentCell);
            return geometry.ToLocal(x, y, height);
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType shape)
        {
            var temp = GameObject.CreatePrimitive(shape);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return mesh;
        }

        private Transform CreateRangeMarker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = RangeCellName;
            Destroy(marker.GetComponent<Collider>());
            marker.GetComponent<MeshRenderer>().sharedMaterial = rangeMaterial;
            marker.transform.SetParent(actorsRoot, false);
            marker.transform.localScale = new Vector3(RangeFootprint, RangeThickness, RangeFootprint);
            marker.SetActive(false);
            return marker.transform;
        }

        /// <summary>ライフの仮表示（残りを明るい点、失った分を暗い点で並べる）。操作バーの数字は T304。</summary>
        private void BuildLifePips()
        {
            lifePips = new MeshRenderer[life.Max];
            var z = -(geometry.Height - 1) * Half - LifePipFrontMargin;
            for (var i = 0; i < lifePips.Length; i++)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pip.name = LifePipName + i;
                Destroy(pip.GetComponent<Collider>());
                pip.transform.SetParent(actorsRoot, false);
                pip.transform.localScale = Vector3.one * LifePipSize;
                pip.transform.localPosition = new Vector3((i - (lifePips.Length - 1) * Half) * LifePipSpacing, LifePipSize * Half, z);
                lifePips[i] = pip.GetComponent<MeshRenderer>();
            }
            RefreshLife();
        }

        private void RefreshLife()
        {
            if (life.Current == shownLife) return;
            shownLife = life.Current;
            for (var i = 0; i < lifePips.Length; i++)
                lifePips[i].sharedMaterial = i < shownLife ? lifeOnMaterial : lifeOffMaterial;
        }
    }
}
