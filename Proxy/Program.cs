using System;
using System.Threading;
using System.Threading.Tasks;
using Taiji.Core;

namespace Taiji.Proxy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var config = ProxyConfig.Load();
            ProxyLog.Info("=== Taiji Proxy ===");
            ProxyLog.Info("OpenAI 兼容代理 · api_key = sessionId");

            using (var api = new TaijiCore())
            {
                try
                {
                    LoginAsync(api, config).GetAwaiter().GetResult();
                    ProxyLog.Info("登录成功，已加载模型列表");
                }
                catch (Exception ex)
                {
                    ProxyLog.Error("启动失败: " + ex.Message);
                    return 1;
                }

                var server = new ProxyServer(api, config);

                server.Start();
                ProxyLog.Info("按 Ctrl+C 停止");
                ProxyLog.Info("GET  /GetSessionList");
                ProxyLog.Info("GET  /GetModelsList");
                ProxyLog.Info("POST /RenameSession");
                ProxyLog.Info("POST /DeleteSession");
                ProxyLog.Info("POST /v1/chat/completions  Bearer <sessionId>");
                ProxyLog.Info("GET  /v1/models");

                using (var stop = new ManualResetEvent(false))
                {
                    Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        ProxyLog.Info("正在停止…");
                        server.Stop();
                        stop.Set();
                    };
                    stop.WaitOne();
                }
            }

            return 0;
        }

        private static async Task LoginAsync(ITaijiCore api, ProxyConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.Account) && !string.IsNullOrWhiteSpace(config.Password))
            {
                await api.LoginAsync(config.Account, config.Password, config.RememberLogin)
                    .ConfigureAwait(false);
                await api.LoadModelsAsync().ConfigureAwait(false);
            }
            else
            {
                await api.EnsureAuthenticatedFromStoreAsync().ConfigureAwait(false);
            }
        }
    }
}
