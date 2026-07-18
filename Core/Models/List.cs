using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taiji.Core.Models
{
    public sealed class PageResult<T>
    {
        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("pages")]
        public int Pages { get; set; }

        [JsonProperty("asc")]
        public bool Asc { get; set; }

        [JsonProperty("search")]
        public string Search { get; set; }

        [JsonProperty("records")]
        public List<T> Records { get; set; }
    }

    public sealed class ChatSessionInfo
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("created")]
        public string Created { get; set; }

        [JsonProperty("updated")]
        public string Updated { get; set; }

        [JsonProperty("uid")]
        public long Uid { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("maxToken")]
        public int MaxToken { get; set; }

        [JsonProperty("contextCount")]
        public int ContextCount { get; set; }

        [JsonProperty("temperature")]
        public double Temperature { get; set; }

        [JsonProperty("presencePenalty")]
        public double PresencePenalty { get; set; }

        [JsonProperty("frequencyPenalty")]
        public double FrequencyPenalty { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("topSort")]
        public int TopSort { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("plugins")]
        public List<object> Plugins { get; set; }

        [JsonProperty("mcp")]
        public List<object> Mcp { get; set; }

        [JsonProperty("webSearch")]
        public bool WebSearch { get; set; }

        [JsonProperty("localPlugins")]
        public object LocalPlugins { get; set; }

        [JsonProperty("useAppId")]
        public long UseAppId { get; set; }

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Name)) return Name;
                return "#" + Id;
            }
        }
    }

    public sealed class ChatRecord
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("created")]
        public string Created { get; set; }

        [JsonProperty("updated")]
        public string Updated { get; set; }

        [JsonProperty("sessionId")]
        public long SessionId { get; set; }

        [JsonProperty("recordType")]
        public string RecordType { get; set; }

        [JsonProperty("userText")]
        public string UserText { get; set; }

        [JsonProperty("aiText")]
        public string AiText { get; set; }

        [JsonProperty("taskId")]
        public string TaskId { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("useTokens")]
        public int UseTokens { get; set; }

        [JsonProperty("deductCount")]
        public int DeductCount { get; set; }
    }
}
