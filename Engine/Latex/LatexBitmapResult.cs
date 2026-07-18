using System.Windows.Media.Imaging;

namespace Taiji.Engine.Latex
{
    public sealed class LatexBitmapResult
    {
        public bool Success { get; private set; }
        public string Error { get; private set; }
        public WriteableBitmap Bitmap { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }

        private LatexBitmapResult()
        {
        }

        public static LatexBitmapResult Ok(WriteableBitmap bitmap, double width, double height)
        {
            return new LatexBitmapResult
            {
                Success = true,
                Bitmap = bitmap,
                Width = width,
                Height = height
            };
        }

        public static LatexBitmapResult Fail(string error)
        {
            return new LatexBitmapResult
            {
                Success = false,
                Error = error ?? "渲染失败"
            };
        }

        public static LatexBitmapResult Empty()
        {
            return new LatexBitmapResult { Success = true };
        }
    }
}
