using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Taiji.GUI
{
    /// <summary>
    /// 当前窗口所在显示器的工作区（屏幕减去任务栏），
    /// 适配上下左右任务栏、Win10 小任务栏、Win11。
    /// </summary>
    internal static class MonitorWorkArea
    {
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int WM_GETMINMAXINFO = 0x0024;

        public static Rect GetDipWorkArea(Window window)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            var hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(hMon, ref mi))
            {
                var wa = SystemParameters.WorkArea;
                return new Rect(wa.Left, wa.Top, wa.Width, wa.Height);
            }

            double sx = 1, sy = 1;
            var src = PresentationSource.FromVisual(window) ?? HwndSource.FromHwnd(hwnd);
            if (src != null && src.CompositionTarget != null)
            {
                var m = src.CompositionTarget.TransformFromDevice;
                sx = m.M11 != 0 ? m.M11 : 1;
                sy = m.M22 != 0 ? m.M22 : 1;
            }

            var left = mi.rcWork.Left * sx;
            var top = mi.rcWork.Top * sy;
            var right = mi.rcWork.Right * sx;
            var bottom = mi.rcWork.Bottom * sy;
            return new Rect(left, top, Math.Max(100, right - left), Math.Max(100, bottom - top));
        }

        /// <summary>把窗口摆到工作区（Normal，不遮挡任务栏）。</summary>
        public static void FillWindow(Window window)
        {
            if (window == null) return;
            var work = GetDipWorkArea(window);
            window.WindowState = WindowState.Normal;
            window.Left = work.Left;
            window.Top = work.Top;
            window.Width = work.Width;
            window.Height = work.Height;
        }

        /// <summary>系统最大化也限制在工作区内。</summary>
        public static void AttachMaximiseHook(Window window)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            var source = HwndSource.FromHwnd(hwnd);
            if (source == null) return;
            source.AddHook(WndProc);
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_GETMINMAXINFO)
                return IntPtr.Zero;

            var hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(hMon, ref mi))
                return IntPtr.Zero;

            var mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            mmi.ptMaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left;
            mmi.ptMaxPosition.Y = mi.rcWork.Top - mi.rcMonitor.Top;
            mmi.ptMaxSize.X = mi.rcWork.Right - mi.rcWork.Left;
            mmi.ptMaxSize.Y = mi.rcWork.Bottom - mi.rcWork.Top;
            Marshal.StructureToPtr(mmi, lParam, false);
            handled = true;
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
    }
}
