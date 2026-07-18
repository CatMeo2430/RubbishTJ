using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Taiji.Engine.Utils;
using Taiji.Engine.Render;

namespace Taiji.Engine
{
    /// <summary>AI 消息气泡工具栏：复制原文、导出为 PNG（不含工具栏）。</summary>
    public sealed class MessageBubbleToolbar : StackPanel
    {
        private readonly Func<string> _getSourceText;

        public MessageBubbleToolbar(Func<string> getSourceText)
        {
            _getSourceText = getSourceText ?? (() => "");
            Orientation = Orientation.Horizontal;
            HorizontalAlignment = HorizontalAlignment.Right;
            Margin = new Thickness(0, 0, 0, 6);

            Children.Add(RenderToolbar.CreateButton("复制", OnCopyClick));
            Children.Add(RenderToolbar.CreateButton("导出", OnExportClick));
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            var text = _getSourceText() ?? "";
            if (text.Length == 0) return;
            Clipboard.SetText(text);
        }

        private void OnExportClick(object sender, RoutedEventArgs e)
        {
            var text = _getSourceText() ?? "";
            if (text.Length == 0) return;

            try
            {
                var owner = Window.GetWindow(this);
                var pageWidth = VisualExportHelper.ResolvePageWidth(this);
                VisualExportHelper.PromptSaveDocumentPng(
                    BuildExportDocument(text, pageWidth),
                    "ai-message.png",
                    owner);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "导出 PNG 失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        internal static FlowDocument BuildExportDocument(string sourceText, double pageWidth)
        {
            var engine = new RenderEngine();
            var (body, rendererId) = engine.RenderBody(new RenderRequest(RenderRole.Ai, sourceText));

            var doc = VisualExportHelper.CreateExportDocument(pageWidth);
            var shell = RenderEngine.CreateBubbleShell(RenderRole.Ai);
            if (body != null)
            {
                foreach (var block in body)
                    shell.Blocks.Add(block);
            }
            doc.Blocks.Add(shell);
            return doc;
        }
    }
}
