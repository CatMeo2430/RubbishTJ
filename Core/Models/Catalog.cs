using System.Collections.Generic;
using Newtonsoft.Json;

namespace Taiji.Core.Models
{
    public sealed class ModelCapabilities
    {
        [JsonProperty("imageInput")]
        public bool ImageInput { get; set; }

        [JsonProperty("stream")]
        public bool Stream { get; set; }

        [JsonProperty("thinking")]
        public bool Thinking { get; set; }
    }

    public sealed class ModelAttr
    {
        [JsonProperty("integral")]
        public string Integral { get; set; }

        [JsonProperty("providerKey")]
        public string ProviderKey { get; set; }

        [JsonProperty("providerName")]
        public string ProviderName { get; set; }

        [JsonProperty("note")]
        public string Note { get; set; }

        [JsonProperty("capabilities")]
        public ModelCapabilities Capabilities { get; set; }
    }

    public sealed class ModelInfo
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("attr")]
        public ModelAttr Attr { get; set; }

        public string ProviderName
        {
            get { return Attr != null ? Attr.ProviderName : null; }
        }

        public string Integral
        {
            get { return Attr != null ? Attr.Integral : null; }
        }

        public bool ImageInput
        {
            get { return Attr != null && Attr.Capabilities != null && Attr.Capabilities.ImageInput; }
        }

        public string NameText
        {
            get { return !string.IsNullOrEmpty(Label) ? Label : (Value ?? ""); }
        }

        public string PointsText
        {
            get { return !string.IsNullOrEmpty(Integral) ? Integral : "?"; }
        }

        public string ModeText
        {
            get { return ImageInput ? "图像多模态" : "纯文本"; }
        }

        public string DisplayLabel
        {
            get { return NameText + "  ·  " + PointsText + "  ·  " + ModeText; }
        }

        public override string ToString()
        {
            return DisplayLabel;
        }
    }

    public sealed class ProviderInfo
    {
        [JsonProperty("idKey")]
        public string IdKey { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("sort")]
        public int Sort { get; set; }
    }

    public sealed class ChatTmpl
    {
        [JsonProperty("defModel")]
        public string DefModel { get; set; }

        [JsonProperty("mFileCount")]
        public int MFileCount { get; set; }

        [JsonProperty("mFileSize")]
        public int MFileSize { get; set; }

        [JsonProperty("models")]
        public List<ModelInfo> Models { get; set; }

        [JsonProperty("providers")]
        public List<ProviderInfo> Providers { get; set; }
    }
}
