namespace UnitySlotTower.Core.Enemies
{
    /// <summary>waves.csv の1行（1体の出現予定）。</summary>
    public sealed class WaveEntry
    {
        public int Wave { get; }
        /// <summary>ウェーブの戦闘開始からの秒数。</summary>
        public float Time { get; }
        public EnemyDef Enemy { get; }
        /// <summary>出現口の記号（"S1" など）。</summary>
        public string Spawn { get; }
        public string PathId { get; }
        public bool IsBoss { get; }

        public WaveEntry(int wave, float time, EnemyDef enemy, string spawn, string pathId, bool isBoss)
        {
            Wave = wave;
            Time = time;
            Enemy = enemy;
            Spawn = spawn;
            PathId = pathId;
            IsBoss = isBoss;
        }
    }
}
