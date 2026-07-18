using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Taiji.Engine.Latex
{
    /// <summary>LaTeX 公式的复制与导出（供 GUI 或控件上下文菜单调用）。</summary>
    public static class LatexInteractions
    {
        public static void CopySourceToClipboard(string latex)
        {
            if (string.IsNullOrEmpty(latex)) return;
            Clipboard.SetText(latex);
        }

        public static LatexExportResult ExportPng(string latex, bool displayMode, float fontSizeEm)
        {
            var opts = LatexRenderOptions.ForPngExport(
                displayMode, fontSizeEm, 10f, 2f, 1f, 1f, 1f, 1f);
            return LatexEngine.Default.ExportPng(latex, opts);
        }

        public static LatexExportResult ExportSvg(string latex, bool displayMode, float fontSizeEm)
        {
            var opts = LatexRenderOptions.ForSvgExport(displayMode, fontSizeEm, 10f);
            return LatexEngine.Default.ExportSvg(latex, opts);
        }

        public static bool SavePngToFile(string latex, string path, bool displayMode, float fontSizeEm, out string error)
        {
            error = null;
            var result = ExportPng(latex, displayMode, fontSizeEm);
            if (!result.Success)
            {
                error = result.Error;
                return false;
            }
            try
            {
                RatexNative.SavePng(result.PngData, path);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool SaveSvgToFile(string latex, string path, bool displayMode, float fontSizeEm, out string error)
        {
            error = null;
            var result = ExportSvg(latex, displayMode, fontSizeEm);
            if (!result.Success)
            {
                error = result.Error;
                return false;
            }
            try
            {
                RatexNative.SaveSvg(result.SvgText, path);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static string PromptExportPng(string latex, bool displayMode, float fontSizeEm, Window owner)
        {
            return PromptExportFile(
                latex,
                owner,
                "PNG 图片|*.png",
                ".png",
                path =>
                {
                    string err;
                    return SavePngToFile(latex, path, displayMode, fontSizeEm, out err)
                        ? null
                        : (err ?? "导出失败");
                });
        }

        public static string PromptExportSvg(string latex, bool displayMode, float fontSizeEm, Window owner)
        {
            return PromptExportFile(
                latex,
                owner,
                "SVG 矢量图|*.svg",
                ".svg",
                path =>
                {
                    string err;
                    return SaveSvgToFile(latex, path, displayMode, fontSizeEm, out err)
                        ? null
                        : (err ?? "导出失败");
                });
        }

        private static string PromptExportFile(
            string latex,
            Window owner,
            string filter,
            string extension,
            Func<string, string> saver)
        {
            if (string.IsNullOrWhiteSpace(latex)) return null;

            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = "formula" + extension
            };
            if (dialog.ShowDialog(owner) != true)
                return null;

            var err = saver(dialog.FileName);
            if (err != null)
                throw new InvalidOperationException(err);

            return dialog.FileName;
        }
    }
}
