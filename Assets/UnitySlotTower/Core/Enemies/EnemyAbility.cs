namespace UnitySlotTower.Core.Enemies
{
    /// <summary>敵の固有行動（enemies.csv の ability。ボスだけが持つ、T306）。</summary>
    public enum EnemyAbility
    {
        None,
        /// <summary>攻撃のたびに、攻撃した味方の隣接マスの味方にも攻撃力の一部（ability_param の割合）でダメージを与える。</summary>
        AreaAttack,
        /// <summary>一定間隔で、いまの位置から雑魚を呼び出す。ability_param は「間隔(秒);体数;敵ID」。</summary>
        SummonMinions,
    }
}
