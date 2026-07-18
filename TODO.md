# TODO

项目审查备忘（Core / Engine / GUI / Proxy）。按 ROI 排序。

## P0 — 性能 / 卡顿

- [x] **流式缓冲改用 `StringBuilder`**：`StreamRenderSession.Append`、`MainWindow._smoothQueue`（当前 `$"{buf}{chunk}"` 长回复 O(n²) GC）
- [x] **历史加载异步/分批渲染**：`BuildDocument` 同步循环阻塞 UI；复用 `RenderBodyAsync` 或分页虚拟化
- [x] **LaTeX 离 UI 线程**：`RatexFormulaView.RebuildLayout` 中 `RenderBitmap` 改后台执行，回 UI 刷新

## P1 — 安全 / 稳定性

- [ ] **凭据加密**：`CredentialStore` 明文 JSON；Proxy `App.config` 密码 → DPAPI 或仅存 token
- [x] **Markdown 轻量路由**：`MarkdownContentRenderer.CanHandle` 对纯文本跳过全量 Markdig
- [x] **流式滚动节流**：`ScrollChatEnd` 不必每 chunk 调用
- [x] **HTTP 开启解压**：`Http.cs` `AutomaticDecompression = None` 可能导致 gzip 响应失败
- [x] **SSE 超时**：`PostStreamCoreAsync` 补 `CancelAfter`（普通请求已有 2 分钟）
- [x] **Proxy 异常捕获**：`Task.Run(() => HandleRequestAsync)` fire-and-forget 需记录未观察异常

## P2 — 减体积 / 删冗余

- [ ] **移除 Engine 多余 NuGet 显式引用**（代码无 direct use，约 −200~400 KB exe）：`System.Buffers`、`System.Memory`、`System.Numerics.Vectors`、`System.Runtime.CompilerServices.Unsafe`
- [ ] **未使用 API**：`ListAllSessionsAsync`、`RenderInteractions`（零引用）、`StreamRenderSession.Complete()` 同步包装
- [ ] **未接线参数**：`thinking` / `webSearch` GUI 恒传 `false`
- [ ] **Proxy 业务重叠**：`/GetModelsList` vs `/v1/models`；`FindSessionAsync` 翻页查找可优化

## P3 — 架构 / 长期

- [ ] Proxy 全局 `SemaphoreSlim(1)` 串行上游，多客户端需并发设计
- [ ] `WebRequestHandler` → `HttpClientHandler`（维护性）
- [ ] 评估 `PlainContentRenderer` 主路径价值（User/AI 几乎总走 Markdown）

## 体积参考

| 组件 | 可减？ |
|------|--------|
| `ratex_ffi.dll`（嵌入 Engine） | 否，除非砍 LaTeX |
| Markdig + AvalonEdit | 否 |
| System.* 显式包 | 是 |
| Costura 单文件开销 | 保留（部署需要） |
