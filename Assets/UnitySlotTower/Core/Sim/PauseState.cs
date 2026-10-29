namespace UnitySlotTower.Core.Sim
{
    /// <summary>
    /// 一時停止の共有状態（FR-012）。戦闘・召喚・強化・ウェーブ進行・スロットが同じ 1 つを参照する。
    /// 停止中は時間（Tick / Step）が進まず、配置・撤退・強化・スロット操作は Paused の結果で拒否される。閲覧（表の読み出し）は止めない。
    /// </summary>
    public sealed class PauseState
    {
        public bool IsPaused { get; private set; }

        public void SetPaused(bool paused) => IsPaused = paused;

        /// <summary>停止と再開を切り替え、切り替え後の IsPaused を返す。</summary>
        public bool Toggle()
        {
            IsPaused = !IsPaused;
            return IsPaused;
        }
    }
}
