using System;
using UnityEngine;
using UnitySlotTower.Core.Combat;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Enemies;
using UnitySlotTower.Core.Map;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Units;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 戦闘の実行役（T209）。Start で表から <see cref="BattleSimulation"/> 一式を組み立て、<see cref="SimClock"/> の固定ステップで進める。
    /// 財布はスロット筐体（<see cref="SlotCabinetView"/>）のものを共有する（なければ別の暫定の財布。統合は T301）。
    /// ウェーブの進行は <see cref="WaveFlow"/>（T304）。準備中は敵が進まず、<see cref="StartWave"/> で戦闘が始まる（▶ ボタンは T308）。
    /// ウェーブが終わると盤面・敵・戦闘を作り直す（<see cref="PrepareNextWave"/>）。ライフ・財布・召喚役・強化の段階は引き継ぐ。
    /// 味方の配置入力は T208。
    /// </summary>
    public sealed class BattleRunner : MonoBehaviour
    {
        // スロットの乱数（Ticks そのもの）と別の列にするための定数
        private const ulong DropSeedSalt = 0x9E3779B97F4A7C15UL;

        private readonly struct DemoUnit
        {
            public readonly string Id;
            public readonly int X;
            public readonly int Y;
            public readonly Facing Facing;

            public DemoUnit(string id, int x, int y, Facing facing)
            {
                Id = id;
                X = x;
                Y = y;
                Facing = facing;
            }
        }

        // w1 用の確認配置。経路の入口側に壁（タンク）、その後ろに剣士、高台に弓・ヒーラー・バッファー
        private static readonly DemoUnit[] DemoUnits =
        {
            new DemoUnit("tank", 2, 1, Facing.Right),
            new DemoUnit("swordsman", 3, 1, Facing.Left),
            new DemoUnit("archer", 2, 2, Facing.Up),
            new DemoUnit("healer", 3, 2, Facing.Up),
            new DemoUnit("buffer", 1, 2, Facing.Right),
        };

        [SerializeField] private PresentationCatalog catalog;
        [SerializeField] private BattleFieldView field;
        [SerializeField] private BattleView view;
        [SerializeField] private SlotCabinetView cabinet;
        [SerializeField, Min(1)] private int wave = 1;
        [Tooltip("確認用。true のとき Start で数体の味方を置く（本番では false）")]
        [SerializeField] private bool placeDemoUnitsOnStart;
        [Tooltip("確認用。true のとき、準備中を飛ばして Start から戦闘を始め、ウェーブ終了後は次のウェーブも自動で始める（▶ ボタンができるまでの既定は true）")]
        [SerializeField] private bool autoStartWave = true;

        private readonly SimClock clock = new SimClock();

        private Action stepAction;
        private Action<int, int> waveClearedAction;
        private UnitTable unitTable;
        private TableSet tables;
        private GameConfig config;
        private GameRandom dropRandom;
        private bool advancePending;

        public PlacementBoard Board { get; private set; }
        public EnemyField Enemies { get; private set; }
        public BattleSimulation Simulation { get; private set; }
        public UnitSummoner Summoner { get; private set; }
        public UnitUpgrades Upgrades { get; private set; }
        public PlayerLife Life { get; private set; }
        public Wallet Wallet { get; private set; }
        /// <summary>ウェーブ進行（準備・戦闘・無料ストック）。Start 前は null。</summary>
        public WaveFlow Flow { get; private set; }
        /// <summary>味方の表（行の順が召喚アイコン・数字キーの順）。Start 前は null。</summary>
        public UnitTable Units => unitTable;

        private void Awake()
        {
            stepAction = Step;
            waveClearedAction = OnWaveCleared;
        }

        private void OnDestroy()
        {
            if (Flow != null) Flow.WaveCleared -= waveClearedAction;
        }

        private void Start()
        {
            if (catalog == null || field == null || view == null || field.Map == null)
            {
                Debug.LogError("BattleRunner: catalog / field / view / マップが揃っていません", this);
                enabled = false;
                return;
            }
            var map = field.Map;
            if (map.Id != WaveTable.MapIdOf(wave))
            {
                Debug.LogError($"BattleRunner: ウェーブ {wave} のマップは '{WaveTable.MapIdOf(wave)}' ですが、フィールドは '{map.Id}' です", this);
                enabled = false;
                return;
            }

            // 表は呼ぶたびに作り直すので、1 度だけ取って保持する
            tables = catalog.LoadTables();
            config = tables.GameConfig;
            unitTable = tables.Units;
            dropRandom = new GameRandom((ulong)DateTime.UtcNow.Ticks ^ DropSeedSalt);

            if (cabinet != null && cabinet.Wallet == null)
                Debug.LogWarning("BattleRunner: 筐体の Wallet がまだありません。別の財布を使います（スロットとコインが共有されません）", this);
            Wallet = cabinet != null && cabinet.Wallet != null ? cabinet.Wallet : new Wallet(config.InitialCoins);
            Summoner = new UnitSummoner(unitTable, Wallet, config.MaxDeployed);
            Life = new PlayerLife(config.StartingLife);
            Flow = new WaveFlow(Summoner, Life, Wallet, config.LifeBonusPerLife);
            Flow.WaveCleared += waveClearedAction;
            BuildBattle(wave, map);

            if (placeDemoUnitsOnStart) PlaceDemoUnits();
            if (autoStartWave) Flow.StartWave();
        }

        /// <summary>
        /// ウェーブ waveNumber 用の盤面・敵・戦闘を作り、準備中にする。2 回目以降は、強化の段階を新しい盤面へ付け替える。
        /// 戦闘は ReachedGoal を購読するので、古い EnemyField と一緒に捨てる（購読元ごと参照されなくなる）。
        /// </summary>
        private void BuildBattle(int waveNumber, MapData map)
        {
            var schedule = tables.Waves.CreateSchedule(waveNumber);
            var first = Board == null;
            Board = new PlacementBoard(map);
            Enemies = new EnemyField(Board, schedule);
            if (first) Upgrades = new UnitUpgrades(tables.UnitUpgrades, Wallet, Board);
            else Upgrades.Rebind(Board);
            Simulation = new BattleSimulation(Board, Enemies, Summoner, tables.UnitRanges, config.BufferAtkMultiplier,
                Life, Wallet, dropRandom, config.RefundRate);
            if (first) view.Bind(field, Board, Enemies, tables.UnitRanges, Life, config.MaxDeployed);
            else view.Rebind(Board, Enemies);
            Flow.Prepare(waveNumber, Board, Enemies, schedule, Simulation);
        }

        /// <summary>準備中なら戦闘を始める（▶ ボタンの入口、T308）。始められたら true。</summary>
        public bool StartWave()
        {
            return Flow != null && Flow.StartWave();
        }

        /// <summary>
        /// ウェーブ終了後（Cleared）に、次のウェーブを準備中にする。用意できたら true。
        /// 戦闘フィールドの表示は 1 つのマップに固定なので、次のウェーブのマップが表示中のものと違うときは用意しない（マップの切替は T305）。
        /// </summary>
        public bool PrepareNextWave()
        {
            if (Flow == null || Flow.State != WaveState.Cleared) return false;
            var next = Flow.WaveNumber + 1;
            if (!tables.Waves.HasWave(next))
            {
                Debug.LogWarning($"BattleRunner: ウェーブ {next} の出現内容が waves.csv にありません", this);
                return false;
            }
            if (field.Map.Id != WaveTable.MapIdOf(next))
            {
                Debug.LogWarning($"BattleRunner: ウェーブ {next} のマップ '{WaveTable.MapIdOf(next)}' は表示中のマップ '{field.Map.Id}' と違います（マップ切替は T305）", this);
                return false;
            }
            BuildBattle(next, field.Map);
            return true;
        }

        // Step の最中に盤面を作り直さないよう、次の Step の頭で進める
        private void OnWaveCleared(int clearedWave, int bonus)
        {
            advancePending = autoStartWave && Flow.State == WaveState.Cleared;
        }

        private void Update()
        {
            clock.Advance(Time.deltaTime, stepAction);
        }

        private void Step()
        {
            if (advancePending)
            {
                advancePending = false;
                if (PrepareNextWave()) Flow.StartWave();
            }
            Summoner.Tick(SimClock.StepSeconds);
            Flow.Tick(SimClock.StepSeconds);
        }

        /// <summary>シミュレーションを seconds 秒ぶん一気に進める（確認用。通常のフレーム駆動と同じ固定ステップ）。</summary>
        public void FastForward(float seconds)
        {
            if (Simulation == null) return;
            var steps = Mathf.RoundToInt(seconds * SimClock.StepsPerSecond);
            for (var i = 0; i < steps; i++) Step();
        }

        /// <summary>
        /// 確認用の味方を置く（w1 の配置）。足りないコインは財布に足してから召喚する。置けた数を返す。
        /// 本番の開始では呼ばれない（placeDemoUnitsOnStart が false の間）。
        /// </summary>
        public int PlaceDemoUnits()
        {
            if (Simulation == null) return 0;
            var total = 0;
            for (var i = 0; i < DemoUnits.Length; i++) total += unitTable.Get(DemoUnits[i].Id).Cost;
            if (Wallet.Coins < total) Wallet.Add(total - Wallet.Coins);

            var placedCount = 0;
            for (var i = 0; i < DemoUnits.Length; i++)
            {
                var demo = DemoUnits[i];
                var result = Summoner.TrySummon(demo.Id, Board, new GridPoint(demo.X, demo.Y), demo.Facing, out _);
                if (result == SummonResult.Success) placedCount++;
                else Debug.LogWarning($"BattleRunner: デモの味方 '{demo.Id}' を置けませんでした: {result}", this);
            }
            return placedCount;
        }

        /// <summary>マス (x, y) の味方の攻撃範囲を強調する。味方がいなければ消す（T208 の選択が使う表示の入口）。Start 前は何もしない。</summary>
        public void ShowRangeAt(int x, int y)
        {
            if (Simulation == null) return;
            view.ShowRange(Board.GetAt(new GridPoint(x, y)));
        }

        public void ShowRange(PlacedUnit unit)
        {
            if (Simulation == null) return;
            view.ShowRange(unit);
        }

        /// <summary>配置前の味方（種類・マス・向き）の攻撃範囲を強調する（T208 のドラッグ中の表示）。</summary>
        public void ShowRangePreview(UnitDef unit, GridPoint cell, Facing facing)
        {
            if (Simulation == null) return;
            view.ShowRangePreview(new PlacedUnit(unit, cell, facing));
        }

        public void ClearRange()
        {
            if (Simulation == null) return;
            view.ClearRange();
        }
    }
}
