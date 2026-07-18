using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Taiji.Engine.Code;
using Taiji.Engine.Latex;
using Taiji.Engine.Theme;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdInline = Markdig.Syntax.Inlines.Inline;
using WpfBlock = System.Windows.Documents.Block;
using WpfTable = System.Windows.Documents.Table;
using WpfTableRow = System.Windows.Documents.TableRow;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableColumn = System.Windows.Documents.TableColumn;
using WpfTableRowGroup = System.Windows.Documents.TableRowGroup;
using WpfParagraph = System.Windows.Documents.Paragraph;
using WpfRun = System.Windows.Documents.Run;
using WpfSpan = System.Windows.Documents.Span;
using WpfHyperlink = System.Windows.Documents.Hyperlink;
using WpfLineBreak = System.Windows.Documents.LineBreak;
using WpfSection = System.Windows.Documents.Section;
using WpfBlockUIContainer = System.Windows.Documents.BlockUIContainer;

namespace Taiji.Engine.Markdown
{
    /// <summary>Markdig AST → FlowDocument Blocks（代码 AvalonEdit，公式 RaTeX）。</summary>
    public sealed class MarkdigFlowConverter
    {
        private bool _forExport;

        public static MarkdownDocument ParseDocument(string markdown)
        {
            var normalized = LatexNormalizer.Normalize(markdown ?? "");
            return Markdig.Markdown.Parse(normalized, MarkdownPipelineFactory.Shared);
        }

        public IList<WpfBlock> Convert(MarkdownDocument document, bool forExport = false)
        {
            _forExport = forExport;
            var list = new List<WpfBlock>();
            if (document == null) return list;
            foreach (var block in document)
                WriteBlock(block, list);
            return list;
        }

        public IList<WpfBlock> Convert(string markdown, bool forExport = false)
        {
            _forExport = forExport;
            var normalized = LatexNormalizer.Normalize(markdown ?? "");
            var doc = Markdig.Markdown.Parse(normalized, MarkdownPipelineFactory.Shared);
            return Convert(doc, forExport);
        }

        private void WriteBlock(MarkdownObject block, IList<WpfBlock> output)
        {
            if (block is HeadingBlock)
            {
                WriteHeading((HeadingBlock)block, output);
            }
            else if (block is ParagraphBlock)
            {
                WriteParagraph((ParagraphBlock)block, output);
            }
            else if (block is FencedCodeBlock)
            {
                WriteCode((FencedCodeBlock)block, output);
            }
            else if (block is CodeBlock)
            {
                WriteIndentedCode((CodeBlock)block, output);
            }
            else if (block is QuoteBlock)
            {
                WriteQuote((QuoteBlock)block, output);
            }
            else if (block is ListBlock)
            {
                WriteList((ListBlock)block, output);
            }
            else if (block is ThematicBreakBlock)
            {
                output.Add(new WpfBlockUIContainer(new System.Windows.Controls.Border
                {
                    BorderBrush = DraculaTheme.CommentBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 10, 0, 10),
                    Height = 1
                }));
            }
            else if (block is MathBlock)
            {
                var mb = (MathBlock)block;
                var latex = mb.Lines.ToString() ?? "";
                output.Add(LatexViewFactory.CreateBlock(latex.Trim()));
            }
            else if (block is MdTable)
            {
                WriteTable((MdTable)block, output);
            }
            else if (block is HtmlBlock)
            {
            }
            else if (block is ContainerBlock)
            {
                foreach (var child in (ContainerBlock)block)
                    WriteBlock(child, output);
            }
        }

        private void WriteHeading(HeadingBlock h, IList<WpfBlock> output)
        {
            var p = new WpfParagraph { Margin = new Thickness(0, 10, 0, 6) };
            Brush fg = DraculaTheme.ForegroundBrush;
            double size = 20;
            switch (h.Level)
            {
                case 1: fg = DraculaTheme.BlueBrush; size = 22; break;
                case 2: fg = DraculaTheme.CyanBrush; size = 18; break;
                case 3: fg = DraculaTheme.ForegroundBrush; size = 16; break;
                case 4: fg = DraculaTheme.CommentBrush; size = 14; break;
                case 5: fg = DraculaTheme.CommentBrush; size = 13; break;
                default: fg = DraculaTheme.CommentBrush; size = 12; break;
            }
            p.FontSize = size;
            p.FontWeight = FontWeights.SemiBold;
            p.Foreground = fg;
            if (h.Inline != null)
                WriteInlines(h.Inline, p);
            output.Add(p);
        }

