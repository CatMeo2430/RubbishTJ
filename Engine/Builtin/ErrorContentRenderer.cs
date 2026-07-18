using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Taiji.Engine.Render;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Builtin
{
    internal sealed class ErrorContentRenderer : IContentRenderer
    {
        public string Id => "error";
        public string DisplayName => "Error";
        public int Priority => 0;

        public bool CanHandle(RenderRequest request)
        {
            return request != null && request.Role == RenderRole.Error;
        }

        public IList<Block> RenderBody(RenderRequest request)
        {
            var text = request != null ? (request.Content ?? "") : "";
            return new List<Block>
            {
                new Paragraph(new Run(text) { Foreground = DraculaTheme.RedBrush })
            };
        }
    }
}
