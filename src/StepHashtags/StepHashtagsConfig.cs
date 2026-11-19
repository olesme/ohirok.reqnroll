using Ohirok.PluginChains.Common;

namespace Ohirok.StepHashtags.ReqnrollPlugin
{
    /// <summary>
    /// Governs only the plain-text echo of a step's hashtags into the step output - the report always shows
    /// per-step hashtags regardless of this setting, since it reads them from the .feature file itself.
    /// </summary>
    public enum HashtagsOutput
    {
        None = 0,
        StdOut = 1
    }

    public static class StepHashtagsConfig
    {
        private static StepHashtagsSection _cached;
        private static StepHashtagsSection Config => _cached ??= PluginConfigProvider.GetSection<StepHashtagsSection>("stepHashtags");

        public static HashtagsOutput Output => Config.Output;
        public static bool Enabled => Config.Enabled;
        public static int GenerationInterceptorOrder => Config.GenerationInterceptorOrder;
    }

    internal class StepHashtagsSection
    {
        public HashtagsOutput Output { get; set; } = HashtagsOutput.StdOut;
        public bool Enabled { get; set; } = true;
        public int GenerationInterceptorOrder { get; set; } = -1000_000_000;
    }
}
