using System;
using System.Threading;
using System.Threading.Tasks;
using Taiji.Core.Models;
using Taiji.Core.Utils;

namespace Taiji.Core.Modules
{
    internal sealed class Auth
    {
        private readonly TaijiHttp _http;
        private readonly object _refreshLock = new object();
        private Task<bool> _refreshTask;

        public Auth(TaijiHttp http)
        {
            if (http == null) throw new ArgumentNullException("http");
            _http = http;
        }

        public LoginResult LastLogin { get; private set; }

        public bool TryApplyStoredToken()
        {
            var cred = CredentialStore.Load();
            if (cred == null || string.IsNullOrEmpty(cred.Token))
                return false;

            _http.Token = cred.Token;
            LastLogin = cred.ToLoginResult();
            return true;
        }

        public LoginPromptInfo GetLoginPromptInfo()
        {
            var cred = CredentialStore.Load();
            if (cred == null)
            {
                return new LoginPromptInfo
                {
                    Account = "",
                    RememberAutoLogin = false
                };
            }

            return new LoginPromptInfo
            {
                Account = cred.Account ?? "",
                RememberAutoLogin = cred.RememberAutoLogin
            };
        }

        public async Task<LoginResult> LoginAsync(
            string account,
            string password,
            bool rememberAutoLogin,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(account))
                throw new ApiException("请输入用户名");
            if (string.IsNullOrEmpty(password))
                throw new ApiException("请输入密码");

            var body = new LoginRequest
            {
                Account = account.Trim(),
                Password = password,
                Code = "",
                Captcha = "",
                Invite = "",
                Agreement = true,
                CaptchaId = ""
            };

            var data = await _http.PostDataAsync<LoginResult>("/user/login", body, false, ct)
                .ConfigureAwait(false);
            if (data == null || string.IsNullOrEmpty(data.Token))
                throw new ApiException("登录成功但未返回 token");

            _http.Token = data.Token;
            LastLogin = data;
            PersistCredentials(account.Trim(), password, rememberAutoLogin, data);
            return data;
        }

        public Task<bool> TryRefreshTokenAsync(CancellationToken ct)
        {
            lock (_refreshLock)
            {
                if (_refreshTask == null || _refreshTask.IsCompleted)
                    _refreshTask = TryRefreshTokenCoreAsync(ct);
                return _refreshTask;
            }
        }

        private async Task<bool> TryRefreshTokenCoreAsync(CancellationToken ct)
        {
            try
            {
                var cred = CredentialStore.Load();
                if (cred == null || !cred.RememberAutoLogin)
                    return false;
                if (string.IsNullOrEmpty(cred.Account) || string.IsNullOrEmpty(cred.Password))
                    return false;

                await LoginAsync(cred.Account, cred.Password, true, ct).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void PersistCredentials(
            string account,
            string password,
            bool rememberAutoLogin,
            LoginResult login)
        {
            var cred = new StoredCredentials
            {
                Token = login.Token,
                User = login.User,
                RememberAutoLogin = rememberAutoLogin
            };

            if (rememberAutoLogin)
            {
                cred.Account = account;
                cred.Password = password;
            }

            CredentialStore.Save(cred);
        }
    }
}
