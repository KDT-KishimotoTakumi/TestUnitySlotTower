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
        public const string PausePath = "Game/Pause";
        public const string RetreatPath = "Game/Retreat";
        public const string CycleSelectionPath = "Game/CycleSelection";
        public const string SummonSelectPrefix = "Game/SummonSelect";
        private const int MaxIcons = 5;
        private const float AxisThreshold = 0.5f;
        // 向きの不感帯（戦闘領域の高さに対する割合。マスの中心から離れるまで向きを変えない）
        private const float FacingDeadZoneFraction = 0.03f;
        // 配置の確定に使った決定キーの直後は、決定キーで ▶ を押さない時間（秒、unscaledTime。確定の連打での誤爆防止）
        public const float StartKeyGuardSeconds = 0.3f;

        [SerializeField] private BattleRunner runner;
        [SerializeField] private BattleFieldView field;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private PlacementMarkerView markers;
        [SerializeField] private SummonIcon[] icons = Array.Empty<SummonIcon>();
        [SerializeField] private TMP_Text deployedText;
        [SerializeField] private RetreatView retreatView;

        private readonly InputAction[] summonActions = new InputAction[MaxIcons];

        private readonly UnitSelection selection = new UnitSelection();

        private Action<int> iconPressed;
        private Action<int> upgradePressed;
        private Action retreatPressed;
        private PlacementController controller;
        private PlacementBoard boundBoard;
        private InputAction pointAction;
        private InputAction pressAction;
        private InputAction cancelAction;
        private InputAction confirmAction;
        private InputAction cursorMoveAction;
        private InputAction pauseAction;
        private InputAction retreatAction;
        private InputAction cycleAction;
        private Vector2 lastPointer;
        private Vector2 lastMove;
        private bool previewShown;
        private bool selectionRangeShown;
        private bool clickArmed;
        private float lastPlacementConfirmTime = float.NegativeInfinity;
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
            upgradePressed = OnUpgradePressed;
            retreatPressed = RetreatSelected;
            if (retreatView != null)
            {
                retreatView.Bind(retreatPressed);
                retreatView.Hide();
            }
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
            pauseAction = Find(PausePath);
            retreatAction = Find(RetreatPath);
            cycleAction = Find(CycleSelectionPath);
            for (var i = 0; i < MaxIcons; i++) summonActions[i] = Find(SummonSelectPrefix + (i + 1));
        }

        private void OnDisable()
        {
            // 無効化されたときに、途中の配置操作・印・範囲の表示、味方の選択を残さない
            AbortPlacement();
            ClearSelection();
            Disable(retreatAction);
            Disable(cycleAction);
            Disable(pointAction);
            Disable(pressAction);
            Disable(cancelAction);
            Disable(confirmAction);
            Disable(cursorMoveAction);
            Disable(pauseAction);
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
            if (runner.IsPaused)
            {
                PollPaused(true);
                RefreshIcons();
                return;
            }
            PollKeyboard();
            // このフレームのキーで一時停止した場合は、マウスを読まずに停止中の処理へ移る（Pause の押下は二重に読まない）
            if (runner.IsPaused)
            {
                PollPaused(false);
                RefreshIcons();
                return;
            }
            PollMouse();
            SyncSelection();
            RefreshPlacementView();
            RefreshIcons();
        }

        /// <summary>
        /// 一時停止中の入力（FR-012）。配置・強化・選択は受け付けず、再開（Pause）だけ読む。
        /// 停止した時点で途中だった配置操作は取りやめ、印と範囲プレビューを消す。
        /// </summary>
        private void PollPaused(bool readPause)
        {
            if (controller.IsActive)
            {
                AbortPlacement();
                RefreshPlacementView();
            }
            // 停止中は味方の選択もできない（撤退ボタンも出さない。閲覧は T308 第 3 段階）
            ClearSelection();
            // マウスの移動量は読み捨てる（再開直後に古い差分で向きが動かないように）
            lastPointer = pointAction.ReadValue<Vector2>();
            lastMove = cursorMoveAction.ReadValue<Vector2>();
            if (readPause && pauseAction.WasPressedThisFrame()) runner.TogglePause();
        }

        private bool EnsureBound()
        {
            if (runner == null || runner.Simulation == null) return false;
            if (controller != null && runner.Board == boundBoard) return true;

            // 次のウェーブで盤面が変わったときは、配置操作を捨てて新しい盤面の操作に作り直す（印とアイコンは作り直さない）
            var rebinding = controller != null;
            if (rebinding)
            {
                AbortPlacement();
                ClearSelection();
            }
            boundBoard = runner.Board;
            controller = new PlacementController(runner.Units, runner.Summoner, runner.Board, runner.Wallet, runner.Flow);
            if (rebinding) return true;
            markers.Bind(field);
            var units = runner.Units.Units;
            for (var i = 0; i < icons.Length; i++)
            {
                var present = i < units.Count && i < MaxIcons;
                icons[i].gameObject.SetActive(present);
                if (present) icons[i].Bind(i, units[i].Cost, iconPressed, upgradePressed);
            }
            return true;
        }

        private void OnIconPressed(int index)
        {
            if (controller == null) return;
            controller.SelectByIcon(index);
        }

        private void OnUpgradePressed(int index)
        {
            if (runner == null || runner.Upgrades == null) return;
            TryUpgrade(index);
        }

        /// <summary>種類 index（アイコンの番号）を強化する。コイン不足・停止中ならカードを赤く光らせる（ボタンと Shift+数字で共通）。</summary>
        private void TryUpgrade(int index)
        {
            var units = runner.Units.Units;
            if (index < 0 || index >= units.Count) return;
            var result = runner.Upgrades.TryUpgrade(units[index].Id);
            if ((result == UpgradeResult.NotEnoughCoins || result == UpgradeResult.Paused) && index < icons.Length)
                icons[index].FlashFailure();
        }

        // ---- 味方の選択と撤退（T308） ----

        /// <summary>選択を外し、撤退ボタンと攻撃範囲の強調を消す。</summary>
        private void ClearSelection()
        {
            selection.Clear();
            HideSelection();
        }

        private void HideSelection()
        {
            if (retreatView != null) retreatView.Hide();
            if (selectionRangeShown && runner != null)
            {
                runner.ClearRange();
                selectionRangeShown = false;
            }
        }

        /// <summary>選択が変わったとき、選択した味方の攻撃範囲を出す（外れたなら消す）。撤退ボタンは SyncSelection が毎フレーム置く。</summary>
        private void ApplySelectionChange()
        {
            if (selection.HasSelection)
            {
                runner.ShowRange(selection.Selected);
                selectionRangeShown = true;
            }
            else
            {
                HideSelection();
            }
        }

        /// <summary>
        /// 選択の後始末と撤退ボタンの位置合わせ（毎フレーム）。配置操作が始まったら選択を外し、
        /// 選択中の味方が倒れた・撤退した・盤面が替わったときも外す。
        /// </summary>
        private void SyncSelection()
        {
            if (!selection.HasSelection) return;
            if (controller.IsActive) selection.Clear();
            else selection.Validate(runner.Board);
            if (!selection.HasSelection)
            {
                HideSelection();
                return;
            }
            if (retreatView == null) return;
            var unit = selection.Selected;
            var world = field.transform.TransformPoint(field.Geometry.ToLocal(unit.Cell, field.SurfaceHeight(unit.Cell)));
            retreatView.Show(battleCamera.WorldToScreenPoint(world), runner.Simulation.RefundFor(unit));
        }

        /// <summary>選択中の味方を撤退させる（× ボタンと X キー）。成功すると選択を外す。停止中は拒否される。</summary>
        private void RetreatSelected()
        {
            if (runner == null || runner.Simulation == null || !selection.HasSelection) return;
            if (!runner.Simulation.TryRetreat(selection.Selected, out _)) return;
            ClearSelection();
        }

        private void ClickField(Vector2 pointer)
        {
            var changed = false;
            if (TryPick(pointer, out var cell)) changed = selection.ClickCell(runner.Board, cell);
            else if (selection.HasSelection)
            {
                selection.Clear();
                changed = true;
            }
            if (changed) ApplySelectionChange();
        }

        private void PollKeyboard()
        {
            var shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            var units = runner.Units.Units;
            for (var i = 0; i < MaxIcons; i++)
            {
                if (!summonActions[i].WasPressedThisFrame()) continue;
                // Shift+数字は強化（T303）。召喚の選択は Shift 中は無視される
                if (shift && i < units.Count) TryUpgrade(i);
                controller.SelectByKey(i, shift);
            }

            // Tab で配置中の味方を順に選び、X で撤退（配置操作中は選べない。選択が変わると攻撃範囲を出す）
            if (cycleAction.WasPressedThisFrame() && !controller.IsActive && selection.Cycle(runner.Board)) ApplySelectionChange();
            if (retreatAction.WasPressedThisFrame()) RetreatSelected();

            // 矢印は押した瞬間に 1 マス（押しっぱなしでは進まない）。上は y が減る向き
            var move = cursorMoveAction.ReadValue<Vector2>();
            var dx = Edge(move.x, lastMove.x);
            var dy = -Edge(move.y, lastMove.y);
            lastMove = move;
            if (dx != 0) controller.Arrow(dx, 0);
            else if (dy != 0) controller.Arrow(0, dy);

            // 決定キーは配置操作の確定。配置操作中でないときの決定キーは ▶（ウェーブ開始。準備中以外は何も起きない）
            if (confirmAction.WasPressedThisFrame())
            {
                if (controller.IsActive)
                {
                    controller.Confirm();   // 成功・失敗とも、このキーは配置に使った
                    lastPlacementConfirmTime = Time.unscaledTime;
                }
                else if (Time.unscaledTime - lastPlacementConfirmTime >= StartKeyGuardSeconds)
                {
                    runner.StartWave();
                }
            }
            // Esc は Cancel と Pause の両方に割り当て済み。優先順は 配置操作の取り消し → 味方の選択解除 → 一時停止。
            // P は Cancel に割り当てがないので、配置操作中でも選択中でも一時停止できる（途中の操作・選択は PollPaused で取りやめる）
            var cancelPressed = cancelAction.WasPressedThisFrame();
            var cancelled = cancelPressed && controller.Cancel();
            var deselected = cancelPressed && !cancelled && selection.HasSelection;
            if (deselected) ClearSelection();
            if (pauseAction.WasPressedThisFrame() && !cancelled && !deselected) runner.TogglePause();
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
            // 撤退ボタンの上は戦闘領域のクリックとして扱わない（ボタン自身が uGUI で受ける）
            var overField = battleCamera.pixelRect.Contains(pointer) && (retreatView == null || !retreatView.Contains(pointer));
            var idleBefore = !controller.IsActive;

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

            // 配置操作中でないときに、押したのも離したのも戦闘領域上なら、そのマスの味方を選ぶ（空きマスなら選択解除）
            if (pressAction.WasPressedThisFrame())
            {
                clickArmed = idleBefore && overField;
                controller.PointerPressed(overField);
            }
            if (pressAction.WasReleasedThisFrame())
            {
                controller.PointerReleased(overField);
                if (clickArmed && idleBefore && overField) ClickField(pointer);
                clickArmed = false;
            }
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
                var stock = runner.Flow != null ? runner.Flow.GetStock(unit.Id) : 0;
                var upgradeCost = runner.Upgrades.GetNextCost(unit.Id);
                var upgradeAffordable = upgradeCost >= 0 && !runner.IsPaused && runner.Wallet.CanAfford(upgradeCost);
                icons[i].Refresh(availability, ratio, selected != null && ReferenceEquals(selected, unit), stock, runner.Upgrades.GetLevel(unit.Id),
                    upgradeCost, upgradeAffordable);
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
