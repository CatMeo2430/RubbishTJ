using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Taiji.Engine.Latex;

namespace Taiji.Engine.Latex
{
    public sealed class RatexFormulaView : FrameworkElement
    {
        public static readonly DependencyProperty LatexProperty =
            DependencyProperty.Register(
                "Latex",
                typeof(string),
                typeof(RatexFormulaView),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

        public static readonly DependencyProperty DisplayModeProperty =
            DependencyProperty.Register(
                "DisplayMode",
                typeof(bool),
                typeof(RatexFormulaView),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

        public static readonly DependencyProperty FontSizeEmProperty =
            DependencyProperty.Register(
                "FontSizeEm",
                typeof(double),
                typeof(RatexFormulaView),
                new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

        public static readonly DependencyProperty ErrorProperty =
            DependencyProperty.Register(
                "Error",
                typeof(string),
                typeof(RatexFormulaView),
                new PropertyMetadata(null));

        private WriteableBitmap _bitmap;
        private double _bitmapWidth;
        private double _bitmapHeight;
        private int _layoutVersion;

        public string Latex
        {
            get => (string)GetValue(LatexProperty);
            set => SetValue(LatexProperty, value);
        }

        public bool DisplayMode
        {
            get => (bool)GetValue(DisplayModeProperty);
            set => SetValue(DisplayModeProperty, value);
        }

        public double FontSizeEm
        {
            get => (double)GetValue(FontSizeEmProperty);
            set => SetValue(FontSizeEmProperty, value);
        }

        public string Error
        {
            get => (string)GetValue(ErrorProperty);
            private set => SetValue(ErrorProperty, value);
        }

        public RatexFormulaView()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Focusable = true;
            ContextMenu = BuildContextMenu();
            Loaded += OnLoaded;
        }

        private ContextMenu BuildContextMenu()
        {
            var menu = new ContextMenu();

            var copyLatex = new MenuItem { Header = "复制 LaTeX 源码" };
            copyLatex.Click += (s, e) => LatexInteractions.CopySourceToClipboard(Latex);

            var exportPng = new MenuItem { Header = "导出 PNG…" };
            exportPng.Click += OnExportPngClick;

            var exportSvg = new MenuItem { Header = "导出 SVG…" };
            exportSvg.Click += OnExportSvgClick;

            menu.Items.Add(copyLatex);
            menu.Items.Add(new Separator());
            menu.Items.Add(exportPng);
            menu.Items.Add(exportSvg);
            return menu;
        }

        private void OnExportPngClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Latex)) return;
            var owner = Window.GetWindow(this);
            try
            {
                LatexInteractions.PromptExportPng(Latex, DisplayMode, (float)FontSizeEm, owner);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "导出 PNG 失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnExportSvgClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Latex)) return;
            var owner = Window.GetWindow(this);
            try
            {
                LatexInteractions.PromptExportSvg(Latex, DisplayMode, (float)FontSizeEm, owner);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "导出 SVG 失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RebuildLayout();
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (RatexFormulaView)d;
            view.RebuildLayout();
        }

        private void RebuildLayout()
        {
            var version = ++_layoutVersion;
            _bitmap = null;
            _bitmapWidth = 0;
            _bitmapHeight = 0;
            Error = null;

            var latex = Latex;
            if (string.IsNullOrWhiteSpace(latex))
            {
                InvalidateVisual();
                InvalidateMeasure();
                return;
            }

            var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var fontSizeEm = FontSizeEm;
            var displayMode = DisplayMode;
            var options = LatexRenderOptions.ForScreen(fontSizeEm, dip, displayMode);
            var engine = LatexEngine.Default as RatexLatexEngine ?? new RatexLatexEngine();

            Task.Run(() => engine.RenderPixelBuffer(latex, options))
                .ContinueWith(t =>
                {
                    if (version != _layoutVersion) return;

                    LatexPixelBuffer pixels;
                    try
                    {
                        pixels = t.IsFaulted
                            ? LatexPixelBuffer.Fail(t.Exception?.GetBaseException().Message ?? "渲染失败")
                            : t.Result;
                    }
                    catch (Exception ex)
                    {
                        pixels = LatexPixelBuffer.Fail(ex.Message);
                    }

                    Dispatcher.BeginInvoke(new Action(() => ApplyPixelBuffer(version, pixels)), DispatcherPriority.Background);
                });
        }

        private void ApplyPixelBuffer(int version, LatexPixelBuffer pixels)
        {
            if (version != _layoutVersion) return;

            _bitmap = null;
            _bitmapWidth = 0;
            _bitmapHeight = 0;
            Error = null;

            if (!pixels.Success)
            {
                if (!string.IsNullOrEmpty(pixels.Error))
                    Error = pixels.Error;
                InvalidateVisual();
                InvalidateMeasure();
                return;
            }

            if (pixels.Pbgra == null)
            {
                InvalidateVisual();
                InvalidateMeasure();
                return;
            }

            try
            {
                _bitmap = RatexBitmapHelper.ToWriteableBitmap(pixels);
                _bitmapWidth = pixels.LayoutWidth;
                _bitmapHeight = pixels.LayoutHeight;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }

            InvalidateVisual();
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_bitmap == null)
                return new Size(0, string.IsNullOrEmpty(Error) ? 0 : 18);

            return new Size(_bitmapWidth, _bitmapHeight);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (!string.IsNullOrEmpty(Error))
            {
                var ft = new FormattedText(
                    Error,
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    Brushes.IndianRed,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                drawingContext.DrawText(ft, new Point(0, 0));
                return;
            }

            if (_bitmap == null)
                return;

            drawingContext.DrawImage(_bitmap, new Rect(0, 0, _bitmapWidth, _bitmapHeight));
        }
    }
}
