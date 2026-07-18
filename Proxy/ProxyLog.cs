using System;
using System.Net;
using System.Text;

namespace Taiji.Proxy
{
    internal enum ProxyLogLevel
    {
        Info,
        Warn,
        Error
    }

    internal sealed class RequestLog
    {
        public string Id { get; private set; }
        public DateTime StartUtc { get; private set; }
        public string ClientIp { get; set; }
        public string UserAgent { get; set; }
        public string Method { get; set; }
        public string Path { get; set; }
        public long? SessionId { get; set; }

        public static RequestLog Begin(HttpListenerRequest req, string path, long? sessionId = null)
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            var log = new RequestLog
            {
                Id = id,
                StartUtc = DateTime.UtcNow,
                Method = req.HttpMethod ?? "?",
                Path = path,
                SessionId = sessionId,
                ClientIp = FormatClient(req),
                UserAgent = Truncate(req.UserAgent, 120)
            };
            return log;
        }

        public long ElapsedMs()
        {
            return (long)(DateTime.UtcNow - StartUtc).TotalMilliseconds;
        }

        private static string FormatClient(HttpListenerRequest req)
        {
            if (req == null) return "?";
            var ep = req.RemoteEndPoint;
            if (ep == null) return "?";
            return ep.Address != null ? ep.Address.ToString() : ep.ToString();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }

    internal static class ProxyLog
    {
        private static readonly object Gate = new object();

        public static void Info(string message)
        {
            Write(ProxyLogLevel.Info, message);
        }

        public static void Warn(string message)
        {
            Write(ProxyLogLevel.Warn, message);
        }

        public static void Error(string message)
        {
            Write(ProxyLogLevel.Error, message);
        }

        public static void RequestStart(RequestLog log, string detail)
        {
            var sb = new StringBuilder();
            sb.Append("--> ");
            sb.Append(log.Method);
            sb.Append(" ");
            sb.Append(log.Path);
            sb.Append(" from ");
            sb.Append(log.ClientIp);
            if (log.SessionId.HasValue)
            {
                sb.Append(" session=");
                sb.Append(log.SessionId.Value);
            }
            if (!string.IsNullOrEmpty(log.UserAgent))
            {
                sb.Append(" ua=\"");
                sb.Append(log.UserAgent);
                sb.Append("\"");
            }
            if (!string.IsNullOrEmpty(detail))
            {
                sb.Append(" ");
                sb.Append(detail);
            }
            Write(ProxyLogLevel.Info, sb.ToString(), log.Id);
        }

        public static void RequestEnd(RequestLog log, int status, string detail)
        {
            var sb = new StringBuilder();
            sb.Append("<-- ");
            sb.Append(status);
            sb.Append(" ");
            sb.Append(log.Method);
            sb.Append(" ");
            sb.Append(log.Path);
            sb.Append(" ");
            sb.Append(log.ElapsedMs());
            sb.Append("ms");
            if (log.SessionId.HasValue)
            {
                sb.Append(" session=");
                sb.Append(log.SessionId.Value);
            }
            if (!string.IsNullOrEmpty(detail))
            {
                sb.Append(" ");
                sb.Append(detail);
            }
            var level = status >= 500 ? ProxyLogLevel.Error : status >= 400 ? ProxyLogLevel.Warn : ProxyLogLevel.Info;
            Write(level, sb.ToString(), log.Id);
        }

        public static string Preview(string text, int max = 160)
        {
            if (string.IsNullOrEmpty(text)) return "\"\"";
            var s = text.Replace("\r", " ").Replace("\n", " ");
            if (s.Length > max) s = s.Substring(0, max) + "…";
            return "\"" + s + "\"";
        }

        private static void Write(ProxyLogLevel level, string message, string requestId = null)
        {
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = ts + " [" + level.ToString().ToUpperInvariant() + "]";
            if (!string.IsNullOrEmpty(requestId))
                line += " [" + requestId + "]";
            line += " " + message;
            lock (Gate)
            {
                if (level == ProxyLogLevel.Error)
                    Console.Error.WriteLine(line);
                else
                    Console.WriteLine(line);
            }
        }
    }
}
