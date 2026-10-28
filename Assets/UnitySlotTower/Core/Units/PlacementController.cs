using System;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Map;

namespace UnitySlotTower.Core.Units
{
    /// <summary>配置操作の状態。</summary>
    public enum PlacementPhase
    {
        /// <summary>何も選んでいない。</summary>
        Idle,
        /// <summary>種類を選んだが、配置先がまだ決まっていない（マウスがフィールドの外など）。</summary>
        Selected,
        /// <summary>種類を選び、配置先の候補マスがある（置けるかどうかは <see cref="PlacementController.CandidateValid"/>）。</summary>
        Hover,
        /// <summary>配置先を決め、向きを選んでいる。</summary>
        Facing,
    }

    /// <summary>種類の選択の結果。</summary>
    public enum PlacementSelect
    {
        Selected,
        /// <summary>Shift 押下中の数字キーは強化（T303）用なので召喚の選択は無視する。</summary>
        IgnoredShift,
        UnknownUnit,
        /// <summary>今は召喚できない（理由は <see cref="PlacementController.LastSelectResult"/>）。</summary>
        Unavailable,
    }

    /// <summary>配置操作を 1 歩進めた結果。</summary>
    public enum PlacementStep
    {
        NoChange,
        EnteredFacing,
        /// <summary>置けないマス（または配置先なし）なので進まなかった。</summary>
        InvalidCell,
        Placed,
        /// <summary>召喚を試みたが失敗した（<see cref="PlacementController.LastSummonResult"/>）。</summary>
        Failed,
        /// <summary>操作を取りやめた。</summary>
        Cancelled,
    }

    /// <summary>
    /// 配置の入力ロジック（T208、FR-002）。マウスとキーボードの両方から使う Unity 非依存の状態機械。
    /// 待機 → 種類選択（アイコン／数字キー）→ 配置先（ドラッグ位置／カーソル）→ 向き → 確定で
    /// <see cref="UnitSummoner.TrySummon(string, PlacementBoard, GridPoint, Facing, out PlacedUnit)"/> を呼ぶ。
    /// 画面座標からマスへの変換は呼び出し側（表示層）が行い、ここにはマス座標と向きだけを渡す。
    /// </summary>
    public sealed class PlacementController
    {
        private readonly UnitTable table;
        private readonly UnitSummoner summoner;
        private readonly PlacementBoard board;
        private readonly Wallet wallet;

        private bool hasCandidate;
        private bool pressedInFacing;

        public PlacementPhase Phase { get; private set; }
        public UnitDef SelectedUnit { get; private set; }
        public GridPoint Candidate { get; private set; }
        public Facing Facing { get; private set; } = Facing.Right;
        /// <summary>キーボードのカーソル位置（常にマップ内）。</summary>
        public GridPoint Cursor { get; private set; }
        /// <summary>true の間はアイコンを押したままのドラッグ中（離したときに置けないマスなら取りやめる）。</summary>
        public bool Dragging { get; private set; }
        public SummonResult LastSummonResult { get; private set; }
        public SummonResult LastSelectResult { get; private set; }
        public PlacedUnit LastPlaced { get; private set; }

        public PlacementController(UnitTable table, UnitSummoner summoner, PlacementBoard board, Wallet wallet)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.summoner = summoner ?? throw new ArgumentNullException(nameof(summoner));
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Cursor = new GridPoint(board.Map.Width / 2, board.Map.Height / 2);
        }

        public bool HasCandidate => hasCandidate;

        /// <summary>候補マスに選択中の種類を置けるか。</summary>
        public bool CandidateValid =>
            hasCandidate && SelectedUnit != null && board.Check(SelectedUnit, Candidate) == PlacementResult.Ok;

        /// <summary>配置操作の途中か（Esc を配置のキャンセルとして扱うかの判断に使う）。</summary>
        public bool IsActive => Phase != PlacementPhase.Idle;

