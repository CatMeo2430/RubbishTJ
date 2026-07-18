using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Diagnostics;
using Taiji.Engine.Render;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Markdown
{
    /// <summary>AI / Markdown → FlowDocument 正文（Markdig + AvalonEdit + RaTeX）。</summary>
    public sealed class MarkdownContentRenderer : IContentRenderer
    {
        private readonly MarkdigFlowConverter _converter = new MarkdigFlowConverter();

        public string Id => "markdown";
        public string DisplayName => "Markdown (Markdig)";
        public int Priority => 10;

        public bool CanHandle(RenderRequest request)
        {
            if (request == null) return false;
            if (request.Role == RenderRole.System || request.Role == RenderRole.Error)
                return false;
            if (string.Equals(request.LanguageHint, "plain", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(request.LanguageHint, "markdown", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.LanguageHint, "md", StringComparison.OrdinalIgnoreCase))
                return true;

            var t = request.Content;
            if (string.IsNullOrEmpty(t)) return false;
            return t.Contains("```")
                || t.Contains("**")
                || t.Contains("`")
                || t.Contains("$")
                || t.Contains("\\[")
                || t.Contains("\\(")
                || Regex.IsMatch(t, @"^#{1,6}\s", RegexOptions.Multiline)
                || Regex.IsMatch(t, @"^\s*[-*]\s+", RegexOptions.Multiline);
        }

        public IList<Block> RenderBody(RenderRequest request)
        {
            var text = request != null ? request.Content : "";
            try
            {
                return _converter.Convert(text, request != null && request.ForExport);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Render] WARN: Markdig 渲染失败: {ex.Message}");
                return new List<Block>
                {
                    new Paragraph(new Run(text ?? "")
                    {
                        Foreground = DraculaTheme.ForegroundBrush
                    })
                };
            }
        }

        public IList<Block> RenderBody(RenderRequest request, Markdig.Syntax.MarkdownDocument document)
        {
            var text = request != null ? request.Content : "";
            try
            {
                return _converter.Convert(document, request != null && request.ForExport);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Render] WARN: Markdig 渲染失败: {ex.Message}");
                return new List<Block>
                {
                    new Paragraph(new Run(text ?? "")
                    {
                        Foreground = DraculaTheme.ForegroundBrush
                    })
                };
            }
        }
    }
}
