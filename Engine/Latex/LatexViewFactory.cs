using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Taiji.Engine.Latex
{
    /// <summary>创建可嵌入 FlowDocument 的 LaTeX 视图。</summary>
    public static class LatexViewFactory
    {
        public static RatexFormulaView CreateFormulaView(string latex, bool displayMode, double fontSizeEm)
        {
            return new RatexFormulaView
            {
                Latex = latex,
                DisplayMode = displayMode,
                FontSizeEm = fontSizeEm
            };
        }

        public static FrameworkElement CreateInlineView(string latex, double fontSizeEm)
        {
            return CreateFormulaView(latex, false, fontSizeEm > 0 ? fontSizeEm : 18);
        }

        public static FrameworkElement CreateBlockView(string latex, double fontSizeEm)
        {
            var view = CreateFormulaView(latex, true, fontSizeEm > 0 ? fontSizeEm : 22);
            view.HorizontalAlignment = HorizontalAlignment.Center;
            return view;
        }

        public static Inline CreateInline(string latex)
        {
            return CreateInline(latex, 18);
        }

        public static Inline CreateInline(string latex, double fontSizeEm)
        {
            var view = CreateInlineView(latex, fontSizeEm);
            return new InlineUIContainer(view)
            {
                BaselineAlignment = BaselineAlignment.TextTop
            };
        }

        public static Block CreateBlock(string latex)
        {
            return CreateBlock(latex, 22);
        }

        public static Block CreateBlock(string latex, double fontSizeEm)
        {
            var view = new LatexBlockView(latex, fontSizeEm);
            return new BlockUIContainer(view)
            {
                Margin = new Thickness(0, 2, 0, 2)
            };
        }
    }
}