        /// <summary>
        /// 今この種類を召喚できるか。<see cref="UnitSummoner"/> の判定順（配置上限 → 待ち時間 → コイン）を、状態を変えずに見る。
        /// 召喚できるなら Success。
        /// </summary>
        public SummonResult Availability(UnitDef unit)
        {
            if (summoner.DeployedCount >= summoner.MaxDeployed) return SummonResult.DeployLimit;
            if (!summoner.IgnoreCooldown && summoner.CooldownRemaining(unit.Id) > 0f) return SummonResult.Cooldown;
            if (!wallet.CanAfford(unit.Cost)) return SummonResult.NotEnoughCoins;
            return SummonResult.Success;
        }

        // ---- 種類の選択 ----

        /// <summary>数字キー（0 始まりの index）での選択。Shift 押下中は無視する。選んだらカーソルの位置を候補にする。</summary>
        public PlacementSelect SelectByKey(int index, bool shiftHeld)
        {
            if (shiftHeld) return PlacementSelect.IgnoredShift;
            if (index < 0 || index >= table.Units.Count) return PlacementSelect.UnknownUnit;
            return Select(table.Units[index], false, true);
        }

        /// <summary>アイコンを押した（ドラッグの開始）。候補は後でポインタの位置から決まる。</summary>
        public PlacementSelect SelectByIcon(int index)
        {
            if (index < 0 || index >= table.Units.Count) return PlacementSelect.UnknownUnit;
            return Select(table.Units[index], true, false);
        }

        private PlacementSelect Select(UnitDef unit, bool dragging, bool useCursor)
        {
            LastSelectResult = Availability(unit);
            if (LastSelectResult != SummonResult.Success) return PlacementSelect.Unavailable;
            SelectedUnit = unit;
            Dragging = dragging;
            pressedInFacing = false;
            hasCandidate = useCursor;
            if (useCursor) Candidate = Cursor;
            UpdateHoverPhase();
            return PlacementSelect.Selected;
        }

        // ---- 配置先（マウス） ----

        /// <summary>ポインタが指しているマスを知らせる（フィールドの外・マップ外なら hasCell=false）。種類選択中だけ効く。</summary>
        public void Hover(bool hasCell, GridPoint cell)
        {
            if (Phase != PlacementPhase.Selected && Phase != PlacementPhase.Hover) return;
            hasCandidate = hasCell;
            if (hasCell)
            {
                Candidate = cell;
                if (board.Map.Contains(cell)) Cursor = cell;   // キーボードのカーソルをマウスの候補に合わせる
            }
            UpdateHoverPhase();
        }

        /// <summary>マウスのボタンを押した。向き選択中にフィールド上で押したときだけ、離したときの確定の対象になる。</summary>
        public void PointerPressed(bool overField)
        {
            pressedInFacing = Phase == PlacementPhase.Facing && overField;
        }

        /// <summary>
        /// マウスのボタンを離した。種類選択中は、置けるマスの上なら向き選択へ進み、置けないマスの上でドラッグしていたなら取りやめる。
        /// フィールドの外（アイコンの上）で離したときはクリックとして選択を続ける。向き選択中は、押したのが向き選択中なら確定する。
        /// </summary>
        public PlacementStep PointerReleased(bool overField)
        {
            switch (Phase)
            {
                case PlacementPhase.Hover:
                case PlacementPhase.Selected:
                {
                    var wasDragging = Dragging;
                    Dragging = false;
                    if (CandidateValid) return EnterFacing();
                    if (wasDragging && overField)
                    {
                        Cancel();
                        return PlacementStep.Cancelled;
                    }
                    return overField ? PlacementStep.InvalidCell : PlacementStep.NoChange;
                }
                case PlacementPhase.Facing:
                {
                    // 押したのも離したのもフィールド上のときだけ確定する。フィールドの外で離したら、誤って置かないよう取りやめる
                    var commit = pressedInFacing;
                    pressedInFacing = false;
                    if (!overField)
                    {
                        Cancel();
                        return PlacementStep.Cancelled;
                    }
                    return commit ? Commit() : PlacementStep.NoChange;
                }
                default:
                    return PlacementStep.NoChange;
            }
        }

        // ---- キーボード ----

