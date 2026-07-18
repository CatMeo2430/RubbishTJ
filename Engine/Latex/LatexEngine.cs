using System;

namespace Taiji.Engine.Latex
{
    /// <summary>
    /// LaTeX 引擎入口（对齐 Taiji.Engine 中对 MathViewFactory 的用法模式）。
    /// GUI 整合时通过 <see cref="Default"/> 获取 <see cref="ILatexRenderEngine"/>。
    /// </summary>
    public static class LatexEngine
    {
        private static ILatexRenderEngine _default = new RatexLatexEngine();

        public static ILatexRenderEngine Default
        {
            get { return _default; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                _default = value;
            }
        }
    }
}
