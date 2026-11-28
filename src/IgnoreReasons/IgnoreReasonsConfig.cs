using System.Text.Json.Serialization;
using Ohirok.PluginChains.Common;

namespace Ohirok.IgnoreReasons
{
    /// <summary>The <c>ignoreReasons</c> section of <c>reqnroll.json</c>.</summary>
    public sealed class IgnoreReasonsSection
    {
        /// <summary>Tag that ignores a scenario like <c>@ignore</c> does, and the reason shown for it: on a scenario, a rule
        /// or a feature, <c>"@ignore:bug": "Known product defect"</c> skips the scenarios it covers with that reason.</summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; } = [];

        [JsonPropertyName("generationInterceptorOrder")]
        public int GenerationInterceptorOrder { get; set; } = 600;
    }

    internal static class IgnoreReasonsConfig
    {
        // Not cached here: the generator builds several projects in one process, and PluginConfigProvider reloads
        // the section for each of them.
        public static IgnoreReasonsSection Section => PluginConfigProvider.GetSection<IgnoreReasonsSection>("ignoreReasons");
    }
}
