using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Render
{
    /// <summary>渲染块右上角工具栏按钮（复制 / 导出等）。</summary>
    internal static class RenderToolbar
    {
        public static StackPanel CreateBar(params Button[] buttons)
        {
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 6)
            };
            if (buttons != null)
            {
                foreach (var btn in buttons)
                {
                    if (btn != null)
                        toolbar.Children.Add(btn);
                }
            }
            return toolbar;
        }

        public static Button CreateButton(string label, RoutedEventHandler click)
        {
            var btn = new Button
            {
                Content = label,
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(10, 2, 10, 2),
                MinWidth = 44,
                Height = 24,
                FontSize = 11,
                Background = DraculaTheme.SelectionBrush,
                Foreground = DraculaTheme.ForegroundBrush,
                BorderBrush = DraculaTheme.CommentBrush,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btn.Click += click;
            return btn;
        }
    }
}
