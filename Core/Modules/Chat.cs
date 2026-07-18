using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taiji.Core.Models;
using Taiji.Core.Utils;

namespace Taiji.Core.Modules
{
    internal sealed class Chat
    {
        private readonly TaijiHttp _http;
        private readonly List _list;

        public Chat(TaijiHttp http, List list)
        {
            if (http == null) throw new ArgumentNullException(nameof(http));
            if (list == null) throw new ArgumentNullException(nameof(list));
            _http = http;
            _list = list;
        }

        public async Task<ChatStreamResult> SendAsync(
            string text,
            IList<ChatFilePayload> files,
            long? sessionId,
            bool thinking,
            bool webSearch,
            Action<string> onChunk,
            CancellationToken ct)
        {
            long sid;
            if (sessionId.HasValue)
                sid = sessionId.Value;
            else if (_list.CurrentSession != null)
                sid = _list.CurrentSession.Id;
            else
                throw new ApiException("无 session，请先创建或加入会话");

            if (string.IsNullOrEmpty(_http.Token))
                throw new ApiException("未登录");

            var body = new CompletionsRequest
            {
                Text = text ?? "",
                SessionId = sid,
                Files = files != null
                    ? new System.Collections.Generic.List<ChatFilePayload>(files)
                    : new System.Collections.Generic.List<ChatFilePayload>(),
                Thinking = thinking,
                WebSearch = webSearch
            };

            var result = new ChatStreamResult
            {
                SessionId = sid,
                Text = ""
            };
            var sb = new StringBuilder();
            var done = false;

            using (var resp = await _http.PostStreamAsync("/chat/completions", body, ct).ConfigureAwait(false))
            {
                if (resp.Headers.Contains("X-Chat-Task-Id"))
                {
                    foreach (var v in resp.Headers.GetValues("X-Chat-Task-Id"))
                    {
                        result.TaskId = v;
                        break;
                    }
                }

                var media = resp.Content.Headers.ContentType != null
                    ? resp.Content.Headers.ContentType.MediaType
                    : "";

                if (media != null && media.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0
                    && media.IndexOf("event-stream", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    var errText = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new ApiException($"非 SSE 响应: {errText}");
                }

                if (!resp.IsSuccessStatusCode)
                {
                    var errText = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new ApiException($"HTTP {(int)resp.StatusCode}: {(errText != null && errText.Length > 200 ? errText.Substring(0, 200) : errText)}");
                }

                using (var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var streamOk = await SseLineReader.ReadDataLinesAsync(stream, payload =>
                    {
                        if (done) return;
                        ct.ThrowIfCancellationRequested();

                        if (payload == "[DONE]")
                        {
                            done = true;
                            return;
                        }

                        SseMessage msg;
                        try
                        {
                            msg = JsonConvert.DeserializeObject<SseMessage>(payload);
                        }
                        catch (JsonException)
                        {
                            return;
                        }

                        if (msg == null) return;
                        if (msg.Code != 0)
                            throw new ApiException(msg.Err ?? msg.Msg ?? "SSE 业务错误", msg.Code);

                        if (!string.IsNullOrEmpty(msg.Id))
                            result.TaskId = msg.Id;

                        if (string.Equals(msg.Type, "string", StringComparison.OrdinalIgnoreCase))
                        {
                            var piece = msg.Data as string ?? Convert.ToString(msg.Data);
                            if (piece == null) piece = "";
                            sb.Append(piece);
                            result.StringEvents++;
                            onChunk?.Invoke(piece);
                        }
                        else if (string.Equals(msg.Type, "object", StringComparison.OrdinalIgnoreCase))
                        {
                            var jo = msg.Data as JObject;
                            if (jo == null && msg.Data != null)
                                jo = JObject.FromObject(msg.Data);
                            result.Record = jo;
                            if (sb.Length == 0 && jo != null)
                            {
                                var ai = (string)jo["aiText"];
                                if (!string.IsNullOrEmpty(ai))
                                {
                                    sb.Append(ai);
                                    if (result.StringEvents == 0)
                                        onChunk?.Invoke(ai);
                                }
                            }
                        }
                    }, ct).ConfigureAwait(false);

                    if (!done || !streamOk)
                        result.StreamInterrupted = true;
                }
            }

            result.Text = sb.ToString();
            return result;
        }

        private sealed class CompletionsRequest
        {
            [JsonProperty("text")]
            public string Text { get; set; }

            [JsonProperty("sessionId")]
            public long SessionId { get; set; }

            [JsonProperty("files")]
            public System.Collections.Generic.List<ChatFilePayload> Files { get; set; }

            [JsonProperty("thinking")]
            public bool Thinking { get; set; }

            [JsonProperty("webSearch")]
            public bool WebSearch { get; set; }
        }

        private sealed class SseMessage
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("data")]
            public object Data { get; set; }

            [JsonProperty("code")]
            public int Code { get; set; }

            [JsonProperty("err")]
            public string Err { get; set; }

            [JsonProperty("msg")]
            public string Msg { get; set; }
        }
    }
}
