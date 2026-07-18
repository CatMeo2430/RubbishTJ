using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Taiji.Core;
using Taiji.Core.Models;
using Taiji.Core.Utils;
using Taiji.Engine.Render;

namespace Taiji.GUI
{
    public partial class MainWindow : Window
    {
        private enum EdgePanel { None, Top, Bottom, Left, Right }

        private readonly ITaijiCore _api = new TaijiCore();
        private readonly RenderEngine _render = new RenderEngine();
        private readonly ObservableCollection<ChatSessionInfo> _sessions = new ObservableCollection<ChatSessionInfo>();
        private readonly ObservableCollection<OutlineItem> _outline = new ObservableCollection<OutlineItem>();

        private readonly List<string> _pendingImages = new List<string>();
        private CancellationTokenSource _cts;
        private bool _busy;
        private bool _suppressSessionSelect;
        private bool _suppressOutlineSelect;
        private bool _loadingHistory;
        private int _historyLoadGen;

        private int _sessionPageLoaded;
        private int _sessionTotalPages = 1;
        private bool _loadingMoreSessions;

        private StreamRenderSession _aiStream;
        private OutlineItem _streamingOutline;
        private int _sseChunkCount;

        private readonly object _chunkLock = new object();
        private readonly StringBuilder _smoothQueue = new StringBuilder();
        private readonly DispatcherTimer _smoothTimer;
        private bool _streamEnded;
        private bool _scrollChatEndQueued;

        private EdgePanel _openPanel = EdgePanel.None;
        private readonly DispatcherTimer _hideTimer;
        private const double AnimMs = 220;
        private const double HotBand = 12;
        private const double CornerDead = 56;
        private Thickness _edgeInset = new Thickness(0);
        private Rect _restoreBounds;
        private bool _filledToWorkArea = true;
        private int _holdRightPanel;

        public MainWindow()
        {
            InitializeComponent();
            SessionList.ItemsSource = _sessions;
            OutlineList.ItemsSource = _outline;
            SessionList.AddHandler(ScrollViewer.ScrollChangedEvent,
                new ScrollChangedEventHandler(SessionList_OnScrollChanged), true);
            Title = $"Taiji  ·  {Constant.AppVersion}";
            ChatBox.Document.Blocks.Clear();
            ChatBox.SizeChanged += ChatBox_OnSizeChanged;
            ChatBox.Loaded += (s, e) =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(SyncChatPageWidth));
            };
            RootGrid.PreviewMouseMove += RootGrid_OnPreviewMouseMove;
            RootGrid.PreviewMouseLeftButtonDown += RootGrid_OnPreviewMouseLeftButtonDown;

