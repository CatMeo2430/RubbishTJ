using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Diagnostics;
using Taiji.Engine.Markdown;
using Taiji.Engine.Theme;
using WpfBlock = System.Windows.Documents.Block;

namespace Taiji.Engine.Render
{
    /// <summary>
    /// SSE 流式：先纯文本追写；CompleteAsync 走完整渲染管线后挂载正文。
    /// </summary>
    public sealed class StreamRenderSession
    {
        private readonly RenderEngine _engine;
        private readonly RenderRole _role;
        private readonly Section _section;
        private readonly Paragraph _bodyParagraph;
        private readonly Run _bodyRun;
        private string _buffer = "";
        private bool _completed;
        private bool _thinking;

        internal StreamRenderSession(RenderEngine engine, RenderRole role, Section section, Paragraph bodyParagraph, Run bodyRun)
        {
            _engine = engine;
            _role = role;
            _section = section;
            _bodyParagraph = bodyParagraph;
            _bodyRun = bodyRun;
        }

        public Section Section { get { return _section; } }
        public string Buffer { get { return _buffer; } }
        public bool IsCompleted { get { return _completed; } }
        public bool IsThinking { get { return _thinking; } }

        public void ShowThinking(string tip)
        {
            if (_completed) return;
            _thinking = true;
            _buffer = "";
            _bodyRun.Text = tip ?? "思考中......";
            _bodyRun.FontStyle = FontStyles.Italic;
            _bodyRun.Foreground = DraculaTheme.CyanBrush;
        }

        public void ClearThinking()
        {
            if (!_thinking) return;
            _thinking = false;
            _bodyRun.FontStyle = FontStyles.Normal;
            _bodyRun.Foreground = DraculaTheme.ForegroundBrush;
            _bodyRun.Text = _buffer ?? "";
        }

        public void Append(string chunk)
        {
            if (_completed || string.IsNullOrEmpty(chunk)) return;
            if (_thinking)
                ClearThinking();
            _buffer = _buffer + chunk;
            _bodyRun.Text = _buffer;
        }

        public RenderResult Complete()
        {
            return CompleteAsync().GetAwaiter().GetResult();
        }

        public async Task<RenderResult> CompleteAsync()
        {
            if (_completed)
                return new RenderResult("stream", _section);
            _completed = true;
            _thinking = false;

            var text = _buffer ?? "";
            if (_bodyParagraph != null && _section.Blocks.Contains(_bodyParagraph))
                _section.Blocks.Remove(_bodyParagraph);

            if (text.Length == 0)
                return new RenderResult("stream", _section);

            Paragraph renderingHint = null;
            var dispatcher = Application.Current != null ? Application.Current.Dispatcher : null;
            if (dispatcher != null && dispatcher.CheckAccess())
            {
                renderingHint = new Paragraph(new Run("正在渲染…")
                {
                    FontStyle = FontStyles.Italic,
                    Foreground = DraculaTheme.CyanBrush
                }) { Margin = new Thickness(0) };
                _section.Blocks.Add(renderingHint);
            }

            IList<WpfBlock> body;
            string rendererId;
            try
            {
                var request = new RenderRequest(_role, text);
                var pair = await _engine.RenderBodyAsync(request).ConfigureAwait(true);
                body = pair.Item1;
                rendererId = pair.Item2;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Render] WARN: 流式 Complete 失败: " + ex.Message);
                body = new List<WpfBlock> { new Paragraph(new Run(text)) };
                rendererId = "plain";
            }
            finally
            {
                if (renderingHint != null && _section.Blocks.Contains(renderingHint))
                    _section.Blocks.Remove(renderingHint);
            }

            foreach (var block in body)
                _section.Blocks.Add(block);

            Debug.WriteLine("[Render] 流式完成 → " + rendererId + " (" + text.Length + " chars)");
            return new RenderResult(rendererId, _section);
        }
    }
}
