using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Taiji.Engine.Latex
{
    internal static class RatexNative
    {
        private const string Lib = "ratex_ffi";
        private static bool? _libraryAvailable;

        internal static bool IsLibraryAvailable()
        {
            if (_libraryAvailable.HasValue)
                return _libraryAvailable.Value;

            string path;
            _libraryAvailable = RatexNativeLoader.TryEnsureLoaded(out path);
            return _libraryAvailable.Value;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RatexColorNative
        {
            public float R;
            public float G;
            public float B;
            public float A;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RatexOptionsNative
        {
            public UIntPtr StructSize;
            public int DisplayMode;
            public IntPtr Color;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RatexRenderOptionsNative
        {
            public UIntPtr StructSize;
            public int DisplayMode;
            public IntPtr Color;
            public float FontSize;
            public float Padding;
            public float DevicePixelRatio;
            public RatexColorNative BackgroundColor;
            public IntPtr FontDir;
            public float StrokeWidth;
            public int EmbedGlyphs;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RatexResultNative
        {
            public IntPtr Data;
            public int ErrorCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RatexBitmapNative
        {
            public IntPtr Data;
            public uint Width;
            public uint Height;
            public uint Stride;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RatexBitmapResultNative
        {
            public RatexBitmapNative Bitmap;
            public int ErrorCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RatexBytesNative
        {
            public IntPtr Data;
            public uint Len;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RatexBytesResultNative
        {
            public RatexBytesNative Bytes;
            public int ErrorCode;
        }

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern RatexBitmapResultNative ratex_render_bitmap(IntPtr latex, IntPtr opts);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ratex_free_bitmap(RatexBitmapNative bitmap);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern RatexBytesResultNative ratex_render_png(IntPtr latex, IntPtr opts);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ratex_free_bytes(RatexBytesNative bytes);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern RatexResultNative ratex_render_svg(IntPtr latex, IntPtr opts);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ratex_free_svg(IntPtr svg);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ratex_get_last_error();

        public static RatexBitmapNative? RenderBitmap(
            string latex,
            LatexRenderOptions options,
            out string error)
        {
            if (options == null)
            {
                error = "LatexRenderOptions 为空";
                return null;
            }
            return InvokeBitmap(
                latex,
                options.DisplayMode,
                options.FontSize,
                options.Padding,
                options.DevicePixelRatio,
                options.ForegroundR,
                options.ForegroundG,
                options.ForegroundB,
                options.ForegroundA,
                options.BackgroundR,
                options.BackgroundG,
                options.BackgroundB,
                options.BackgroundA,
                ratex_render_bitmap,
                out error);
        }

        public static byte[] RenderPng(
            string latex,
            LatexRenderOptions options,
            out string error)
        {
            error = null;
            if (options == null)
            {
                error = "LatexRenderOptions 为空";
                return null;
            }
            IntPtr latexPtr = IntPtr.Zero;
            IntPtr colorPtr = IntPtr.Zero;
            IntPtr optsPtr = IntPtr.Zero;
            try
            {
                if (!TryAllocRenderCall(
                    latex,
                    options.DisplayMode,
                    options.FontSize,
                    options.Padding,
                    options.DevicePixelRatio,
                    options.ForegroundR,
                    options.ForegroundG,
                    options.ForegroundB,
                    options.ForegroundA,
                    options.BackgroundR,
                    options.BackgroundG,
                    options.BackgroundB,
                    options.BackgroundA,
                    out latexPtr,
                    out colorPtr,
                    out optsPtr,
                    out error))
                    return null;

                var result = ratex_render_png(latexPtr, optsPtr);
                if (result.ErrorCode != 0 || result.Bytes.Data == IntPtr.Zero)
                {
                    error = GetLastError("ratex_render_png");
                    return null;
                }

                var bytes = new byte[result.Bytes.Len];
                Marshal.Copy(result.Bytes.Data, bytes, 0, bytes.Length);
                ratex_free_bytes(result.Bytes);
                return bytes;
            }
            catch (DllNotFoundException)
            {
                error = "未找到 ratex_ffi.dll";
                return null;
            }
            finally
            {
                FreeRenderCall(latexPtr, colorPtr, optsPtr);
            }
        }

        public static string RenderSvg(
            string latex,
            LatexRenderOptions options,
            out string error)
        {
            error = null;
            if (options == null)
            {
                error = "LatexRenderOptions 为空";
                return null;
            }
            IntPtr latexPtr = IntPtr.Zero;
            IntPtr colorPtr = IntPtr.Zero;
            IntPtr optsPtr = IntPtr.Zero;
            try
            {
                if (!TryAllocRenderCall(
                    latex,
                    options.DisplayMode,
                    options.FontSize,
                    options.Padding,
                    options.DevicePixelRatio,
                    options.ForegroundR,
                    options.ForegroundG,
                    options.ForegroundB,
                    options.ForegroundA,
                    options.BackgroundR,
                    options.BackgroundG,
                    options.BackgroundB,
                    options.BackgroundA,
                    out latexPtr,
                    out colorPtr,
                    out optsPtr,
                    out error))
                    return null;

                var opts = (RatexRenderOptionsNative)Marshal.PtrToStructure(optsPtr, typeof(RatexRenderOptionsNative));
                opts.StrokeWidth = options.SvgStrokeWidth > 0 ? options.SvgStrokeWidth : 1.5f;
                opts.EmbedGlyphs = options.SvgEmbedGlyphs ? 1 : 0;
                Marshal.StructureToPtr(opts, optsPtr, false);

                var result = ratex_render_svg(latexPtr, optsPtr);
                if (result.ErrorCode != 0 || result.Data == IntPtr.Zero)
                {
                    error = GetLastError("ratex_render_svg");
                    return null;
                }

                var svg = PtrToUtf8(result.Data);
                ratex_free_svg(result.Data);
                return svg;
            }
            catch (DllNotFoundException)
            {
                error = "未找到 ratex_ffi.dll";
                return null;
            }
            finally
            {
                FreeRenderCall(latexPtr, colorPtr, optsPtr);
            }
        }

        public static void FreeBitmap(RatexBitmapNative bitmap)
        {
            if (bitmap.Data != IntPtr.Zero)
                ratex_free_bitmap(bitmap);
        }

        public static void SavePng(byte[] png, string path)
        {
            if (png == null || png.Length == 0)
                throw new ArgumentException("PNG 数据为空");
            File.WriteAllBytes(path, png);
        }

        public static void SaveSvg(string svg, string path)
        {
            if (string.IsNullOrEmpty(svg))
                throw new ArgumentException("SVG 内容为空");
            File.WriteAllText(path, svg, new UTF8Encoding(false));
        }

        private delegate RatexBitmapResultNative BitmapRenderer(IntPtr latex, IntPtr opts);

        private static RatexBitmapNative? InvokeBitmap(
            string latex,
            bool displayMode,
            float fontSize,
            float padding,
            float devicePixelRatio,
            float fgR,
            float fgG,
            float fgB,
            float fgA,
            float bgR,
            float bgG,
            float bgB,
            float bgA,
            BitmapRenderer render,
            out string error)
        {
            error = null;
            IntPtr latexPtr = IntPtr.Zero;
            IntPtr colorPtr = IntPtr.Zero;
            IntPtr optsPtr = IntPtr.Zero;
            try
            {
                if (!TryAllocRenderCall(
                    latex,
                    displayMode,
                    fontSize,
                    padding,
                    devicePixelRatio,
                    fgR,
                    fgG,
                    fgB,
                    fgA,
                    bgR,
                    bgG,
                    bgB,
                    bgA,
                    out latexPtr,
                    out colorPtr,
                    out optsPtr,
                    out error))
                    return null;

                var result = render(latexPtr, optsPtr);
                if (result.ErrorCode != 0 || result.Bitmap.Data == IntPtr.Zero)
                {
                    error = GetLastError("ratex_render_bitmap");
                    return null;
                }

                return result.Bitmap;
            }
            catch (DllNotFoundException)
            {
                error = "未找到 ratex_ffi.dll";
                return null;
            }
            finally
            {
                FreeRenderCall(latexPtr, colorPtr, optsPtr);
            }
        }

        private static bool TryAllocRenderCall(
            string latex,
            bool displayMode,
            float fontSize,
            float padding,
            float devicePixelRatio,
            float fgR,
            float fgG,
            float fgB,
            float fgA,
            float bgR,
            float bgG,
            float bgB,
            float bgA,
            out IntPtr latexPtr,
            out IntPtr colorPtr,
            out IntPtr optsPtr,
            out string error)
        {
            latexPtr = IntPtr.Zero;
            colorPtr = IntPtr.Zero;
            optsPtr = IntPtr.Zero;
            error = null;

            if (string.IsNullOrEmpty(latex))
            {
                error = "LaTeX 为空";
                return false;
            }

            if (!IsLibraryAvailable())
            {
                error = "未找到 ratex_ffi.dll";
                return false;
            }

            var bytes = Encoding.UTF8.GetBytes(latex);
            latexPtr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, latexPtr, bytes.Length);
            Marshal.WriteByte(latexPtr, bytes.Length, 0);

            var color = new RatexColorNative { R = fgR, G = fgG, B = fgB, A = fgA };
            colorPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RatexColorNative)));
            Marshal.StructureToPtr(color, colorPtr, false);

            var opts = new RatexRenderOptionsNative
            {
                StructSize = new UIntPtr((uint)Marshal.SizeOf(typeof(RatexRenderOptionsNative))),
                DisplayMode = displayMode ? 1 : 0,
                Color = colorPtr,
                FontSize = fontSize,
                Padding = padding,
                DevicePixelRatio = devicePixelRatio,
                BackgroundColor = new RatexColorNative
                {
                    R = bgR,
                    G = bgG,
                    B = bgB,
                    A = bgA
                },
                FontDir = IntPtr.Zero,
                StrokeWidth = 1.5f,
                EmbedGlyphs = 1
            };
            optsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(RatexRenderOptionsNative)));
            Marshal.StructureToPtr(opts, optsPtr, false);
            return true;
        }

        private static void FreeRenderCall(IntPtr latexPtr, IntPtr colorPtr, IntPtr optsPtr)
        {
            if (optsPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(optsPtr);
            if (colorPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(colorPtr);
            if (latexPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(latexPtr);
        }

        private static string GetLastError(string fallback)
        {
            var err = PtrToUtf8(ratex_get_last_error());
            return string.IsNullOrEmpty(err) ? fallback + " 失败" : err;
        }

        private static string PtrToUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;
            var len = 0;
            while (Marshal.ReadByte(ptr, len) != 0)
                len++;
            if (len == 0)
                return string.Empty;
            var buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }
    }
}
