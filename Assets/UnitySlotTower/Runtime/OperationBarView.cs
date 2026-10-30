using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Combat;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 操作バーの左（ライフ・コイン・ウェーブ番号）と右（▶ ウェーブ開始・❚❚ 一時停止/再開）の表示と押下（T308、FR-002、FR-003）。
    /// 毎フレーム <see cref="BattleRunner"/> の状態を読み取る方式で、購読は持たない。数字は変わったときだけ書き換える。
    /// 文字は数字だけ。ハート・旗・コイン・三角は文字ではなく、Awake で作る単色のスプライトで描く（このコンポーネントが所有し、破棄時に片付ける）。
    /// 召喚カードは <see cref="SummonIcon"/>（<see cref="PlacementInput"/> が更新する）。
    /// </summary>
    public sealed class OperationBarView : MonoBehaviour
    {
        public static readonly Color LifeNormal = Color.white;
        public static readonly Color LifeAlert = new Color(1f, 0.15f, 0.15f);
        public static readonly Color LifeHeart = new Color(1f, 0.35f, 0.5f);
        public static readonly Color CoinNormal = new Color(0.95f, 0.78f, 0.2f);
        public static readonly Color CoinGain = new Color(0.45f, 1f, 0.5f);
        public static readonly Color CoinSpend = new Color(1f, 0.5f, 0.3f);
        public static readonly Color FlagColor = new Color(0.55f, 0.8f, 1f);
        public static readonly Color StartOn = new Color(0.2f, 0.62f, 0.3f);
        public static readonly Color StartOff = new Color(0.14f, 0.18f, 0.16f);
        public static readonly Color PauseNormal = new Color(0.3f, 0.36f, 0.52f);
        public static readonly Color PauseResume = new Color(0.2f, 0.62f, 0.3f);
        public static readonly Color GlyphOn = Color.white;
        public static readonly Color GlyphOff = new Color(0.45f, 0.45f, 0.45f);

        private const float LifeBlinkSeconds = 0.8f;
        private const float LifeBlinkHz = 6f;
        private const float CoinFlashSeconds = 0.4f;
        private const int ButtonStateStart = 1;
        private const int ButtonStatePaused = 2;

        [SerializeField] private BattleRunner runner;
        [SerializeField] private TMP_Text lifeText;
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private Image lifeIcon;
        [SerializeField] private Image coinIcon;
        [SerializeField] private Image flagIcon;
        [SerializeField] private BarButton startButton;
        [SerializeField] private Image startBackground;
        [SerializeField] private Image startGlyph;
        [SerializeField] private BarButton pauseButton;
        [SerializeField] private Image pauseBackground;
        [SerializeField] private GameObject pauseBars;
        [SerializeField] private Image resumeGlyph;

        private readonly List<Object> owned = new List<Object>();

        private int shownLife = -1;
        private int shownCoins = -1;
        private int shownWave = -1;
        private int shownWaveCount = -1;
        private int shownButtons = -1;
        private float lifeBlink;
        private float coinFlash;
        private Color coinFlashColor = CoinNormal;
        private Color shownCoinColor = CoinNormal;
        private int shownLifeBlinkOn;

        private void Awake()
        {
            lifeIcon.sprite = ShapeSprites.Create(Heart, owned);
            coinIcon.sprite = ShapeSprites.Create(Coin, owned);
            flagIcon.sprite = ShapeSprites.Create(Flag, owned);
            var triangle = ShapeSprites.Create(Triangle, owned);
            startGlyph.sprite = triangle;
            resumeGlyph.sprite = triangle;
            lifeIcon.color = LifeHeart;
            coinIcon.color = CoinNormal;
            flagIcon.color = FlagColor;
            startButton.Bind(OnStartPressed);
            pauseButton.Bind(OnPausePressed);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < owned.Count; i++)
                if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
        }

        private void OnStartPressed()
        {
            if (runner != null) runner.StartWave();
        }

        private void OnPausePressed()
        {
            if (runner != null) runner.TogglePause();
        }

        private void Update()
        {
            if (runner == null || runner.Flow == null || runner.Life == null || runner.Wallet == null) return;
            RefreshLife();
            RefreshCoins();
            RefreshWave();
            RefreshButtons();
        }

        private void RefreshLife()
        {
            var life = runner.Life.Current;
            if (life != shownLife)
            {
                // 減ったときだけ赤く点滅させる（最初の表示・回復では点滅しない）
                if (shownLife >= 0 && life < shownLife) lifeBlink = LifeBlinkSeconds;
                shownLife = life;
                lifeText.SetText("{0:0}", life);
            }
            if (lifeBlink <= 0f) return;
            // 位相は減算の前に計算する（減ったフレームは経過 0 秒で、赤から始まる）
            var elapsed = LifeBlinkSeconds - lifeBlink;
            var on = Mathf.FloorToInt(elapsed * LifeBlinkHz * 2f) % 2 == 0;
            lifeBlink -= Time.unscaledDeltaTime;
            if (lifeBlink <= 0f) on = false;
            var flag = on ? 1 : 0;
            if (flag == shownLifeBlinkOn) return;
            shownLifeBlinkOn = flag;
            lifeText.color = on ? LifeAlert : LifeNormal;
            lifeIcon.color = on ? LifeAlert : LifeHeart;
        }

        private void RefreshCoins()
        {
            var coins = runner.Wallet.Coins;
            if (coins != shownCoins)
            {
                if (shownCoins >= 0)
                {
                    coinFlash = CoinFlashSeconds;
                    coinFlashColor = coins > shownCoins ? CoinGain : CoinSpend;
                }
                shownCoins = coins;
                coinText.SetText("{0:0}", coins);
            }
            if (coinFlash <= 0f) return;
            coinFlash = Mathf.Max(0f, coinFlash - Time.unscaledDeltaTime);
            var color = Color.Lerp(CoinNormal, coinFlashColor, coinFlash / CoinFlashSeconds);
            if (color == shownCoinColor) return;
            shownCoinColor = color;
            coinText.color = color;
        }

        private void RefreshWave()
        {
            var wave = runner.Flow.WaveNumber;
            var count = runner.Flow.WaveCount;
            if (wave == shownWave && count == shownWaveCount) return;
            shownWave = wave;
            shownWaveCount = count;
            waveText.SetText("{0:0}/{1:0}", (float)wave, (float)count);
        }

        private void RefreshButtons()
        {
            var canStart = runner.Flow.State == WaveState.Preparing && !runner.IsPaused;
            var paused = runner.IsPaused;
            var state = (canStart ? ButtonStateStart : 0) | (paused ? ButtonStatePaused : 0);
            if (state == shownButtons) return;
            shownButtons = state;
            startBackground.color = canStart ? StartOn : StartOff;
            startGlyph.color = canStart ? GlyphOn : GlyphOff;
            pauseBackground.color = paused ? PauseResume : PauseNormal;
            pauseBars.SetActive(!paused);
            resumeGlyph.gameObject.SetActive(paused);
        }

        // ---- 図形（x, y は -1〜1、y は上向き） ----

        public static bool Heart(float x, float y)
        {
            var u = x * 1.15f;
            var v = y * 1.15f + 0.12f;
            var a = u * u + v * v - 1f;
            return a * a * a - u * u * v * v * v <= 0f;
        }

        public static bool Coin(float x, float y)
        {
            var r = Mathf.Sqrt(x * x + y * y);
            return r <= 1f && (r < 0.62f || r > 0.72f);
        }

        public static bool Flag(float x, float y)
        {
            if (x >= -0.75f && x <= -0.55f && y >= -1f && y <= 1f) return true;
            return ShapeSprites.InTriangle(x, y, -0.55f, 0.95f, 0.85f, 0.45f, -0.55f, -0.05f);
        }

        public static bool Triangle(float x, float y) => ShapeSprites.InTriangle(x, y, -0.55f, -0.8f, -0.55f, 0.8f, 0.85f, 0f);
    }
}
