namespace Taiji.Engine.Render
{
    public sealed class RenderRequest
    {
        public RenderRequest(RenderRole role, string content, string languageHint = null)
        {
            Role = role;
            Content = content ?? "";
            LanguageHint = languageHint;
        }

        public RenderRole Role { get; private set; }
        public string Content { get; private set; }
        public string LanguageHint { get; private set; }
    }
}
