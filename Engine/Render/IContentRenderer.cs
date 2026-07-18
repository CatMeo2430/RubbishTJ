using System.Collections.Generic;
using System.Windows.Documents;

namespace Taiji.Engine.Render
{
    /// <summary>正文渲染模块（不含外层气泡）。</summary>
    public interface IContentRenderer
    {
        string Id { get; }
        string DisplayName { get; }
        int Priority { get; }
        bool CanHandle(RenderRequest request);
        IList<Block> RenderBody(RenderRequest request);
    }
}
