using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 味方の配置入力（T208、FR-002）。Game マップの入力（ポインタ位置・押す・矢印・決定・取り消し・数字キー）と召喚アイコンの押下を
    /// <see cref="PlacementController"/>（ロジック）に渡し、状態を印（<see cref="PlacementMarkerView"/>）、攻撃範囲、アイコンに映す。
    /// 入力はコールバックではなく毎フレームの状態読み取りで受ける（Reload Domain 無効でも InputAction に購読が残らない）。
    /// 使うアクションだけを OnEnable で有効にし、OnDisable で無効にする。
    /// </summary>
    public sealed class PlacementInput : MonoBehaviour
    {
        public const string PointPath = "Game/Point";
        public const string PressPath = "Game/Press";
        public const string CancelPath = "Game/Cancel";
        public const string ConfirmPath = "Game/Confirm";
        public const string CursorMovePath = "Game/CursorMove";
        public const string SummonSelectPrefix = "Game/SummonSelect";
        private const int MaxIcons = 5;
        private const float AxisThreshold = 0.5f;
        // 向きの不感帯（戦闘領域の高さに対する割合。マスの中心から離れるまで向きを変えない）
        private const float FacingDeadZoneFraction = 0.03f;

        [SerializeField] private BattleRunner runner;
        [SerializeField] private BattleFieldView field;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private PlacementMarkerView markers;
        [SerializeField] private SummonIcon[] icons = Array.Empty<SummonIcon>();
        [SerializeField] private TMP_Text deployedText;

        private readonly InputAction[] summonActions = new InputAction[MaxIcons];

        private Action<int> iconPressed;
        private PlacementController controller;
        private PlacementBoard boundBoard;
        private InputAction pointAction;
        private InputAction pressAction;
        private InputAction cancelAction;
        private InputAction confirmAction;
        private InputAction cursorMoveAction;
        private Vector2 lastPointer;
        private Vector2 lastMove;
        private bool previewShown;
        private int shownDeployed = -1;

        // 前回の表示に使った状態（変わったときだけ印と範囲を更新する）
        private PlacementPhase shownPhase = (PlacementPhase)(-1);
        private GridPoint shownCell;
        private bool shownValid;
        private Facing shownFacing;
        private UnitDef shownUnit;

        public PlacementController Controller => controller;

        private void Awake()
        {
            iconPressed = OnIconPressed;
        }

        private void OnEnable()
        {
            if (inputActions == null)
            {
                Debug.LogError("PlacementInput: inputActions がありません", this);
                return;
            }
            pointAction = Find(PointPath);
            pressAction = Find(PressPath);
            cancelAction = Find(CancelPath);
            confirmAction = Find(ConfirmPath);
            cursorMoveAction = Find(CursorMovePath);
            for (var i = 0; i < MaxIcons; i++) summonActions[i] = Find(SummonSelectPrefix + (i + 1));
        }

        private void OnDisable()
        {
            // 無効化されたときに、途中の配置操作・印・範囲の表示を残さない
            AbortPlacement();
            Disable(pointAction);
            Disable(pressAction);
            Disable(cancelAction);
            Disable(confirmAction);
            Disable(cursorMoveAction);
            for (var i = 0; i < MaxIcons; i++) Disable(summonActions[i]);
            lastMove = Vector2.zero;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) AbortPlacement();
        }

        /// <summary>配置操作を取りやめ、フィールド上の印と範囲プレビューを消す。</summary>
        private void AbortPlacement()
        {
            if (controller == null) return;
            controller.Cancel();
            if (markers != null) markers.HideAll();
            if (previewShown && runner != null)
            {
                runner.ClearRange();
                previewShown = false;
            }
            shownPhase = (PlacementPhase)(-1);
        }

        // 他の入力を巻き込まないよう、マップ全体ではなく使うアクションだけを有効にする
        private InputAction Find(string actionName)
        {
            var action = inputActions.FindAction(actionName, true);
            action.Enable();
            return action;
        }

        private static void Disable(InputAction action)
        {
            if (action != null && action.enabled) action.Disable();
        }

        private void Update()
        {
            if (!EnsureBound()) return;
            Poll();
        }

        /// <summary>入力を 1 回ぶん読んで反映する（Update が呼ぶ。テストからも呼べる）。</summary>
        public void Poll()
        {
            if (!EnsureBound() || pointAction == null) return;
            PollKeyboard();
            PollMouse();
            RefreshPlacementView();
            RefreshIcons();
        }

        private bool EnsureBound()
        {
            if (runner == null || runner.Simulation == null) return false;
            if (controller != null && runner.Board == boundBoard) return true;

            // 次のウェーブで盤面が変わったときは、配置操作を捨てて新しい盤面の操作に作り直す（印とアイコンは作り直さない）
            var rebinding = controller != null;
            if (rebinding) AbortPlacement();
            boundBoard = runner.Board;
            controller = new PlacementController(runner.Units, runner.Summoner, runner.Board, runner.Wallet);
            if (rebinding) return true;
            markers.Bind(field);
            var units = runner.Units.Units;
            for (var i = 0; i < icons.Length; i++)
            {
                var present = i < units.Count && i < MaxIcons;
                icons[i].gameObject.SetActive(present);
                if (present) icons[i].Bind(i, units[i].Cost, iconPressed);
            }
            return true;
        }

        private void OnIconPressed(int index)
        {
            if (controller == null) return;
            controller.SelectByIcon(index);
        }

        private void PollKeyboard()
        {
            var shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            var units = runner.Units.Units;
            for (var i = 0; i < MaxIcons; i++)
            {
                if (!summonActions[i].WasPressedThisFrame()) continue;
                // Shift+数字は強化（T303）。召喚の選択は Shift 中は無視される
                if (shift && i < units.Count) runner.Upgrades.TryUpgrade(units[i].Id);
                controller.SelectByKey(i, shift);
            }

            // 矢印は押した瞬間に 1 マス（押しっぱなしでは進まない）。上は y が減る向き
            var move = cursorMoveAction.ReadValue<Vector2>();
            var dx = Edge(move.x, lastMove.x);
            var dy = -Edge(move.y, lastMove.y);
            lastMove = move;
            if (dx != 0) controller.Arrow(dx, 0);
            else if (dy != 0) controller.Arrow(0, dy);

            if (confirmAction.WasPressedThisFrame()) controller.Confirm();
            // Esc は Cancel と Pause の両方に割り当て済み。配置操作中でなければ何もしない（Pause は T304 以降）
            if (cancelAction.WasPressedThisFrame()) controller.Cancel();
        }

        private static int Edge(float now, float before)
        {
            if (now > AxisThreshold && before <= AxisThreshold) return 1;
            if (now < -AxisThreshold && before >= -AxisThreshold) return -1;
            return 0;
        }

        private void PollMouse()
        {
            var pointer = pointAction.ReadValue<Vector2>();
            var moved = pointer != lastPointer;
            lastPointer = pointer;
            var overField = battleCamera.pixelRect.Contains(pointer);

            if (moved || controller.Dragging)
            {
                if (controller.Phase == PlacementPhase.Selected || controller.Phase == PlacementPhase.Hover)
                {
                    var cell = default(GridPoint);
                    var hasCell = overField && TryPick(pointer, out cell);
                    controller.Hover(hasCell, cell);
                }
                else if (moved && controller.Phase == PlacementPhase.Facing)
                {
                    UpdateFacingFromPointer(pointer);
                }
            }

            if (pressAction.WasPressedThisFrame()) controller.PointerPressed(overField);
            if (pressAction.WasReleasedThisFrame()) controller.PointerReleased(overField);
        }

        private bool TryPick(Vector2 screen, out GridPoint cell)
        {
            var ray = battleCamera.ScreenPointToRay(screen);
            return field.TryPickCell(ray.origin, ray.direction, out cell);
        }

        private void UpdateFacingFromPointer(Vector2 pointer)
        {
            var cell = controller.Candidate;
            var world = field.transform.TransformPoint(field.Geometry.ToLocal(cell, field.SurfaceHeight(cell)));
            var center = battleCamera.WorldToScreenPoint(world);
            // 画面の下向きがマス座標の y の増える向き
            controller.SetFacingFromDelta(pointer.x - center.x, center.y - pointer.y, battleCamera.pixelRect.height * FacingDeadZoneFraction);
        }

        private void RefreshPlacementView()
        {
            var phase = controller.Phase;
            var unit = controller.SelectedUnit;
            var cell = controller.Candidate;
            var valid = controller.CandidateValid;
            var facing = controller.Facing;
            if (phase == shownPhase && unit == shownUnit && valid == shownValid && facing == shownFacing
                && cell.X == shownCell.X && cell.Y == shownCell.Y) return;
            shownPhase = phase;
            shownUnit = unit;
            shownValid = valid;
            shownFacing = facing;
            shownCell = cell;

            if (controller.HasCandidate) markers.ShowTile(cell, valid);
            else markers.HideTile();
            if (phase == PlacementPhase.Facing) markers.ShowArrows(cell, facing);
            else markers.HideArrows();

            // 置けるマスの候補・向き選択中は、その場に置いたときの攻撃範囲を出す
            if (valid)
            {
                runner.ShowRangePreview(unit, cell, facing);
                previewShown = true;
            }
            else if (previewShown)
            {
                runner.ClearRange();
                previewShown = false;
            }
        }

        private void RefreshIcons()
        {
            var units = runner.Units.Units;
            var selected = controller.SelectedUnit;
            for (var i = 0; i < icons.Length && i < units.Count; i++)
            {
                var unit = units[i];
                var availability = controller.Availability(unit);
                var ratio = unit.Cooldown > 0f && !runner.Summoner.IgnoreCooldown
                    ? runner.Summoner.CooldownRemaining(unit.Id) / unit.Cooldown
                    : 0f;
                icons[i].Refresh(availability, ratio, selected != null && ReferenceEquals(selected, unit));
            }

            var deployed = runner.Summoner.DeployedCount;
            if (deployedText != null && deployed != shownDeployed)
            {
                shownDeployed = deployed;
                deployedText.SetText("{0:0}/{1:0}", (float)deployed, (float)runner.Summoner.MaxDeployed);
            }
        }
    }
}
