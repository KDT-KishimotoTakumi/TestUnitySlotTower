namespace UnitySlotTower.Core.Text
{
    /// <summary>文字を使わない UI（FR-003）で表示してよい文字。数字と記号だけ。</summary>
    public static class AllowedText
    {
        public const string Characters = "0123456789+-−×/% ";

        /// <summary>許可されていない最初の文字の位置。なければ -1。</summary>
        public static int IndexOfDisallowed(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            for (var i = 0; i < text.Length; i++)
                if (Characters.IndexOf(text[i]) < 0) return i;
            return -1;
        }
    }
}
