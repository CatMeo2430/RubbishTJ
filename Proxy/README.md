# Taiji Proxy

OpenAI 兼容 HTTP 代理，将 `api_key` 映射为 TaiJi `sessionId`。

## 运行

1. 编辑 `Proxy\App.config` 配置 `Account` / `Password`（或先用 GUI 登录保存凭据）
2. `build.bat` 后运行 `publish\Taiji.Proxy.exe`
3. 默认监听 `http://127.0.0.1:8765/`

## 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/GetSessionList` | 会话列表（前 10 页） |
| GET | `/GetModelsList` | 模型与厂商分类 |
| POST | `/RenameSession` | `{ "sessionId": 123, "name": "新名称" }` |
| POST | `/DeleteSession` | `{ "sessionId": 123 }` |
| POST | `/v1/chat/completions` | `Authorization: Bearer <sessionId>` |
| GET | `/v1/models` | OpenAI 模型列表 |

## Python 示例

```python
from openai import OpenAI

# 先用 GET /GetSessionList 拿到 sessionId
SESSION_ID = "12345"

client = OpenAI(
    base_url="http://127.0.0.1:8765/v1",
    api_key=SESSION_ID,
)

r = client.chat.completions.create(
    model="模型value",  # 来自 GetModelsList
    messages=[{"role": "user", "content": "你好"}],
    stream=True,
)
for chunk in r:
    print(chunk.choices[0].delta.content or "", end="")
```

## 说明

- 聊天仅发送 `messages` 中最后一条 user 消息（上下文由上游 session 维护）
- 请求经单线程队列转发，避免上游并发问题
- 管理端点（GetSessionList 等）无需 Bearer，仅限本机使用
