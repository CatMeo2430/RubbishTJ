# Taiji.GUI

Taiji 的 WPF 桌面客户端（.NET Framework 4.8，x64）。基于 [Core](../Core/README.md) 调用网页 API，通过 [Engine](../Engine/README.md) 渲染对话内容。

## 在解决方案中的位置

```
TaiJi.sln
├── Core          # API 客户端（Taiji.Core.dll）
├── Engine        # Markdown / 代码 / LaTeX 渲染（Taiji.Engine.dll）
└── GUI           # ★ 本程序（Taiji.GUI.exe）
```

GUI 引用 **Core + Engine**；LaTeX 导出依赖 Engine 构建时复制的 `ratex_ffi.dll`（来自 `RaTeX-FFI`）。

## 构建与运行

```bat
cd ..
build.bat
publish\Taiji.GUI.exe
```

`publish\` 需包含（由解决方案一并输出）：

| 文件 | 说明 |
|------|------|
| `Taiji.GUI.exe` | 本程序 |
| `Taiji.Core.dll` | API 客户端 |
| `Taiji.Engine.dll` | 富文本渲染 |
| `ratex_ffi.dll` | LaTeX 位图 / PNG·SVG 导出（Engine 复制） |
| `Newtonsoft.Json.dll` | JSON 序列化 |
| `Markdig.dll`、`ICSharpCode.AvalonEdit.dll` 等 | Engine 传递依赖 |

## 功能

- **登录与模型**：启动自动登录，拉取厂商 / 模型列表
- **对话**：新建会话、选择模型、**Ctrl+Enter** 发送、SSE 流式打字机输出、生成中可停止
- **富文本渲染**：Markdown、代码高亮、LaTeX 公式（见下方「渲染」）
- **图片**：多模态模型可附加本地图片（base64，数量与大小受模型模板限制）
- **历史**：右侧会话列表（分页懒加载）、右键改名 / 删除、点击加载历史
- **大纲**：左侧消息大纲，点击跳转到对应对话块；流式回复时预览随内容更新
- **布局**：无边框窗口贴合工作区；鼠标移到屏幕边缘滑出顶 / 底 / 左 / 右面板

## 目录结构

```
GUI/
├── App.xaml / App.xaml.cs       # One Dark 主题与控件样式
├── MainWindow.xaml              # 主界面布局（四边滑出面板 + 中央对话区）
├── MainWindow.xaml.cs           # 业务逻辑（API、流式、大纲、边缘面板）
├── OutlineItem.cs               # 大纲项（角色标签 + 预览 + 锚点 Block）
├── WorkAreaInsets.cs            # 任务栏边距计算
├── MonitorWorkArea.cs           # 工作区贴合 / 最大化
├── Properties/AssemblyInfo.cs
├── App.config
└── packages.config
```

## 界面操作

| 区域 | 操作 |
|------|------|
| 顶栏（上边缘滑出） | 新对话、刷新历史、厂商 / 模型选择、窗口控制 |
| 底栏（下边缘滑出） | 输入框、发送、图片、清图、停止 |
| 左栏（左边缘滑出） | 对话大纲，点击跳转 |
| 右栏（右边缘滑出） | 历史会话列表，右键改名 / 删除 |
| 中央 | 对话内容（`RichTextBox` + Engine 渲染块） |

快捷键：**Ctrl+Enter** 发送。发送成功后底栏自动收起；点击对话区外部也可收起底栏。

## 架构

```
MainWindow
    │
    ├─ ITaijiCore _api          # 登录、模型、会话、SSE 对话
    └─ RenderEngine _render     # 消息 → FlowDocument Block
            │
            ├─ RenderBlock()           # 用户 / 系统 / 错误（即时）
            ├─ BeginStream()           # AI 流式：先纯文本 +「思考中」
            │     └─ CompleteAsync()   # 结束后完整 Markdown / LaTeX 重渲染
            └─ BuildDocument()         # 加载历史时批量重建
```

入口代码：

```csharp
private readonly ITaijiCore _api = new TaijiCore();
private readonly RenderEngine _render = new RenderEngine();
```

账号与站点配置见 Core 的 `Utils/Constant.cs`，GUI 不单独维护凭据。

## 渲染

GUI 只把 `RenderResult.Root` 或 `StreamRenderSession.Section` 挂到 `RichTextBox`，解析与高亮均在 Engine 完成。详见 [Engine/README.md](../Engine/README.md)。

| 内容类型 | 交互 |
|----------|------|
| Markdown | 标题、列表、表格、引用等 |
| 代码块 | 右上角 **复制 / 导出**（源码；PNG）；仍支持局部选中 + **Ctrl+C** |
| 块级 LaTeX（`$$`、`` ```latex ``） | 右上角 **复制 / PNG / SVG**（经 `ratex_ffi`） |
| 行内 LaTeX（`$...$`） | 右键菜单：复制源码、导出 PNG / SVG |
| **AI 回复** | 气泡内 **复制 / 导出**（Markdown 原文；整条消息 PNG） |

**流式输出**：SSE 分块经定时器平滑写入 `StreamRenderSession`（纯文本追写）；流结束后 `CompleteAsync()` 走完整管线，公式与代码块才以富文本呈现。大纲预览在流式过程中周期性更新。

## 窗口行为

- 默认铺满当前显示器**工作区**（不遮挡任务栏）
- 标题栏「最大化」在工作区铺满与上次窗口尺寸间切换
- 四角为死区，不触发边缘面板，避免误触

## 依赖

| 项目 / 包 | 用途 |
|-----------|------|
| Taiji.Core | API 客户端 |
| Taiji.Engine | Markdown / 代码 / LaTeX 渲染 |
| Newtonsoft.Json | 序列化（随 Core 传递） |

## 已知限制

与 Core / 网页端一致：上下文弱、长输出易截断、不宜多线程并发对话。图片仅支持常见格式（png / jpg / gif / webp / bmp）。LaTeX 导出需 `ratex_ffi.dll` 与可执行文件同目录。
