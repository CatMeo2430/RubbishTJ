using System.Windows;
using System.Windows.Controls;
using Taiji.Engine.Render;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Code
{
    /// <summary>代码块：编辑器 + 复制工具栏。</summary>
    public sealed class CodeBlockView : Border
    {
        public CodeBlockView(string code, string language)
        {
            Code = code ?? "";
            CodeLanguage = language ?? "";

            Background = DraculaTheme.CurrentLineBrush;
            BorderBrush = DraculaTheme.SelectionBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(4);
            Padding = new Thickness(8, 6, 8, 8);
            Margin = new Thickness(0, 4, 0, 8);
            HorizontalAlignment = HorizontalAlignment.Stretch;
            SnapsToDevicePixels = true;

            var root = new StackPanel();

            root.Children.Add(RenderToolbar.CreateBar(
                RenderToolbar.CreateButton("复制", OnCopyClick)));

            var editor = new CodeBlockEditor(Code, CodeLanguage)
            {
                Background = DraculaTheme.CurrentLineBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            root.Children.Add(editor);

            Child = root;
            PreviewMouseWheel += CodeBlockViewFactory.BubbleWheelToChat;
        }

        public string Code { get; private set; }

        public string CodeLanguage { get; private set; }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Code)) return;
            CodeInteractions.CopySourceToClipboard(Code);
        }
    }
}
