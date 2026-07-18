namespace Taiji.Engine.Latex
{
    /// <summary>
    /// LaTeX 渲染引擎抽象。GUI / Engine 层仅依赖此接口，便于替换实现或单元测试。
    /// </summary>
    public interface ILatexRenderEngine
    {
        /// <summary>ratex_ffi.dll 是否可用。</summary>
        bool IsAvailable { get; }

        /// <summary>渲染为 WPF 位图（屏幕显示）。</summary>
        LatexBitmapResult RenderBitmap(string latex, LatexRenderOptions options);

        /// <summary>导出 PNG 二进制。</summary>
        LatexExportResult ExportPng(string latex, LatexRenderOptions options);

        /// <summary>导出自包含 SVG 文本。</summary>
        LatexExportResult ExportSvg(string latex, LatexRenderOptions options);
    }
}
