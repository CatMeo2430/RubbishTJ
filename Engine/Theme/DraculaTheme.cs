using System.Windows;
using System.Windows.Media;

namespace Taiji.Engine.Theme
{
    /// <summary>Atom One Dark 色板（聊天渲染）。</summary>
    public static class DraculaTheme
    {
        public static readonly Color Background = Color.FromRgb(0x28, 0x2C, 0x34);
        public static readonly Color CurrentLine = Color.FromRgb(0x2C, 0x31, 0x3A);
        public static readonly Color Selection = Color.FromRgb(0x3E, 0x44, 0x51);
        public static readonly Color Foreground = Color.FromRgb(0xFF, 0xFF, 0xFF);
        public static readonly Color Comment = Color.FromRgb(0x5C, 0x63, 0x70);
        public static readonly Color Cyan = Color.FromRgb(0x56, 0xB6, 0xC2);
        public static readonly Color Green = Color.FromRgb(0x98, 0xC3, 0x79);
        public static readonly Color Orange = Color.FromRgb(0xD1, 0x9A, 0x66);
        public static readonly Color Pink = Color.FromRgb(0xC6, 0x78, 0xDD);   // purple
        public static readonly Color Purple = Color.FromRgb(0xC6, 0x78, 0xDD);
        public static readonly Color Red = Color.FromRgb(0xE0, 0x6C, 0x75);
        public static readonly Color Yellow = Color.FromRgb(0xE5, 0xC0, 0x7B);
        public static readonly Color Blue = Color.FromRgb(0x61, 0xAF, 0xEF);

        public static readonly Brush BackgroundBrush = Freeze(new SolidColorBrush(Background));
        public static readonly Brush CurrentLineBrush = Freeze(new SolidColorBrush(CurrentLine));
        public static readonly Brush SelectionBrush = Freeze(new SolidColorBrush(Selection));
        public static readonly Brush ForegroundBrush = Freeze(new SolidColorBrush(Foreground));
        public static readonly Brush CommentBrush = Freeze(new SolidColorBrush(Comment));
        public static readonly Brush CyanBrush = Freeze(new SolidColorBrush(Cyan));
        public static readonly Brush GreenBrush = Freeze(new SolidColorBrush(Green));
        public static readonly Brush OrangeBrush = Freeze(new SolidColorBrush(Orange));
        public static readonly Brush PinkBrush = Freeze(new SolidColorBrush(Pink));
        public static readonly Brush PurpleBrush = Freeze(new SolidColorBrush(Purple));
        public static readonly Brush RedBrush = Freeze(new SolidColorBrush(Red));
        public static readonly Brush YellowBrush = Freeze(new SolidColorBrush(Yellow));
        public static readonly Brush BlueBrush = Freeze(new SolidColorBrush(Blue));

        public static readonly FontFamily UiFont =
            new FontFamily("Cascadia Code, Consolas, Microsoft YaHei UI");
        public static readonly FontFamily MonoFont =
            new FontFamily("Cascadia Code, Consolas");

        private static Brush Freeze(SolidColorBrush b)
        {
            if (b.CanFreeze) b.Freeze();
            return b;
        }
    }
}
