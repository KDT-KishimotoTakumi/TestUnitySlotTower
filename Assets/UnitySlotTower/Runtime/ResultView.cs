using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Core.Combat;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// リザルト（クリア／ゲームオーバー）の表示と操作（T402、FR-005、D-35）。Game シーンの中のオーバーレイで、文字は使わない。
    /// 結果の印（★ か ×）、到達ウェーブ（旗 + 数字）、難易度（数字）、リトライ（↻）とタイトル（⌂）のボタン。
    /// Enter / Space でリトライ、Esc でタイトル。結果が決まるまでは何も出さず、毎フレーム <see cref="BattleRunner.Outcome"/> を読むだけ（購読なし）。
    /// リトライは Game を読み直す（状態は全部作り直される）。
    /// </summary>
    public sealed class ResultView : MonoBehaviour
    {
        public static readonly Color ClearColor = new Color(1f, 0.82f, 0.2f);
        public static readonly Color GameOverColor = new Color(0.9f, 0.2f, 0.2f);
        /// <summary>結果が出てからこの秒数のあいだは決定・戻るを受け付けない（スロットの連打 Space で、結果を読む前にリトライしてしまわないように）。実時間で数える。</summary>
        public const float InputGuardSeconds = 0.6f;

        [SerializeField] private BattleRunner runner;
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("結果が出たときだけ有効にする子オブジェクト（暗幕と印とボタン）")]
        [SerializeField] private GameObject content;
        [SerializeField] private Image outcomeIcon;
        [SerializeField] private Image flagIcon;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text difficultyText;
        [SerializeField] private Image retryGlyph;
        [SerializeField] private Image homeGlyph;
        [SerializeField] private BarButton retryButton;
        [SerializeField] private BarButton titleButton;

        private readonly List<Object> owned = new List<Object>();
        private readonly MenuInput menu = new MenuInput();
        private GameOutcome shown;
        private float shownAt;
        private bool loading;

        public bool IsShown => content != null && content.activeSelf;

        private void Awake()
        {
            retryButton.Bind(Retry);
            titleButton.Bind(GoTitle);
            menu.Confirmed += Retry;
            menu.Back += GoTitle;
            flagIcon.sprite = ShapeSprites.Create(OperationBarView.Flag, owned);
            retryGlyph.sprite = ShapeSprites.Create(ShapeSprites.Retry, owned);
            homeGlyph.sprite = ShapeSprites.Create(ShapeSprites.Home, owned);
            content.SetActive(false);
        }

        private void OnEnable()
        {
            // 結果表示中に無効→有効になっても、キー操作を取り戻す
            if (shown != GameOutcome.None) menu.Enable(inputActions);
        }

        private void OnDisable() => menu.Disable();

        private void OnDestroy()
        {
            for (var i = 0; i < owned.Count; i++)
                if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
        }

        private void Update()
        {
            if (runner == null || runner.Outcome == shown) return;
            shown = runner.Outcome;
            if (shown == GameOutcome.None)
            {
                content.SetActive(false);
                menu.Disable();
                return;
            }
            var cleared = shown == GameOutcome.Cleared;
            outcomeIcon.sprite = ShapeSprites.Create(cleared ? (ShapeSprites.Shape)ShapeSprites.Star : ShapeSprites.Cross, owned);
            outcomeIcon.color = cleared ? ClearColor : GameOverColor;
            waveText.SetText("{0:0}/{1:0}", (float)runner.ReachedWave, (float)runner.Flow.WaveCount);
            difficultyText.SetText("{0:0}", GameSession.Difficulty);
            shownAt = Time.unscaledTime;
            content.SetActive(true);
            menu.Enable(inputActions);
        }

        // 表示直後（入力ガード中）と、すでに読み込みを始めたあとは受け付けない
        private bool CanAct => IsShown && !loading && Time.unscaledTime - shownAt >= InputGuardSeconds;

        private void Retry()
        {
            if (!CanAct) return;
            loading = true;
            GameSession.LoadGame();
        }

        private void GoTitle()
        {
            if (!CanAct) return;
            loading = true;
            GameSession.LoadTitle();
        }
    }
}
