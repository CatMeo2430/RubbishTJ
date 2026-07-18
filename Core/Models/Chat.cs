using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Taiji.Core.Models
{
    public sealed class ChatFilePayload
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }
    }

    public sealed class ChatStreamResult
    {
        public string TaskId { get; set; }
        public string Text { get; set; }
        public long SessionId { get; set; }
        public int StringEvents { get; set; }
        public JObject Record { get; set; }

        /// <summary>SSE 未收到 [DONE] 或连接异常中断时为 true。</summary>
        public bool StreamInterrupted { get; set; }
    }
}
