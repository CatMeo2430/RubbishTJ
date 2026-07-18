using System;
using System.IO;

namespace Taiji.Engine.Latex
{
    /// <summary>基于 ratex_ffi.dll 的默认 LaTeX 渲染引擎。</summary>
    public sealed class RatexLatexEngine : ILatexRenderEngine
    {
        public bool IsAvailable => RatexNative.IsLibraryAvailable();

        public LatexBitmapResult RenderBitmap(string latex, LatexRenderOptions options)
        {
            var pixels = RenderPixelBuffer(latex, options);
            if (!pixels.Success)
                return string.IsNullOrEmpty(pixels.Error) ? LatexBitmapResult.Empty() : LatexBitmapResult.Fail(pixels.Error);

            if (pixels.Pbgra == null)
                return LatexBitmapResult.Empty();

            var wb = RatexBitmapHelper.ToWriteableBitmap(pixels);
            if (wb == null)
                return LatexBitmapResult.Fail("位图转换失败");

            return LatexBitmapResult.Ok(wb, pixels.LayoutWidth, pixels.LayoutHeight);
        }

        /// <summary>在后台线程调用：FFI 渲染 + 像素转换，不创建 WPF 位图。</summary>
        internal LatexPixelBuffer RenderPixelBuffer(string latex, LatexRenderOptions options)
        {
            if (options == null)
                return LatexPixelBuffer.Fail("LatexRenderOptions 为空");

            var cleaned = LatexNormalizer.CleanFormula(latex);
            if (string.IsNullOrWhiteSpace(cleaned))
                return new LatexPixelBuffer();

            RatexNative.RatexBitmapNative? nativeBitmap = null;
            try
            {
                string err;
                nativeBitmap = RatexNative.RenderBitmap(cleaned, options, out err);

                if (!nativeBitmap.HasValue)
                    return LatexPixelBuffer.Fail(err);

                var bmp = nativeBitmap.Value;
                var dpr = options.DevicePixelRatio > 0 ? options.DevicePixelRatio : 1f;
                var dpi = 96.0 * dpr;
                var pbgra = RatexBitmapHelper.ConvertRgbaToPbgra(bmp);
                if (pbgra == null)
                    return LatexPixelBuffer.Fail("位图转换失败");

                return new LatexPixelBuffer
                {
                    Pbgra = pbgra,
                    Width = (int)bmp.Width,
                    Height = (int)bmp.Height,
                    Dpi = dpi,
                    LayoutWidth = (double)bmp.Width / dpr,
                    LayoutHeight = (double)bmp.Height / dpr
                };
            }
            catch (DllNotFoundException)
            {
                return LatexPixelBuffer.Fail("未找到 ratex_ffi.dll");
            }
            catch (Exception ex)
            {
                return LatexPixelBuffer.Fail(ex.Message);
            }
            finally
            {
                if (nativeBitmap.HasValue)
                    RatexNative.FreeBitmap(nativeBitmap.Value);
            }
        }

        public LatexExportResult ExportPng(string latex, LatexRenderOptions options)
        {
            if (options == null)
                return LatexExportResult.Fail("LatexRenderOptions 为空");

            var cleaned = LatexNormalizer.CleanFormula(latex);
            if (string.IsNullOrWhiteSpace(cleaned))
                return LatexExportResult.Fail("LaTeX 为空");

            try
            {
                string err;
                var png = RatexNative.RenderPng(cleaned, options, out err);

                if (png == null)
                    return LatexExportResult.Fail(err);

                return LatexExportResult.FromPng(png);
            }
            catch (DllNotFoundException)
            {
                return LatexExportResult.Fail("未找到 ratex_ffi.dll");
            }
            catch (Exception ex)
            {
                return LatexExportResult.Fail(ex.Message);
            }
        }

        public LatexExportResult ExportSvg(string latex, LatexRenderOptions options)
        {
            if (options == null)
                return LatexExportResult.Fail("LatexRenderOptions 为空");

            var cleaned = LatexNormalizer.CleanFormula(latex);
            if (string.IsNullOrWhiteSpace(cleaned))
                return LatexExportResult.Fail("LaTeX 为空");

            try
            {
                string err;
                var svg = RatexNative.RenderSvg(cleaned, options, out err);

                if (svg == null)
                    return LatexExportResult.Fail(err);

                return LatexExportResult.FromSvg(svg);
            }
            catch (DllNotFoundException)
            {
                return LatexExportResult.Fail("未找到 ratex_ffi.dll");
            }
            catch (Exception ex)
            {
                return LatexExportResult.Fail(ex.Message);
            }
        }
    }
}
