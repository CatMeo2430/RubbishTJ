# Taiji.Engine

WPF 富文本渲染类库。聊天中的**纯文本、Markdown、代码块、LaTeX 公式**全部由 Engine 解析并渲染为 WPF 元素；`Taiji.GUI` 只负责把 `RenderResult.Root` 挂到 `RichTextBox`，不直接接触 Markdig、AvalonEdit 或 RaTeX FFI。

配色基于 **Atom One Dark**（`Theme/DraculaTheme`），LaTeX 屏幕渲染默认**白字透明底**，适配暗黑界面。

## 在解决方案中的位置

```
TaiJi.sln
├── Core          # API 客户端
├── Engine        # ★ 本库（Taiji.Engine.dll）
└── GUI           # 引用 Engine，调用 RenderEngine
```

构建产物：`publish\Taiji.Engine.dll`（与 `Taiji.GUI.exe` 同目录，依赖 DLL 一并复制）。

## 目录结构

```
Engine/
├── RenderEngine.cs              # 统一入口
├── RenderRequest.cs / RenderResult.cs / RenderRole.cs
├── IContentRenderer.cs
├── StreamRenderSession.cs         # SSE 流式渲染
├── RenderInteractions.cs          # 公式/代码块交互 API
├── RenderToolbar.cs               # 工具栏按钮样式
├── MessageBubbleToolbar.cs        # AI 气泡复制 / 导出
├── Utils/
│   ├── ExportDialog.cs
│   └── VisualExportHelper.cs      # WPF 元素 / FlowDocument → PNG
├── Markdown/
│   ├── MarkdownPipelineFactory.cs
│   ├── MarkdigFlowConverter.cs
│   └── MarkdownContentRenderer.cs
├── Code/
│   ├── CodeBlockEditor.cs
│   ├── CodeBlockView.cs           # 代码块 + 工具栏
│   ├── CodeInteractions.cs
│   ├── CodeBlockViewFactory.cs
│   └── OneDarkHighlighting.cs
├── Latex/
│   ├── ILatexRenderEngine.cs
│   ├── LatexEngine.cs             # 默认 RatexLatexEngine
│   ├── LatexRenderOptions.cs      # 前景/背景/DPI 等
│   ├── LatexViewFactory.cs
│   ├── LatexBlockView.cs          # 块级公式 + 工具栏
│   ├── LatexNormalizer.cs
│   ├── LatexInteractions.cs       # 复制 LaTeX / 导出 PNG·SVG
│   ├── RatexLatexEngine.cs
│   ├── RatexNative.cs             # P/Invoke ratex_ffi.dll
│   ├── RatexBitmapHelper.cs
│   └── RatexFormulaView.cs        # 公式位图 + 右键菜单
├── Theme/
│   └── DraculaTheme.cs
└── Builtin/
    ├── SystemContentRenderer.cs
    ├── ErrorContentRenderer.cs
    └── PlainContentRenderer.cs
```

## 渲染管线

```
消息文本 + RenderRole
        ↓
RenderEngine.Select() → IContentRenderer
        │
        ├─ System  → SystemContentRenderer（斜体系统提示）
        ├─ Error   → ErrorContentRenderer（红色错误）
        ├─ User/Ai → MarkdownContentRenderer（默认）
        │              ↓
        │         LatexNormalizer.Normalize()
        │              ↓
        │         Markdig.Parse（表格 / 围栏代码 / 数学）
        │              ↓
        │         MarkdigFlowConverter
        │              ├─ 段落 / 标题 / 列表 / 表格 / 引用
        │              ├─ 代码块 → CodeBlockEditor（AvalonEdit）
        │              └─ $...$ / $$...$$ → RatexFormulaView（RaTeX）
        └─ plain 提示 → PlainContentRenderer（强制纯文本）
        ↓
RenderEngine 包裹气泡壳（You / AI / 错误）
        ↓
RenderResult.Root → GUI FlowDocument
```

**流式 AI 回复**：`BeginStream()` 先纯文本追写 → `CompleteAsync()` 后台完整 Markdown 重渲染。

### 渲染器优先级

| 渲染器 | Priority | 触发条件 |
|--------|----------|----------|
| `SystemContentRenderer` | 0 | `RenderRole.System` |
| `ErrorContentRenderer` | 0 | `RenderRole.Error` |
| `MarkdownContentRenderer` | 10 | User/Ai 消息，或含 markdown 特征 |
| `PlainContentRenderer` | 100 | `languageHint = "plain"` |

普通 User/Ai 文本即使无 Markdown 语法也会走 Markdig（渲染为普通段落）。仅当显式指定 `plain` 时才跳过 Markdown。

## 依赖

| 包 / 运行时 | 版本 | 用途 |
|-------------|------|------|
| Markdig | 0.37 | CommonMark + 数学扩展 |
| AvalonEdit | 6.3 | 语法高亮代码块 |
| ratex_ffi.dll | — | RaTeX 原生位图/SVG 渲染（x64） |

