using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Text;

namespace UnitySlotTower.EditorTools
{
    /// <summary>GameObject 階層の文字表示が「数字・記号だけ」の規則（FR-003）を守っているか調べる。</summary>
    public static class TextRuleScanner
    {
        public static List<string> FindViolations(GameObject root, TMP_FontAsset requiredFont)
        {
            var violations = new List<string>();

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var bad = AllowedText.IndexOfDisallowed(text.text);
                if (bad >= 0)
                    violations.Add($"{PathOf(text.transform)}: 許可されない文字 '{text.text[bad]}'（\"{text.text}\"）");
                if (text.font != requiredFont)
                    violations.Add($"{PathOf(text.transform)}: フォントが数字専用ではありません");
            }

            foreach (var legacy in root.GetComponentsInChildren<Text>(true))
                violations.Add($"{PathOf(legacy.transform)}: 旧 UI Text は使えません（TextMeshPro を使う）");

            return violations;
        }

        private static string PathOf(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
    }
}
