using System;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Core.Enemies
{
    /// <summary>
    /// 戦闘中の敵1体。経路のマス列を一定速度で進み（マス間は直線で補間）、射程内の味方を攻撃する。
    /// 空中敵は空中経路を進む（経路の種類が違うだけで、動きは同じ）。
    /// </summary>
    public sealed class EnemyInstance
    {
        public EnemyDef Def { get; }
        public MapPath Path { get; }
        /// <summary>経路上の進み具合（マス数）。0 が出現口、Cells.Count - 1 がゴール。</summary>
        public double Progress { get; private set; }
        public bool ReachedGoal { get; private set; }
        public float Hp { get; }
        /// <summary>次に攻撃できるまでの残り時間（秒）。攻撃できる状態では 0 以下。</summary>
        public float AttackTimer { get; private set; }

        public EnemyInstance(EnemyDef def, MapPath path)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            if (def.IsAir != path.IsAir) throw new ArgumentException($"敵 '{def.Id}' と経路 '{path.Id}' の空中／地上が合っていません");
            Hp = def.MaxHp;
        }

        /// <summary>連続座標（マスの中心を整数とする。GridPoint と同じ向き）。</summary>
        public float PositionX
        {
            get
            {
                GetSegment(out var from, out var to, out var t);
                return from.X + (to.X - from.X) * t;
            }
        }

        public float PositionY
        {
            get
            {
                GetSegment(out var from, out var to, out var t);
                return from.Y + (to.Y - from.Y) * t;
            }
        }

        /// <summary>今いるマス（位置にいちばん近いマスの中心）。射程の起点。</summary>
        public GridPoint CurrentCell
        {
            get
            {
                var index = (int)(Progress + 0.5);
                var last = Path.Cells.Count - 1;
                return Path.Cells[index < last ? index : last];
            }
        }

        /// <summary>deltaSeconds だけ経路を進める。ゴールに着いたら ReachedGoal が true になり、そこで止まる。</summary>
        public void Move(float deltaSeconds)
        {
            if (ReachedGoal) return;
            var last = Path.Cells.Count - 1;
            Progress += (double)Def.MoveSpeed * deltaSeconds;
            if (Progress >= last)
            {
                Progress = last;
                ReachedGoal = true;
            }
        }

        /// <summary>
        /// 攻撃間隔を進め、攻撃できて射程内に味方がいれば 1 回攻撃する（HP を減らすだけ）。
        /// 攻撃したら true。攻撃できる状態で対象がいない間は、待ち時間が 0 で止まる（間隔を溜めない）。
        /// </summary>
        public bool TryAttack(float deltaSeconds, PlacementBoard board)
        {
            AttackTimer -= deltaSeconds;
            if (AttackTimer > 0f) return false;

            var target = FindTarget(board);
            if (target == null)
            {
                AttackTimer = 0f;
                return false;
            }
            target.TakeDamage(DamageFormula.Calculate(Def.Atk, target.Def.Def));
            AttackTimer += Def.AttackInterval;
            return true;
        }

        /// <summary>射程内でいちばん近い（同距離なら先に見つかった）、HP が残っている味方。いなければ null。</summary>
        public PlacedUnit FindTarget(PlacementBoard board)
        {
            var origin = CurrentCell;
            var reach = (int)Math.Ceiling(Def.Range);
            var rangeSquared = (double)Def.Range * Def.Range;
            PlacedUnit best = null;
            var bestDistance = double.MaxValue;
            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -reach; dx <= reach; dx++)
                {
                    var distance = (double)dx * dx + (double)dy * dy;
                    if (distance > rangeSquared || distance >= bestDistance) continue;
                    var unit = board.GetAt(new GridPoint(origin.X + dx, origin.Y + dy));
                    if (unit == null || unit.Hp <= 0f) continue;
                    best = unit;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void GetSegment(out GridPoint from, out GridPoint to, out float t)
        {
            var cells = Path.Cells;
            var last = cells.Count - 1;
            var index = (int)Progress;
            if (index >= last)
            {
                from = to = cells[last];
                t = 0f;
                return;
            }
            from = cells[index];
            to = cells[index + 1];
            t = (float)(Progress - index);
        }
    }
}