        private void WriteParagraph(ParagraphBlock block, IList<WpfBlock> output)
        {
            var p = new WpfParagraph
            {
                Margin = new Thickness(0, 2, 0, 6),
                Foreground = DraculaTheme.ForegroundBrush,
                LineHeight = 22
            };
            if (block.Inline != null)
                WriteInlines(block.Inline, p);
            output.Add(p);
        }

        private void WriteCode(FencedCodeBlock block, IList<WpfBlock> output)
        {
            var code = block.Lines.ToString() ?? "";
            var lang = block.Info ?? "";
            if (LatexNormalizer.LooksLikeLatexDocument(lang, code))
            {
                output.Add(LatexViewFactory.CreateBlock(code));
                return;
            }
            output.Add(new WpfBlockUIContainer(CodeBlockViewFactory.Create(code, lang, _forExport))
            {
                Margin = new Thickness(0, 4, 0, 4)
            });
        }

        private void WriteIndentedCode(CodeBlock block, IList<WpfBlock> output)
        {
            var code = block.Lines.ToString() ?? "";
            if (LatexNormalizer.LooksLikeLatexDocument("", code))
            {
                output.Add(LatexViewFactory.CreateBlock(code));
                return;
            }
            output.Add(new WpfBlockUIContainer(CodeBlockViewFactory.Create(code, "", _forExport))
            {
                Margin = new Thickness(0, 4, 0, 4)
            });
        }

        private void WriteQuote(QuoteBlock quote, IList<WpfBlock> output)
        {
            var section = new WpfSection
            {
                BorderBrush = DraculaTheme.CommentBrush,
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(10, 2, 4, 2),
                Margin = new Thickness(0, 4, 0, 6),
                Foreground = DraculaTheme.CommentBrush
            };
            var nested = new List<WpfBlock>();
            foreach (var child in quote)
                WriteBlock(child, nested);
            foreach (var b in nested)
            {
                ApplyQuoteTint(b);
                section.Blocks.Add(b);
            }
            output.Add(section);
        }

        private static void ApplyQuoteTint(WpfBlock b)
        {
            if (b is WpfParagraph p)
            {
                p.Foreground = DraculaTheme.CommentBrush;
                p.FontStyle = FontStyles.Italic;
            }
        }

        private void WriteList(ListBlock list, IList<WpfBlock> output)
        {
            var index = 1;
            foreach (ListItemBlock item in list)
            {
                var prefix = list.IsOrdered ? $"{index}. " : "• ";
                var itemBlocks = new List<WpfBlock>();
                foreach (var child in item)
                    WriteBlock(child, itemBlocks);

                if (itemBlocks.Count == 0)
                {
                    var p = new WpfParagraph(new WpfRun(prefix) { Foreground = DraculaTheme.CyanBrush })
                    {
                        Margin = new Thickness(8, 1, 0, 1),
                        Foreground = DraculaTheme.ForegroundBrush
                    };
                    output.Add(p);
                }
                else
                {
                    if (itemBlocks[0] is WpfParagraph first)
                    {
                        var prefixRun = new WpfRun(prefix) { Foreground = DraculaTheme.CyanBrush };
                        if (first.Inlines.FirstInline != null)
                            first.Inlines.InsertBefore(first.Inlines.FirstInline, prefixRun);
                        else
                            first.Inlines.Add(prefixRun);
                        first.Margin = new Thickness(8, 1, 0, 1);
                        output.Add(first);
                        for (var i = 1; i < itemBlocks.Count; i++)
                            output.Add(itemBlocks[i]);
                    }
                    else
                    {
                        foreach (var b in itemBlocks)
                            output.Add(b);
                    }
                }
                index++;
            }
        }

        private void WriteTable(MdTable table, IList<WpfBlock> output)
        {
            var wpf = new WpfTable
            {
                CellSpacing = 0,
                BorderBrush = DraculaTheme.CurrentLineBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 6, 0, 8)
            };
            var colCount = 0;
            foreach (MdTableRow row in table)
            {
                if (row.Count > colCount) colCount = row.Count;
            }
            for (var i = 0; i < colCount; i++)
                wpf.Columns.Add(new WpfTableColumn());

