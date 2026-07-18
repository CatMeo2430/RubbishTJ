using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Taiji.Engine.Latex
{
    /// <summary>后台线程可生成的像素缓冲，在 UI 线程转为 WriteableBitmap。</summary>
    internal sealed class LatexPixelBuffer
    {
        public byte[] Pbgra { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Dpi { get; set; }
        public double LayoutWidth { get; set; }
        public double LayoutHeight { get; set; }
        public string Error { get; set; }
        public bool Success => Error == null && Pbgra != null;

        public static LatexPixelBuffer Fail(string error)
        {
            return new LatexPixelBuffer { Error = error ?? "渲染失败" };
        }
    }

    internal static class RatexBitmapHelper
    {
        /// <summary>
        /// tiny-skia 输出 premultiplied RGBA8，转换为 WPF Pbgra32 字节（可在后台线程执行）。
        /// </summary>
        public static byte[] ConvertRgbaToPbgra(RatexNative.RatexBitmapNative src)
        {
            if (src.Data == IntPtr.Zero || src.Width == 0 || src.Height == 0)
                return null;

            var length = checked((int)(src.Stride * src.Height));
            var rgba = new byte[length];
            Marshal.Copy(src.Data, rgba, 0, length);

            var dst = new byte[length];
            for (var i = 0; i < length; i += 4)
            {
                dst[i] = rgba[i + 2];
                dst[i + 1] = rgba[i + 1];
                dst[i + 2] = rgba[i];
                dst[i + 3] = rgba[i + 3];
            }
            return dst;
        }

        /// <summary>
        /// tiny-skia 输出 premultiplied RGBA8，转换为 WPF Pbgra32。
        /// </summary>
        public static WriteableBitmap ToWriteableBitmap(RatexNative.RatexBitmapNative src, double dpi)
        {
            var pbgra = ConvertRgbaToPbgra(src);
            if (pbgra == null)
                return null;

            return ToWriteableBitmap(new LatexPixelBuffer
            {
                Pbgra = pbgra,
                Width = (int)src.Width,
                Height = (int)src.Height,
                Dpi = dpi
            });
        }

        public static WriteableBitmap ToWriteableBitmap(LatexPixelBuffer buffer)
        {
            if (buffer == null || buffer.Pbgra == null || buffer.Width == 0 || buffer.Height == 0)
                return null;

            var wb = new WriteableBitmap(
                buffer.Width,
                buffer.Height,
                buffer.Dpi,
                buffer.Dpi,
                PixelFormats.Pbgra32,
                null);

            wb.Lock();
            try
            {
                Marshal.Copy(buffer.Pbgra, 0, wb.BackBuffer, buffer.Pbgra.Length);
                wb.AddDirtyRect(new Int32Rect(0, 0, buffer.Width, buffer.Height));
            }
            finally
            {
                wb.Unlock();
            }

            return wb;
        }
    }
}
