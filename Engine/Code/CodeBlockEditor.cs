using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace Taiji.Engine.Code
{
    /// <summary>只读代码块编辑器：支持局部选中与 Ctrl+C 复制。</summary>
    public sealed class CodeBlockEditor : Border
    {
        public CodeBlockEditor(string code, string language)
        {
            Code = code ?? "";
            CodeLanguage = language ?? "";
            Child = Editor = CreateEditor(Code, CodeLanguage);
            AttachContextMenu();
        }

        public string Code { get; private set; }
        public string CodeLanguage { get; private set; }
        public TextEditor Editor { get; private set; }

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

        private TextEditor CreateEditor(string code, string language)
        {
            if (code.EndsWith("\r\n")) code = code.Substring(0, code.Length - 2);
            else if (code.EndsWith("\n")) code = code.Substring(0, code.Length - 1);

            var editor = new TextEditor
            {
                IsReadOnly = true,
                IsEnabled = true,
                Focusable = true,
                ShowLineNumbers = true,
                WordWrap = true,
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

            var lines = System.Math.Max(1, editor.LineCount);
            editor.Height = System.Math.Min(420, System.Math.Max(48, lines * 17 + 16));
            editor.MinWidth = 120;

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

            return editor;
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