            var headerGroup = new WpfTableRowGroup();
            var bodyGroup = new WpfTableRowGroup();
            var rowIndex = 0;
            foreach (MdTableRow mdRow in table)
            {
                var tr = new WpfTableRow();
                foreach (MdTableCell mdCell in mdRow)
                {
                    var cell = new WpfTableCell
                    {
                        BorderBrush = DraculaTheme.CurrentLineBrush,
                        BorderThickness = new Thickness(0.5),
                        Padding = new Thickness(6, 4, 6, 4),
                        Background = rowIndex == 0 ? DraculaTheme.CurrentLineBrush : Brushes.Transparent
                    };
                    var cellBlocks = new List<WpfBlock>();
                    foreach (var child in mdCell)
                        WriteBlock(child, cellBlocks);
                    if (cellBlocks.Count == 0)
                        cell.Blocks.Add(new WpfParagraph());
                    else
                    {
                        foreach (var b in cellBlocks)
                            cell.Blocks.Add(b);
                    }
                    if (rowIndex == 0)
                        cell.Foreground = DraculaTheme.BlueBrush;
                    tr.Cells.Add(cell);
                }
                if (rowIndex == 0) headerGroup.Rows.Add(tr);
                else bodyGroup.Rows.Add(tr);
                rowIndex++;
            }
            if (headerGroup.Rows.Count > 0) wpf.RowGroups.Add(headerGroup);
            if (bodyGroup.Rows.Count > 0) wpf.RowGroups.Add(bodyGroup);
            output.Add(wpf);
        }

        private void WriteInlines(ContainerInline container, WpfParagraph target)
        {
            for (var inline = container.FirstChild; inline != null; inline = inline.NextSibling)
                WriteInline(inline, target.Inlines);
        }

        private void WriteInline(MdInline inline, InlineCollection target)
        {
            if (inline is LiteralInline)
            {
                target.Add(new WpfRun(((LiteralInline)inline).Content.ToString())
                {
                    Foreground = DraculaTheme.ForegroundBrush
                });
            }
            else if (inline is CodeInline)
            {
                target.Add(new WpfRun(((CodeInline)inline).Content)
                {
                    FontFamily = DraculaTheme.MonoFont,
                    Foreground = DraculaTheme.YellowBrush,
                    Background = DraculaTheme.CurrentLineBrush
                });
            }
            else if (inline is EmphasisInline)
            {
                var em = (EmphasisInline)inline;
                var span = new WpfSpan();
                if (em.DelimiterCount >= 2)
                    span.FontWeight = FontWeights.Bold;
                else
                    span.FontStyle = FontStyles.Italic;
                span.Foreground = DraculaTheme.ForegroundBrush;
                for (var c = em.FirstChild; c != null; c = c.NextSibling)
                    WriteInline(c, span.Inlines);
                target.Add(span);
            }
            else if (inline is LinkInline)
            {
                var link = (LinkInline)inline;
                if (link.IsImage)
                {
                    target.Add(new WpfRun($"[图片] {link.Url ?? ""}")
                    {
                        Foreground = DraculaTheme.CyanBrush,
                        FontStyle = FontStyles.Italic
                    });
                }
                else
                {
                    var hyper = new WpfHyperlink
                    {
                        NavigateUri = TryUri(link.Url),
                        Foreground = DraculaTheme.CyanBrush
                    };
                    hyper.RequestNavigate += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
                            e.Handled = true;
                        }
                        catch { }
                    };
                    for (var c = link.FirstChild; c != null; c = c.NextSibling)
                        WriteInline(c, hyper.Inlines);
                    if (hyper.Inlines.Count == 0)
                        hyper.Inlines.Add(new WpfRun(link.Url ?? ""));
                    target.Add(hyper);
                }
            }
            else if (inline is LineBreakInline)
            {
                target.Add(new WpfLineBreak());
            }
            else if (inline is MathInline)
            {
                target.Add(LatexViewFactory.CreateInline(((MathInline)inline).Content.ToString() ?? ""));
            }
            else if (inline is HtmlInline || inline is HtmlEntityInline)
            {
            }
            else if (inline is ContainerInline)
            {
                for (var c = ((ContainerInline)inline).FirstChild; c != null; c = c.NextSibling)
                    WriteInline(c, target);
            }
        }

        private static Uri TryUri(string url)
        {
            Uri u;
            if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out u))
                return u;
            return null;
        }
    }
}
