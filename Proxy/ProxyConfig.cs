using System;
using System.Configuration;

namespace Taiji.Proxy
{
    internal sealed class ProxyConfig
    {
        public string ListenUrl { get; private set; }
        public string Account { get; private set; }
        public string Password { get; private set; }
        public bool RememberLogin { get; private set; }

        public static ProxyConfig Load()
        {
            return new ProxyConfig
            {
                ListenUrl = Get("ListenUrl", "http://127.0.0.1:8765/"),
                Account = Get("Account", ""),
                Password = Get("Password", ""),
                RememberLogin = GetBool("RememberLogin", true)
            };
        }

        private static string Get(string key, string fallback)
        {
            var v = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        }

        private static bool GetBool(string key, bool fallback)
        {
            var v = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(v)) return fallback;
            return v.Equals("true", StringComparison.OrdinalIgnoreCase)
                || v.Equals("1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