- DLL 来源：解决方案根目录 `ratex_ffi.dll`，构建时复制到 `publish\`
- 使用 embed-fonts 构建，**无需** `fonts\` 目录
- 平台：**x64**，DLL 须与 exe 同目录

## GUI 快速用法

```csharp
using Taiji.Engine;

private readonly RenderEngine _render = new RenderEngine();

// 单条消息
chatBox.Document.Blocks.Add(_render.RenderBlock(RenderRole.Ai, markdown));

// 批量历史
chatBox.Document = _render.BuildDocument(messages);

// 流式 SSE
var stream = _render.BeginStream(RenderRole.Ai);
chatBox.Document.Blocks.Add(stream.Section);
stream.ShowThinking("思考中......");
stream.Append(chunk);
await stream.CompleteAsync();
```

`RenderRole`：`User` / `Ai` / `System` / `Error`。

## 交互能力

### 代码块

`CodeBlockView` 右上角工具栏：

| 按钮 | 说明 |
|------|------|
| 复制 | 原始代码写入剪贴板 |
| 导出 | 弹出保存对话框，将代码块（含高亮）导出为 PNG |

编辑器仍支持拖选部分代码、**Ctrl+C** 复制选中内容（无选中则复制全部）、右键复制 / 全选；滚轮转发给外层 `RichTextBox`。

### AI 消息气泡

`MessageBubbleToolbar` 插入 AI 气泡标题下方：

| 按钮 | 说明 |
|------|------|
| 复制 | 原始 Markdown 原文写入剪贴板 |
| 导出 | 弹出保存对话框，离屏重渲染整条 AI 消息（含气泡样式，不含工具栏）为 PNG |

流式回复在生成过程中即可复制 / 导出当前已缓冲内容。

### LaTeX 公式

块级公式（`$$...$$`、` ```latex ` 围栏、识别为 LaTeX 的代码块）使用 `LatexBlockView`，右上角工具栏：

| 按钮 | 说明 |
|------|------|
| 复制 | 原始 LaTeX 写入剪贴板 |
| PNG | 弹出保存对话框，经 `LatexEngine` → `ratex_ffi` 导出位图 |
| SVG | 弹出保存对话框，经 `LatexEngine` → `ratex_ffi` 导出矢量图 |

行内公式（`$...$`）的 `RatexFormulaView` 仍保留右键菜单（复制 / 导出 PNG·SVG）。

### LaTeX 配色（`LatexRenderOptions`）

| 工厂方法 | 前景 | 背景 | 用途 |
|----------|------|------|------|
| `ForScreen()` | 白 `(1,1,1,1)` | 透明 | 暗黑 GUI 聊天显示 |
| `ForPngExport()` | 黑 `(0,0,0,1)` | 可配置（默认白底） | 打印/分享 |
| `ForSvgExport()` | 白 | 透明 | 暗黑界面导出矢量图 |

可通过 `ForegroundR/G/B/A` 自定义任意颜色。

### 编程式 API

内置菜单已覆盖常见操作；GUI 自定义菜单时可调用：

```csharp
using Taiji.Engine;
using Taiji.Engine.Latex;

// 公式：从点击元素查找
string latex;
bool displayMode;
if (RenderInteractions.TryGetLatexSource(element, out latex, out displayMode))
{
    RenderInteractions.CopyLatexSource(latex);
    RenderInteractions.PromptExportLatexPng(latex, displayMode, 22f, ownerWindow);
}

// 代码块
CodeBlockEditor editor;
if (RenderInteractions.TryFindCodeEditor(element, out editor))
    editor.CopySelectionOrAll();

// 不经过 Markdown 管线直接导出
LatexInteractions.SavePngToFile(@"\frac{1}{2}", @"C:\out.png", true, 24f, out var err);
```

## 扩展渲染器

```csharp
public sealed class MyRenderer : IContentRenderer
{
    public string Id { get { return "my-format"; } }
    public int Priority { get { return 5; } }   // 越小越优先
    public bool CanHandle(RenderRequest request) { /* ... */ }
    public IList<Block> RenderBody(RenderRequest request) { /* ... */ }
}

engine.Register(new MyRenderer());
```

Markdown 渲染失败时 `RenderEngine` 自动降级为 `PlainContentRenderer`。

## 构建

```bat
:: 解决方案根目录
build.bat

:: 或单独构建
nuget restore Engine\packages.config -PackagesDirectory packages
msbuild Engine\Engine.csproj /p:Configuration=Debug /p:Platform=x64
```

## 与 RaTeX-FFI 的关系

LaTeX 渲染通过 P/Invoke 调用 `ratex_ffi.dll`。将预编译 DLL 放在解决方案根目录，由 `build.bat` / `Engine.csproj` 复制到 `publish\`。`RaTeX-FFI` 为可选源码目录，不参与 .NET 构建。
