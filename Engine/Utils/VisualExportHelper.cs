using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Utils
{
    /// <summary>WPF 可视元素 / FlowDocument 导出为 PNG。</summary>
    internal static class VisualExportHelper
    {
        private const double DefaultDpi = 192;

        public static bool TrySaveElement(FrameworkElement element, string path, out string error)
        {
            error = null;
            if (element == null)
            {
                error = "无可导出的内容";
                return false;
            }

            try
            {
                EnsureLayout(element);

                var width = Math.Max(1, element.ActualWidth);
                var height = Math.Max(1, element.ActualHeight);
                var target = new RenderTargetBitmap(
                    (int)Math.Ceiling(width),
                    (int)Math.Ceiling(height),
                    DefaultDpi,
                    DefaultDpi,
                    PixelFormats.Pbgra32);

                target.Render(element);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(target));
                using (var stream = File.Create(path))
                    encoder.Save(stream);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TrySaveFlowDocument(FlowDocument document, string path, out string error)
        {
            error = null;
            if (document == null)
            {
                error = "无可导出的内容";
                return false;
            }

            DocumentPage page = null;
            try
            {
                if (document.Background == null)
                    document.Background = DraculaTheme.BackgroundBrush;
                document.PagePadding = new Thickness(0);

                var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                if (document.PageWidth <= 0 || double.IsInfinity(document.PageWidth))
                    document.PageWidth = 720;
                if (document.ColumnWidth <= 0 || double.IsInfinity(document.ColumnWidth))
                    document.ColumnWidth = document.PageWidth;

                paginator.PageSize = new Size(document.PageWidth, double.PositiveInfinity);
                page = paginator.GetPage(0);

                var size = page.Size;
                var target = new RenderTargetBitmap(
                    (int)Math.Ceiling(size.Width),
                    (int)Math.Ceiling(size.Height),
                    DefaultDpi,
                    DefaultDpi,
                    PixelFormats.Pbgra32);
                target.Render(page.Visual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(target));
                using (var stream = File.Create(path))
                    encoder.Save(stream);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static FlowDocument CreateExportDocument(double pageWidth)
        {
            var width = pageWidth > 0 && !double.IsInfinity(pageWidth) ? pageWidth : 720;
            return new FlowDocument
            {
                Background = DraculaTheme.BackgroundBrush,
                Foreground = DraculaTheme.ForegroundBrush,
                FontFamily = DraculaTheme.UiFont,
                FontSize = 13.5,
                PageWidth = width,
                ColumnWidth = width,
                PagePadding = new Thickness(0),
                LineHeight = 22
            };
        }

        public static double ResolvePageWidth(DependencyObject from, double fallback = 720)
        {
            var walk = from;
            while (walk != null)
            {
                if (walk is RichTextBox rtb)
                {
                    var doc = rtb.Document;
                    if (doc != null && doc.PageWidth > 0 && !double.IsInfinity(doc.PageWidth))
                        return doc.PageWidth;
                    if (rtb.ActualWidth > 64)
                        return Math.Max(320, rtb.ActualWidth - 48);
                }

                var parent = VisualTreeHelper.GetParent(walk);
                if (parent == null)
                    parent = LogicalTreeHelper.GetParent(walk);
                walk = parent;
            }
            return fallback;
        }

        public static string PromptSaveElementPng(FrameworkElement element, string defaultFileName, Window owner)
        {
            return ExportDialog.PromptSave(
                owner,
                "PNG 图片|*.png",
                defaultFileName,
                path =>
                {
                    string err;
                    return TrySaveElement(element, path, out err) ? null : (err ?? "导出失败");
                });
        }

        public static string PromptSaveDocumentPng(FlowDocument document, string defaultFileName, Window owner)
        {
            return ExportDialog.PromptSave(
                owner,
                "PNG 图片|*.png",
                defaultFileName,
                path =>
                {
                    string err;
                    return TrySaveFlowDocument(document, path, out err) ? null : (err ?? "导出失败");
                });
        }

        private static void EnsureLayout(FrameworkElement element)
        {
            if (element.ActualWidth > 0 && element.ActualHeight > 0)
                return;

            var width = double.IsNaN(element.Width) || element.Width <= 0
                ? double.PositiveInfinity
                : element.Width;
            element.Measure(new Size(width, double.PositiveInfinity));
            element.Arrange(new Rect(0, 0, element.DesiredSize.Width, element.DesiredSize.Height));
            element.UpdateLayout();
        }
    }
}
