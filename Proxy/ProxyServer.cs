using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Taiji.Core;
using Taiji.Core.Models;
using Taiji.Core.Utils;

namespace Taiji.Proxy
{
    internal sealed class ProxyServer
    {
        private const int SessionListMaxPages = 10;

        private readonly ITaijiCore _api;
        private readonly ProxyConfig _config;
        private readonly HttpListener _listener;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private volatile bool _running;

        public ProxyServer(ITaijiCore api, ProxyConfig config)
        {
            _api = api;
            _config = config;
            _listener = new HttpListener();
            var prefix = config.ListenUrl;
            if (!prefix.EndsWith("/"))
                prefix += "/";
            _listener.Prefixes.Add(prefix);
        }

        public void Start()
        {
            _listener.Start();
            _running = true;
            ProxyLog.Info("监听 " + string.Join(", ", _listener.Prefixes));
            ListenLoop();
        }

        public void Stop()
        {
            _running = false;
            try { _listener.Stop(); }
            catch { /* ignore */ }
            ProxyLog.Info("HTTP 服务已停止");
        }

        private async void ListenLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await Task.Factory.FromAsync<HttpListenerContext>(
                        _listener.BeginGetContext,
                        _listener.EndGetContext,
                        null).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    if (!_running) break;
                    continue;
                }

                Task.Run(() => HandleRequestAsync(ctx));
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var path = NormalizePath(req);
            var log = RequestLog.Begin(req, path);

