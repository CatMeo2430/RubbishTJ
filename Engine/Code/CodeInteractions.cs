using System;
using System.Windows;
using Taiji.Engine.Utils;

namespace Taiji.Engine.Code
{
    /// <summary>代码块复制与 PNG 导出。</summary>
    public static class CodeInteractions
    {
        public static void CopySourceToClipboard(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            Clipboard.SetText(code);
        }

        public static string PromptExportPng(FrameworkElement visual, string language, Window owner)
        {
            if (visual == null) return null;
            var name = string.IsNullOrWhiteSpace(language) ? "code" : language.Trim();
            try
            {
                return VisualExportHelper.PromptSaveElementPng(visual, $"{name}.png", owner);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "导出 PNG 失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
        }
    }
}
