using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Code
{
    /// <summary>用 AvalonEdit 渲染带语法高亮的代码块（可选中、可复制）。</summary>
    public static class CodeBlockViewFactory
    {
        public static FrameworkElement Create(string code, string language)
        {
            return new CodeBlockView(code, language);
        }

        internal static void BubbleWheelToChat(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            if (!(sender is DependencyObject src)) return;

            RichTextBox chat = null;
            var walk = src;
            while (walk != null)
            {
                if (walk is RichTextBox box)
                {
                    chat = box;
                    break;
                }
                var parent = VisualTreeHelper.GetParent(walk);
                if (parent == null)
                    parent = LogicalTreeHelper.GetParent(walk);
                walk = parent;
            }
            if (chat == null) return;

            e.Handled = true;
            var steps = System.Math.Max(1, System.Math.Abs(e.Delta) / 40);
            if (e.Delta > 0)
            {
                for (var i = 0; i < steps; i++)
                    chat.LineUp();
            }
            else
            {
                for (var i = 0; i < steps; i++)
                    chat.LineDown();
            }
        }

        internal static IHighlightingDefinition ResolveHighlighting(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return null;
            language = language.Trim().ToLowerInvariant();

            if (language == "c#" || language == "csharp" || language == "cs")
                language = "C#";
            else if (language == "js" || language == "javascript" || language == "node")
                language = "JavaScript";
            else if (language == "ts" || language == "typescript")
                language = "JavaScript";
            else if (language == "py" || language == "python")
                language = "Python";
            else if (language == "rb" || language == "ruby")
                language = "Ruby";
            else if (language == "ps" || language == "ps1" || language == "powershell")
                language = "PowerShell";
            else if (language == "sh" || language == "bash" || language == "shell" || language == "zsh")
                language = "JavaScript";
            else if (language == "json")
                language = "JavaScript";
            else if (language == "xml" || language == "html" || language == "htm" || language == "xaml")
                language = "XML";
            else if (language == "css")
                language = "CSS";
            else if (language == "sql")
                language = "SQL";
            else if (language == "java")
                language = "Java";
            else if (language == "cpp" || language == "c++" || language == "c")
                language = "C++";
            else if (language == "php")
                language = "PHP";
            else if (language == "vb" || language == "vbnet")
                language = "VB";
            else if (language == "patch" || language == "diff")
                language = "Patch";
            else if (language.Length > 0)
                language = $"{char.ToUpperInvariant(language[0])}{language.Substring(1)}";

            try
            {
                return HighlightingManager.Instance.GetDefinition(language);
            }
            catch
            {
                return null;
            }
        }
    }
}
