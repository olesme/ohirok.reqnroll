using System.Text.Json.Serialization;
using Ohirok.PluginChains.Common;

namespace Ohirok.Variants
{
    /// <summary>The <c>variants</c> section of <c>reqnroll.json</c>.</summary>
    public sealed class VariantsSection
    {
        /// <summary>Tag names that mark variants: with <c>Browser</c>, <c>@Browser:Chrome</c> is a variant. Nothing is
        /// expanded until at least one key is configured, so other <c>name:value</c> tags are never touched.</summary>
        [JsonPropertyName("keys")]
        public string[] Keys { get; set; } = [];

        [JsonPropertyName("generationInterceptorOrder")]
        public int GenerationInterceptorOrder { get; set; } = 1000;
    }

    internal static class VariantsConfig
    {
        // Not cached here: the generator builds several projects in one process, and PluginConfigProvider reloads
        // the section for each of them.
        public static VariantsSection Section => PluginConfigProvider.GetSection<VariantsSection>("variants");
    }
}
