using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Taiji.Core.Models;
using Taiji.Core.Modules;
using Taiji.Core.Utils;
using ListModule = Taiji.Core.Modules.List;

namespace Taiji.Core
{
    public interface ITaijiCore : IDisposable
    {
        string Token { get; }
        LoginResult LastLogin { get; }
        ChatTmpl ModelTmpl { get; }
        ChatSessionInfo CurrentSession { get; }

        Task<LoginResult> LoginAsync(
            string account,
            string password,
            bool rememberAutoLogin,
            CancellationToken ct = default(CancellationToken));

        LoginPromptInfo GetLoginPromptInfo();

        Task<LoginResult> EnsureAuthenticatedAsync(
            Func<LoginPromptInfo, Task<LoginCredentials>> promptLoginAsync,
            CancellationToken ct = default(CancellationToken));

        /// <summary>从 CredentialStore 恢复登录（无交互），供 Proxy 等无 UI 场景使用。</summary>
        Task<LoginResult> EnsureAuthenticatedFromStoreAsync(CancellationToken ct = default(CancellationToken));

        Task<ChatTmpl> LoadModelsAsync(CancellationToken ct = default(CancellationToken));
        IList<ProviderInfo> GetProvidersOrdered();
        IList<string> GetProviderNamesWithModels();
        IList<ModelInfo> ModelsByProviderName(string providerName);
        ModelInfo FindModelByValue(string value);

        Task<PageResult<ChatSessionInfo>> ListSessionsPageAsync(int page = 1, string search = null, CancellationToken ct = default(CancellationToken));
        Task<List<ChatSessionInfo>> ListAllSessionsAsync(CancellationToken ct = default(CancellationToken));
        Task<ChatSessionInfo> CreateSessionAsync(string model, bool webSearch = false, CancellationToken ct = default(CancellationToken));
        void AttachSession(ChatSessionInfo session);
        Task<ChatSessionInfo> UpdateSessionAsync(ChatSessionInfo session, CancellationToken ct = default(CancellationToken));
        Task<ChatSessionInfo> RenameSessionAsync(ChatSessionInfo session, string newName, CancellationToken ct = default(CancellationToken));
        Task DeleteSessionAsync(long sessionId, CancellationToken ct = default(CancellationToken));

        Task<PageResult<ChatRecord>> ListRecordsPageAsync(long sessionId, int page = 1, CancellationToken ct = default(CancellationToken));
        Task<List<ChatRecord>> ListAllRecordsAsync(long sessionId, CancellationToken ct = default(CancellationToken));

        Task<ChatStreamResult> SendMessageAsync(
            string text,
            IList<ChatFilePayload> files = null,
            long? sessionId = null,
            bool thinking = false,
            bool webSearch = false,
            Action<string> onChunk = null,
            CancellationToken ct = default(CancellationToken));
    }

    public sealed class TaijiCore : ITaijiCore
    {
        private readonly TaijiHttp _http;
        private readonly Auth _auth;
        private readonly Catalog _catalog;
        private readonly ListModule _list;
        private readonly Chat _chat;

        public TaijiCore(string baseUrl = null)
        {
            _http = new TaijiHttp(baseUrl);
            _auth = new Auth(_http);
            _http.TokenRefresher = ct => _auth.TryRefreshTokenAsync(ct);
            _catalog = new Catalog(_http);
            _list = new ListModule(_http);
            _chat = new Chat(_http, _list);
        }

        public string Token
        {
            get { return _http.Token; }
        }

        public LoginResult LastLogin
        {
            get { return _auth.LastLogin; }
        }

        public ChatTmpl ModelTmpl
        {
            get { return _catalog.Tmpl; }
        }

