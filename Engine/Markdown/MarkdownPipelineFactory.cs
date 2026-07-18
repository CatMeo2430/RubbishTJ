using Markdig;

namespace Taiji.Engine.Markdown
{
    public static class MarkdownPipelineFactory
    {
        private static MarkdownPipeline _pipeline;

        public static MarkdownPipeline Shared
        {
            get
            {
                if (_pipeline == null)
                {
                    _pipeline = new MarkdownPipelineBuilder()
                        .UseAdvancedExtensions()
                        .UseMathematics()
                        .UseSoftlineBreakAsHardlineBreak()
                        .Build();
                }
                return _pipeline;
            }
        }
    }
}
