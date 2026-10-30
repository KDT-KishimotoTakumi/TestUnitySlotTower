using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Core.Audio;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// Game シーンの BGM（コード生成の簡易ループ、002 FR-110〜FR-112）と、操作バーのスピーカーアイコン・音量の数字。
    /// 一時停止中は BGM も止まり、再開で続きから鳴る。音量はクリックで 0〜5 を巡り、キーで 1 段階ずつ上下する。BGM だけに効く（効果音は別の AudioSource）。
    /// 毎フレームは一時停止の状態を見るだけで、確保はしない。スプライトとクリップはこのコンポーネントが所有し、破棄時に片付ける。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class BgmPlayer : MonoBehaviour
    {
        public static readonly Color IconOn = new Color(0.9f, 0.95f, 1f);
        public static readonly Color IconMuted = new Color(0.45f, 0.45f, 0.5f);

        private const string UpActionName = "Game/BgmVolumeUp";
        private const string DownActionName = "Game/BgmVolumeDown";
        // BGM 全体の大きさ。音量の段階（Gain）をかけて使う。スロットの効果音より控えめにする
        private const float MasterVolume = 0.5f;
        private const float ClipAmplitude = 0.6f;
        private const float NoteSeconds = 0.25f;
        private const float BassSeconds = 2f;
        // コード進行 Am - F - C - G（各 8 音のアルペジオ + 低音）
        private static readonly float[][] Chords =
        {
            new[] { 220f, 261.63f, 329.63f },
            new[] { 174.61f, 220f, 261.63f },
            new[] { 261.63f, 329.63f, 392f },
            new[] { 196f, 246.94f, 293.66f },
        };
        private static readonly float[] ChordBass = { 110f, 87.31f, 130.81f, 98f };
        private static readonly int[] ArpeggioPattern = { 0, 1, 2, 1, 0, 1, 2, 1 };

        [SerializeField] private BattleRunner runner;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private BarButton speakerButton;
        [SerializeField] private Image speakerImage;
        [SerializeField] private TMP_Text volumeText;

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        private AudioSource source;
        private AudioClip clip;
        private Action<InputAction.CallbackContext> upCallback;
        private Action<InputAction.CallbackContext> downCallback;
        private InputAction upAction;
        private InputAction downAction;
        private int shownLevel = -1;
        private bool sourcePaused;

        public BgmVolume Volume { get; } = new BgmVolume();

        /// <summary>BGM を鳴らす AudioSource（テスト・確認用）。</summary>
        public AudioSource Source => source;

        private void Awake()
        {
            // Reload Domain 無効でも安全なよう、コールバックは OnEnable より前（かつ失敗しうる処理より前）に作る
            upCallback = _ => StepVolume(true);
            downCallback = _ => StepVolume(false);

            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            clip = CreateClip();
            source.clip = clip;

            speakerImage.sprite = ShapeSprites.Create(Speaker, owned);
            speakerButton.Bind(OnSpeakerPressed);
            Refresh();
            source.Play();
        }

        private void OnEnable()
        {
            upAction = inputActions.FindAction(UpActionName, true);
            upAction.performed += upCallback;
            upAction.Enable();
            downAction = inputActions.FindAction(DownActionName, true);
            downAction.performed += downCallback;
            downAction.Enable();
        }

        private void OnDisable()
        {
            if (upAction != null)
            {
                upAction.performed -= upCallback;
                upAction.Disable();
            }
            if (downAction != null)
            {
                downAction.performed -= downCallback;
                downAction.Disable();
            }
            if (sourcePaused && source != null) source.UnPause();
            sourcePaused = false;
        }

        private void OnDestroy()
        {
            for (var i = 0; i < owned.Count; i++)
                if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
            if (clip != null) Destroy(clip);
        }

        private void Update()
        {
            var paused = runner != null && runner.IsPaused;
            if (paused == sourcePaused) return;
            sourcePaused = paused;
            if (paused) source.Pause();
            else source.UnPause();
        }

        private void OnSpeakerPressed()
        {
            Volume.Cycle();
            Refresh();
        }

        /// <summary>キー入力の入口（テストからも呼べる）。</summary>
        public void StepVolume(bool up)
        {
            if (up ? Volume.Up() : Volume.Down()) Refresh();
        }

        private void Refresh()
        {
            var level = Volume.Level;
            source.volume = MasterVolume * Volume.Gain;
            if (level == shownLevel) return;
            shownLevel = level;
            volumeText.SetText("{0:0}", level);
            speakerImage.color = Volume.IsMuted ? IconMuted : IconOn;
        }

        private static AudioClip CreateClip()
        {
            var notes = new List<float>();
            foreach (var chord in Chords)
                foreach (var index in ArpeggioPattern) notes.Add(chord[index] * (index == 2 ? 2f : 1f));
            return ToneSynth.CreateLoop("Bgm", notes.ToArray(), NoteSeconds, ChordBass, BassSeconds, ClipAmplitude);
        }

        // 小さなスピーカー: 左に四角、そこから右へ広がる台形（三角 2 つ）
        private static bool Speaker(float x, float y)
        {
            if (x >= -0.8f && x <= -0.35f && y >= -0.3f && y <= 0.3f) return true;
            return ShapeSprites.InTriangle(x, y, -0.35f, 0.3f, 0.45f, 0.85f, 0.45f, -0.85f)
                || ShapeSprites.InTriangle(x, y, -0.35f, 0.3f, -0.35f, -0.3f, 0.45f, -0.85f);
        }
    }
}