        public ChatSessionInfo CurrentSession
        {
            get { return _list.CurrentSession; }
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        public Task<LoginResult> LoginAsync(
            string account,
            string password,
            bool rememberAutoLogin,
            CancellationToken ct = default(CancellationToken))
        {
            return _auth.LoginAsync(account, password, rememberAutoLogin, ct);
        }

        public LoginPromptInfo GetLoginPromptInfo()
        {
            return _auth.GetLoginPromptInfo();
        }

        public async Task<LoginResult> EnsureAuthenticatedAsync(
            Func<LoginPromptInfo, Task<LoginCredentials>> promptLoginAsync,
            CancellationToken ct = default(CancellationToken))
        {
            if (_auth.TryApplyStoredToken())
            {
                try
                {
                    await _catalog.LoadAsync(ct).ConfigureAwait(false);
                    return _auth.LastLogin;
                }
                catch (ApiException)
                {
                }
            }

            var stored = CredentialStore.Load();
            if (stored != null && stored.RememberAutoLogin
                && !string.IsNullOrEmpty(stored.Account)
                && !string.IsNullOrEmpty(stored.Password))
            {
                try
                {
                    var login = await _auth.LoginAsync(stored.Account, stored.Password, true, ct)
                        .ConfigureAwait(false);
                    await _catalog.LoadAsync(ct).ConfigureAwait(false);
                    return login;
                }
                catch
                {
                }
            }

            if (promptLoginAsync == null)
            {
                throw new ApiException(
                    "无法从凭据文件登录（" + CredentialStore.FilePath
                    + "）。请先用 GUI 登录，或在 Proxy\\App.config 配置 Account/Password。");
            }

            var hint = _auth.GetLoginPromptInfo();
            var input = await promptLoginAsync(hint).ConfigureAwait(false);
            if (input == null || input.Cancelled)
                throw new ApiException("用户取消登录");

            var result = await _auth.LoginAsync(
                input.Account,
                input.Password,
                input.RememberAutoLogin,
                ct).ConfigureAwait(false);
            await _catalog.LoadAsync(ct).ConfigureAwait(false);
            return result;
        }

        public Task<LoginResult> EnsureAuthenticatedFromStoreAsync(CancellationToken ct = default(CancellationToken))
        {
            return EnsureAuthenticatedAsync(null, ct);
        }

        public Task<ChatTmpl> LoadModelsAsync(CancellationToken ct = default(CancellationToken))
        {
            return _catalog.LoadAsync(ct);
        }

        public IList<ProviderInfo> GetProvidersOrdered()
        {
            return _catalog.GetProvidersOrdered();
        }

        public IList<string> GetProviderNamesWithModels()
        {
            return _catalog.GetProviderNamesWithModels();
        }

        public IList<ModelInfo> ModelsByProviderName(string providerName)
        {
            return _catalog.ModelsByProviderName(providerName);
        }

        public ModelInfo FindModelByValue(string value)
        {
            return _catalog.FindByValue(value);
        }

        public Task<PageResult<ChatSessionInfo>> ListSessionsPageAsync(int page = 1, string search = null, CancellationToken ct = default(CancellationToken))
        {
            return _list.ListSessionsPageAsync(page, search, ct);
        }

        public Task<List<ChatSessionInfo>> ListAllSessionsAsync(CancellationToken ct = default(CancellationToken))
        {
            return _list.ListAllSessionsAsync(ct);
        }

        public Task<ChatSessionInfo> CreateSessionAsync(string model, bool webSearch = false, CancellationToken ct = default(CancellationToken))
        {
            return _list.CreateSessionAsync(model, webSearch, ct);
        }

        public void AttachSession(ChatSessionInfo session)
        {
            _list.AttachSession(session);
        }

        public Task<ChatSessionInfo> UpdateSessionAsync(ChatSessionInfo session, CancellationToken ct = default(CancellationToken))
        {
            return _list.UpdateSessionAsync(session, ct);
        }

        public Task<ChatSessionInfo> RenameSessionAsync(ChatSessionInfo session, string newName, CancellationToken ct = default(CancellationToken))
        {
            return _list.RenameSessionAsync(session, newName, ct);
        }

        public Task DeleteSessionAsync(long sessionId, CancellationToken ct = default(CancellationToken))
        {
            return _list.DeleteSessionAsync(sessionId, ct);
        }

        public Task<PageResult<ChatRecord>> ListRecordsPageAsync(long sessionId, int page = 1, CancellationToken ct = default(CancellationToken))
        {
            return _list.ListRecordsPageAsync(sessionId, page, ct);
        }

        public Task<List<ChatRecord>> ListAllRecordsAsync(long sessionId, CancellationToken ct = default(CancellationToken))
        {
            return _list.ListAllRecordsAsync(sessionId, ct);
        }

        public Task<ChatStreamResult> SendMessageAsync(
            string text,
            IList<ChatFilePayload> files = null,
            long? sessionId = null,
            bool thinking = false,
            bool webSearch = false,
            Action<string> onChunk = null,
            CancellationToken ct = default(CancellationToken))
        {
            return _chat.SendAsync(text, files, sessionId, thinking, webSearch, onChunk, ct);
        }
    }
}
