using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Taiji.Engine.Render;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Builtin
{
    internal sealed class SystemContentRenderer : IContentRenderer
    {
        public string Id { get { return "system"; } }
        public string DisplayName { get { return "System"; } }
        public int Priority { get { return 0; } }

        public bool CanHandle(RenderRequest request)
        {
            return request != null && request.Role == RenderRole.System;
        }

        public IList<Block> RenderBody(RenderRequest request)
        {
            var text = request != null ? (request.Content ?? "") : "";
            return new List<Block>
            {
                new Paragraph(new Run(text)
                {
                    Foreground = DraculaTheme.ForegroundBrush,
                    FontStyle = FontStyles.Italic,
                    FontSize = 12
                })
                {
                    Margin = new Thickness(4, 6, 4, 6),
                    Padding = new Thickness(6, 2, 6, 2)
                }
            };
        }
    }
}
