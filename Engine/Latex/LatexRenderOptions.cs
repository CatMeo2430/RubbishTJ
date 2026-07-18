namespace Taiji.Engine.Latex
{
    /// <summary>ratex_ffi 渲染参数（对应 C 侧 RatexRenderOptions）。</summary>
    public sealed class LatexRenderOptions
    {
        public bool DisplayMode { get; set; }
        public float FontSize { get; set; }
        public float Padding { get; set; }
        public float DevicePixelRatio { get; set; }
        public float BackgroundR { get; set; }
        public float BackgroundG { get; set; }
        public float BackgroundB { get; set; }
        public float BackgroundA { get; set; }
        public float ForegroundR { get; set; }
        public float ForegroundG { get; set; }
        public float ForegroundB { get; set; }
        public float ForegroundA { get; set; }
        public float SvgStrokeWidth { get; set; }
        public bool SvgEmbedGlyphs { get; set; }

        public LatexRenderOptions()
        {
            DisplayMode = true;
            FontSize = 20f;
            Padding = 4f;
            DevicePixelRatio = 1f;
            ForegroundR = 1f;
            ForegroundG = 1f;
            ForegroundB = 1f;
            ForegroundA = 1f;
            SvgStrokeWidth = 1.5f;
            SvgEmbedGlyphs = true;
        }

        /// <summary>屏幕绘制：透明背景、白色前景（暗黑主题），跟随 WPF DPI。</summary>
        public static LatexRenderOptions ForScreen(double fontSizeEm, double pixelsPerDip, bool displayMode)
        {
            return new LatexRenderOptions
            {
                DisplayMode = displayMode,
                FontSize = (float)fontSizeEm,
                Padding = 4f,
                DevicePixelRatio = (float)pixelsPerDip,
                BackgroundR = 0f,
                BackgroundG = 0f,
                BackgroundB = 0f,
                BackgroundA = 0f,
                ForegroundR = 1f,
                ForegroundG = 1f,
                ForegroundB = 1f,
                ForegroundA = 1f
            };
        }

        /// <summary>PNG 导出：白底黑字。</summary>
        public static LatexRenderOptions ForPngExport(
            bool displayMode,
            float fontSize,
            float padding,
            float devicePixelRatio,
            float bgR,
            float bgG,
            float bgB,
            float bgA)
        {
            return new LatexRenderOptions
            {
                DisplayMode = displayMode,
                FontSize = fontSize,
                Padding = padding,
                DevicePixelRatio = devicePixelRatio,
                BackgroundR = bgR,
                BackgroundG = bgG,
                BackgroundB = bgB,
                BackgroundA = bgA,
                ForegroundR = 0f,
                ForegroundG = 0f,
                ForegroundB = 0f,
                ForegroundA = 1f
            };
        }

        /// <summary>SVG 导出：透明背景、白色笔画（适合暗黑界面）。</summary>
        public static LatexRenderOptions ForSvgExport(bool displayMode, float fontSize, float padding)
        {
            return new LatexRenderOptions
            {
                DisplayMode = displayMode,
                FontSize = fontSize,
                Padding = padding,
                DevicePixelRatio = 1f,
                BackgroundR = 0f,
                BackgroundG = 0f,
                BackgroundB = 0f,
                BackgroundA = 0f,
                ForegroundR = 1f,
                ForegroundG = 1f,
                ForegroundB = 1f,
                ForegroundA = 1f,
                SvgStrokeWidth = 1.5f,
                SvgEmbedGlyphs = true
            };
        }
    }
}
