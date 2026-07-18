using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Taiji.Proxy
{
    internal sealed class SessionListItem
    {
        [JsonProperty("sessionId")]
        public long SessionId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("updated")]
        public string Updated { get; set; }
    }

    internal sealed class SessionListResponse
    {
        [JsonProperty("sessions")]
        public List<SessionListItem> Sessions { get; set; }

        [JsonProperty("pagesLoaded")]
        public int PagesLoaded { get; set; }
    }

    internal sealed class SessionIdRequest
    {
        [JsonProperty("sessionId")]
        public long SessionId { get; set; }
    }

    internal sealed class RenameSessionRequest
    {
        [JsonProperty("sessionId")]
        public long SessionId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    internal sealed class ModelListItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("imageInput")]
        public bool ImageInput { get; set; }

        [JsonProperty("integral")]
        public string Integral { get; set; }
    }

    internal sealed class ProviderGroup
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("models")]
        public List<ModelListItem> Models { get; set; }
    }

    internal sealed class ModelsListResponse
    {
        [JsonProperty("defaultModel")]
        public string DefaultModel { get; set; }

        [JsonProperty("maxFileCount")]
        public int MaxFileCount { get; set; }

        [JsonProperty("maxFileSizeMb")]
        public int MaxFileSizeMb { get; set; }

        [JsonProperty("providers")]
        public List<ProviderGroup> Providers { get; set; }
    }

    internal sealed class OpenAiErrorResponse
    {
        [JsonProperty("error")]
        public OpenAiErrorBody Error { get; set; }
    }

    internal sealed class OpenAiErrorBody
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }
    }

    internal sealed class OpenAiChatRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("messages")]
        public List<OpenAiMessage> Messages { get; set; }

        [JsonProperty("stream")]
        public bool Stream { get; set; }
    }

    internal sealed class OpenAiMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public JToken Content { get; set; }
    }

    internal sealed class OpenAiModelDto
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("owned_by")]
        public string OwnedBy { get; set; }
    }

    internal sealed class OpenAiModelsResponse
    {
        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("data")]
        public List<OpenAiModelDto> Data { get; set; }
    }
}
