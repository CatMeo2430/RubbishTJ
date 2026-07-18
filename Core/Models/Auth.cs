using Newtonsoft.Json;

namespace Taiji.Core.Models
{
    public sealed class LoginRequest
    {
        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("captcha")]
        public string Captcha { get; set; }

        [JsonProperty("invite")]
        public string Invite { get; set; }

        [JsonProperty("agreement")]
        public bool Agreement { get; set; }

        [JsonProperty("captchaId")]
        public string CaptchaId { get; set; }
    }

    public sealed class LoginUser
    {
        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }
    }

    public sealed class LoginResult
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("user")]
        public LoginUser User { get; set; }
    }

    /// <summary>磁盘持久化的登录凭据。</summary>
    public sealed class StoredCredentials
    {
        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("rememberAutoLogin")]
        public bool RememberAutoLogin { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("user")]
        public LoginUser User { get; set; }

        public LoginResult ToLoginResult()
        {
            return new LoginResult
            {
                Token = Token,
                User = User
            };
        }
    }

    /// <summary>登录框预填信息。</summary>
    public sealed class LoginPromptInfo
    {
        public string Account { get; set; }
        public bool RememberAutoLogin { get; set; }
    }

    /// <summary>用户在登录框提交的内容。</summary>
    public sealed class LoginCredentials
    {
        public string Account { get; set; }
        public string Password { get; set; }
        public bool RememberAutoLogin { get; set; }
        public bool Cancelled { get; set; }

        public static LoginCredentials CancelledResult()
        {
            return new LoginCredentials { Cancelled = true };
        }
    }
}
