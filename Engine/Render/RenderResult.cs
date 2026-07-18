using System.Windows.Documents;

namespace Taiji.Engine.Render
{
    public sealed class RenderResult
    {
        public RenderResult(string rendererId, Block root)
        {
            RendererId = rendererId ?? "";
            Root = root;
        }

        public string RendererId { get; private set; }
        public Block Root { get; private set; }
    }
}
