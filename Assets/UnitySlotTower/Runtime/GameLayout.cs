namespace UnitySlotTower.Runtime
{
    /// <summary>横長 16:9 画面の区画割り（左に戦闘領域と操作バー、右にスロット筐体）。</summary>
    public static class GameLayout
    {
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;
        public const float BattleWidthFraction = 0.6f;
        public const float OperationBarHeightFraction = 0.1f;
        public const float BattleCameraPitchDegrees = 55f;
        public const int DefaultWindowWidth = 1280;
        public const int DefaultWindowHeight = 720;
    }
}
