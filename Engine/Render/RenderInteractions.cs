using System.Windows;
using System.Windows.Media;
using Taiji.Engine.Code;
using Taiji.Engine.Latex;

namespace Taiji.Engine.Render
{
    /// <summary>
    /// Engine 对外暴露的交互能力：公式复制/导出、从可视树查找公式源等。
    /// 常规用法下，代码块与公式已内置上下文菜单，GUI 无需额外处理。
    /// </summary>
    public static class RenderInteractions
    {
        public static bool TryFindFormulaView(DependencyObject source, out RatexFormulaView view)
        {
            view = null;
            var walk = source;
            while (walk != null)
            {
                view = walk as RatexFormulaView;
                if (view != null) return true;
                walk = VisualTreeHelper.GetParent(walk) ?? LogicalTreeHelper.GetParent(walk);
            }
            return false;
        }

        public static bool TryGetLatexSource(DependencyObject source, out string latex, out bool displayMode)
        {
            latex = null;
            displayMode = true;
            RatexFormulaView view;
            if (!TryFindFormulaView(source, out view) || view == null)
                return false;
            latex = view.Latex;
            displayMode = view.DisplayMode;
            return !string.IsNullOrEmpty(latex);
        }

        public static void CopyLatexSource(string latex)
        {
            LatexInteractions.CopySourceToClipboard(latex);
        }

        public static string PromptExportLatexPng(string latex, bool displayMode, float fontSizeEm, Window owner)
        {
            return LatexInteractions.PromptExportPng(latex, displayMode, fontSizeEm, owner);
        }

        public static string PromptExportLatexSvg(string latex, bool displayMode, float fontSizeEm, Window owner)
        {
            return LatexInteractions.PromptExportSvg(latex, displayMode, fontSizeEm, owner);
        }

        public static bool TryFindCodeEditor(DependencyObject source, out CodeBlockEditor editor)
        {
            editor = null;
            var walk = source;
            while (walk != null)
            {
                editor = walk as CodeBlockEditor;
                if (editor != null) return true;
                walk = VisualTreeHelper.GetParent(walk) ?? LogicalTreeHelper.GetParent(walk);
            }
            return false;
        }
    }
}
