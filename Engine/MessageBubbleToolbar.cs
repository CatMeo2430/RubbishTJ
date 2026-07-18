using System;
using System.Windows;
using System.Windows.Controls;
using Taiji.Engine.Render;

namespace Taiji.Engine
{
    /// <summary>AI 消息气泡工具栏：复制原文。</summary>
    public sealed class MessageBubbleToolbar : StackPanel
    {
        private readonly Func<string> _getSourceText;

        public MessageBubbleToolbar(Func<string> getSourceText)
        {
            _getSourceText = getSourceText ?? (() => "");
            Orientation = Orientation.Horizontal;
            HorizontalAlignment = HorizontalAlignment.Right;
            Margin = new Thickness(0, 0, 0, 6);

            Children.Add(RenderToolbar.CreateButton("复制", OnCopyClick));
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var text = _getSourceText() ?? "";
            if (text.Length == 0) return;
            Clipboard.SetText(text);
        }
    }
}
