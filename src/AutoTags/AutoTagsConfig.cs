using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Ohirok.PluginChains.Common;

namespace Ohirok.AutoTags
{
    /// <summary>The <c>autoTags</c> section of <c>reqnroll.json</c>.</summary>
    public sealed class AutoTagsSection
    {
        /// <summary>Tag to add, and the regular expressions of the step texts that add it: a scenario gets the tag when
        /// one of its steps, or of the Background steps that run before it, matches one of the patterns.</summary>
        [JsonPropertyName("tags")]
        public Dictionary<string, string[]> Tags { get; set; } = [];

        [JsonPropertyName("generationInterceptorOrder")]
        public int GenerationInterceptorOrder { get; set; } = 500;
    }

    internal static class AutoTagsConfig
    {
        private static readonly ConditionalWeakTable<AutoTagsSection, AutoTagsTransformation> _transformations = new();

        // Not cached here: the generator builds several projects in one process, and PluginConfigProvider reloads
        // the section for each of them.
        public static AutoTagsSection Section => PluginConfigProvider.GetSection<AutoTagsSection>("autoTags");

        /// <summary>The transformation for the current section, with its patterns compiled once for all the feature
        /// files of the project rather than once per file.</summary>
        public static AutoTagsTransformation Transformation =>
            _transformations.GetValue(Section, section => new AutoTagsTransformation(section.Tags));
    }
}
