using System;
using System.Collections.Generic;
using System.Windows.Documents;
using Taiji.Engine.Render;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Builtin
{
    public sealed class PlainContentRenderer : IContentRenderer
    {
        public string Id => "plain";
        public string DisplayName => "Plain Text";
        public int Priority => 100;

        public bool CanHandle(RenderRequest request)
        {
            if (request == null) return false;
            if (request.Role == RenderRole.System || request.Role == RenderRole.Error)
                return false;
            return string.Equals(request.LanguageHint, "plain", StringComparison.OrdinalIgnoreCase);
        }

        public IList<Block> RenderBody(RenderRequest request)
        {
            var text = request != null ? (request.Content ?? "") : "";
            return new List<Block>
            {
                new Paragraph(new Run(text) { Foreground = DraculaTheme.ForegroundBrush })
            };
        }
    }
}
