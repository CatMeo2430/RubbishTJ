namespace Taiji.Engine.Latex
{
    public sealed class LatexExportResult
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }
        public byte[] PngData { get; private set; }
        public string SvgText { get; private set; }

        private LatexExportResult()
        {
        }

        public static LatexExportResult FromPng(byte[] data)
        {
            return new LatexExportResult
            {
                Success = true,
                PngData = data
            };
        }

        public static LatexExportResult FromSvg(string svg)
        {
            return new LatexExportResult
            {
                Success = true,
                SvgText = svg
            };
        }

        public static LatexExportResult Fail(string error)
        {
            return new LatexExportResult
            {
                Success = false,
                Error = error ?? "导出失败"
            };
        }
    }
}
