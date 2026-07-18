namespace Taiji.Engine.Render
{
    public sealed class RenderRequest
    {
        public RenderRequest(RenderRole role, string content, string languageHint = null, bool forExport = false)
        {
            Role = role;
            Content = content ?? "";
            LanguageHint = languageHint;
            ForExport = forExport;
        }

        public RenderRole Role { get; private set; }
        public string Content { get; private set; }
        public string LanguageHint { get; private set; }
        public bool ForExport { get; private set; }
    }
}