            try
            {
                await HandleRequestCoreAsync(ctx, log).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ProxyLog.Error("[" + log.Id + "] 未处理异常: " + ex.Message);
                try
                {
                    if (ctx.Response.OutputStream.CanWrite)
                    {
                        WriteJson(log, ctx.Response, 500, new OpenAiErrorResponse
                        {
                            Error = new OpenAiErrorBody
                            {
                                Message = ex.Message,
                                Type = "internal_error"
                            }
                        }, "error=" + ProxyLog.Preview(ex.Message, 80));
                    }
                }
                catch (Exception writeEx)
                {
                    ProxyLog.Error("[" + log.Id + "] 写入错误响应失败: " + writeEx.Message);
                }
            }
        }

        private async Task HandleRequestCoreAsync(HttpListenerContext ctx, RequestLog log)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            var path = log.Path;

            if (req.HttpMethod == "OPTIONS")
            {
                ProxyLog.RequestStart(log, "CORS preflight");
                AddCors(res);
                res.StatusCode = 204;
                res.Close();
                ProxyLog.RequestEnd(log, 204, "CORS ok");
                return;
            }

            AddCors(res);

            if (path.Equals("/GetSessionList", StringComparison.OrdinalIgnoreCase)
                && req.HttpMethod == "GET")
            {
                ProxyLog.RequestStart(log, null);
                await RunGatedAsync(log, () => GetSessionListAsync(res, log)).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/GetModelsList", StringComparison.OrdinalIgnoreCase)
                && req.HttpMethod == "GET")
            {
                ProxyLog.RequestStart(log, null);
                await RunGatedAsync(log, () => GetModelsListAsync(res, log)).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/RenameSession", StringComparison.OrdinalIgnoreCase)
                && req.HttpMethod == "POST")
            {
                var body = await ReadBodyAsync(req).ConfigureAwait(false);
                var payload = JsonConvert.DeserializeObject<RenameSessionRequest>(body);
                var detail = payload != null
                    ? "sessionId=" + payload.SessionId + " name=" + ProxyLog.Preview(payload.Name ?? "", 60)
                    : "body=" + ProxyLog.Preview(body, 80);
                ProxyLog.RequestStart(log, detail);
                await RunGatedAsync(log, () => RenameSessionAsync(res, log, payload)).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/DeleteSession", StringComparison.OrdinalIgnoreCase)
                && req.HttpMethod == "POST")
            {
                var body = await ReadBodyAsync(req).ConfigureAwait(false);
                var payload = JsonConvert.DeserializeObject<SessionIdRequest>(body);
                var detail = payload != null
                    ? "sessionId=" + payload.SessionId
                    : "body=" + ProxyLog.Preview(body, 80);
                ProxyLog.RequestStart(log, detail);
                await RunGatedAsync(log, () => DeleteSessionAsync(res, log, payload)).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/v1/models", StringComparison.OrdinalIgnoreCase)
                && req.HttpMethod == "GET")
            {
                ProxyLog.RequestStart(log, null);
                await RunGatedAsync(log, () => GetOpenAiModelsAsync(res, log)).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/v1/chat/completions", StringComparison.OrdinalIgnoreCase)
                && req.HttpMethod == "POST")
            {
                var sessionId = ParseSessionApiKey(req);
                log.SessionId = sessionId;

                var body = await ReadBodyAsync(req).ConfigureAwait(false);
                var chatReq = JsonConvert.DeserializeObject<OpenAiChatRequest>(body);

                if (!sessionId.HasValue)
                {
                    ProxyLog.RequestStart(log, "invalid api_key body=" + ProxyLog.Preview(body, 80));
                    WriteJson(log, res, 401, new OpenAiErrorResponse
                    {
                        Error = new OpenAiErrorBody
                        {
                            Message = "Authorization Bearer 须为数字 sessionId",
                            Type = "invalid_api_key"
                        }
                    }, "invalid api_key");
                    return;
                }

                if (chatReq == null || chatReq.Messages == null || chatReq.Messages.Count == 0)
                {
                    ProxyLog.RequestStart(log, "empty messages");
                    WriteJson(log, res, 400, new OpenAiErrorResponse
                    {
                        Error = new OpenAiErrorBody { Message = "messages 不能为空", Type = "invalid_request" }
                    }, "empty messages");
                    return;
                }

                string preview;
                int imageCount;
                SummarizeChatRequest(chatReq, out preview, out imageCount);
                var chatDetail = "model=" + (chatReq.Model ?? "")
                    + " stream=" + (chatReq.Stream ? "true" : "false")
                    + " messages=" + chatReq.Messages.Count
                    + " images=" + imageCount
                    + " prompt=" + preview;
                ProxyLog.RequestStart(log, chatDetail);

                if (chatReq.Stream)
                    await RunGatedStreamingAsync(log, res, sessionId.Value, chatReq).ConfigureAwait(false);
                else
                    await RunGatedAsync(log, () => ChatCompletionsAsync(res, log, sessionId.Value, chatReq)).ConfigureAwait(false);
                return;
            }

            ProxyLog.RequestStart(log, "unknown route");
            WriteJson(log, res, 404, new OpenAiErrorResponse
            {
                Error = new OpenAiErrorBody { Message = "Not found: " + path, Type = "not_found" }
            }, "not_found");
        }

        private static string NormalizePath(HttpListenerRequest req)
        {
            var path = (req.Url.AbsolutePath ?? "/").TrimEnd('/');
            if (path.Length == 0) path = "/";
            return path;
        }

        private static void SummarizeChatRequest(OpenAiChatRequest chatReq, out string preview, out int imageCount)
        {
            preview = "";
            imageCount = 0;
            if (chatReq.Messages == null) return;

            OpenAiMessage lastUser = null;
            for (var i = chatReq.Messages.Count - 1; i >= 0; i--)
            {
                if (string.Equals(chatReq.Messages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    lastUser = chatReq.Messages[i];
                    break;
                }
            }
            if (lastUser == null) return;

            string text;
            List<ChatFilePayload> files;
            try
            {
                ParseUserMessage(chatReq.Messages, out text, out files);
                preview = ProxyLog.Preview(text, 120);
                imageCount = files != null ? files.Count : 0;
            }
            catch
            {
                preview = "(parse error)";
            }
        }

        private async Task RunGatedAsync(RequestLog log, Func<Task> action)
        {
            ProxyLog.Info("[" + log.Id + "] 等待上游队列…");
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                ProxyLog.Info("[" + log.Id + "] 开始处理");
                await action().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task RunGatedStreamingAsync(RequestLog log, HttpListenerResponse res, long sessionId, OpenAiChatRequest chatReq)
        {
            ProxyLog.Info("[" + log.Id + "] 等待上游队列…");
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                ProxyLog.Info("[" + log.Id + "] 开始流式处理");
                await ChatCompletionsStreamAsync(res, log, sessionId, chatReq).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private static void AddCors(HttpListenerResponse res)
        {
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Authorization, Content-Type");
        }

        private static async Task<string> ReadBodyAsync(HttpListenerRequest req)
        {
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        private static long? ParseSessionApiKey(HttpListenerRequest req)
        {
            var auth = req.Headers["Authorization"];
            if (string.IsNullOrWhiteSpace(auth)) return null;
            const string prefix = "Bearer ";
            if (!auth.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            var key = auth.Substring(prefix.Length).Trim();
            long id;
            return long.TryParse(key, out id) ? id : (long?)null;
        }

        private async Task GetSessionListAsync(HttpListenerResponse res, RequestLog log)
        {
            var items = new List<SessionListItem>();
            var pagesLoaded = 0;
            for (var page = 1; page <= SessionListMaxPages; page++)
            {
                var p = await _api.ListSessionsPageAsync(page).ConfigureAwait(false);
                pagesLoaded = page;
                if (p.Records != null)
                {
                    foreach (var s in p.Records)
                    {
                        items.Add(new SessionListItem
                        {
                            SessionId = s.Id,
                            Name = s.DisplayName,
                            Model = s.Model,
                            Updated = s.Updated
                        });
                    }
                }
                if (p.Pages > 0 && page >= p.Pages) break;
                if (p.Records == null || p.Records.Count == 0) break;
            }

            WriteJson(log, res, 200, new SessionListResponse
            {
                Sessions = items,
                PagesLoaded = pagesLoaded
            }, "sessions=" + items.Count + " pages=" + pagesLoaded);
        }

        private Task GetModelsListAsync(HttpListenerResponse res, RequestLog log)
        {
            var tmpl = _api.ModelTmpl;
            if (tmpl == null)
                throw new ApiException("模型列表未加载");

            var groups = new List<ProviderGroup>();
            foreach (var providerName in _api.GetProviderNamesWithModels())
            {
                var models = _api.ModelsByProviderName(providerName);
                groups.Add(new ProviderGroup
                {
                    Name = providerName,
                    Models = models.Select(m => new ModelListItem
                    {
                        Id = m.Value,
                        Label = m.NameText,
                        Provider = m.ProviderName ?? providerName,
                        ImageInput = m.ImageInput,
                        Integral = m.Integral
                    }).ToList()
                });
            }

            var modelCount = groups.Sum(g => g.Models != null ? g.Models.Count : 0);
            WriteJson(log, res, 200, new ModelsListResponse
            {
                DefaultModel = tmpl.DefModel,
                MaxFileCount = tmpl.MFileCount > 0 ? tmpl.MFileCount : Constant.DefaultMaxFileCount,
                MaxFileSizeMb = tmpl.MFileSize > 0 ? tmpl.MFileSize : Constant.DefaultMaxFileMb,
                Providers = groups
            }, "providers=" + groups.Count + " models=" + modelCount);
            return Task.FromResult(0);
        }

        private Task GetOpenAiModelsAsync(HttpListenerResponse res, RequestLog log)
        {
            var tmpl = _api.ModelTmpl;
            if (tmpl == null || tmpl.Models == null)
                throw new ApiException("模型列表未加载");

            var count = tmpl.Models.Count;
            WriteJson(log, res, 200, new OpenAiModelsResponse
            {
                Object = "list",
                Data = tmpl.Models.Select(m => new OpenAiModelDto
                {
                    Id = m.Value,
                    Object = "model",
                    OwnedBy = m.ProviderName ?? "taiji"
                }).ToList()
            }, "models=" + count);
            return Task.FromResult(0);
        }

        private async Task RenameSessionAsync(HttpListenerResponse res, RequestLog log, RenameSessionRequest req)
        {
            if (req == null || req.SessionId <= 0)
            {
                WriteJson(log, res, 400, new OpenAiErrorResponse
                {
                    Error = new OpenAiErrorBody { Message = "sessionId 无效", Type = "invalid_request" }
                }, "invalid sessionId");
                return;
            }
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                WriteJson(log, res, 400, new OpenAiErrorResponse
                {
                    Error = new OpenAiErrorBody { Message = "name 不能为空", Type = "invalid_request" }
                }, "empty name");
                return;
            }

            var session = await FindSessionAsync(req.SessionId).ConfigureAwait(false);
            if (session == null)
            {
                WriteJson(log, res, 404, new OpenAiErrorResponse
                {
                    Error = new OpenAiErrorBody { Message = "会话不存在", Type = "not_found" }
                }, "session not found");
                return;
            }

            var updated = await _api.RenameSessionAsync(session, req.Name.Trim()).ConfigureAwait(false);
            WriteJson(log, res, 200, new SessionListItem
            {
                SessionId = updated.Id,
                Name = updated.DisplayName,
                Model = updated.Model,
                Updated = updated.Updated
            }, "renamed name=" + ProxyLog.Preview(updated.DisplayName, 60));
        }

        private async Task DeleteSessionAsync(HttpListenerResponse res, RequestLog log, SessionIdRequest req)
        {
            if (req == null || req.SessionId <= 0)
            {
                WriteJson(log, res, 400, new OpenAiErrorResponse
                {
                    Error = new OpenAiErrorBody { Message = "sessionId 无效", Type = "invalid_request" }
                }, "invalid sessionId");
                return;
            }

            await _api.DeleteSessionAsync(req.SessionId).ConfigureAwait(false);
            WriteJson(log, res, 200, new { ok = true, sessionId = req.SessionId }, "deleted");
        }

        private async Task<ChatSessionInfo> FindSessionAsync(long sessionId)
        {
            for (var page = 1; page <= SessionListMaxPages; page++)
            {
                var p = await _api.ListSessionsPageAsync(page).ConfigureAwait(false);
                if (p.Records != null)
                {
                    var hit = p.Records.FirstOrDefault(s => s.Id == sessionId);
                    if (hit != null) return hit;
                }
                if (p.Pages > 0 && page >= p.Pages) break;
                if (p.Records == null || p.Records.Count == 0) break;
            }
            return null;
        }

        private async Task ChatCompletionsAsync(HttpListenerResponse res, RequestLog log, long sessionId, OpenAiChatRequest chatReq)
        {
            string text;
            List<ChatFilePayload> files;
            ParseUserMessage(chatReq.Messages, out text, out files);

            var result = await _api.SendMessageAsync(
                string.IsNullOrEmpty(text) ? " " : text,
                files,
                sessionId,
                false,
                false,
                null).ConfigureAwait(false);

            var completionId = "chatcmpl-" + (result.TaskId ?? sessionId.ToString());
            var respModel = chatReq.Model;
            if (string.IsNullOrEmpty(respModel) && result.Record != null)
                respModel = (string)result.Record["model"];

            var answer = result.Text ?? "";
            WriteJson(log, res, 200, new
            {
                id = completionId,
                @object = "chat.completion",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = respModel,
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = answer },
                        finish_reason = result.StreamInterrupted ? "length" : "stop"
                    }
                },
                session_id = sessionId
            }, "chars=" + answer.Length
                + " interrupted=" + (result.StreamInterrupted ? "true" : "false")
                + " preview=" + ProxyLog.Preview(answer, 100));
        }

        private async Task ChatCompletionsStreamAsync(HttpListenerResponse res, RequestLog log, long sessionId, OpenAiChatRequest chatReq)
        {
            string text;
            List<ChatFilePayload> files;
            ParseUserMessage(chatReq.Messages, out text, out files);

            res.StatusCode = 200;
            res.ContentType = "text/event-stream; charset=utf-8";
            res.Headers.Add("Cache-Control", "no-cache");
            res.SendChunked = true;

            var completionId = "chatcmpl-" + sessionId + "-" + DateTime.UtcNow.Ticks;
            var model = chatReq.Model ?? "";
            var stream = res.OutputStream;
            var enc = new UTF8Encoding(false);
            var chunkCount = 0;
            var totalChars = 0;
            var interrupted = false;
            string errorMsg = null;

            try
            {
                var result = await _api.SendMessageAsync(
                    string.IsNullOrEmpty(text) ? " " : text,
                    files,
                    sessionId,
                    false,
                    false,
                    piece =>
                    {
                        if (string.IsNullOrEmpty(piece)) return;
                        chunkCount++;
                        totalChars += piece.Length;
                        WriteSseChunk(stream, enc, completionId, model, piece);
                    }).ConfigureAwait(false);

                interrupted = result.StreamInterrupted;
                WriteSseChunk(stream, enc, completionId, model, null, "stop");
                if (result.StreamInterrupted)
                {
                    var warn = enc.GetBytes("data: " + JsonConvert.SerializeObject(new
                    {
                        error = new { message = "连接已中断，回复可能不完整", type = "stream_interrupted" }
                    }) + "\n\n");
                    stream.Write(warn, 0, warn.Length);
                }

                var done = enc.GetBytes("data: [DONE]\n\n");
                stream.Write(done, 0, done.Length);
            }
            catch (ApiException ex)
            {
                errorMsg = ex.Message;
                var err = enc.GetBytes("data: " + JsonConvert.SerializeObject(new
                {
                    error = new { message = ex.Message, type = "api_error", code = ex.Code }
                }) + "\n\n");
                stream.Write(err, 0, err.Length);
                var done = enc.GetBytes("data: [DONE]\n\n");
                stream.Write(done, 0, done.Length);
            }
            finally
            {
                stream.Flush();
                res.Close();
                var detail = "stream chunks=" + chunkCount
                    + " chars=" + totalChars
                    + " interrupted=" + (interrupted ? "true" : "false");
                if (!string.IsNullOrEmpty(errorMsg))
                    detail += " error=" + ProxyLog.Preview(errorMsg, 80);
                ProxyLog.RequestEnd(log, errorMsg != null ? 502 : 200, detail);
            }
        }

        private static void WriteSseChunk(Stream stream, UTF8Encoding enc, string id, string model, string content, string finishReason = null)
        {
            object payload;
            if (finishReason != null)
            {
                payload = new
                {
                    id,
                    @object = "chat.completion.chunk",
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            delta = new { },
                            finish_reason = finishReason
                        }
                    }
                };
            }
            else
            {
                payload = new
                {
                    id,
                    @object = "chat.completion.chunk",
                    model,
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            delta = new { content },
                            finish_reason = (string)null
                        }
                    }
                };
            }

            var line = enc.GetBytes("data: " + JsonConvert.SerializeObject(payload) + "\n\n");
            stream.Write(line, 0, line.Length);
            stream.Flush();
        }

        private static void ParseUserMessage(
            IList<OpenAiMessage> messages,
            out string text,
            out List<ChatFilePayload> files)
        {
            text = "";
            files = new List<ChatFilePayload>();
            if (messages == null || messages.Count == 0) return;

            OpenAiMessage lastUser = null;
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                if (string.Equals(messages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    lastUser = messages[i];
                    break;
                }
            }
            if (lastUser == null)
                throw new ApiException("messages 中缺少 user 消息");

            var content = lastUser.Content;
            if (content == null)
                return;

            if (content.Type == JTokenType.String)
            {
                text = content.Value<string>() ?? "";
                return;
            }

            if (content.Type == JTokenType.Array)
            {
                var sb = new StringBuilder();
                foreach (var part in content)
                {
                    var type = (string)part["type"];
                    if (type == "text")
                    {
                        var t = (string)part["text"];
                        if (!string.IsNullOrEmpty(t))
                        {
                            if (sb.Length > 0) sb.Append("\n");
                            sb.Append(t);
                        }
                    }
                    else if (type == "image_url")
                    {
                        var imageUrl = part["image_url"] as JObject;
                        var url = imageUrl != null ? (string)imageUrl["url"] : null;
                        if (!string.IsNullOrEmpty(url))
                            files.Add(ParseImageUrl(url));
                    }
                }
                text = sb.ToString();
            }
        }

        private static ChatFilePayload ParseImageUrl(string url)
        {
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return new ChatFilePayload
                {
                    Name = "image.png",
                    Data = url
                };
            }
            throw new ApiException("仅支持 data:image/...;base64,... 图片 URL");
        }

        private static void WriteJson(RequestLog log, HttpListenerResponse res, int status, object body, string responseDetail)
        {
            var json = JsonConvert.SerializeObject(body);
            var bytes = Encoding.UTF8.GetBytes(json);
            res.StatusCode = status;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.Close();

            var detail = responseDetail ?? "";
            if (detail.Length > 0)
                detail += " ";
            detail += "bytes=" + bytes.Length;
            ProxyLog.RequestEnd(log, status, detail);
        }
    }
}
