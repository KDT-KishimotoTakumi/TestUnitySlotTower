using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Sim;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 下部のスロット筐体の仮表示（T110）。<see cref="SlotMachine"/> を読んで描くだけで、ロジックは持たない。
    /// 入力はレバー・ストップのボタン（uGUI）と InputSystem_Actions の Game マップのキー。
    /// 筐体は暫定でここが Wallet と SlotMachine を作る（戦闘との財布の共有は T301 で統合する）。
    /// </summary>
    public sealed class SlotCabinetView : MonoBehaviour
    {
        public static readonly Color ButtonOff = new Color(0.3f, 0.12f, 0.12f);
        public static readonly Color ButtonOn = new Color(0.95f, 0.35f, 0.25f);
        public static readonly Color LampOff = new Color(0.25f, 0.2f, 0.08f);
        public static readonly Color LampOn = new Color(1f, 0.85f, 0.2f);
        public static readonly Color ReplayLampOff = new Color(0.1f, 0.15f, 0.3f);
        public static readonly Color ReplayLampOn = new Color(0.3f, 0.6f, 1f);
        public static readonly Color CoinNormal = new Color(0.95f, 0.78f, 0.2f);
        public static readonly Color CoinAlert = new Color(0.9f, 0.1f, 0.1f);

        private const string LeverActionName = "SlotLever";
        private static readonly string[] StopActionNames = { "StopReelLeft", "StopReelCenter", "StopReelRight" };

        private const float CoinBlinkSeconds = 1.2f;
        private const float CoinBlinkHalfPeriod = 0.15f;
        private const float LampPulseSpeed = 6f;
        private const float LampPulseMinBrightness = 0.6f;
        private const double MaxSlideSymbols = SlotLayout.MaxSlipLimit + 1;

        private const float PremonitionSeconds = 0.6f;
        private const float PremonitionFirstHz = 880f;
        private const float PremonitionSecondHz = 1320f;
        private const float PremonitionDecay = 3f;
        private const float LeverSeconds = 0.08f;
        private const float LeverHz = 220f;
        private const float StopSeconds = 0.06f;
        private const float StopHz = 440f;
        private const float DenySeconds = 0.25f;
        private const float DenyHz = 110f;
        private const float ClipDecay = 12f;

        [SerializeField] private PresentationCatalog catalog;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField, Range(SlotProbabilityTable.MinSetting, SlotProbabilityTable.MaxSetting)] private int setting = 1;
        [SerializeField] private SlotReelView[] reelViews = new SlotReelView[0];
        [SerializeField] private Button leverButton;
        [SerializeField] private Button[] stopButtons = new Button[0];
        [SerializeField] private TMP_Text creditText;
        [SerializeField] private TMP_Text payoutText;
        [SerializeField] private Image premonitionLamp;
        [SerializeField] private Image replayLamp;
        [SerializeField] private Image coinIcon;
        [SerializeField] private RectTransform waitGauge;
        [SerializeField] private AudioSource audioSource;

        private readonly SimClock clock = new SimClock();
        private readonly double[] shown = new double[SlotLayout.ReelCount];
        private readonly Action<InputAction.CallbackContext>[] stopCallbacks = new Action<InputAction.CallbackContext>[SlotLayout.ReelCount];
        private readonly UnityAction[] stopClicks = new UnityAction[SlotLayout.ReelCount];
        private readonly InputAction[] stopActions = new InputAction[SlotLayout.ReelCount];

        private Action stepAction;
        private Action<InputAction.CallbackContext> leverCallback;
        private UnityAction leverClick;
        private InputAction leverAction;
        private AudioClip leverClip;
        private AudioClip stopClip;
        private AudioClip denyClip;
        private AudioClip premonitionClip;
        private float waitSeconds;
        private float coinBlinkRemaining;
        private int shownCoins = -1;
        private int shownPayout = -1;
        private float shownWaitRatio = -1f;

        public Wallet Wallet { get; private set; }
        public SlotMachine Machine { get; private set; }

        private void Awake()
        {
            // OnEnable は Awake が途中で失敗しても呼ばれる。null のコールバックを InputAction に登録すると、
            // Reload Domain 無効ではアセット側に残り続け、以降の Play の登録（performed +=）で NRE になるため先に作る
            stepAction = () => Machine.Tick(SimClock.StepSeconds);
            leverCallback = _ => HandleLever();
            leverClick = HandleLever;
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                var index = reel;
                stopCallbacks[reel] = _ => HandleStop(index);
                stopClicks[reel] = () => HandleStop(index);
            }

            var tables = catalog.LoadTables();
            waitSeconds = tables.SlotRules.WaitSeconds;
            Wallet = new Wallet(tables.GameConfig.InitialCoins);
            Machine = new SlotMachine(tables, Wallet, new GameRandom((ulong)DateTime.UtcNow.Ticks), setting);

            var reels = tables.SlotReels;
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++) reelViews[reel].Bind(reels, reel);

            leverClip = ToneSynth.Create("SlotLever", LeverSeconds, LeverHz, LeverHz, ClipDecay);
            stopClip = ToneSynth.Create("SlotStop", StopSeconds, StopHz, StopHz, ClipDecay);
            denyClip = ToneSynth.Create("SlotDeny", DenySeconds, DenyHz, DenyHz, ClipDecay / 2f, true);
            premonitionClip = ToneSynth.Create("SlotPremonition", PremonitionSeconds, PremonitionFirstHz, PremonitionSecondHz, PremonitionDecay);
        }

        private void OnEnable()
        {
            leverButton.onClick.AddListener(leverClick);
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++) stopButtons[reel].onClick.AddListener(stopClicks[reel]);

            // 他の入力を巻き込まないよう、マップ全体ではなく使うアクションだけを有効にする
            leverAction = inputActions.FindAction(LeverActionName, true);
            leverAction.performed += leverCallback;
            leverAction.Enable();
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                stopActions[reel] = inputActions.FindAction(StopActionNames[reel], true);
                stopActions[reel].performed += stopCallbacks[reel];
                stopActions[reel].Enable();
            }
        }

        private void OnDisable()
        {
            leverButton.onClick.RemoveListener(leverClick);
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++) stopButtons[reel].onClick.RemoveListener(stopClicks[reel]);

            if (leverAction != null)
            {
                leverAction.performed -= leverCallback;
                leverAction.Disable();
            }
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                if (stopActions[reel] == null) continue;
                stopActions[reel].performed -= stopCallbacks[reel];
                stopActions[reel].Disable();
            }
        }

        private void OnDestroy()
        {
            DestroyClip(leverClip);
            DestroyClip(stopClip);
            DestroyClip(denyClip);
            DestroyClip(premonitionClip);
        }

        private static void DestroyClip(AudioClip clip)
        {
            if (clip != null) Destroy(clip);
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            clock.Advance(deltaTime, stepAction);
            RefreshReels(deltaTime);
            RefreshDisplays(deltaTime);
            RefreshLamps();
        }

        /// <summary>レバー（ベット込み）。ボタンとキーの両方から呼ばれる。</summary>
        public void HandleLever()
        {
            switch (Machine.PullLever())
            {
                case LeverResult.Started:
                    Play(leverClip);
                    if (Machine.Premonition) Play(premonitionClip);
                    break;
                case LeverResult.NotEnoughCoins:
                    coinBlinkRemaining = CoinBlinkSeconds;
                    Play(denyClip);
                    break;
            }
        }

        /// <summary>リール停止。reel は 0=左, 1=中, 2=右。</summary>
        public void HandleStop(int reel)
        {
            if (Machine.PressStop(reel) == StopResult.Stopped) Play(stopClip);
        }

        private void Play(AudioClip clip) => audioSource.PlayOneShot(clip);

        private void RefreshReels(float deltaTime)
        {
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
            {
                var position = Machine.ReelPosition(reel);
                if (Machine.IsStopped(reel)) position = Slide(reel, position, deltaTime);
                else shown[reel] = position;
                reelViews[reel].Show(position);
            }
        }

        /// <summary>停止が決まったリールを、決まった位置まで回転と同じ速さで滑らせて止める（引き込みの見た目）。</summary>
        private double Slide(int reel, double target, float deltaTime)
        {
            var gap = ((target - shown[reel]) % SlotLayout.ReelSize + SlotLayout.ReelSize) % SlotLayout.ReelSize;
            if (gap <= 0.0 || gap > MaxSlideSymbols)
            {
                shown[reel] = target;
                return target;
            }
            var step = SlotMachine.SpinSymbolsPerSecond * deltaTime;
            shown[reel] = gap <= step ? target : (shown[reel] + step) % SlotLayout.ReelSize;
            return shown[reel];
        }

        private void RefreshDisplays(float deltaTime)
        {
            var coins = Wallet.Coins;
            if (coins != shownCoins)
            {
                shownCoins = coins;
                creditText.SetText("{0:0}", coins);
            }

            var payout = Machine.Phase == SlotPhase.Idle && Machine.LastResult != null ? Machine.LastResult.Payout : 0;
            if (payout != shownPayout)
            {
                shownPayout = payout;
                payoutText.SetText("{0:0}", payout);
            }

            // コイン不足でレバーが効かなかったとき、コインのアイコンを点滅させる
            var alert = false;
            if (coinBlinkRemaining > 0f)
            {
                coinBlinkRemaining -= deltaTime;
                alert = ((int)(Mathf.Max(coinBlinkRemaining, 0f) / CoinBlinkHalfPeriod) & 1) == 1;
            }
            coinIcon.color = alert ? CoinAlert : CoinNormal;

            var ratio = waitSeconds > 0f ? Mathf.Clamp01(Machine.WaitRemaining / waitSeconds) : 0f;
            if (!Mathf.Approximately(ratio, shownWaitRatio))
            {
                shownWaitRatio = ratio;
                waitGauge.anchorMax = new Vector2(ratio, 1f);
            }
        }

        private void RefreshLamps()
        {
            var spinning = Machine.Phase == SlotPhase.Spinning;
            var ready = Machine.Phase == SlotPhase.Idle && Machine.WaitRemaining <= 0f;
            leverButton.targetGraphic.color = ready ? ButtonOn : ButtonOff;
            for (var reel = 0; reel < SlotLayout.ReelCount; reel++)
                stopButtons[reel].targetGraphic.color = spinning && !Machine.IsStopped(reel) ? ButtonOn : ButtonOff;

            // 先バレ: 回転中だけランプが脈打つ
            var pulse = LampPulseMinBrightness + (1f - LampPulseMinBrightness) * (0.5f + 0.5f * Mathf.Sin(Time.time * LampPulseSpeed));
            premonitionLamp.color = spinning && Machine.Premonition ? Color.Lerp(LampOff, LampOn, pulse) : LampOff;
            replayLamp.color = Machine.ReplayPending ? ReplayLampOn : ReplayLampOff;
        }
    }
}
