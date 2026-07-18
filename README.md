# TaiJi

基于 [ai.avuuq.cn](https://ai.avuuq.cn) 的 WPF 桌面客户端（.NET Framework 4.8，C# 7.3，x64）。逆向封装网页 API，在本地提供对话、历史、流式输出，以及 Markdown / 代码高亮 / LaTeX 富文本渲染。附带 **Taiji.Proxy** — OpenAI 兼容 HTTP 代理。

## 解决方案结构

```
RubbishTJ/
├── Core/                  # API 客户端（Taiji.Core.dll）
├── Engine/                # 富文本渲染（Taiji.Engine.dll，内嵌 ratex_ffi.dll）
├── GUI/                   # WPF 主程序（Taiji.GUI.exe）
├── Proxy/                 # OpenAI 兼容代理（Taiji.Proxy.exe）
├── ratex_ffi.dll          # LaTeX 原生库（构建时嵌入 Taiji.Engine.dll）
├── build.bat              # 本地一键提交 + 云端编译 + 下载产物
├── .github/workflows/     # GitHub Actions 云端单文件构建
├── TaiJi.sln
├── packages/              # NuGet 包（CI 生成，不纳入版本控制）
└── publish/               # 编译产物（不纳入版本控制）
```

| 项目 | 说明 | 文档 |
|------|------|------|
| **Core** | 登录、模型列表、会话/历史、SSE 流式对话 | [Core/README.md](Core/README.md) |
| **Engine** | Markdig、AvalonEdit、RaTeX FFI 渲染管线 | [Engine/README.md](Engine/README.md) |
| **GUI** | 无边框对话界面，引用 Core + Engine | [GUI/README.md](GUI/README.md) |
| **Proxy** | OpenAI 兼容代理，`api_key` = `sessionId` | [Proxy/README.md](Proxy/README.md) |

依赖关系：`GUI` → `Core`、`Engine`；`Proxy` → `Core`。`ratex_ffi.dll` 在构建时嵌入 `Taiji.Engine.dll`，运行时解压到临时目录加载；GUI / Proxy 通过 [Costura.Fody](https://github.com/Fody/Costura) 打包为单文件 exe。

## 环境要求

本地**无需**安装 Visual Studio、MSBuild 或 NuGet，只需：

- Windows x64
- `git`、`gh` 已安装并在 PATH 中，且 `gh` 已登录 GitHub
- 仓库已关联远程（`origin`）

云端 CI（GitHub Actions）负责完整编译，使用 **Release | x64**。

## 构建（全云编译）

在项目根目录运行：

```bat
build.bat "本次更改内容摘要"
```

`build.bat` 会依次执行：

1. `git add .`
2. `git commit -m "本次更改内容摘要"`
3. `git push -u origin HEAD`（触发 GitHub Actions）
4. 按本次 commit SHA 查找对应 CI run（`gh run list --workflow ci.yml --commit <sha>`）
5. `gh run watch <run-id> --exit-status` 等待 CI 完成（仅显示进度，不输出逐步详细日志）
6. 若失败，执行 `gh run view <run-id> --log-failed` 打印失败步骤日志
7. 若成功，执行 `gh run download <run-id> --name TaiJi-singlefile-x64 --dir .artifacts` 并解压到 `publish\`

> `gh run watch` 本身不流式输出完整构建日志。失败时用已定位的 `run-id` 调用 `--log-failed`（官方无 `--last-failed` 参数，用 commit 对应的 run-id 更准确）。

示例：

```bat
build.bat "迁移至 C# 7.3 并启用云端编译"
```

CI 产物为两个单文件可执行文件：

| 文件 | 说明 |
|------|------|
| `Taiji.GUI.exe` | WPF 主程序（含 Core、Engine 及依赖） |
| `Taiji.Proxy.exe` | OpenAI 兼容代理 |

GitHub Actions 配置见 [`.github/workflows/ci.yml`](.github/workflows/ci.yml)。Artifact 名称：`TaiJi-singlefile-x64`。

> .NET Framework 4.8 没有官方的 `dotnet publish -p:PublishSingleFile`；单文件由 Costura 在 MSBuild 编译后将托管 DLL 织入 exe 实现。`ratex_ffi.dll` 已预先嵌入 `Taiji.Engine.dll`，随 Engine 一并打入 GUI 单文件包。

## 运行

### GUI

```bat
publish\Taiji.GUI.exe
```

登录凭据保存在 `%APPDATA%\MeowCSharp\TaiJi\credentials.json`，GUI 与 Proxy 共用。

### Proxy

```bat
publish\Taiji.Proxy.exe
```

默认监听 `http://127.0.0.1:8765/`。单文件包使用代码内默认配置，无需附带 `.config`。

详见 [Proxy/README.md](Proxy/README.md)。

## 功能概览

**GUI**

- 启动自动登录，选择厂商/模型，新建会话与 SSE 流式对话
- 多模态模型附加本地图片（base64）
- 历史会话分页、改名/删除；左侧大纲跳转
- 无边框全屏式布局，四边滑出面板
- Markdown、代码块（复制/导出 PNG）、LaTeX 公式（复制/PNG/SVG）、AI 回复（复制/导出 PNG）

**Proxy**

- OpenAI 兼容 `/v1/chat/completions`、`/v1/models`
- 自定义端点：会话列表、模型列表、改名、删除
- `Authorization: Bearer <sessionId>` 映射上游会话

账号与 API 地址在 `Core/Utils/Constant.cs` 中配置。

## 版本

应用版本号见 `Core/Utils/Constant.cs` 的 `AppVersion`（窗口标题同步显示）。

## 仓库忽略项

见根目录 [.gitignore](.gitignore)：`OLD/`、`packages/`、`publish/`、`RaTeX-FFI/` 等不纳入版本控制。
