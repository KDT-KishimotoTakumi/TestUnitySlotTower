using NUnit.Framework;
using UnityEngine;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Units;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>T208: 配置の入力ロジック（状態遷移、取り消し、Shift 中の無視、置けないマス、召喚結果ごとの扱い）と、画面→マスの光線計算。</summary>
    public class PlacementInputTests
    {
        private const int TankKey = 0;
        private const int SwordsmanKey = 1;
        private const int ArcherKey = 2;
        private const int TankCost = 15;
        private const int PlentyOfCoins = 1000;
        private const int MaxDeployed = 8;
        private const float DeadZone = 10f;
        private const float Tolerance = 1e-4f;

        // maps/w1.csv: (1,1)=G, (2,1)=G, (3,3)=H, (0,1)=出現口, (4,0)=X
        private static readonly GridPoint GroundCell = new GridPoint(1, 1);
        private static readonly GridPoint OtherGroundCell = new GridPoint(2, 1);
        private static readonly GridPoint HighCell = new GridPoint(3, 3);
        private static readonly GridPoint SpawnCell = new GridPoint(0, 1);

        private sealed class Fixture
        {
            public PlacementController Controller;
            public UnitSummoner Summoner;
            public PlacementBoard Board;
            public Wallet Wallet;
        }

        private static Fixture Create(int coins = PlentyOfCoins, int maxDeployed = MaxDeployed)
        {
            var tables = TableSet.Load(TableTests.ReadFile);
            var f = new Fixture { Wallet = new Wallet(coins), Board = new PlacementBoard(tables.Maps[0]) };
            f.Summoner = new UnitSummoner(tables.Units, f.Wallet, maxDeployed);
            f.Controller = new PlacementController(tables.Units, f.Summoner, f.Board, f.Wallet);
            return f;
        }

        // ---- 状態遷移 ----

        [Test]
        public void Initial_IsIdle_AndEscapeIsNotHandled()
        {
            var f = Create();
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
            Assert.IsFalse(f.Controller.IsActive);
            Assert.IsFalse(f.Controller.Cancel(), "配置操作中でない Esc は何もしない");
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
        }

        [Test]
        public void IconSelect_GoesToSelected_ThenHoverWhenOverCell()
        {
            var f = Create();
            Assert.AreEqual(PlacementSelect.Selected, f.Controller.SelectByIcon(TankKey));
            Assert.AreEqual(PlacementPhase.Selected, f.Controller.Phase);
            Assert.AreEqual("tank", f.Controller.SelectedUnit.Id);
            Assert.IsTrue(f.Controller.Dragging);

            f.Controller.Hover(true, GroundCell);
            Assert.AreEqual(PlacementPhase.Hover, f.Controller.Phase);
            Assert.IsTrue(f.Controller.CandidateValid);

            f.Controller.Hover(false, default);
            Assert.AreEqual(PlacementPhase.Selected, f.Controller.Phase);
            Assert.IsFalse(f.Controller.HasCandidate);
        }

        [Test]
        public void DragAndDropOnValidCell_GoesToFacing_ThenReleaseAfterNewPressPlaces()
        {
            var f = Create(30);
            f.Controller.SelectByIcon(TankKey);
            f.Controller.PointerPressed(false);
            f.Controller.Hover(true, GroundCell);
            Assert.AreEqual(PlacementStep.EnteredFacing, f.Controller.PointerReleased(true));
            Assert.AreEqual(PlacementPhase.Facing, f.Controller.Phase);
            Assert.AreEqual(0, f.Board.Units.Count, "ドロップだけではまだ置かれない");

            f.Controller.SetFacingFromDelta(0f, 50f, DeadZone);   // 下
            Assert.AreEqual(Facing.Down, f.Controller.Facing);
            f.Controller.PointerPressed(true);
            Assert.AreEqual(PlacementStep.Placed, f.Controller.PointerReleased(true));
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
            Assert.AreEqual(SummonResult.Success, f.Controller.LastSummonResult);

            var placed = f.Board.GetAt(GroundCell);
            Assert.IsNotNull(placed);
            Assert.AreEqual("tank", placed.Def.Id);
            Assert.AreEqual(Facing.Down, placed.Facing);
            Assert.AreEqual(30 - TankCost, f.Wallet.Coins);
        }

        [Test]
        public void ReleaseThatEndedTheDrag_DoesNotAlsoPlace()
        {
            var f = Create();
            f.Controller.SelectByIcon(TankKey);
            f.Controller.Hover(true, GroundCell);
            f.Controller.PointerReleased(true);
            // 向き選択に入った同じ離す操作では確定しない。押し直さずに離しても置かれない
            Assert.AreEqual(PlacementStep.NoChange, f.Controller.PointerReleased(true));
            Assert.AreEqual(0, f.Board.Units.Count);
            Assert.AreEqual(PlacementPhase.Facing, f.Controller.Phase);
        }

        [Test]
        public void PressOutsideField_DuringFacing_CancelsOnRelease_WithoutPlacing()
        {
            var f = Create();
            f.Controller.SelectByIcon(TankKey);
            f.Controller.Hover(true, GroundCell);
            f.Controller.PointerReleased(true);
            f.Controller.PointerPressed(false);   // たとえばレバーのクリック
            Assert.AreEqual(PlacementStep.Cancelled, f.Controller.PointerReleased(false));
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
            Assert.AreEqual(0, f.Board.Units.Count);
            Assert.AreEqual(PlentyOfCoins, f.Wallet.Coins);
        }

        [Test]
        public void PressOnField_ReleaseOutsideField_DuringFacing_CancelsAndKeepsCoins()
        {
            var f = Create();
            f.Controller.SelectByIcon(TankKey);
            f.Controller.Hover(true, GroundCell);
            f.Controller.PointerReleased(true);
            f.Controller.PointerPressed(true);
            Assert.AreEqual(PlacementStep.Cancelled, f.Controller.PointerReleased(false), "スロット領域などで離したら誤配置しない");
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
            Assert.AreEqual(0, f.Board.Units.Count);
            Assert.AreEqual(PlentyOfCoins, f.Wallet.Coins);
            Assert.AreEqual(0, f.Summoner.DeployedCount);
        }

        [Test]
        public void Hover_SyncsKeyboardCursor_ToTheMouseCell()
        {
            var f = Create();
            f.Controller.SelectByIcon(TankKey);
            f.Controller.Hover(true, OtherGroundCell);
            Assert.AreEqual(OtherGroundCell.X, f.Controller.Cursor.X);
            Assert.AreEqual(OtherGroundCell.Y, f.Controller.Cursor.Y);
            f.Controller.Hover(false, default);
            Assert.AreEqual(OtherGroundCell.X, f.Controller.Cursor.X, "候補がないときはカーソルを動かさない");
            // マウスで動かしたあと、矢印はそこから 1 マス
            f.Controller.Hover(true, GroundCell);
            f.Controller.Arrow(1, 0);
            Assert.AreEqual(GroundCell.X + 1, f.Controller.Cursor.X);
            Assert.AreEqual(GroundCell.Y, f.Controller.Cursor.Y);
        }

        [Test]
        public void IconOrder_MatchesUnitRoleOrder()
        {
            // GameSceneBuilder はアイコン i を UnitRole の i 番目の色・形で作る。units.csv の行順と一致している必要がある
            var units = TableSet.Load(TableTests.ReadFile).Units.Units;
            var roles = (UnitRole[])System.Enum.GetValues(typeof(UnitRole));
            Assert.AreEqual(roles.Length, units.Count);
            for (var i = 0; i < roles.Length; i++) Assert.AreEqual(roles[i], units[i].Role, $"units.csv の {i} 行目");
        }

        [Test]
        public void PlacementInput_ActionPaths_ResolveToTheGameMap()
        {
            // マップ名なしの FindAction は先に定義された UI マップの同名アクション（Point/Cancel）に当たる。必ず Game マップのものを使う
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var paths = new System.Collections.Generic.List<string>
            {
                PlacementInput.PointPath, PlacementInput.PressPath, PlacementInput.CancelPath,
                PlacementInput.ConfirmPath, PlacementInput.CursorMovePath,
            };
            for (var n = 1; n <= 5; n++) paths.Add(PlacementInput.SummonSelectPrefix + n);
            foreach (var path in paths)
            {
                var action = asset.FindAction(path, true);
                Assert.AreEqual("Game", action.actionMap.name, path);
            }
        }

        [Test]
        public void DropOnInvalidCell_AfterDrag_Cancels()
        {
            var f = Create();
            f.Controller.SelectByIcon(TankKey);
            f.Controller.Hover(true, HighCell);   // タンクは高台に置けない
            Assert.IsFalse(f.Controller.CandidateValid);
            Assert.AreEqual(PlacementStep.Cancelled, f.Controller.PointerReleased(true));
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
        }

        [Test]
        public void ReleaseOverIcon_AfterClick_KeepsSelection_ThenClickOnCellContinues()
        {
            var f = Create();
            f.Controller.SelectByIcon(TankKey);
            Assert.AreEqual(PlacementStep.NoChange, f.Controller.PointerReleased(false), "アイコンの上で離しただけ＝クリック");
            Assert.AreEqual(PlacementPhase.Selected, f.Controller.Phase);
            Assert.IsFalse(f.Controller.Dragging);

            f.Controller.Hover(true, HighCell);
            Assert.AreEqual(PlacementStep.InvalidCell, f.Controller.PointerReleased(true), "クリックの場合は置けないマスでも選択を続ける");
            Assert.AreEqual(PlacementPhase.Hover, f.Controller.Phase);

            f.Controller.Hover(true, GroundCell);
            Assert.AreEqual(PlacementStep.EnteredFacing, f.Controller.PointerReleased(true));
        }

        [Test]
        public void SelectingAnotherIcon_ReplacesTheSelection()
        {
            var f = Create();
            f.Controller.SelectByIcon(TankKey);
            f.Controller.Hover(true, GroundCell);
            f.Controller.SelectByIcon(ArcherKey);
            Assert.AreEqual("archer", f.Controller.SelectedUnit.Id);
            Assert.AreEqual(PlacementPhase.Selected, f.Controller.Phase);
            Assert.IsFalse(f.Controller.HasCandidate);
        }

        // ---- キーボード ----

        [Test]
        public void KeySelect_StartsAtCursor_ArrowsMoveIt_ConfirmEntersFacing_ArrowsTurn_ConfirmPlaces()
        {
            var f = Create();
            Assert.AreEqual(PlacementSelect.Selected, f.Controller.SelectByKey(TankKey, false));
            Assert.AreEqual(PlacementPhase.Hover, f.Controller.Phase, "キーでは候補がカーソルの位置にある");
            Assert.AreEqual(f.Controller.Cursor.X, f.Controller.Candidate.X);

            // カーソルを (1,1) へ動かす
            MoveCursorTo(f.Controller, GroundCell);
            Assert.AreEqual(GroundCell.X, f.Controller.Candidate.X);
            Assert.AreEqual(GroundCell.Y, f.Controller.Candidate.Y);
            Assert.IsTrue(f.Controller.CandidateValid);

            Assert.AreEqual(PlacementStep.EnteredFacing, f.Controller.Confirm());
            Assert.AreEqual(PlacementPhase.Facing, f.Controller.Phase);

            Assert.IsTrue(f.Controller.Arrow(0, -1));   // 上
            Assert.AreEqual(Facing.Up, f.Controller.Facing);
            Assert.IsTrue(f.Controller.Arrow(-1, 0));
            Assert.AreEqual(Facing.Left, f.Controller.Facing);
            Assert.AreEqual(GroundCell.X, f.Controller.Candidate.X, "向き選択中の矢印はカーソルを動かさない");

            Assert.AreEqual(PlacementStep.Placed, f.Controller.Confirm());
            Assert.AreEqual(Facing.Left, f.Board.GetAt(GroundCell).Facing);
        }

        [Test]
        public void Cursor_StaysInsideTheMap()
        {
            var f = Create();
            f.Controller.SelectByKey(TankKey, false);
            for (var i = 0; i < 30; i++) f.Controller.Arrow(-1, -1);
            Assert.AreEqual(0, f.Controller.Cursor.X);
            Assert.AreEqual(0, f.Controller.Cursor.Y);
            for (var i = 0; i < 30; i++) f.Controller.Arrow(1, 1);
            Assert.AreEqual(f.Board.Map.Width - 1, f.Controller.Cursor.X);
            Assert.AreEqual(f.Board.Map.Height - 1, f.Controller.Cursor.Y);
        }

        [Test]
        public void ArrowsDoNothing_WhenIdle()
        {
            var f = Create();
            var before = f.Controller.Cursor;
            Assert.IsFalse(f.Controller.Arrow(1, 0));
            Assert.AreEqual(before.X, f.Controller.Cursor.X);
            Assert.AreEqual(PlacementStep.NoChange, f.Controller.Confirm());
        }

        [Test]
        public void Confirm_OnInvalidCell_DoesNotAdvance()
        {
            var f = Create();
            f.Controller.SelectByKey(TankKey, false);
            MoveCursorTo(f.Controller, SpawnCell);   // 出現口は置けない
            Assert.IsFalse(f.Controller.CandidateValid);
            Assert.AreEqual(PlacementStep.InvalidCell, f.Controller.Confirm());
            Assert.AreEqual(PlacementPhase.Hover, f.Controller.Phase);
            Assert.AreEqual(0, f.Board.Units.Count);
        }

        [Test]
        public void ArcherKey_NeedsHighCell()
        {
            var f = Create();
            f.Controller.SelectByKey(ArcherKey, false);
            MoveCursorTo(f.Controller, GroundCell);
            Assert.IsFalse(f.Controller.CandidateValid);
            MoveCursorTo(f.Controller, HighCell);
            Assert.IsTrue(f.Controller.CandidateValid);
        }

        // ---- Shift・Esc ----

        [Test]
        public void ShiftHeld_IgnoresSummonSelection()
        {
            var f = Create();
            Assert.AreEqual(PlacementSelect.IgnoredShift, f.Controller.SelectByKey(TankKey, true));
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
            Assert.IsNull(f.Controller.SelectedUnit);

            // 操作中でも、Shift+数字で選択が変わったり取り消されたりしない
            f.Controller.SelectByKey(TankKey, false);
            Assert.AreEqual(PlacementSelect.IgnoredShift, f.Controller.SelectByKey(SwordsmanKey, true));
            Assert.AreEqual("tank", f.Controller.SelectedUnit.Id);
        }

        [Test]
        public void UnknownKey_IsIgnored()
        {
            var f = Create();
            Assert.AreEqual(PlacementSelect.UnknownUnit, f.Controller.SelectByKey(99, false));
            Assert.AreEqual(PlacementSelect.UnknownUnit, f.Controller.SelectByIcon(-1));
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
        }

        [Test]
        public void Cancel_FromAnyActivePhase_ReturnsToIdle_AndIsHandled()
        {
            var f = Create();
            f.Controller.SelectByIcon(TankKey);
            Assert.IsTrue(f.Controller.Cancel());
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
            Assert.IsNull(f.Controller.SelectedUnit);

            f.Controller.SelectByKey(TankKey, false);
            Assert.IsTrue(f.Controller.Cancel());

            f.Controller.SelectByKey(TankKey, false);
            MoveCursorTo(f.Controller, GroundCell);
            f.Controller.Confirm();
            Assert.AreEqual(PlacementPhase.Facing, f.Controller.Phase);
            Assert.IsTrue(f.Controller.Cancel(), "向き選択中の Esc も配置のキャンセル");
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
            Assert.AreEqual(0, f.Board.Units.Count);
            Assert.AreEqual(PlentyOfCoins, f.Wallet.Coins);

            Assert.IsFalse(f.Controller.Cancel(), "2 回目の Esc は配置と無関係");
        }

        // ---- 召喚できない種類・結果ごとの扱い ----

        [Test]
        public void Select_Unavailable_ReportsTheReason_AndDoesNotStart()
        {
            var f = Create(coins: TankCost - 1);
            Assert.AreEqual(PlacementSelect.Unavailable, f.Controller.SelectByIcon(TankKey));
            Assert.AreEqual(SummonResult.NotEnoughCoins, f.Controller.LastSelectResult);
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);

            var limited = Create(maxDeployed: 1);
            PlaceTank(limited, GroundCell);
            Assert.AreEqual(PlacementSelect.Unavailable, limited.Controller.SelectByKey(SwordsmanKey, false));
            Assert.AreEqual(SummonResult.DeployLimit, limited.Controller.LastSelectResult);

            var cooling = Create();
            PlaceTank(cooling, GroundCell);
            Assert.AreEqual(SummonResult.Cooldown, cooling.Controller.Availability(cooling.Board.GetAt(GroundCell).Def), "同じ種類は待ち時間中");
        }

        [Test]
        public void Commit_NotEnoughCoinsAtTheEnd_EndsOperation_AndKeepsState()
        {
            var f = Create(coins: TankCost);
            f.Controller.SelectByKey(TankKey, false);
            MoveCursorTo(f.Controller, GroundCell);
            f.Controller.Confirm();
            f.Wallet.TrySpend(1);   // 向き選択中にスロットなどでコインが減った
            Assert.AreEqual(PlacementStep.Failed, f.Controller.Confirm());
            Assert.AreEqual(SummonResult.NotEnoughCoins, f.Controller.LastSummonResult);
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
            Assert.AreEqual(0, f.Board.Units.Count);
            Assert.AreEqual(TankCost - 1, f.Wallet.Coins, "失敗してもコインは変わらない");
            Assert.AreEqual(0, f.Summoner.DeployedCount);
        }

        [Test]
        public void Commit_DeployLimitAtTheEnd_EndsOperation()
        {
            var f = Create(maxDeployed: 1);
            f.Controller.SelectByKey(SwordsmanKey, false);
            MoveCursorTo(f.Controller, OtherGroundCell);
            f.Controller.Confirm();
            f.Summoner.TrySummon("tank");   // 向き選択中に別の手段で枠が埋まった
            Assert.AreEqual(PlacementStep.Failed, f.Controller.Confirm());
            Assert.AreEqual(SummonResult.DeployLimit, f.Controller.LastSummonResult);
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
        }

        [Test]
        public void Commit_CellTakenAtTheEnd_ReturnsToCellChoice_KeepingTheUnit()
        {
            var f = Create();
            f.Controller.SelectByKey(SwordsmanKey, false);
            MoveCursorTo(f.Controller, GroundCell);
            f.Controller.Confirm();
            // 向き選択中に同じマスが埋まった
            var tables = TableSet.Load(TableTests.ReadFile);
            f.Board.TryPlace(tables.Units.Get("tank"), GroundCell, Facing.Right, out _);
            var coins = f.Wallet.Coins;

            Assert.AreEqual(PlacementStep.Failed, f.Controller.Confirm());
            Assert.AreEqual(SummonResult.CellOccupied, f.Controller.LastSummonResult);
            Assert.AreEqual(PlacementPhase.Hover, f.Controller.Phase);
            Assert.AreEqual("swordsman", f.Controller.SelectedUnit.Id);
            Assert.IsFalse(f.Controller.CandidateValid);
            Assert.AreEqual(coins, f.Wallet.Coins);

            MoveCursorTo(f.Controller, OtherGroundCell);
            f.Controller.Confirm();
            Assert.AreEqual(PlacementStep.Placed, f.Controller.Confirm());
        }

        [Test]
        public void SecondPlacement_Works_AfterTheFirst()
        {
            var f = Create();
            PlaceTank(f, GroundCell);
            Assert.AreEqual(PlacementPhase.Idle, f.Controller.Phase);
            Assert.AreEqual(PlacementSelect.Selected, f.Controller.SelectByKey(SwordsmanKey, false));
            MoveCursorTo(f.Controller, OtherGroundCell);
            f.Controller.Confirm();
            Assert.AreEqual(PlacementStep.Placed, f.Controller.Confirm());
            Assert.AreEqual(2, f.Board.Units.Count);
        }

        // ---- 向きの決め方 ----

        [Test]
        public void FacingFromDelta_UsesTheDominantAxis_AndKeepsInsideTheDeadZone()
        {
            Assert.AreEqual(Facing.Right, PlacementController.FacingFromDelta(30f, 5f, Facing.Up, DeadZone));
            Assert.AreEqual(Facing.Left, PlacementController.FacingFromDelta(-30f, -5f, Facing.Up, DeadZone));
            Assert.AreEqual(Facing.Down, PlacementController.FacingFromDelta(5f, 30f, Facing.Up, DeadZone));
            Assert.AreEqual(Facing.Up, PlacementController.FacingFromDelta(-5f, -30f, Facing.Right, DeadZone));
            Assert.AreEqual(Facing.Left, PlacementController.FacingFromDelta(2f, 2f, Facing.Left, DeadZone), "不感帯の中は今の向きのまま");
            Assert.AreEqual(Facing.Down, PlacementController.FacingFromDelta(0f, 0f, Facing.Down, DeadZone));
        }

        // ---- 画面→マス（光線と水平面の交点） ----

        [Test]
        public void TryRayToCell_StraightDown_HitsTheCellBelow()
        {
            var geometry = new FieldGeometry(9, 6);
            var target = geometry.ToLocal(new GridPoint(3, 2), 0f);
            Assert.IsTrue(geometry.TryRayToCell(target + Vector3.up * 5f, Vector3.down, 0.1f, out var cell));
            Assert.AreEqual(3, cell.X);
            Assert.AreEqual(2, cell.Y);
        }

        [Test]
        public void TryRayToCell_AngledRay_LandsWhereTheRayCrossesTheHeight()
        {
            var geometry = new FieldGeometry(9, 6);
            // 高さ 0.5 の面の上で (6,1) の中心を通る光線（カメラのように手前上方から奥へ）
            var hit = geometry.ToLocal(new GridPoint(6, 1), 0.5f);
            var direction = new Vector3(0f, -1f, 1f).normalized;
            var origin = hit - direction * 8f;
            Assert.IsTrue(geometry.TryRayToCell(origin, direction, 0.5f, out var cell));
            Assert.AreEqual(6, cell.X);
            Assert.AreEqual(1, cell.Y);
            // 同じ光線でも低い面ではもっと奥（y が小さい側）に当たる
            Assert.IsTrue(geometry.TryRayToCell(origin, direction, 0.1f, out var lower));
            Assert.Less(lower.Y, cell.Y + 1);
            Assert.GreaterOrEqual(lower.Y, 0);
        }

        [Test]
        public void TryRayToCell_RayThatNeverReachesThePlane_Fails()
        {
            var geometry = new FieldGeometry(9, 6);
            Assert.IsFalse(geometry.TryRayToCell(Vector3.up * 5f, Vector3.forward, 0.1f, out _));
            Assert.IsFalse(geometry.TryRayToCell(Vector3.up * 5f, Vector3.up, 0.1f, out _));
            Assert.IsFalse(geometry.TryRayToCell(Vector3.zero, Vector3.down, 1f, out _), "面が光線の始点より上にある");
        }

        [Test]
        public void ToGrid_IsTheInverseOfToLocal()
        {
            var geometry = new FieldGeometry(9, 6);
            var local = geometry.ToLocal(2.25f, 3.5f, 0f);
            geometry.ToGrid(local.x, local.z, out var x, out var y);
            Assert.AreEqual(2.25f, x, Tolerance);
            Assert.AreEqual(3.5f, y, Tolerance);
        }

        // ---- ヘルパー ----

        private static void MoveCursorTo(PlacementController controller, GridPoint target)
        {
            for (var i = 0; i < 40 && controller.Cursor.X != target.X; i++) controller.Arrow(controller.Cursor.X < target.X ? 1 : -1, 0);
            for (var i = 0; i < 40 && controller.Cursor.Y != target.Y; i++) controller.Arrow(0, controller.Cursor.Y < target.Y ? 1 : -1);
            Assert.AreEqual(target.X, controller.Cursor.X);
            Assert.AreEqual(target.Y, controller.Cursor.Y);
        }

        private static void PlaceTank(Fixture f, GridPoint cell)
        {
            f.Controller.SelectByKey(TankKey, false);
            MoveCursorTo(f.Controller, cell);
            f.Controller.Confirm();
            Assert.AreEqual(PlacementStep.Placed, f.Controller.Confirm());
        }
    }
}
