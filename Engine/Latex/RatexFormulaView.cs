using System;
using System.Windows;
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

        public string Latex
        {
            get { return (string)GetValue(LatexProperty); }
            set { SetValue(LatexProperty, value); }
        }

        public bool DisplayMode
        {
            get { return (bool)GetValue(DisplayModeProperty); }
            set { SetValue(DisplayModeProperty, value); }
        }

        public double FontSizeEm
        {
            get { return (double)GetValue(FontSizeEmProperty); }
            set { SetValue(FontSizeEmProperty, value); }
        }

        public string Error
        {
            get { return (string)GetValue(ErrorProperty); }
            private set { SetValue(ErrorProperty, value); }
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

            try
            {
                var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                var options = LatexRenderOptions.ForScreen(FontSizeEm, dip, DisplayMode);
                var result = LatexEngine.Default.RenderBitmap(latex, options);

                if (!result.Success)
                {
                    Error = result.Error;
                    InvalidateVisual();
                    InvalidateMeasure();
                    return;
                }

                _bitmap = result.Bitmap;
                _bitmapWidth = result.Width;
                _bitmapHeight = result.Height;
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
