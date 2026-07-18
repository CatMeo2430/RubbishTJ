using System;
using System.Windows;
using System.Windows.Controls;
using Taiji.Engine.Code;
using Taiji.Engine.Theme;
using Taiji.Engine.Render;

namespace Taiji.Engine.Latex
{
    /// <summary>块级 LaTeX：公式视图 + 复制 / PNG / SVG 工具栏。</summary>
    public sealed class LatexBlockView : Border
    {
        private readonly RatexFormulaView _formula;

        public LatexBlockView(string latex, double fontSizeEm)
        {
            Latex = latex ?? "";
            FontSizeEm = fontSizeEm > 0 ? fontSizeEm : 22;

            Background = DraculaTheme.CurrentLineBrush;
            BorderBrush = DraculaTheme.SelectionBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(4);
            Padding = new Thickness(8, 6, 8, 8);
            Margin = new Thickness(0, 4, 0, 8);
            SnapsToDevicePixels = true;

            var root = new StackPanel();

            var toolbar = RenderToolbar.CreateBar(
                RenderToolbar.CreateButton("复制", OnCopyClick),
                RenderToolbar.CreateButton("PNG", OnExportPngClick),
                RenderToolbar.CreateButton("SVG", OnExportSvgClick));
            root.Children.Add(toolbar);

            _formula = LatexViewFactory.CreateFormulaView(Latex, true, FontSizeEm);
            _formula.HorizontalAlignment = HorizontalAlignment.Center;
            root.Children.Add(_formula);

            Child = root;
            PreviewMouseWheel += CodeBlockViewFactory.BubbleWheelToChat;
        }

        public string Latex { get; private set; }

        public double FontSizeEm { get; private set; }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Latex)) return;
            LatexInteractions.CopySourceToClipboard(Latex);
        }

        private void OnExportPngClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Latex)) return;
            try
            {
                LatexInteractions.PromptExportPng(Latex, true, (float)FontSizeEm, Window.GetWindow(this));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "导出 PNG 失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnExportSvgClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Latex)) return;
            try
            {
                LatexInteractions.PromptExportSvg(Latex, true, (float)FontSizeEm, Window.GetWindow(this));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "导出 SVG 失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
