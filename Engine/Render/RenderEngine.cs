using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using Taiji.Engine.Builtin;
using Taiji.Engine.Markdown;
using Taiji.Engine.Theme;

namespace Taiji.Engine.Render
{
    /// <summary>
    /// 统一渲染入口：纯文本、Markdown、代码块、LaTeX 均由 Engine 处理，GUI 只挂载 <see cref="RenderResult.Root"/>。
    /// </summary>
    public sealed class RenderEngine
    {
        private readonly List<IContentRenderer> _renderers = new List<IContentRenderer>();

        private static readonly Brush UserBg = Freeze(new SolidColorBrush(Color.FromRgb(0x2F, 0x48, 0x6A)));
        private static readonly Brush UserBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x61, 0xAF, 0xEF)));
        private static readonly Brush UserFg = DraculaTheme.CyanBrush;
        private static readonly Brush AiBg = Freeze(new SolidColorBrush(Color.FromRgb(0x21, 0x25, 0x2B)));
        private static readonly Brush AiBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x98, 0xC3, 0x79)));
        private static readonly Brush AiFg = DraculaTheme.GreenBrush;
        private static readonly Brush ErrBg = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x2E)));
        private static readonly Brush ErrBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xE0, 0x6C, 0x75)));
        private static readonly Brush ErrFg = DraculaTheme.RedBrush;
        private static readonly Brush BodyFg = DraculaTheme.ForegroundBrush;

        public RenderEngine()
        {
            Register(new SystemContentRenderer());
            Register(new ErrorContentRenderer());
            Register(new MarkdownContentRenderer());
            Register(new PlainContentRenderer());
        }

        public IEnumerable<IContentRenderer> Renderers
        {
            get { return _renderers; }
        }

        public void Register(IContentRenderer renderer)
        {
            if (renderer == null) throw new ArgumentNullException("renderer");
            _renderers.RemoveAll(r => string.Equals(r.Id, renderer.Id, StringComparison.OrdinalIgnoreCase));
            _renderers.Add(renderer);
            _renderers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            Debug.WriteLine("[Render] 注册 " + renderer.Id + " priority=" + renderer.Priority);
        }

        /// <summary>渲染单条消息，返回可直接加入 FlowDocument 的根 Block。</summary>
        public RenderResult Render(RenderRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");

            IList<Block> body;
            string id;
            RenderBody(request, out body, out id);

            var root = WrapRoot(request.Role, body, request.Content);
            Debug.WriteLine("[Render] Render " + request.Role + " via " + id);
            return new RenderResult(id, root);
        }

        public RenderResult Render(RenderRole role, string content, string languageHint = null)
        {
            return Render(new RenderRequest(role, content, languageHint));
        }

        /// <summary>便捷方法：直接返回 WPF 根 Block。</summary>
        public Block RenderBlock(RenderRole role, string content, string languageHint = null)
        {
            return Render(role, content, languageHint).Root;
        }

        /// <summary>批量渲染完整对话文档。</summary>
        public FlowDocument BuildDocument(IEnumerable<Tuple<RenderRole, string>> messages)
        {
            var doc = new FlowDocument
            {
                Background = DraculaTheme.BackgroundBrush,
                Foreground = BodyFg,
                FontFamily = DraculaTheme.UiFont,
                FontSize = 13.5,
                PagePadding = new Thickness(0),
                LineHeight = 22
            };
            if (messages == null) return doc;
            foreach (var m in messages)
            {
                var result = Render(m.Item1, m.Item2);
                if (result.Root != null)
                    doc.Blocks.Add(result.Root);
            }
            return doc;
        }

        public StreamRenderSession BeginStream(RenderRole role)
        {
            if (role != RenderRole.Ai && role != RenderRole.User)
                throw new ArgumentException("流式仅支持 User/Ai", "role");

            var section = CreateBubbleShell(role);
            var run = new Run("") { Foreground = BodyFg };
            var body = new Paragraph(run) { Margin = new Thickness(0) };
            section.Blocks.Add(body);
            var session = new StreamRenderSession(this, role, section, body, run);
            if (role == RenderRole.Ai)
                AttachAiToolbar(section, () => session.Buffer);
            return session;
        }

        internal void RenderBody(RenderRequest request, out IList<Block> body, out string rendererId)
        {
            var renderer = Select(request);
            rendererId = renderer.Id;
            try
            {
                body = renderer.RenderBody(request) ?? new List<Block>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Render] WARN: " + renderer.Id + " 失败: " + ex.Message);
                body = new PlainContentRenderer().RenderBody(request);
                rendererId = "plain";
            }
            if (body.Count == 0)
            {
                body = new List<Block>
                {
                    new Paragraph(new Run(request.Content ?? "") { Foreground = BodyFg })
                };
            }
        }

        internal async Task<Tuple<IList<Block>, string>> RenderBodyAsync(RenderRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");

            var renderer = Select(request);
            var md = renderer as MarkdownContentRenderer;
            var dispatcher = Application.Current != null ? Application.Current.Dispatcher : null;

            if (md != null && dispatcher != null)
            {
                Markdig.Syntax.MarkdownDocument parsed = null;
                try
                {
                    parsed = await Task.Run(() =>
                        MarkdigFlowConverter.ParseDocument(request.Content ?? "")).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Render] WARN: Markdig 解析失败: " + ex.Message);
                }

                return await dispatcher.InvokeAsync(() =>
                {
                    IList<Block> body;
                    string rendererId = renderer.Id;
                    try
                    {
                        body = parsed != null
                            ? md.RenderBody(request, parsed)
                            : renderer.RenderBody(request) ?? new List<Block>();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Render] WARN: " + renderer.Id + " 失败: " + ex.Message);
                        body = new PlainContentRenderer().RenderBody(request);
                        rendererId = "plain";
                    }
                    if (body.Count == 0)
                    {
                        body = new List<Block>
                        {
                            new Paragraph(new Run(request.Content ?? "") { Foreground = BodyFg })
                        };
                    }
                    return Tuple.Create(body, rendererId);
                }, DispatcherPriority.Background);
            }

            if (dispatcher != null)
            {
                return await dispatcher.InvokeAsync(() =>
                {
                    IList<Block> body;
                    string id;
                    RenderBody(request, out body, out id);
                    return Tuple.Create(body, id);
                }, DispatcherPriority.Background);
            }

            IList<Block> syncBody;
            string syncId;
            RenderBody(request, out syncBody, out syncId);
            return Tuple.Create(syncBody, syncId);
        }

        private IContentRenderer Select(RenderRequest request)
        {
            foreach (var r in _renderers)
            {
                if (r.CanHandle(request))
                    return r;
            }
            return new PlainContentRenderer();
        }

        private static Block WrapRoot(RenderRole role, IList<Block> body, string sourceText)
        {
            if (body == null || body.Count == 0)
                return new Paragraph();

            if (role == RenderRole.System)
                return body[0];

            var section = CreateBubbleShell(role);
            foreach (var block in body)
                section.Blocks.Add(block);
            if (role == RenderRole.Ai)
                AttachAiToolbar(section, () => sourceText ?? "");
            return section;
        }

        internal static void AttachAiToolbar(Section section, Func<string> getSourceText)
        {
            if (section == null || getSourceText == null) return;

            var toolbar = new BlockUIContainer(new MessageBubbleToolbar(getSourceText));
            var blocks = new List<Block>();
            foreach (Block block in section.Blocks)
                blocks.Add(block);

            for (var i = blocks.Count - 1; i >= 1; i--)
                section.Blocks.Remove(blocks[i]);

            section.Blocks.Add(toolbar);
            for (var i = 1; i < blocks.Count; i++)
                section.Blocks.Add(blocks[i]);
        }

        internal static Section CreateBubbleShell(RenderRole role)
        {
            Brush bg;
            Brush border;
            Brush labelFg;
            string label;
            switch (role)
            {
                case RenderRole.User:
                    bg = UserBg;
                    border = UserBorder;
                    labelFg = UserFg;
                    label = "You";
                    break;
                case RenderRole.Error:
                    bg = ErrBg;
                    border = ErrBorder;
                    labelFg = ErrFg;
                    label = "错误";
                    break;
                default:
                    bg = AiBg;
                    border = AiBorder;
                    labelFg = AiFg;
                    label = "AI";
                    break;
            }

            var section = new Section
            {
                Background = bg,
                BorderBrush = border,
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 8, 0, 8)
            };

            var head = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
            head.Inlines.Add(new Run(label)
            {
                Foreground = labelFg,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            });
            section.Blocks.Add(head);
            return section;
        }

        private static Brush Freeze(SolidColorBrush b)
        {
            if (b.CanFreeze) b.Freeze();
            return b;
        }
    }
}
