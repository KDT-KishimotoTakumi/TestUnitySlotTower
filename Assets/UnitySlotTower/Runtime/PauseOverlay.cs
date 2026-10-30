using UnityEngine;

namespace UnitySlotTower.Runtime
{
    /// <summary>
    /// 一時停止中に戦闘領域へ重ねる表示（T307、FR-012）。暗幕と停止の印（縦の 2 本線）の子オブジェクトを、停止中だけ出す。
    /// 文字は使わない（FR-003。許可文字は数字と記号だけで「PAUSE」は出せない）ので、印は Image で描く。
    /// 停止中に見せるキャラクター詳細・スロットの説明の本体は T308 / T404。読み出しの入口は
    /// <see cref="BattleRunner.Units"/>（味方のステータス）と <see cref="SlotCabinetView.Payouts"/>（役・配当）。
    /// </summary>
    public sealed class PauseOverlay : MonoBehaviour
    {
        [SerializeField] private BattleRunner runner;
        [Tooltip("停止中だけ有効にする子オブジェクト（暗幕と印）")]
        [SerializeField] private GameObject content;

        private int shown = -1;

        private void Update()
        {
            if (runner == null || content == null) return;
            // 結果画面が出ている間は一時停止の印を重ねない
            var paused = runner.IsPaused && !runner.IsEnded ? 1 : 0;
            if (paused == shown) return;
            shown = paused;
            content.SetActive(paused == 1);
        }
    }
}
