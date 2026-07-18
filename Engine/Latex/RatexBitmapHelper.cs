using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Taiji.Engine.Latex;

namespace Taiji.Engine.Latex
{
    internal static class RatexBitmapHelper
    {
        /// <summary>
        /// tiny-skia 输出 premultiplied RGBA8，转换为 WPF Pbgra32。
        /// </summary>
        public static WriteableBitmap ToWriteableBitmap(RatexNative.RatexBitmapNative src, double dpi)
        {
            if (src.Data == IntPtr.Zero || src.Width == 0 || src.Height == 0)
                return null;

            var length = checked((int)(src.Stride * src.Height));
            var rgba = new byte[length];
            Marshal.Copy(src.Data, rgba, 0, length);

            var wb = new WriteableBitmap(
                (int)src.Width,
                (int)src.Height,
                dpi,
                dpi,
                PixelFormats.Pbgra32,
                null);

            wb.Lock();
            try
            {
                var dst = new byte[length];
                for (var i = 0; i < length; i += 4)
                {
                    dst[i] = rgba[i + 2];
                    dst[i + 1] = rgba[i + 1];
                    dst[i + 2] = rgba[i];
                    dst[i + 3] = rgba[i + 3];
                }

                Marshal.Copy(dst, 0, wb.BackBuffer, length);
                wb.AddDirtyRect(new Int32Rect(0, 0, (int)src.Width, (int)src.Height));
            }
            finally
            {
                wb.Unlock();
            }

            return wb;
        }
    }
}
