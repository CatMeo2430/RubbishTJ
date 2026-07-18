using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Taiji.GUI
{
    /// <summary>
    /// 计算窗口相对显示器工作区被任务栏等遮挡的边距（DIP），
    /// 用于把边缘热区/面板内移到鼠标仍能点到的区域。
    /// </summary>
    internal static class WorkAreaInsets
    {
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        public static Thickness Get(Window window)
        {
            if (window == null || !window.IsLoaded || window.ActualWidth <= 0 || window.ActualHeight <= 0)
                return new Thickness(0);

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return new Thickness(0);

            RECT winRect;
            if (!GetWindowRect(hwnd, out winRect))
                return new Thickness(0);

            var hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(hMon, ref mi))
                return new Thickness(0);

            // 窗口伸出工作区的部分 = 被任务栏等挡住、收不到鼠标
            var topPx = Math.Max(0, mi.rcWork.Top - winRect.Top);
            var leftPx = Math.Max(0, mi.rcWork.Left - winRect.Left);
            var rightPx = Math.Max(0, winRect.Right - mi.rcWork.Right);
            var bottomPx = Math.Max(0, winRect.Bottom - mi.rcWork.Bottom);

            double sx = 1, sy = 1;
            var src = PresentationSource.FromVisual(window);
            if (src != null && src.CompositionTarget != null)
            {
                var m = src.CompositionTarget.TransformToDevice;
                sx = m.M11 != 0 ? m.M11 : 1;
                sy = m.M22 != 0 ? m.M22 : 1;
            }

            return new Thickness(
                leftPx / sx,
                topPx / sy,
                rightPx / sx,
                bottomPx / sy);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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
