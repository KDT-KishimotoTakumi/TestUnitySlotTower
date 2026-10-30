using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// タイトル画面（T401、FR-005、D-35、D-36、D-38）。ロゴ（ここだけ文字可）と ▶ → 難易度選択（設定 1〜6 のプレート）→ Game。
    /// 操作: ▶ のクリック / Enter / Space で選択へ、プレートのクリック / 数字キー 1〜6 で決定して Game へ、◀ / Esc でタイトルへ戻る。
    /// 選んだ難易度は <see cref="GameSession"/> に入れる。
    /// </summary>
    public sealed class TitleView : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject selectPanel;
        [SerializeField] private BarButton startButton;
        [SerializeField] private Image startGlyph;
        [SerializeField] private BarButton backButton;
        [SerializeField] private Image backGlyph;
        [Tooltip("設定 1〜6 のプレート（添字 0 が設定 1）")]
        [SerializeField] private BarButton[] plates = new BarButton[0];

        private readonly List<Object> owned = new List<Object>();
        private readonly MenuInput menu = new MenuInput();
        private bool loading;

        public bool IsSelecting => selectPanel != null && selectPanel.activeSelf;

        private void Awake()
        {
            var triangle = ShapeSprites.Create(OperationBarView.Triangle, owned);
            startGlyph.sprite = triangle;
            backGlyph.sprite = triangle;
            startButton.Bind(ShowSelect);
            backButton.Bind(ShowTitle);
            for (var i = 0; i < plates.Length; i++)
            {
                var difficulty = i + GameSession.MinDifficulty;
                plates[i].Bind(() => Choose(difficulty));
            }
            menu.Confirmed += OnConfirmed;
            menu.Back += ShowTitle;
            menu.DigitPressed += Choose;
            ShowTitle();
        }

        private void OnEnable() => menu.Enable(inputActions);

        private void OnDisable() => menu.Disable();

        private void OnDestroy()
        {
            for (var i = 0; i < owned.Count; i++)
                if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
        }

        public void ShowTitle()
        {
            titlePanel.SetActive(true);
            selectPanel.SetActive(false);
        }

        public void ShowSelect()
        {
            titlePanel.SetActive(false);
            selectPanel.SetActive(true);
        }

        private void OnConfirmed()
        {
            if (!IsSelecting) ShowSelect();
        }

        /// <summary>難易度 difficulty（設定 1〜6）で Game を始める。選択画面でないとき・範囲外は何もしない。</summary>
        public void Choose(int difficulty)
        {
            if (!IsSelecting || loading) return;
            if (difficulty < GameSession.MinDifficulty || difficulty > GameSession.MaxDifficulty) return;
            loading = true;
            GameSession.Difficulty = difficulty;
            GameSession.LoadGame();
        }
    }
}
