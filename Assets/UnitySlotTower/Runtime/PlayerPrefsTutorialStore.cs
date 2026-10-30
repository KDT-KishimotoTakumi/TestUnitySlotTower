using UnityEngine;
using UnitySlotTower.Core.Tutorial;

namespace UnitySlotTower.Runtime
{
    /// <summary>チュートリアルを見終えたかを PlayerPrefs に保存する（FR-004「初回のみ」）。一度保存したら以後出さない。</summary>
    public sealed class PlayerPrefsTutorialStore : ITutorialStore
    {
        public const string SeenKey = "UnitySlotTower.TutorialSeen";
        private const int SeenValue = 1;

        public bool IsSeen => PlayerPrefs.GetInt(SeenKey, 0) == SeenValue;

        public void MarkSeen()
        {
            PlayerPrefs.SetInt(SeenKey, SeenValue);
            PlayerPrefs.Save();
        }
    }
}
