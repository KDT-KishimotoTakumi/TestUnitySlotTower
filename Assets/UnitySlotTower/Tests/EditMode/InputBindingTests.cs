using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace UnitySlotTower.Tests
{
    /// <summary>plan 3章の操作割り当て（FR-002）。</summary>
    public class InputBindingTests
    {
        private const string AssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string MapName = "Game";
        private const string Scheme = "Keyboard&Mouse";

        // アクション名 → そのアクションに割り当てるべきパス（複合バインドは構成要素のパス）
        private static readonly Dictionary<string, string[]> Expected = BuildExpected();

        private static Dictionary<string, string[]> BuildExpected()
        {
            var expected = new Dictionary<string, string[]>
            {
                { "Point", new[] { "<Mouse>/position" } },
                { "Press", new[] { "<Mouse>/leftButton" } },
                { "Cancel", new[] { "<Mouse>/rightButton", "<Keyboard>/escape" } },
                { "Confirm", new[] { "<Keyboard>/enter", "<Keyboard>/numpadEnter" } },
                { "CursorMove", new[] { "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow" } },
                { "CycleSelection", new[] { "<Keyboard>/tab" } },
                { "Retreat", new[] { "<Keyboard>/x" } },
                { "Help", new[] { "<Keyboard>/h", "<Keyboard>/slash" } },
                { "SlotLever", new[] { "<Keyboard>/space" } },
                { "Pause", new[] { "<Keyboard>/p", "<Keyboard>/escape" } },
                { "MenuConfirm", new[] { "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space" } },
                { "MenuBack", new[] { "<Keyboard>/escape" } },
                { "MenuDigit", new[] { "<Keyboard>/1", "<Keyboard>/numpad1", "<Keyboard>/2", "<Keyboard>/numpad2", "<Keyboard>/3", "<Keyboard>/numpad3", "<Keyboard>/4", "<Keyboard>/numpad4", "<Keyboard>/5", "<Keyboard>/numpad5", "<Keyboard>/6", "<Keyboard>/numpad6" } },
                { "BgmVolumeUp", new[] { "<Keyboard>/equals", "<Keyboard>/numpadPlus" } },
                { "BgmVolumeDown", new[] { "<Keyboard>/minus", "<Keyboard>/numpadMinus" } },
            };
            for (var n = 1; n <= 5; n++)
            {
                expected[$"SummonSelect{n}"] = new[] { $"<Keyboard>/{n}" };
                expected[$"Upgrade{n}"] = new[] { "<Keyboard>/shift", $"<Keyboard>/{n}" };
            }
            return expected;
        }

        private static InputActionMap LoadMap()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.IsNotNull(asset, "InputSystem_Actions.inputactions がありません");
            var map = asset.FindActionMap(MapName);
            Assert.IsNotNull(map, $"アクションマップ '{MapName}' がありません");
            return map;
        }

        [Test]
        public void GameMap_HasExactlyThePlannedActions()
        {
            var actual = LoadMap().actions.Select(a => a.name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(Expected.Keys.OrderBy(n => n).ToArray(), actual);
        }

        [Test]
        public void GameMap_BindsPlannedPathsToEachAction()
        {
            var map = LoadMap();
            foreach (var pair in Expected)
            {
                var paths = map.FindAction(pair.Key).bindings.Where(b => !b.isComposite).Select(b => b.path).ToArray();
                CollectionAssert.AreEquivalent(pair.Value, paths, $"{pair.Key} のバインドが計画と違います");
            }
        }

        [Test]
        public void GameMap_SupportsMouseAndKeyboard()
        {
            var map = LoadMap();
            var all = map.actions.SelectMany(a => a.bindings).Where(b => !b.isComposite).Select(b => b.path).ToArray();
            Assert.IsTrue(all.Any(p => p.StartsWith("<Mouse>/")), "マウスの割り当てがありません");
            Assert.IsTrue(all.Any(p => p.StartsWith("<Keyboard>/")), "キーボードの割り当てがありません");
            foreach (var binding in map.actions.SelectMany(a => a.bindings).Where(b => !b.isComposite))
                StringAssert.Contains(Scheme, binding.groups, $"{binding.action}:{binding.path} がスキームに属していません");
        }

        [Test]
        public void GameMap_UpgradeIsShiftPlusNumber()
        {
            var map = LoadMap();
            for (var n = 1; n <= 5; n++)
            {
                var bindings = map.FindAction($"Upgrade{n}").bindings.ToArray();
                Assert.AreEqual("ButtonWithOneModifier", bindings[0].path);
                Assert.AreEqual("<Keyboard>/shift", bindings.Single(b => b.name == "Modifier").path);
                Assert.AreEqual($"<Keyboard>/{n}", bindings.Single(b => b.name == "Button").path);
            }
        }

        [Test]
        public void GameMap_AllPathsResolveToRealControls()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>("T008Keyboard");
            var mouse = InputSystem.AddDevice<Mouse>("T008Mouse");
            try
            {
                foreach (var binding in LoadMap().actions.SelectMany(a => a.bindings).Where(b => !b.isComposite))
                {
                    var device = binding.path.StartsWith("<Mouse>") ? (InputDevice)mouse : keyboard;
                    Assert.IsNotNull(InputControlPath.TryFindControl(device, binding.path),
                        $"{binding.action}: パス {binding.path} に該当するコントロールがありません");
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }
        }
    }
}
