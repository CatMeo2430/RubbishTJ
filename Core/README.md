# Taiji.Core

逆向封装 [ai.avuuq.cn](https://ai.avuuq.cn) 网页 API 的 .NET Framework 4.8 类库。提供登录、模型列表、会话/历史、SSE 流式对话等能力。

## 构建

```bat
cd ..
build.bat
```

产物：`publish\Taiji.Core.dll`（依赖同目录 `Newtonsoft.Json.dll`）。

## 目录结构

```
Core/
├── CoreInterface.cs      # ITaijiCore / TaijiCore（对外唯一入口）
├── Models/               # DTO / 分页壳
│   ├── Auth.cs
│   ├── Catalog.cs
│   ├── Chat.cs
│   └── List.cs
├── Modules/              # 业务逻辑（internal）
│   ├── Auth.cs
│   ├── Catalog.cs
│   ├── Chat.cs
│   └── List.cs
└── Utils/
    ├── Constant.cs       # 账号、站点、协议常量
    ├── Exception.cs      # ApiException
    ├── Http.cs           # HTTP + SSE（internal）
    └── ImageEncoder.cs   # 本地图片 → base64 data-URI
```

## 快速开始

```csharp
using System;
using Taiji.Core;
using Taiji.Core.Utils;

using (ITaijiCore api = new TaijiCore())
{
    await api.LoginAsync();
    await api.LoadModelsAsync();

    await api.CreateSessionAsync(api.ModelTmpl.DefModel);

    var result = await api.SendMessageAsync(
        "你好",
        onChunk: chunk => Console.Write(chunk));

    Console.WriteLine("\n" + result.Text);
}
```

带图片（最多 5 张，经 base64 内联，无独立上传接口）：

```csharp
var files = ImageEncoder.EncodeFiles(new[] { @"C:\img.png" });
await api.SendMessageAsync("描述这张图", files, onChunk: ...);
```

## 公开 API 一览

| 方法 | 说明 | HTTP |
|------|------|------|
| `LoginAsync` | 登录（默认读 `Constant` 硬编码账号） | `POST /user/login` |
| `LoadModelsAsync` | 模型与厂商列表 | `GET /chat/tmpl` |
| `GetProvidersOrdered` 等 | 模型分类/查询（需先 `LoadModelsAsync`） | — |
| `ListSessionsPageAsync` | 历史对话分页 | `GET /chat/session?page=N` |
| `ListAllSessionsAsync` | 翻页合并全部会话 | 同上 |
| `CreateSessionAsync` | 新建对话 | `POST /chat/session` |
| `AttachSession` | 切换到已有对话 | 本地 |
| `ListAllRecordsAsync` | 某会话全部消息（旧→新） | `GET /chat/record/{id}?page=` |
| `SendMessageAsync` | 发消息，SSE 流式 | `POST /chat/completions` |

## 流式响应

`SendMessageAsync` 的 `onChunk` 在每个 `type=string` 增量到达时回调；完整文本在返回的 `ChatStreamResult.Text` 中。

```csharp
var result = await api.SendMessageAsync(
    text,
    onChunk: piece => { /* 边下边更新 UI */ });

// result.TaskId, result.SessionId, result.Record（最终 object 事件）
```

HTTP 层使用 `ResponseHeadersRead` + 字节级 SSE 解析，避免整包缓冲。

## 配置

编辑 `Utils/Constant.cs`：

| 常量 | 含义 |
|------|------|
| `Account` / `Password` | 登录凭据 |
| `DefaultBaseUrl` | API 根地址 |
| `AppVersion` | 请求头 `X-APP-VERSION` |
| `DefaultMaxFileCount` / `DefaultMaxFileMb` | 图片数量与大小上限 |

自定义站点：

```csharp
var api = new TaijiCore("https://your-host/api");
```

## 异常

业务/网络错误抛出 `Taiji.Core.Utils.ApiException`；`Code == 2` 表示登录过期。

## 限制（源自网页端）

- 输入约 4K tokens，长输出易截断
- 图片仅 base64 内联，最多 5 张
- 不支持文件上传、Tool/MCP、可靠多轮上下文
- 不宜并发多路对话（建议单线程使用）

## 命名空间

| 命名空间 | 内容 |
|----------|------|
| `Taiji.Core` | `ITaijiCore`, `TaijiCore` |
| `Taiji.Core.Models` | 公开 DTO |
| `Taiji.Core.Utils` | `Constant`, `ApiException`, `ImageEncoder` |