        /// <summary>
        /// 矢印キー（1 マス分の方向。グリッドの y は下向きが正）。種類選択中はカーソルを動かして候補にし、向き選択中は向きを変える。
        /// 何かを変えたら true。
        /// </summary>
        public bool Arrow(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return false;
            if (Phase == PlacementPhase.Facing)
            {
                Facing = dx != 0 ? (dx > 0 ? Facing.Right : Facing.Left) : (dy > 0 ? Facing.Down : Facing.Up);
                return true;
            }
            if (Phase != PlacementPhase.Selected && Phase != PlacementPhase.Hover) return false;
            var x = Clamp(Cursor.X + dx, 0, board.Map.Width - 1);
            var y = Clamp(Cursor.Y + dy, 0, board.Map.Height - 1);
            Cursor = new GridPoint(x, y);
            Candidate = Cursor;
            hasCandidate = true;
            UpdateHoverPhase();
            return true;
        }

        /// <summary>決定キー。候補が置けるなら向き選択へ、向き選択中なら配置を確定する。</summary>
        public PlacementStep Confirm()
        {
            switch (Phase)
            {
                case PlacementPhase.Hover:
                case PlacementPhase.Selected:
                    Dragging = false;
                    return CandidateValid ? EnterFacing() : PlacementStep.InvalidCell;
                case PlacementPhase.Facing:
                    return Commit();
                default:
                    return PlacementStep.NoChange;
            }
        }

        // ---- 向き ----

        /// <summary>
        /// 配置マスの中心からポインタまでの差（マス座標系の向き: dy は下が正）で向きを決める。
        /// 差が deadZone より小さい間は今の向きのまま。向き選択中だけ効く。
        /// </summary>
        public void SetFacingFromDelta(float dx, float dy, float deadZone)
        {
            if (Phase != PlacementPhase.Facing) return;
            var facing = FacingFromDelta(dx, dy, Facing, deadZone);
            Facing = facing;
        }

        public static Facing FacingFromDelta(float dx, float dy, Facing current, float deadZone)
        {
            if (dx * dx + dy * dy < deadZone * deadZone) return current;
            var ax = dx < 0f ? -dx : dx;
            var ay = dy < 0f ? -dy : dy;
            if (ax >= ay) return dx > 0f ? Facing.Right : Facing.Left;
            return dy > 0f ? Facing.Down : Facing.Up;
        }

        // ---- 確定・取りやめ ----

        /// <summary>向き選択中に配置を確定する。成功すれば待機へ戻る。</summary>
        public PlacementStep Commit()
        {
            if (Phase != PlacementPhase.Facing) return PlacementStep.NoChange;
            var cell = Candidate;
            var result = summoner.TrySummon(SelectedUnit.Id, board, cell, Facing, out var placed);
            LastSummonResult = result;
            if (result == SummonResult.Success)
            {
                LastPlaced = placed;
                Reset();
                return PlacementStep.Placed;
            }
            pressedInFacing = false;
            switch (result)
            {
                case SummonResult.OutOfMap:
                case SummonResult.WrongCellType:
                case SummonResult.CellOccupied:
                    // 配置先の事情（確認から確定までの間に埋まった等）。種類は選んだまま配置先の選び直しへ
                    Phase = PlacementPhase.Hover;
                    break;
                default:
                    // 上限・待ち時間・コインの事情。その種類は今は召喚できないので操作を終える
                    Reset();
                    break;
            }
            return PlacementStep.Failed;
        }

        /// <summary>配置操作を取りやめる。操作中だったなら true（false のときの Esc は配置とは無関係）。</summary>
        public bool Cancel()
        {
            if (Phase == PlacementPhase.Idle) return false;
            Reset();
            return true;
        }

        private PlacementStep EnterFacing()
        {
            Phase = PlacementPhase.Facing;
            pressedInFacing = false;
            return PlacementStep.EnteredFacing;
        }

        private void UpdateHoverPhase() => Phase = hasCandidate ? PlacementPhase.Hover : PlacementPhase.Selected;

        private void Reset()
        {
            Phase = PlacementPhase.Idle;
            SelectedUnit = null;
            hasCandidate = false;
            Dragging = false;
            pressedInFacing = false;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
