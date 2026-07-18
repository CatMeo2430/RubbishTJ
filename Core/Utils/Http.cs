using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Taiji.Core.Utils
{
    internal sealed class ApiEnvelope
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("msg")]
        public string Msg { get; set; }

        [JsonProperty("data")]
        public JToken Data { get; set; }
    }

    internal sealed class TaijiHttp : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly object _refreshLock = new object();
        private Task<bool> _refreshTask;

        static TaijiHttp()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false;
        }

        public TaijiHttp(string baseUrl = null)
        {
            _baseUrl = (baseUrl ?? Constant.DefaultBaseUrl).TrimEnd('/');
            AppVersion = Constant.AppVersion;

            var handler = new WebRequestHandler
            {
                AutomaticDecompression = DecompressionMethods.None
            };

            _http = new HttpClient(handler);
            _http.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite);
        }

        public string Token { get; set; }

        public string AppVersion { get; set; }

        public Func<CancellationToken, Task<bool>> TokenRefresher { get; set; }

        public void Dispose()
        {
            _http.Dispose();
        }

        public async Task<T> GetDataAsync<T>(string path, CancellationToken ct = default(CancellationToken))
        {
            return await ExecuteWithAuthRetryAsync(
                () => GetDataCoreAsync<T>(path, ct),
                ct).ConfigureAwait(false);
        }

        public async Task<T> PostDataAsync<T>(string path, object body, bool auth = true, CancellationToken ct = default(CancellationToken))
        {
            return await ExecuteWithAuthRetryAsync(
                () => PostDataCoreAsync<T>(path, body, auth, ct),
                ct,
                auth).ConfigureAwait(false);
        }

        public async Task<T> PutDataAsync<T>(string path, object body, CancellationToken ct = default(CancellationToken))
        {
            return await ExecuteWithAuthRetryAsync(
                () => PutDataCoreAsync<T>(path, body, ct),
                ct).ConfigureAwait(false);
        }

        public async Task DeleteAsync(string path, CancellationToken ct = default(CancellationToken))
        {
            await ExecuteWithAuthRetryAsync(
                () => DeleteCoreAsync(path, ct),
                ct).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> PostStreamAsync(string path, object body, CancellationToken ct = default(CancellationToken))
        {
            var resp = await PostStreamCoreAsync(path, body, ct).ConfigureAwait(false);
            if (!NeedsAuthRetry(resp))
                return resp;

            resp.Dispose();
            if (!await TryRefreshTokenAsync(ct).ConfigureAwait(false))
                throw new ApiException("登录已过期", 2);

            return await PostStreamCoreAsync(path, body, ct).ConfigureAwait(false);
        }

        private async Task<T> GetDataCoreAsync<T>(string path, CancellationToken ct)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromMinutes(2));
                using (var req = CreateRequest(HttpMethod.Get, path, true))
                using (var resp = await _http.SendAsync(req, cts.Token).ConfigureAwait(false))
                {
                    return await ReadDataAsync<T>(resp).ConfigureAwait(false);
                }
            }
        }

        private async Task<T> PostDataCoreAsync<T>(string path, object body, bool auth, CancellationToken ct)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromMinutes(2));
                using (var req = CreateRequest(HttpMethod.Post, path, auth))
                {
                    if (body != null)
                    {
                        var json = JsonConvert.SerializeObject(body);
                        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    }
                    using (var resp = await _http.SendAsync(req, cts.Token).ConfigureAwait(false))
                    {
                        return await ReadDataAsync<T>(resp).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task<T> PutDataCoreAsync<T>(string path, object body, CancellationToken ct)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromMinutes(2));
                using (var req = CreateRequest(HttpMethod.Put, path, true))
                {
                    if (body != null)
                    {
                        var json = JsonConvert.SerializeObject(body);
                        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    }
                    using (var resp = await _http.SendAsync(req, cts.Token).ConfigureAwait(false))
                    {
                        return await ReadDataAsync<T>(resp).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task DeleteCoreAsync(string path, CancellationToken ct)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromMinutes(2));
                using (var req = CreateRequest(HttpMethod.Delete, path, true))
                using (var resp = await _http.SendAsync(req, cts.Token).ConfigureAwait(false))
                {
                    await ReadDataTokenAsync(resp).ConfigureAwait(false);
                }
            }
        }

        private async Task<HttpResponseMessage> PostStreamCoreAsync(string path, object body, CancellationToken ct)
        {
            var req = CreateRequest(HttpMethod.Post, path, true);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            req.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
            req.Headers.ConnectionClose = false;

            var json = JsonConvert.SerializeObject(body);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
        }

        private async Task<T> ExecuteWithAuthRetryAsync<T>(
            Func<Task<T>> action,
            CancellationToken ct,
            bool auth = true)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                if (!auth || ex.Code != 2)
                    throw;
            }

            if (!await TryRefreshTokenAsync(ct).ConfigureAwait(false))
                throw new ApiException("登录已过期", 2);

            return await action().ConfigureAwait(false);
        }

        private async Task ExecuteWithAuthRetryAsync(
            Func<Task> action,
            CancellationToken ct,
            bool auth = true)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (ApiException ex)
            {
                if (!auth || ex.Code != 2)
                    throw;
            }

            if (!await TryRefreshTokenAsync(ct).ConfigureAwait(false))
                throw new ApiException("登录已过期", 2);

            await action().ConfigureAwait(false);
        }

        private Task<bool> TryRefreshTokenAsync(CancellationToken ct)
        {
            if (TokenRefresher == null)
                return Task.FromResult(false);

            lock (_refreshLock)
            {
                if (_refreshTask == null || _refreshTask.IsCompleted)
                    _refreshTask = TokenRefresher(ct);
                return _refreshTask;
            }
        }

        private static bool NeedsAuthRetry(HttpResponseMessage resp)
        {
            return resp != null && (int)resp.StatusCode == 401;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool auth)
        {
            var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? path
                : _baseUrl + (path.StartsWith("/") ? path : "/" + path);
            var req = new HttpRequestMessage(method, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            req.Headers.TryAddWithoutValidation("X-APP-VERSION", AppVersion);
            if (auth && !string.IsNullOrEmpty(Token))
                req.Headers.TryAddWithoutValidation("Authorization", Token);
            return req;
        }

        private static async Task<T> ReadDataAsync<T>(HttpResponseMessage resp)
        {
            var token = await ReadDataTokenAsync(resp).ConfigureAwait(false);
            if (token == null || token.Type == JTokenType.Null)
                return default(T);
            return token.ToObject<T>();
        }

        private static async Task<JToken> ReadDataTokenAsync(HttpResponseMessage resp)
        {
            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            ApiEnvelope env;
            try
            {
                env = JsonConvert.DeserializeObject<ApiEnvelope>(text);
            }
            catch (Exception ex)
            {
                throw new ApiException("非 JSON 响应 HTTP " + (int)resp.StatusCode + ": " +
                    (text != null && text.Length > 200 ? text.Substring(0, 200) : text), ex);
            }

            if (env == null)
                throw new ApiException("空响应");

            if ((int)resp.StatusCode == 401 || env.Code == 2)
                throw new ApiException(env.Msg ?? "登录已过期", 2);

            if (env.Code != 0)
                throw new ApiException(env.Msg ?? ("业务错误 code=" + env.Code), env.Code);

            return env.Data;
        }
    }

    internal static class SseLineReader
    {
        /// <returns>true 表示正常读到流结束；false 表示 I/O 异常中断。</returns>
        public static async Task<bool> ReadDataLinesAsync(
            Stream stream,
            Action<string> onDataLine,
            CancellationToken ct)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (onDataLine == null) throw new ArgumentNullException("onDataLine");

            var buf = new byte[256];
            var lineBuf = new MemoryStream(512);
            var ioError = false;

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                int n;
                try
                {
                    n = await stream.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (IOException)
                {
                    ioError = true;
                    break;
                }

                if (n <= 0)
                    break;

                for (var i = 0; i < n; i++)
                {
                    var b = buf[i];
                    if (b == (byte)'\n')
                    {
                        EmitLine(lineBuf, onDataLine);
                        lineBuf.SetLength(0);
                    }
                    else if (b != (byte)'\r')
                    {
                        lineBuf.WriteByte(b);
                    }
                }
            }

            if (lineBuf.Length > 0)
                EmitLine(lineBuf, onDataLine);

            return !ioError;
        }

        private static void EmitLine(MemoryStream lineBuf, Action<string> onDataLine)
        {
            if (lineBuf.Length == 0)
                return;

            var line = Encoding.UTF8.GetString(lineBuf.ToArray());
            if (line.Length == 0 || line[0] == ':')
                return;
            if (!line.StartsWith("data:", StringComparison.Ordinal))
                return;

            onDataLine(line.Substring(5).TrimStart());
        }
    }
}
