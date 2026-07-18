using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Taiji.Core.Models;
using Taiji.Core.Utils;

namespace Taiji.Core.Modules
{
    internal sealed class List
    {
        private readonly TaijiHttp _http;

        public List(TaijiHttp http)
        {
            if (http == null) throw new ArgumentNullException(nameof(http));
            _http = http;
        }

        public ChatSessionInfo CurrentSession { get; private set; }

        public async Task<ChatSessionInfo> CreateSessionAsync(
            string model,
            bool webSearch,
            CancellationToken ct)
        {
            var body = new CreateSessionRequest
            {
                Model = model,
                Plugins = new System.Collections.Generic.List<object>(),
                Mcp = new System.Collections.Generic.List<object>(),
                WebSearch = webSearch
            };
            var session = await _http.PostDataAsync<ChatSessionInfo>("/chat/session", body, true, ct)
                .ConfigureAwait(false);
            if (session == null)
                throw new ApiException("创建会话失败：服务器未返回会话信息");
            CurrentSession = session;
            return session;
        }

        public void AttachSession(ChatSessionInfo session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            CurrentSession = session;
        }

        public async Task<PageResult<ChatSessionInfo>> ListSessionsPageAsync(
            int page,
            string search,
            CancellationToken ct)
        {
            if (page < 1) page = 1;
            var path = $"/chat/session?page={page}";
            if (!string.IsNullOrEmpty(search))
                path = $"{path}&search={Uri.EscapeDataString(search)}";

            var pageResult = await _http.GetDataAsync<PageResult<ChatSessionInfo>>(path, ct)
                .ConfigureAwait(false);
            if (pageResult == null)
                pageResult = new PageResult<ChatSessionInfo>();
            if (pageResult.Records == null)
                pageResult.Records = new System.Collections.Generic.List<ChatSessionInfo>();
            if (pageResult.Page <= 0)
                pageResult.Page = page;
            return pageResult;
        }

        public async Task<System.Collections.Generic.List<ChatSessionInfo>> ListAllSessionsAsync(CancellationToken ct)
        {
            var all = new System.Collections.Generic.List<ChatSessionInfo>();
            var page = 1;
            int pages;
            do
            {
                var p = await ListSessionsPageAsync(page, null, ct).ConfigureAwait(false);
                if (p.Records != null && p.Records.Count > 0)
                    all.AddRange(p.Records);
                pages = p.Pages > 0 ? p.Pages : 1;
                page++;
            } while (page <= pages);
            return all;
        }

        public async Task<ChatSessionInfo> UpdateSessionAsync(ChatSessionInfo session, CancellationToken ct)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var updated = await _http.PutDataAsync<ChatSessionInfo>(
                $"/chat/session/{session.Id}", session, ct).ConfigureAwait(false);
            if (updated == null)
                updated = session;
            if (CurrentSession != null && CurrentSession.Id == updated.Id)
                CurrentSession = updated;
            return updated;
        }

        public Task<ChatSessionInfo> RenameSessionAsync(
            ChatSessionInfo session,
            string newName,
            CancellationToken ct)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            session.Name = newName ?? "";
            return UpdateSessionAsync(session, ct);
        }

        public async Task DeleteSessionAsync(long sessionId, CancellationToken ct)
        {
            await _http.DeleteAsync($"/chat/session/{sessionId}", ct).ConfigureAwait(false);
            if (CurrentSession != null && CurrentSession.Id == sessionId)
                CurrentSession = null;
        }

        public async Task<PageResult<ChatRecord>> ListRecordsPageAsync(
            long sessionId,
            int page,
            CancellationToken ct)
        {
            if (page < 1) page = 1;
            var path = $"/chat/record/{sessionId}?page={page}";
            var pageResult = await _http.GetDataAsync<PageResult<ChatRecord>>(path, ct)
                .ConfigureAwait(false);
            if (pageResult == null)
                pageResult = new PageResult<ChatRecord>();
            if (pageResult.Records == null)
                pageResult.Records = new System.Collections.Generic.List<ChatRecord>();
            return pageResult;
        }

        public async Task<System.Collections.Generic.List<ChatRecord>> ListAllRecordsAsync(
            long sessionId,
            CancellationToken ct)
        {
            var all = new System.Collections.Generic.List<ChatRecord>();
            var page = 1;
            int pages;
            do
            {
                var p = await ListRecordsPageAsync(sessionId, page, ct).ConfigureAwait(false);
                if (p.Records != null && p.Records.Count > 0)
                    all.AddRange(p.Records);
                pages = p.Pages > 0 ? p.Pages : 1;
                page++;
            } while (page <= pages);

            all.Reverse();
            return all;
        }

        private sealed class CreateSessionRequest
        {
            [JsonProperty("model")]
            public string Model { get; set; }

            [JsonProperty("plugins")]
            public System.Collections.Generic.List<object> Plugins { get; set; }

            [JsonProperty("mcp")]
            public System.Collections.Generic.List<object> Mcp { get; set; }

            [JsonProperty("webSearch")]
            public bool WebSearch { get; set; }
        }
    }
}
