using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Text;
using UnitySlotTower.Core.Tutorial;
using UnitySlotTower.Runtime;

namespace UnitySlotTower.Tests
{
    /// <summary>
    /// T406（FR-003）: 実行時に出る文字（SetText で組み立てる数字、実行時に作る一覧表、チュートリアル、結果、スロットの各状態、Title の選択画面）が
    /// 数字・記号だけで、数字専用フォントで描かれていること。静的な文字は EditMode の TextRuleTests が調べる。
    /// </summary>
    public class TextRulePlayModeTests : InputTestFixture
    {
        private const string LogoName = "Logo";
        private const int MaxStartupFrames = 60;
        private const int LethalDamage = 1000;
        private const int PlentyOfCoins = 100000;
        private const int MaxSlotGames = 3000;

        private BattleRunner runner;
        private SlotCabinetView cabinet;
        private PlacementInput input;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            new PlayerPrefsTutorialStore().MarkSeen();   // 以後のテストのために「見た」へ戻す
            base.TearDown();
        }

        private IEnumerator LoadGame()
        {
            new PlayerPrefsTutorialStore().MarkSeen();
            yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            for (var i = 0; i < MaxStartupFrames && (input == null || input.Controller == null); i++)
            {
                yield return null;
                runner = Object.FindFirstObjectByType<BattleRunner>();
                cabinet = Object.FindFirstObjectByType<SlotCabinetView>();
                input = Object.FindFirstObjectByType<PlacementInput>();
            }
            Assert.IsNotNull(input.Controller, "PlacementInput が戦闘につながっていません");
            yield return null;
        }

        /// <summary>画面に出ている TMP 文字を、実際に描かれる文字（SetText の書式を展開したあと）で調べる。</summary>
        private static List<string> FindViolations(string state, string exceptName = null)
        {
            var violations = new List<string>();
            var requiredFont = TMP_Settings.defaultFontAsset;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
            {
                if (exceptName != null && text.name == exceptName) continue;
                text.ForceMeshUpdate();
                var info = text.textInfo;
                for (var i = 0; i < info.characterCount; i++)
                {
                    var c = info.characterInfo[i].character;
                    if (AllowedText.Characters.IndexOf(c) >= 0) continue;
                    violations.Add($"[{state}] {text.name}: 許可されない文字 '{c}'");
                    break;
                }
                if (text.font != requiredFont) violations.Add($"[{state}] {text.name}: フォントが数字専用ではありません");
            }
            foreach (var legacy in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
                violations.Add($"[{state}] {legacy.name}: 旧 UI Text は使えません");
            return violations;
        }

        private static void AssertClean(string state, string exceptName = null)
        {
            var violations = FindViolations(state, exceptName);
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        [UnityTest]
        public IEnumerator GameScreens_NormalPausedHelpAndResult_HaveOnlyDigitsAndSymbols()
        {
            yield return LoadGame();
            AssertClean("通常");

            runner.SetPaused(true);
            yield return null;
            yield return null;
            AssertClean("一時停止");

            var help = Object.FindFirstObjectByType<HelpView>();
            Assert.IsTrue(help.Toggle(), "一時停止中は「?」パネルを開ける");
            yield return null;
            yield return null;
            Assert.IsTrue(help.IsOpen);
            AssertClean("「?」パネル");
            help.Toggle();
            runner.SetPaused(false);
            yield return null;

            runner.Life.TakeDamage(LethalDamage);
            yield return null;
            yield return null;
            Assert.IsTrue(Object.FindFirstObjectByType<ResultView>().IsShown);
            AssertClean("結果（ゲームオーバー）");
        }

        [UnityTest]
        public IEnumerator SlotStates_NormalKakuhenAndBonus_HaveOnlyDigitsAndSymbols()
        {
            yield return LoadGame();
            cabinet.SlotWallet.Add(PlentyOfCoins);
            cabinet.Wallet.Add(PlentyOfCoins);

            var seen = new HashSet<SlotState>();
            for (var game = 0; game < MaxSlotGames && seen.Count < 3; game++)
            {
                cabinet.Machine.PlayInstant();
                if (seen.Add(cabinet.Machine.State))
                {
                    yield return null;
                    yield return null;
                    AssertClean($"スロット {cabinet.Machine.State}");
                }
                else if (game % 10 == 0)
                {
                    yield return null;
                }
            }
            Assert.IsTrue(seen.Contains(SlotState.Normal));
            Assert.Greater(seen.Count, 1, $"通常以外の状態に入れませんでした（{MaxSlotGames} ゲーム）");
        }

        [UnityTest]
        public IEnumerator Tutorial_EveryStep_HasOnlyDigitsAndSymbols()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsTutorialStore.SeenKey);
            yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            TutorialView view = null;
            for (var i = 0; i < MaxStartupFrames && (view == null || !view.IsShown); i++)
            {
                yield return null;
                view = Object.FindFirstObjectByType<TutorialView>();
            }
            Assert.IsTrue(view.IsShown, "初回はチュートリアルが出る");

            for (var step = 0; step < TutorialFlow.StepCount; step++)
            {
                yield return null;
                AssertClean($"チュートリアル {step}");
                view.Advance();
            }
        }

        [UnityTest]
        public IEnumerator TitleAndSelect_ExceptLogo_HaveOnlyDigitsAndSymbols()
        {
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return null;
            var title = Object.FindFirstObjectByType<TitleView>();
            Assert.IsNotNull(title, "TitleView がありません");
            AssertClean("Title", LogoName);

            title.ShowSelect();
            yield return null;
            yield return null;
            Assert.IsTrue(title.IsSelecting);
            AssertClean("難易度選択", LogoName);
        }
    }
}
