using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using Taiji.Engine.Utils;

namespace Taiji.Engine.Code
{
    /// <summary>只读代码块编辑器：支持局部选中与 Ctrl+C 复制。</summary>
    public sealed class CodeBlockEditor : Border
    {
        private const double MaxDisplayHeight = 420;
        private const double LineHeight = 18;
        private const double MinEditorHeight = 48;

        public CodeBlockEditor(string code, string language, bool forExport = false)
        {
            Code = code ?? "";
            CodeLanguage = language ?? "";
            ForExport = forExport;
            Child = Editor = CreateEditor(Code, CodeLanguage, forExport);
            if (!forExport)
                AttachContextMenu();
        }

        public string Code { get; private set; }
        public string CodeLanguage { get; private set; }
        public bool ForExport { get; private set; }
        public TextEditor Editor { get; private set; }

        /// <summary>按完整内容布局，用于 PNG 导出（不受聊天区 420px 高度限制）。</summary>
        internal FrameworkElement CreateExportVisual(double width)
        {
            var exportWidth = width > 64 ? width : 640;
            var editor = CreateEditor(Code, CodeLanguage, forExport: true);
            editor.WordWrap = false;

            var shell = new Border
            {
                Background = Background ?? Theme.DraculaTheme.CurrentLineBrush,
                Padding = Padding,
                Child = editor,
                SnapsToDevicePixels = true,
                Width = exportWidth
            };

            VisualExportHelper.PrepareForExportLayout(shell, exportWidth);
            ApplyFullDocumentHeight(editor, exportWidth, allowWrap: false);
            VisualExportHelper.PrepareForExportLayout(shell, exportWidth);
            return shell;
        }

        public void CopySelectionOrAll()
        {
            var area = Editor.TextArea;
            if (area.Selection.IsEmpty)
                Clipboard.SetText(Editor.Text ?? "");
            else
                Clipboard.SetText(area.Selection.GetText());
        }

        public void SelectAll()
        {
            Editor.SelectAll();
            Editor.Focus();
        }

        private TextEditor CreateEditor(string code, string language, bool forExport = false)
        {
            if (code.EndsWith("\r\n")) code = code.Substring(0, code.Length - 2);
            else if (code.EndsWith("\n")) code = code.Substring(0, code.Length - 1);

            var editor = new TextEditor
            {
                IsReadOnly = true,
                IsEnabled = true,
                Focusable = !forExport,
                ShowLineNumbers = true,
                WordWrap = !forExport,
                FontFamily = Theme.DraculaTheme.MonoFont,
                FontSize = 12.5,
                Background = Theme.DraculaTheme.CurrentLineBrush,
                Foreground = Theme.DraculaTheme.ForegroundBrush,
                LineNumbersForeground = Theme.DraculaTheme.CommentBrush,
                BorderThickness = new Thickness(0),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(4, 2, 4, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            editor.Options.EnableHyperlinks = false;
            editor.Options.EnableEmailHyperlinks = false;
            editor.Options.EnableTextDragDrop = false;
            editor.Text = code;

            var def = CodeBlockViewFactory.ResolveHighlighting(language);
            if (def != null)
                editor.SyntaxHighlighting = OneDarkHighlighting.Apply(def);

            if (forExport)
            {
                var lines = Math.Max(1, editor.LineCount);
                editor.MinHeight = Math.Max(MinEditorHeight, lines * LineHeight + 16);
                editor.Height = double.NaN;
            }
            else
            {
                var lines = Math.Max(1, editor.LineCount);
                editor.Height = Math.Min(MaxDisplayHeight, Math.Max(MinEditorHeight, lines * LineHeight + 16));
            }

            editor.MinWidth = 120;

            if (!forExport)
            {
                editor.CommandBindings.Add(new CommandBinding(
                    ApplicationCommands.Copy,
                    (s, e) => { CopySelectionOrAll(); e.Handled = true; }));
                editor.CommandBindings.Add(new CommandBinding(
                    ApplicationCommands.SelectAll,
                    (s, e) => { SelectAll(); e.Handled = true; }));

                editor.PreviewMouseDown += (s, e) =>
                {
                    if (!editor.IsFocused)
                        editor.Focus();
                };

                editor.PreviewMouseWheel += CodeBlockViewFactory.BubbleWheelToChat;
            }

            return editor;
        }

        internal static void ExpandForDocumentExport(CodeBlockEditor editor, double contentWidth)
        {
            if (editor?.Editor == null) return;
            ApplyFullDocumentHeight(editor.Editor, contentWidth, allowWrap: true);
            editor.Height = editor.Editor.Height;
            editor.MinHeight = editor.Editor.Height;
        }

        internal static void ApplyFullDocumentHeight(TextEditor editor, double width, bool allowWrap)
        {
            if (editor == null) return;

            if (width > 64)
                editor.Width = width;

            editor.WordWrap = allowWrap;
            editor.Measure(new Size(width > 64 ? width : double.PositiveInfinity, double.PositiveInfinity));
            var measureWidth = width > 64 ? width : Math.Max(120, editor.DesiredSize.Width);
            var probeHeight = Math.Max(MinEditorHeight, editor.LineCount * LineHeight + 32);
            editor.Arrange(new Rect(0, 0, measureWidth, probeHeight));
            editor.UpdateLayout();

            var contentHeight = editor.ExtentHeight;
            if (contentHeight <= 0 || double.IsNaN(contentHeight))
                contentHeight = Math.Max(MinEditorHeight, editor.LineCount * LineHeight + 16);

            var height = Math.Ceiling(contentHeight + editor.Padding.Top + editor.Padding.Bottom + 8);
            editor.Height = Math.Max(MinEditorHeight, height);
            editor.Arrange(new Rect(0, 0, measureWidth, editor.Height));
            editor.UpdateLayout();
        }

        private void AttachContextMenu()
        {
            var menu = new ContextMenu();
            var copy = new MenuItem { Header = "复制" };
            copy.Click += (s, e) => CopySelectionOrAll();
            var selectAll = new MenuItem { Header = "全选" };
            selectAll.Click += (s, e) => SelectAll();
            menu.Items.Add(copy);
            menu.Items.Add(selectAll);
            ContextMenu = menu;
        }
    }
}