            SourceInitialized += (s, e) =>
            {
                MonitorWorkArea.AttachMaximiseHook(this);
                MonitorWorkArea.FillWindow(this);
                _filledToWorkArea = true;
                RefreshEdgeInsets();
            };
            SizeChanged += (s, e) => RefreshEdgeInsets();
            LocationChanged += (s, e) => RefreshEdgeInsets();
            StateChanged += (s, e) =>
            {
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                    MonitorWorkArea.FillWindow(this);
                    _filledToWorkArea = true;
                }
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshEdgeInsets));
            };

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _hideTimer.Tick += HideTimer_OnTick;

            _smoothTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _smoothTimer.Tick += SmoothTimer_OnTick;

            Loaded += MainWindow_OnLoaded;
            Closed += (s, e) =>
            {
                _hideTimer.Stop();
                _smoothTimer.Stop();
                _cts?.Cancel();
                _api.Dispose();
            };
        }

        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SetStatus("验证登录…");
                var login = await _api.EnsureAuthenticatedAsync(PromptLoginAsync).ConfigureAwait(true);
                var nick = login.User != null && !string.IsNullOrEmpty(login.User.Nickname)
                    ? login.User.Nickname
                    : (login.User != null ? login.User.Id.ToString() : "用户");
                AppendSys($"已登录: {nick}");

                SetStatus("拉取历史会话…");
                await ResetSessionsToFirstPageAsync(null).ConfigureAwait(true);

                if (_sessions.Count > 0)
                {
                    var first = _sessions[0];
                    _suppressSessionSelect = true;
                    try { SessionList.SelectedItem = first; }
                    finally { _suppressSessionSelect = false; }
                    await OpenSessionHistoryAsync(first).ConfigureAwait(true);
                }

                FillProviders();
                SetBusy(false);
                RefreshEdgeInsets();
                SetStatus("就绪 · 移到边缘打开面板");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("失败");
                Close();
            }
        }

        private Task<LoginCredentials> PromptLoginAsync(LoginPromptInfo hint)
        {
            var tcs = new TaskCompletionSource<LoginCredentials>();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var dlg = new LoginWindow(hint) { Owner = this };
                if (dlg.ShowDialog() == true)
                    tcs.SetResult(dlg.Credentials);
                else
                    tcs.SetResult(LoginCredentials.CancelledResult());
            }));
            return tcs.Task;
        }

        private void RefreshEdgeInsets()
        {
            if (!IsLoaded) return;
            var inset = WorkAreaInsets.Get(this);
            if (inset.Top > 0) inset.Top += 2;
            if (inset.Bottom > 0) inset.Bottom += 2;
            if (inset.Left > 0) inset.Left += 2;
            if (inset.Right > 0) inset.Right += 2;
            _edgeInset = inset;

            HotTop.Height = HotBand;
            HotBottom.Height = HotBand;
            HotLeft.Width = HotBand;
            HotRight.Width = HotBand;
            HotTop.Margin = new Thickness(CornerDead, inset.Top, CornerDead, 0);
            HotBottom.Margin = new Thickness(CornerDead, 0, CornerDead, inset.Bottom);
            HotLeft.Margin = new Thickness(inset.Left, inset.Top + CornerDead, 0, inset.Bottom + CornerDead);
            HotRight.Margin = new Thickness(0, inset.Top + CornerDead, inset.Right, inset.Bottom + CornerDead);

            PanelTop.Margin = new Thickness(0, inset.Top, 0, 0);
            PanelBottom.Margin = new Thickness(0, 0, 0, inset.Bottom);
            PanelLeft.Margin = new Thickness(inset.Left, 0, 0, 0);
            PanelRight.Margin = new Thickness(0, 0, inset.Right, 0);
        }

        private void RootGrid_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_openPanel != EdgePanel.None) return;
            var hit = HitEdgePanel(e.GetPosition(RootGrid));
            if (hit != EdgePanel.None)
                ShowPanel(hit);
        }

        private EdgePanel HitEdgePanel(Point p)
        {
            var w = RootGrid.ActualWidth;
            var h = RootGrid.ActualHeight;
            if (w < 80 || h < 80) return EdgePanel.None;

            var top0 = _edgeInset.Top;
            var bottom0 = h - _edgeInset.Bottom;
            var left0 = _edgeInset.Left;
            var right0 = w - _edgeInset.Right;

            var inTopBand = p.Y >= top0 && p.Y <= top0 + HotBand;
            var inBottomBand = p.Y >= bottom0 - HotBand && p.Y <= bottom0;
            var inLeftBand = p.X >= left0 && p.X <= left0 + HotBand;
            var inRightBand = p.X >= right0 - HotBand && p.X <= right0;

            if ((inTopBand || inBottomBand) && (inLeftBand || inRightBand))
                return EdgePanel.None;

            var xOkForHoriz = p.X >= left0 + CornerDead && p.X <= right0 - CornerDead;
            var yOkForVert = p.Y >= top0 + CornerDead && p.Y <= bottom0 - CornerDead;

            if (inTopBand && xOkForHoriz) return EdgePanel.Top;
            if (inBottomBand && xOkForHoriz) return EdgePanel.Bottom;
            if (inLeftBand && yOkForVert) return EdgePanel.Left;
            if (inRightBand && yOkForVert) return EdgePanel.Right;
            return EdgePanel.None;
        }

        private void HotTop_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (HitEdgePanel(e.GetPosition(RootGrid)) == EdgePanel.Top)
                ShowPanel(EdgePanel.Top);
        }

        private void HotBottom_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (HitEdgePanel(e.GetPosition(RootGrid)) == EdgePanel.Bottom)
                ShowPanel(EdgePanel.Bottom);
        }

        private void HotLeft_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (HitEdgePanel(e.GetPosition(RootGrid)) == EdgePanel.Left)
                ShowPanel(EdgePanel.Left);
        }

        private void HotRight_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (HitEdgePanel(e.GetPosition(RootGrid)) == EdgePanel.Right)
                ShowPanel(EdgePanel.Right);
        }

        private void Panel_OnMouseEnter(object sender, MouseEventArgs e)
        {
            _hideTimer.Stop();
        }

        private void PanelTop_OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (ProviderBox.IsDropDownOpen || ModelBox.IsDropDownOpen) return;
            ScheduleHide(EdgePanel.Top);
        }

        private void PanelLeft_OnMouseLeave(object sender, MouseEventArgs e) { ScheduleHide(EdgePanel.Left); }
        private void PanelRight_OnMouseLeave(object sender, MouseEventArgs e) { ScheduleHide(EdgePanel.Right); }

        private void PanelBottom_OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (InputBox.IsKeyboardFocusWithin) return;
            ScheduleHide(EdgePanel.Bottom);
        }

        private void RootGrid_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_openPanel != EdgePanel.Bottom) return;

            var src = e.OriginalSource as DependencyObject;
            if (IsUnder(src, PanelBottom) || IsUnder(src, HotBottom)) return;

            e.Handled = true;
            DismissBottomPanel();
        }

        private void DismissBottomPanel()
        {
            if (_openPanel != EdgePanel.Bottom) return;
            _hideTimer.Stop();
            if (InputBox.IsKeyboardFocusWithin)
                Keyboard.ClearFocus();
            HidePanel(EdgePanel.Bottom);
        }

        private static bool IsUnder(DependencyObject src, DependencyObject ancestor)
        {
            while (src != null)
            {
                if (ReferenceEquals(src, ancestor)) return true;
                src = GetParentObject(src);
            }
            return false;
        }

        private static DependencyObject GetParentObject(DependencyObject current)
        {
            if (current is Visual)
                return VisualTreeHelper.GetParent(current);

            if (current is FrameworkContentElement fce)
                return fce.Parent;

            return LogicalTreeHelper.GetParent(current);
        }

        private void HoldRightPanel()
        {
            _holdRightPanel++;
            _hideTimer.Stop();
        }

        private void ReleaseRightPanel()
        {
            if (_holdRightPanel > 0)
                _holdRightPanel--;
        }

        private void SessionContextMenu_OnOpened(object sender, RoutedEventArgs e)
        {
            HoldRightPanel();
        }

        private void SessionContextMenu_OnClosed(object sender, RoutedEventArgs e)
        {
            ReleaseRightPanel();
        }

        private void ScheduleHide(EdgePanel panel)
        {
            if (_openPanel != panel) return;
            if (panel == EdgePanel.Right && _holdRightPanel > 0) return;
            _hideTimer.Stop();
            _hideTimer.Tag = panel;
            _hideTimer.Start();
        }

        private void HideTimer_OnTick(object sender, EventArgs e)
        {
            _hideTimer.Stop();
            var panel = _hideTimer.Tag is EdgePanel ? (EdgePanel)_hideTimer.Tag : EdgePanel.None;
            if (_openPanel != panel) return;
            if (panel == EdgePanel.Top && (ProviderBox.IsDropDownOpen || ModelBox.IsDropDownOpen)) return;
            if (panel == EdgePanel.Right && _holdRightPanel > 0) return;
            if (IsPanelUnderMouse(panel)) return;
            HidePanel(panel);
        }

        private bool IsPanelUnderMouse(EdgePanel panel)
        {
            switch (panel)
            {
                case EdgePanel.Top: return PanelTop.IsMouseOver;
                case EdgePanel.Bottom: return PanelBottom.IsMouseOver;
                case EdgePanel.Left: return PanelLeft.IsMouseOver;
                case EdgePanel.Right: return PanelRight.IsMouseOver;
                default: return false;
            }
        }

        private void ShowPanel(EdgePanel panel)
        {
            _hideTimer.Stop();
            if (_openPanel != EdgePanel.None && _openPanel != panel)
                HidePanel(_openPanel, false);
            _openPanel = panel;
            AnimatePanel(panel, true);
        }

        private void HidePanel(EdgePanel panel, bool clear = true)
        {
            AnimatePanel(panel, false);
            if (clear && _openPanel == panel)
                _openPanel = EdgePanel.None;
        }

        private void AnimatePanel(EdgePanel panel, bool open)
        {
            DoubleAnimation anim;
            switch (panel)
            {
                case EdgePanel.Top:
                    PanelTop.IsHitTestVisible = open;
                    HotTop.IsHitTestVisible = !open;
                    anim = new DoubleAnimation(open ? 0 : -86, TimeSpan.FromMilliseconds(AnimMs))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    PanelTopTx.BeginAnimation(TranslateTransform.YProperty, null);
                    PanelTopTx.BeginAnimation(TranslateTransform.YProperty, anim);
                    break;
                case EdgePanel.Bottom:
                    PanelBottom.IsHitTestVisible = open;
                    HotBottom.IsHitTestVisible = !open;
                    anim = new DoubleAnimation(open ? 0 : 180, TimeSpan.FromMilliseconds(AnimMs))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    PanelBottomTx.BeginAnimation(TranslateTransform.YProperty, null);
                    PanelBottomTx.BeginAnimation(TranslateTransform.YProperty, anim);
                    break;
                case EdgePanel.Left:
                    anim = new DoubleAnimation(open ? 0 : -280, TimeSpan.FromMilliseconds(AnimMs))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    PanelLeftTx.BeginAnimation(TranslateTransform.XProperty, null);
                    PanelLeftTx.BeginAnimation(TranslateTransform.XProperty, anim);
                    break;
                case EdgePanel.Right:
                    anim = new DoubleAnimation(open ? 0 : 300, TimeSpan.FromMilliseconds(AnimMs))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    PanelRightTx.BeginAnimation(TranslateTransform.XProperty, null);
                    PanelRightTx.BeginAnimation(TranslateTransform.XProperty, anim);
                    break;
            }
        }

        private void BtnMinimize_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_OnClick(object sender, RoutedEventArgs e)
        {
            if (_filledToWorkArea)
            {
                if (_restoreBounds.Width > 100 && _restoreBounds.Height > 100)
                {
                    WindowState = WindowState.Normal;
                    Left = _restoreBounds.Left;
                    Top = _restoreBounds.Top;
                    Width = _restoreBounds.Width;
                    Height = _restoreBounds.Height;
                }
                _filledToWorkArea = false;
            }
            else
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);
                MonitorWorkArea.FillWindow(this);
                _filledToWorkArea = true;
            }
            RefreshEdgeInsets();
            SyncChatPageWidth();
        }

        private void BtnClose_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Title_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                BtnMaximize_OnClick(sender, e);
                return;
            }
            try { DragMove(); }
            catch { /* ignore */ }
        }

        private void ChatBox_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
                SyncChatPageWidth();
        }

        private void SyncChatPageWidth()
        {
            var doc = ChatBox.Document;
            if (doc == null) return;

            double w = ChatBox.ViewportWidth;
            if (double.IsNaN(w) || w < 40)
            {
                w = ChatBox.ActualWidth
                    - ChatBox.Padding.Left
                    - ChatBox.Padding.Right
                    - SystemParameters.VerticalScrollBarWidth;
            }
            if (double.IsNaN(w) || w < 40) return;

            w = Math.Floor(w);
            if (Math.Abs(doc.PageWidth - w) > 0.5)
                doc.PageWidth = w;
        }

        private void ClearOutline()
        {
            _suppressOutlineSelect = true;
            try
            {
                _outline.Clear();
                OutlineList.SelectedItem = null;
            }
            finally
            {
                _suppressOutlineSelect = false;
            }
            _streamingOutline = null;
        }

        private OutlineItem AddOutline(RenderRole role, string text, Block anchor)
        {
            if (role != RenderRole.User && role != RenderRole.Ai) return null;
            if (anchor == null) return null;
            var label = role == RenderRole.User ? "用户" : "AI";
            Brush accent = Brushes.LightGray;
            try
            {
                accent = (Brush)FindResource(role == RenderRole.User ? "BrushGreen" : "BrushPurple");
            }
            catch { /* keep fallback */ }
            var item = new OutlineItem(label, MakeSnippet(text), anchor, accent);
            _outline.Add(item);
            return item;
        }

        private void RebuildOutline(IList<(RenderRole role, string content)> messages, FlowDocument doc)
        {
            ClearOutline();
            if (messages == null || doc == null) return;
            var blocks = doc.Blocks.ToList();
            var n = Math.Min(messages.Count, blocks.Count);
            for (var i = 0; i < n; i++)
            {
                var (role, content) = messages[i];
                if (role != RenderRole.User && role != RenderRole.Ai) continue;
                AddOutline(role, content, blocks[i]);
            }
        }

        private static string MakeSnippet(string text)
        {
            if (string.IsNullOrEmpty(text)) return "…";
            var one = text.Replace("\r", " ").Replace("\n", " ").Trim();
            while (one.Contains("  ")) one = one.Replace("  ", " ");
            if (one.Length > 36) return $"{one.Substring(0, 36)}…";
            return one;
        }

        private void OutlineList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressOutlineSelect) return;
            if (!(OutlineList.SelectedItem is OutlineItem item) || item.Anchor == null) return;
            JumpToBlock(item.Anchor);
            ScheduleHide(EdgePanel.Left);
        }

        private void JumpToBlock(Block block)
        {
            try
            {
                if (block is FrameworkContentElement fce)
                {
                    fce.BringIntoView();
                    return;
                }
                block.BringIntoView();
            }
            catch { /* ignore */ }
        }

        private async Task ResetSessionsToFirstPageAsync(long? selectId)
        {
            _sessionPageLoaded = 0;
            _sessionTotalPages = 1;
            _suppressSessionSelect = true;
            try
            {
                _sessions.Clear();
            }
            finally
            {
                _suppressSessionSelect = false;
            }
            await LoadNextSessionPageAsync().ConfigureAwait(true);
            if (selectId.HasValue)
                SelectSessionById(selectId.Value);
        }

        private async Task LoadNextSessionPageAsync()
        {
            if (_loadingMoreSessions) return;
            if (_sessionPageLoaded >= _sessionTotalPages && _sessionPageLoaded > 0)
                return;

            _loadingMoreSessions = true;
            try
            {
                var next = _sessionPageLoaded + 1;
                SetStatus($"加载会话第 {next} 页…");
                var page = await _api.ListSessionsPageAsync(next).ConfigureAwait(true);
                _sessionTotalPages = page.Pages > 0 ? page.Pages : 1;
                _sessionPageLoaded = page.Page > 0 ? page.Page : next;

                _suppressSessionSelect = true;
                try
                {
                    foreach (var s in page.Records)
                        _sessions.Add(s);
                }
                finally
                {
                    _suppressSessionSelect = false;
                }

                SetStatus($"会话 {_sessions.Count} 条 · 第 {_sessionPageLoaded}/{_sessionTotalPages} 页");
            }
            finally
            {
                _loadingMoreSessions = false;
            }
        }

        private void SelectSessionById(long id)
        {
            _suppressSessionSelect = true;
            try
            {
                ChatSessionInfo hit = null;
                foreach (var s in _sessions)
                {
                    if (s.Id == id) { hit = s; break; }
                }
                SessionList.SelectedItem = hit;
            }
            finally
            {
                _suppressSessionSelect = false;
            }
        }

        private void SoftSyncCurrentSessionInList()
        {
            var cur = _api.CurrentSession;
            if (cur == null) return;
            foreach (var s in _sessions)
            {
                if (s.Id != cur.Id) continue;
                if (!string.IsNullOrEmpty(cur.Name))
                    s.Name = cur.Name;
                return;
            }
            _suppressSessionSelect = true;
            try
            {
                _sessions.Insert(0, cur);
                SessionList.SelectedItem = cur;
            }
            finally
            {
                _suppressSessionSelect = false;
            }
        }

        private async void SessionList_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_loadingMoreSessions) return;
            if (e.VerticalChange <= 0) return;
            if (e.ExtentHeightChange != 0) return;

            if (!(e.OriginalSource is ScrollViewer sv)) return;
            if (sv.ScrollableHeight <= 0) return;
            if (sv.VerticalOffset < sv.ScrollableHeight - 48) return;
            if (_sessionPageLoaded >= _sessionTotalPages) return;
            try
            {
                await LoadNextSessionPageAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载更多会话失败: {ex.Message}");
            }
        }

        private async void BtnRefreshSessions_OnClick(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                long? keep = _api.CurrentSession != null ? (long?)_api.CurrentSession.Id : null;
                await ResetSessionsToFirstPageAsync(keep).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "刷新失败");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SessionList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is ListBoxItem))
                dep = GetParentObject(dep);
            if (!(dep is ListBoxItem item)) return;
            _hideTimer.Stop();
            _suppressSessionSelect = true;
            try { item.IsSelected = true; }
            finally { _suppressSessionSelect = false; }
            e.Handled = true;
        }

        private async void MenuRename_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(SessionList.SelectedItem is ChatSessionInfo session)) return;
            var name = PromptText("改名", "会话名称：", session.Name ?? "");
            if (name == null) return;
            name = name.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show("名称不能为空");
                return;
            }
            SetBusy(true);
            try
            {
                var updated = await _api.RenameSessionAsync(session, name).ConfigureAwait(true);
                var final = updated ?? session;
                var idx = _sessions.IndexOf(session);
                if (idx >= 0)
                {
                    _suppressSessionSelect = true;
                    try
                    {
                        _sessions.RemoveAt(idx);
                        _sessions.Insert(idx, final);
                        SessionList.SelectedItem = final;
                    }
                    finally
                    {
                        _suppressSessionSelect = false;
                    }
                }
                SetStatus($"已改名 #{final.Id} → {final.DisplayName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "改名失败");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void MenuDelete_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(SessionList.SelectedItem is ChatSessionInfo session)) return;
            HoldRightPanel();
            MessageBoxResult r;
            try
            {
                r = MessageBox.Show(
                    $"确定删除会话「{session.DisplayName}」(#{session.Id})？",
                    "删除确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
            }
            finally
            {
                ReleaseRightPanel();
            }
            if (r != MessageBoxResult.Yes) return;

            SetBusy(true);
            try
            {
                var id = session.Id;
                await _api.DeleteSessionAsync(id).ConfigureAwait(true);
                _suppressSessionSelect = true;
                try
                {
                    _sessions.Remove(session);
                }
                finally
                {
                    _suppressSessionSelect = false;
                }
                if (_api.CurrentSession == null || _api.CurrentSession.Id == id)
                {
                    ChatBox.Document.Blocks.Clear();
                    ClearOutline();
                    AppendSys("会话已删除");
                }

                if (SessionList.SelectedItem is ChatSessionInfo next)
                    await OpenSessionHistoryAsync(next).ConfigureAwait(true);

                SetStatus($"已删除 #{id}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "删除失败");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private string PromptText(string title, string label, string defaultValue)
        {
            HoldRightPanel();
            try
            {
                var win = new Window
                {
                    Title = title,
                    Width = 420,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Background = (Brush)FindResource("BrushBg")
                };
                var root = new DockPanel { Margin = new Thickness(16) };
                var btnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 14, 0, 0)
                };
                DockPanel.SetDock(btnPanel, Dock.Bottom);
                var ok = new Button
                {
                    Content = "确定", Width = 80, Height = 32, IsDefault = true,
                    Margin = new Thickness(0, 0, 8, 0),
                    Style = (Style)FindResource("OdButtonPrimary")
                };
                var cancel = new Button
                {
                    Content = "取消", Width = 80, Height = 32, IsCancel = true,
                    Style = (Style)FindResource("OdButton")
                };
                btnPanel.Children.Add(ok);
                btnPanel.Children.Add(cancel);

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = label,
                    Margin = new Thickness(0, 0, 0, 8),
                    Foreground = (Brush)FindResource("BrushWhite")
                });
                var tb = new TextBox
                {
                    Text = defaultValue ?? "",
                    MinHeight = 44,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Style = (Style)FindResource("OdTextBox")
                };
                stack.Children.Add(tb);

                root.Children.Add(btnPanel);
                root.Children.Add(stack);
                win.Content = root;

                string result = null;
                ok.Click += (s, e) => { result = tb.Text; win.DialogResult = true; };
                cancel.Click += (s, e) => { win.DialogResult = false; };
                tb.Focus();
                tb.SelectAll();
                return win.ShowDialog() == true ? result : null;
            }
            finally
            {
                ReleaseRightPanel();
            }
        }

        private async void SessionList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSessionSelect || _loadingHistory || _busy) return;
            if (!(SessionList.SelectedItem is ChatSessionInfo session)) return;
            await OpenSessionHistoryAsync(session).ConfigureAwait(true);
            ScheduleHide(EdgePanel.Right);
        }

        private async Task OpenSessionHistoryAsync(ChatSessionInfo session)
        {
            if (session == null) return;
            var gen = ++_historyLoadGen;
            _loadingHistory = true;
            SetStatus($"加载历史 #{session.Id}…");
            try
            {
                _api.AttachSession(session);

                var records = await _api.ListAllRecordsAsync(session.Id).ConfigureAwait(true);
                if (gen != _historyLoadGen) return;

                var messages = new List<(RenderRole role, string content)>
                {
                    (RenderRole.System, $"— 会话 #{session.Id} · {session.DisplayName} · {session.Model}")
                };
                foreach (var r in records)
                {
                    if (!string.IsNullOrEmpty(r.UserText))
                        messages.Add((RenderRole.User, r.UserText));
                    if (!string.IsNullOrEmpty(r.AiText))
                        messages.Add((RenderRole.Ai, r.AiText));
                }
                if (records.Count == 0)
                    messages.Add((RenderRole.System, "（尚无消息）"));

                var doc = await _render.BuildDocumentAsync(messages).ConfigureAwait(true);
                if (gen != _historyLoadGen) return;

                ChatBox.Document = doc;
                RebuildOutline(messages, doc);
                SyncChatPageWidth();
                ScrollChatEnd();

                SetStatus($"会话 #{session.Id} · {records.Count} 轮");
            }
            catch (Exception ex)
            {
                if (gen != _historyLoadGen) return;
                AppendError(ex.Message);
                SetStatus("加载历史失败");
            }
            finally
            {
                if (gen == _historyLoadGen)
                    _loadingHistory = false;
            }
        }

        private void FillProviders()
        {
            ProviderBox.Items.Clear();
            var names = _api.GetProviderNamesWithModels();
            foreach (var n in names)
                ProviderBox.Items.Add(n);

            var def = _api.FindModelByValue(_api.ModelTmpl.DefModel);
            if (def != null && !string.IsNullOrEmpty(def.ProviderName) && names.Contains(def.ProviderName))
                ProviderBox.SelectedItem = def.ProviderName;
            else if (ProviderBox.Items.Count > 0)
                ProviderBox.SelectedIndex = 0;
            else
                ReloadModels(null);
        }

        private void ProviderBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReloadModels(ProviderBox.SelectedItem as string);
        }

        private void ReloadModels(string providerName)
        {
            ModelBox.Items.Clear();
            if (string.IsNullOrEmpty(providerName))
                return;

            var prefer = _api.ModelTmpl != null ? _api.ModelTmpl.DefModel : null;
            ModelInfo select = null;
            foreach (var m in _api.ModelsByProviderName(providerName))
            {
                ModelBox.Items.Add(m);
                if (prefer != null && m.Value == prefer)
                    select = m;
            }
            if (select != null)
                ModelBox.SelectedItem = select;
            else if (ModelBox.Items.Count > 0)
                ModelBox.SelectedIndex = 0;
        }

        private ModelInfo SelectedModel()
        {
            return ModelBox.SelectedItem as ModelInfo;
        }

        private async void BtnNewSession_OnClick(object sender, RoutedEventArgs e)
        {
            var model = SelectedModel();
            if (model == null)
            {
                MessageBox.Show("请先选择模型");
                return;
            }
            SetBusy(true);
            try
            {
                SetStatus("创建会话…");
                var sess = await _api.CreateSessionAsync(model.Value).ConfigureAwait(true);
                ChatBox.Document.Blocks.Clear();
                ClearOutline();
                AppendSys($"— 会话 #{sess.Id} · {sess.Model}");
                await ResetSessionsToFirstPageAsync(sess.Id).ConfigureAwait(true);
                SetStatus($"会话 #{sess.Id}");
                ShowPanel(EdgePanel.Bottom);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "创建会话失败");
                SetStatus("失败");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnImage_OnClick(object sender, RoutedEventArgs e)
        {
            var model = SelectedModel();
            if (model == null || !model.ImageInput)
            {
                MessageBox.Show("当前模型不支持图像输入");
                return;
            }
            var max = _api.ModelTmpl.MFileCount;
            if (_pendingImages.Count >= max)
            {
                MessageBox.Show($"最多 {max} 张");
                return;
            }
            var dlg = new OpenFileDialog
            {
                Filter = "图片|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|全部|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) != true) return;
            foreach (var p in dlg.FileNames)
            {
                if (_pendingImages.Count >= max) break;
                if (!_pendingImages.Contains(p))
                    _pendingImages.Add(p);
            }
            RefreshAttach();
        }

        private void BtnClearImg_OnClick(object sender, RoutedEventArgs e)
        {
            _pendingImages.Clear();
            RefreshAttach();
        }

        private void RefreshAttach()
        {
            if (_pendingImages.Count == 0)
                AttachText.Text = "未附加图片";
            else
                AttachText.Text = $"已附加 {_pendingImages.Count} 张: {string.Join(", ", _pendingImages.Select(Path.GetFileName))}";
            BtnClearImg.IsEnabled = !_busy && _pendingImages.Count > 0;
        }

        private void BtnStop_OnClick(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            SetStatus("正在停止…");
        }

        private void InputBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                BtnSend_OnClick(sender, e);
            }
        }

        private async void BtnSend_OnClick(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            var text = (InputBox.Text ?? "").Trim();
            var images = new List<string>(_pendingImages);
            if (text.Length == 0 && images.Count == 0) return;

            var model = SelectedModel();
            if (model == null)
            {
                MessageBox.Show("请先选择模型");
                ShowPanel(EdgePanel.Top);
                return;
            }
            if (images.Count > 0 && !model.ImageInput)
            {
                MessageBox.Show("当前模型不支持图像输入");
                return;
            }

            SetBusy(true);
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var savedText = text;
            var savedImages = new List<string>(images);

            InputBox.Text = "";
            _pendingImages.Clear();
            RefreshAttach();
            if (InputBox.IsKeyboardFocusWithin)
                Keyboard.ClearFocus();
            HidePanel(EdgePanel.Bottom);

            var shown = text;
            if (images.Count > 0)
            {
                var names = string.Join(", ", images.Select(Path.GetFileName));
                shown = text.Length > 0 ? $"{text}\n[图片: {names}]" : $"[图片: {names}]";
            }
            AppendUser(shown);

            _sseChunkCount = 0;
            _streamEnded = false;
            lock (_chunkLock) { _smoothQueue.Clear(); }
            SetStatus("SSE 连接中…");
            Exception catchEx = null;
            ChatStreamResult result = null;

            try
            {
                if (_api.CurrentSession == null || _api.CurrentSession.Model != model.Value)
                {
                    var sess = await _api.CreateSessionAsync(model.Value, false, token).ConfigureAwait(true);
                    if (sess == null)
                        throw new ApiException("创建会话失败：服务器未返回会话信息");
                    AppendSys($"— 会话 #{sess.Id} · {sess.Model}");
                    await ResetSessionsToFirstPageAsync(sess.Id).ConfigureAwait(true);
                }

                var files = savedImages.Count > 0
                    ? ImageEncoder.EncodeFiles(savedImages, _api.ModelTmpl.MFileCount, _api.ModelTmpl.MFileSize)
                    : null;

                BeginAi();
                result = await _api.SendMessageAsync(
                    savedText.Length > 0 ? savedText : " ",
                    files,
                    null,
                    false,
                    false,
                    piece => QueueAiChunk(piece),
                    token).ConfigureAwait(true);

                await DrainSmoothAndEndAsync().ConfigureAwait(true);

                if (result.StreamInterrupted)
                    AppendError("连接已中断，回复可能不完整");

                SetStatus($"就绪 · SSE {result.StringEvents} events · #{result.SessionId}");

                SoftSyncCurrentSessionInList();
            }
            catch (Exception ex)
            {
                catchEx = ex;
            }

            if (catchEx != null)
            {
                try { await DrainSmoothAndEndAsync().ConfigureAwait(true); }
                catch { /* ignore */ }
            }

            if (catchEx is OperationCanceledException)
            {
                AppendError("[已停止]");
                SetStatus("已停止");
            }
            else if (catchEx != null)
            {
                AppendError(catchEx.Message);
                RestoreFailedSend(savedText, savedImages, catchEx.Message);
                SetStatus("出错");
            }

            SetBusy(false);
            _cts = null;
            _aiStream = null;
        }

        private void RestoreFailedSend(string text, IList<string> images, string error)
        {
            InputBox.Text = text ?? "";
            _pendingImages.Clear();
            if (images != null)
                _pendingImages.AddRange(images);
            RefreshAttach();
            ShowPanel(EdgePanel.Bottom);

            var preview = text ?? "";
            if (images != null && images.Count > 0)
            {
                var names = string.Join(", ", images.Select(Path.GetFileName));
                preview = preview.Length > 0
                    ? $"{preview}\n[图片: {names}]"
                    : $"[图片: {names}]";
            }
            if (preview.Length > 1500)
                preview = $"{preview.Substring(0, 1500)}…";

            MessageBox.Show(
                $"{error ?? "发送失败"}\n\n以下内容已恢复到输入框：\n{preview}",
                "发送失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void QueueAiChunk(string piece)
        {
            if (string.IsNullOrEmpty(piece)) return;
            lock (_chunkLock)
            {
                _smoothQueue.Append(piece);
            }
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(StartSmoothTimerIfNeeded));
                return;
            }
            StartSmoothTimerIfNeeded();
        }

        private void StartSmoothTimerIfNeeded()
        {
            if (!_smoothTimer.IsEnabled)
                _smoothTimer.Start();
        }

        private void SmoothTimer_OnTick(object sender, EventArgs e)
        {
            string piece;
            int remain;
            lock (_chunkLock)
            {
                if (_smoothQueue.Length == 0)
                {
                    if (_streamEnded)
                        _smoothTimer.Stop();
                    return;
                }
                var take = _smoothQueue.Length;
                if (!_streamEnded)
                {
                    take = Math.Max(3, Math.Min(36, _smoothQueue.Length / 10 + 4));
                    take = Math.Min(take, _smoothQueue.Length);
                }
                piece = _smoothQueue.ToString(0, take);
                _smoothQueue.Remove(0, take);
                remain = _smoothQueue.Length;
            }
            AppendAiChunk(piece);
            _sseChunkCount++;
            if (remain == 0 && _streamEnded)
                _smoothTimer.Stop();
        }

        private async Task DrainSmoothAndEndAsync()
        {
            _streamEnded = true;
            for (var guard = 0; guard < 10000; guard++)
            {
                int left;
                lock (_chunkLock) { left = _smoothQueue.Length; }
                if (left == 0) break;
                SmoothTimer_OnTick(null, EventArgs.Empty);
            }
            _smoothTimer.Stop();
            lock (_chunkLock) { _smoothQueue.Clear(); }
            await EndAiAsync().ConfigureAwait(true);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            var ready = _api.ModelTmpl != null && !busy;
            BtnSend.IsEnabled = ready;
            BtnNewSession.IsEnabled = ready;
            BtnImage.IsEnabled = ready;
            BtnRefreshSessions.IsEnabled = ready;
            BtnStop.IsEnabled = busy;
            ProviderBox.IsEnabled = ready;
            ModelBox.IsEnabled = ready;
            SessionList.IsEnabled = true;
            OutlineList.IsEnabled = true;
            BtnClearImg.IsEnabled = ready && _pendingImages.Count > 0;
            InputBox.IsEnabled = ready;
        }

        private void SetStatus(string s)
        {
            if (!string.IsNullOrEmpty(s))
                Debug.WriteLine($"[GUI] {s}");
        }

        private void Present(Block block, RenderRole role, string sourceText)
        {
            if (block == null) return;
            SyncChatPageWidth();
            ChatBox.Document.Blocks.Add(block);
            if (role == RenderRole.User || role == RenderRole.Ai)
                AddOutline(role, sourceText, block);
            ScrollChatEnd();
        }

        private void AppendUser(string text)
        {
            Present(_render.RenderBlock(RenderRole.User, text), RenderRole.User, text);
        }

        private void BeginAi()
        {
            _streamEnded = false;
            lock (_chunkLock) { _smoothQueue.Clear(); }
            _aiStream = _render.BeginStream(RenderRole.Ai);
            _aiStream.ShowThinking("思考中......");
            ChatBox.Document.Blocks.Add(_aiStream.Section);
            _streamingOutline = AddOutline(RenderRole.Ai, "思考中……", _aiStream.Section);
            ScrollChatEnd();
        }

        private void AppendAiChunk(string piece)
        {
            if (_aiStream == null || string.IsNullOrEmpty(piece)) return;
            _aiStream.Append(piece);
            if (_streamingOutline != null && (_sseChunkCount % 4) == 0)
                _streamingOutline.Preview = MakeSnippet(_aiStream.Buffer);
            RequestScrollChatEnd();
        }

        private async Task EndAiAsync()
        {
            if (_aiStream == null) return;
            var stream = _aiStream;
            _aiStream = null;
            SetStatus("正在渲染回复…");
            await stream.CompleteAsync().ConfigureAwait(true);
            if (_streamingOutline != null)
            {
                _streamingOutline.Preview = MakeSnippet(stream.Buffer);
                _streamingOutline = null;
            }
            ScrollChatEnd();
        }

        private void AppendSys(string text)
        {
            Present(_render.RenderBlock(RenderRole.System, text), RenderRole.System, text);
        }

        private void AppendError(string text)
        {
            Present(_render.RenderBlock(RenderRole.Error, text), RenderRole.Error, text);
        }

        private void ScrollChatEnd()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ScrollChatEnd));
                return;
            }
            ChatBox.ScrollToEnd();
        }

        private void RequestScrollChatEnd()
        {
            if (_scrollChatEndQueued) return;
            _scrollChatEndQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _scrollChatEndQueued = false;
                ScrollChatEnd();
            }));
        }
    }
}
