using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Code
{
    /// <summary>把 AvalonEdit 默认高亮刷成 One Dark 低饱和配色。</summary>
    internal static class OneDarkHighlighting
    {
        public static IHighlightingDefinition Apply(IHighlightingDefinition source)
        {
            if (source == null) return null;
            try
            {
                foreach (var color in source.NamedHighlightingColors)
                {
                    if (color == null) continue;
                    var od = MapColor(color.Name);
                    color.Foreground = new SimpleHighlightingBrush(od);
                    // 去掉刺眼粗体/背景
                    color.FontWeight = null;
                    color.Background = null;
                }
            }
            catch (Exception)
            {
                // 保持原高亮
            }
            return source;
        }

        private static Color MapColor(string name)
        {
            var n = (name ?? "").ToLowerInvariant();

            if (n.Contains("comment") || n.Contains("doc"))
                return DraculaTheme.Comment;
            if (n.Contains("string") || n.Contains("char") || n.Contains("xmlattribut"))
                return DraculaTheme.Green;
            if (n.Contains("keyword") || n.Contains("attributename") || n.Contains("preprocessor"))
                return DraculaTheme.Purple;
            if (n.Contains("number") || n.Contains("digit") || n.Contains("const"))
                return DraculaTheme.Orange;
            if (n.Contains("method") || n.Contains("function") || n.Contains("accessor"))
                return DraculaTheme.Blue;
            if (n.Contains("type") || n.Contains("class") || n.Contains("struct") || n.Contains("enum")
                || n.Contains("interface") || n.Contains("value"))
                return DraculaTheme.Yellow;
            if (n.Contains("tag") || n.Contains("element") || n.Contains("entity"))
                return DraculaTheme.Red;
            if (n.Contains("punctuation") || n.Contains("operator") || n.Contains("selector"))
                return DraculaTheme.Cyan;
            if (n.Contains("url") || n.Contains("uri") || n.Contains("link"))
                return DraculaTheme.Blue;
            if (n.Contains("regex") || n.Contains("escape"))
                return DraculaTheme.Orange;
            if (n.Contains("error") || n.Contains("warning") || n.Contains("invalid"))
                return DraculaTheme.Red;

            return DraculaTheme.Foreground;
        }
    }
}
