using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Taiji.Engine.Theme;
using Taiji.Engine.Code;

namespace Taiji.Engine.Utils
{
    /// <summary>WPF 可视元素 / FlowDocument 导出为 PNG。</summary>
    internal static class VisualExportHelper
    {
        private const double ExportDpi = 96;
        private const double StripHeightDip = 1800;
        private const long MaxBitmapPixels = 40_000_000;

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
                return RunInHiddenHost(element, host =>
                {
                    var dipWidth = Math.Max(1, host.ActualWidth > 0 ? host.ActualWidth : host.DesiredSize.Width);
                    var dipHeight = Math.Max(1, host.ActualHeight > 0 ? host.ActualHeight : host.DesiredSize.Height);
                    RenderToPng(host, dipWidth, dipHeight, path, out error);
                    return string.IsNullOrEmpty(error);
                });
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

            try
            {
                if (document.Background == null)
                    document.Background = DraculaTheme.BackgroundBrush;
                document.PagePadding = new Thickness(0);

                if (document.PageWidth <= 0 || double.IsInfinity(document.PageWidth))
                    document.PageWidth = 720;
                if (document.ColumnWidth <= 0 || double.IsInfinity(document.ColumnWidth))
                    document.ColumnWidth = document.PageWidth;

                ExpandEmbeddedCodeBlocks(document);

                var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                paginator.PageSize = new Size(document.PageWidth, StripHeightDip);

                var strips = new List<Tuple<double, DocumentPage>>();
                var totalHeight = 0.0;
                for (var i = 0; i < paginator.PageCount; i++)
                {
                    var page = paginator.GetPage(i);
                    strips.Add(Tuple.Create(totalHeight, page));
                    totalHeight += page.Size.Height;
                }

                if (totalHeight <= 0)
                {
                    error = "无可导出的内容";
                    return false;
                }

                var dipWidth = document.PageWidth;
                RenderTargetBitmap bitmap;
                if (!TryCreateBitmap(dipWidth, totalHeight, out bitmap, out error))
                    return false;

                var drawing = new DrawingVisual();
                using (var dc = drawing.RenderOpen())
                {
                    foreach (var strip in strips)
                    {
                        var page = strip.Item2;
                        dc.DrawRectangle(
                            DraculaTheme.BackgroundBrush,
                            null,
                            new Rect(0, strip.Item1, page.Size.Width, page.Size.Height));
                        var brush = new VisualBrush(page.Visual)
                        {
                            Stretch = Stretch.None,
                            AlignmentX = AlignmentX.Left,
                            AlignmentY = AlignmentY.Top
                        };
                        dc.DrawRectangle(brush, null, new Rect(0, strip.Item1, page.Size.Width, page.Size.Height));
                    }
                }

                bitmap.Render(drawing);
                SaveBitmap(bitmap, path);
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

        internal static void PrepareForExportLayout(FrameworkElement element, double width)
        {
            if (element == null) return;
            if (width > 64)
                element.Width = width;
            element.Measure(new Size(width > 64 ? width : double.PositiveInfinity, double.PositiveInfinity));
            var w = element.DesiredSize.Width > 0 ? element.DesiredSize.Width : width;
            var h = element.DesiredSize.Height > 0 ? element.DesiredSize.Height : 1;
            element.Arrange(new Rect(0, 0, w, h));
            element.UpdateLayout();
        }

        private static bool RunInHiddenHost(FrameworkElement element, Func<FrameworkElement, bool> action)
        {
            var hostWidth = element.Width > 64 ? element.Width : 800;
            var host = new Grid { Width = hostWidth };
            host.Children.Add(element);

            var window = new Window
            {
                Content = host,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Opacity = 0,
                Width = hostWidth,
                Height = 1,
                Left = -20000,
                Top = -20000,
                ShowActivated = false
            };

            window.Show();
            try
            {
                host.UpdateLayout();
                element.UpdateLayout();
                window.UpdateLayout();

                host.Measure(new Size(hostWidth, double.PositiveInfinity));
                var hostHeight = Math.Max(1, host.DesiredSize.Height);
                host.Arrange(new Rect(0, 0, hostWidth, hostHeight));
                host.UpdateLayout();
                element.UpdateLayout();
                window.UpdateLayout();

                return action(element);
            }
            finally
            {
                host.Children.Remove(element);
                window.Close();
            }
        }

        private static void RenderToPng(Visual visual, double dipWidth, double dipHeight, string path, out string error)
        {
            RenderTargetBitmap bitmap;
            if (!TryCreateBitmap(dipWidth, dipHeight, out bitmap, out error))
                return;

            bitmap.Render(visual);
            SaveBitmap(bitmap, path);
        }

        private static bool TryCreateBitmap(double dipWidth, double dipHeight, out RenderTargetBitmap bitmap, out string error)
        {
            bitmap = null;
            error = null;

            var dpi = ExportDpi;
            var pixelWidth = DipToPixels(dipWidth, dpi);
            var pixelHeight = DipToPixels(dipHeight, dpi);
            var pixels = (long)pixelWidth * pixelHeight;

            if (pixels > MaxBitmapPixels)
            {
                var scale = Math.Sqrt(MaxBitmapPixels / (double)pixels);
                dpi = Math.Max(72, Math.Floor(dpi * scale));
                pixelWidth = DipToPixels(dipWidth, dpi);
                pixelHeight = DipToPixels(dipHeight, dpi);
                pixels = (long)pixelWidth * pixelHeight;
            }

            if (pixels > MaxBitmapPixels)
            {
                error = $"内容过大（约 {dipHeight:0}×{dipWidth:0}），无法导出为单张 PNG。请缩短内容或分段导出。";
                return false;
            }

            bitmap = new RenderTargetBitmap(
                pixelWidth,
                pixelHeight,
                dpi,
                dpi,
                PixelFormats.Pbgra32);
            return true;
        }

        private static int DipToPixels(double dip, double dpi)
        {
            return Math.Max(1, (int)Math.Ceiling(dip * dpi / 96.0));
        }

        private static void SaveBitmap(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path))
                encoder.Save(stream);
        }

        private static void ExpandEmbeddedCodeBlocks(FlowDocument document)
        {
            if (document == null) return;
            var contentWidth = Math.Max(240, document.PageWidth - 48);
            foreach (var block in document.Blocks)
                ExpandCodeBlocksInBlock(block, contentWidth);
        }

        private static void ExpandCodeBlocksInBlock(Block block, double contentWidth)
        {
            if (block is BlockUIContainer container && container.Child is CodeBlockEditor editor)
            {
                CodeBlockEditor.ExpandForDocumentExport(editor, contentWidth);
                return;
            }

            if (block is Section section)
            {
                foreach (Block child in section.Blocks)
                    ExpandCodeBlocksInBlock(child, contentWidth);
                return;
            }

            if (block is Table table)
            {
                foreach (var rowGroup in table.RowGroups)
                {
                    foreach (var row in rowGroup.Rows)
                    {
                        foreach (var cell in row.Cells)
                        {
                            foreach (Block child in cell.Blocks)
                                ExpandCodeBlocksInBlock(child, contentWidth);
                        }
                    }
                }
            }
        }
    }
}
