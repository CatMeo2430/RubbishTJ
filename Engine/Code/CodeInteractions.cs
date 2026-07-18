using System.Windows;

namespace Taiji.Engine.Code
{
    /// <summary>代码块复制。</summary>
    public static class CodeInteractions
    {
        public static void CopySourceToClipboard(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            Clipboard.SetText(code);
        }
    }
}
