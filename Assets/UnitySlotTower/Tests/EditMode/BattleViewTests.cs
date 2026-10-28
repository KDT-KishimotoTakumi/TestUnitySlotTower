using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Units;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>T209: 戦闘表示の座標変換・ゲージ・プール・形の割り当て（Unity オブジェクトを作らない部分）。</summary>
    public class BattleViewTests
    {
        private const float Tolerance = 1e-4f;

        // ---- 座標変換 ----

        [Test]
        public void ToLocal_PutsFieldCenterAtOrigin_AndTopRowAtBack()
        {
            var geometry = new FieldGeometry(9, 6);
            var topLeft = geometry.ToLocal(0, 0, 0.5f);
            Assert.AreEqual(-4f, topLeft.x, Tolerance);
            Assert.AreEqual(0.5f, topLeft.y, Tolerance);
            Assert.AreEqual(2.5f, topLeft.z, Tolerance);
            var bottomRight = geometry.ToLocal(new GridPoint(8, 5), 0f);
            Assert.AreEqual(4f, bottomRight.x, Tolerance);
            Assert.AreEqual(-2.5f, bottomRight.z, Tolerance);
        }

        [Test]
        public void ToLocal_InterpolatesBetweenCells()
        {
            var geometry = new FieldGeometry(9, 6);
            var middle = geometry.ToLocal(1.5f, 1f, 0f);
            Assert.AreEqual(-2.5f, middle.x, Tolerance);
            Assert.AreEqual(1.5f, middle.z, Tolerance);
        }

        [Test]
        public void EnemyOnPath_MapsContinuousProgressToWorld()
        {
            // w1 の地上経路の先頭: (0,1) → (1,1) → (2,1)
            var cells = new List<GridPoint> { new GridPoint(0, 1), new GridPoint(1, 1), new GridPoint(2, 1), new GridPoint(2, 2) };
            var path = new MapPath("g1", false, cells);
            var def = new EnemyDef("grunt", EnemyKind.Melee, 10, 1, 0, 1f, 1f, 0f, false, 1, 1, 0, 0, 0);
            var enemy = new EnemyInstance(def, path);
            var geometry = new FieldGeometry(9, 6);

            enemy.Move(1.5f);
            var position = geometry.ToLocal(enemy.PositionX, enemy.PositionY, 0f);
            Assert.AreEqual(1.5f - 4f, position.x, Tolerance);
            Assert.AreEqual(2.5f - 1f, position.z, Tolerance);

            // 次の区間（(2,1) → (2,2)）では奥行きだけが変わる
            enemy.Move(1f);   // progress 2.5
            position = geometry.ToLocal(enemy.PositionX, enemy.PositionY, 0f);
            Assert.AreEqual(2f - 4f, position.x, Tolerance);
            Assert.AreEqual(2.5f - 1.5f, position.z, Tolerance);
        }

        [Test]
        public void YawDegrees_PointsTheFacingDirectionInLocalSpace()
        {
            foreach (var facing in new[] { Facing.Right, Facing.Down, Facing.Left, Facing.Up })
            {
                var forward = Quaternion.Euler(0f, FieldGeometry.YawDegrees(facing), 0f) * Vector3.forward;
                // Facing.Rotate(1,0) が指すマスの向き（y は下向きなので z は反転）
                var offset = facing.Rotate(new GridPoint(1, 0));
                Assert.AreEqual(offset.X, forward.x, Tolerance, facing.ToString());
                Assert.AreEqual(-offset.Y, forward.z, Tolerance, facing.ToString());
            }
        }

        // ---- HP ゲージ ----

        [Test]
        public void Gauge_FullHasFullWidthCentered()
        {
            HpGauge.Layout(1f, 0.8f, out var width, out var offset);
            Assert.AreEqual(0.8f, width, Tolerance);
            Assert.AreEqual(0f, offset, Tolerance);
        }

        [Test]
        public void Gauge_ShrinksTowardsTheLeftEdge()
        {
            HpGauge.Layout(0.25f, 0.8f, out var width, out var offset);
            Assert.AreEqual(0.2f, width, Tolerance);
            // 左端は枠の左端（-0.4）のまま: 中心 - 幅/2 = -0.4
            Assert.AreEqual(-0.4f, offset - width * 0.5f, Tolerance);
        }

        [Test]
        public void Gauge_RatioIsClamped()
        {
            Assert.AreEqual(0f, HpGauge.Ratio(-5f, 100f), Tolerance);
            Assert.AreEqual(1f, HpGauge.Ratio(150f, 100f), Tolerance);
            Assert.AreEqual(0.5f, HpGauge.Ratio(50f, 100f), Tolerance);
            Assert.AreEqual(0f, HpGauge.Ratio(10f, 0f), Tolerance);
            HpGauge.Layout(0f, 0.8f, out var width, out _);
            Assert.AreEqual(0f, width, Tolerance);
        }

        // ---- 形 ----

        [Test]
        public void EveryUnitRole_HasADistinctShapeOrAngle()
        {
            var seen = new HashSet<string>();
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                var style = BattleVisuals.ForUnit(role);
                Assert.Greater(style.Height, 0f, role.ToString());
                Assert.IsTrue(seen.Add($"{style.Shape}/{style.Euler}"), $"{role} の形が他の職と同じ");
            }
        }

        [Test]
        public void EveryEnemyKind_HasADistinctShapeOrAngle_ExceptBossSizes()
        {
            var seen = new HashSet<string>();
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                var style = BattleVisuals.ForEnemy(kind);
                Assert.Greater(style.Height, 0f, kind.ToString());
                if (kind == EnemyKind.Melee || kind == EnemyKind.Ranged || kind == EnemyKind.Air)
                    Assert.IsTrue(seen.Add($"{style.Shape}/{style.Euler}"), $"{kind} の形が他の種類と同じ");
            }
            Assert.Greater(BattleVisuals.ForEnemy(EnemyKind.FinalBoss).Height, BattleVisuals.ForEnemy(EnemyKind.Melee).Height);
        }

        [Test]
        public void BodyStyleHeight_DoublesForCylinderAndCapsule()
        {
            Assert.AreEqual(0.8f, new BodyStyle(BodyShape.Cylinder, new Vector3(1f, 0.4f, 1f), Vector3.zero).Height, Tolerance);
            Assert.AreEqual(0.4f, new BodyStyle(BodyShape.Cube, new Vector3(1f, 0.4f, 1f), Vector3.zero).Height, Tolerance);
        }

        // ---- プール ----

        private sealed class FakeView
        {
            public int Id;
            public string Source;
            public bool Active;
        }

        private static ViewSet<string, FakeView> NewSet(int capacity, out List<FakeView> created)
        {
            var list = new List<FakeView>();
            created = list;
            return new ViewSet<string, FakeView>(
                () =>
                {
                    var view = new FakeView { Id = list.Count };
                    list.Add(view);
                    return view;
                },
                (view, source) =>
                {
                    view.Source = source;
                    view.Active = true;
                },
                view => view.Active = false,
                capacity);
        }

        [Test]
        public void ViewSet_PrewarmsPooledViewsInactive()
        {
            var set = NewSet(3, out var created);
            Assert.AreEqual(3, created.Count);
            Assert.AreEqual(3, set.PooledCount);
            Assert.AreEqual(0, set.ActiveCount);
            foreach (var view in created) Assert.IsFalse(view.Active);
        }

        [Test]
        public void ViewSet_ReusesTheSameViewForTheSameSource_AndDoesNotCreateWithinCapacity()
        {
            var set = NewSet(2, out var created);
            for (var frame = 0; frame < 5; frame++)
            {
                set.BeginFrame();
                var a = set.Get("a");
                var b = set.Get("b");
                set.EndFrame();
                Assert.AreNotSame(a, b);
                Assert.IsTrue(a.Active && b.Active);
            }
            Assert.AreEqual(2, created.Count);
            Assert.AreEqual(2, set.ActiveCount);
        }

        [Test]
        public void ViewSet_ReturnsViewsOfVanishedSourcesToThePool_AndReusesThem()
        {
            var set = NewSet(1, out var created);
            set.BeginFrame();
            var first = set.Get("a");
            set.EndFrame();

            set.BeginFrame();   // a が消えた
            set.EndFrame();
            Assert.IsFalse(first.Active);
            Assert.AreEqual(0, set.ActiveCount);
            Assert.AreEqual(1, set.PooledCount);

            set.BeginFrame();
            var second = set.Get("b");
            set.EndFrame();
            Assert.AreSame(first, second);
            Assert.AreEqual("b", second.Source);
            Assert.AreEqual(1, created.Count);
        }

        [Test]
        public void ViewSet_GrowsOnlyWhenThePoolIsEmpty()
        {
            var set = NewSet(1, out var created);
            set.BeginFrame();
            set.Get("a");
            set.Get("b");
            set.Get("c");
            set.EndFrame();
            Assert.AreEqual(3, created.Count);
            Assert.AreEqual(3, set.ActiveCount);

            set.BeginFrame();
            set.Get("b");
            set.EndFrame();
            Assert.AreEqual(1, set.ActiveCount);
            Assert.AreEqual(2, set.PooledCount);
        }
    }
}
