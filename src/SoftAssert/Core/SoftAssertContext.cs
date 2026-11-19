using Catchy;
using Reqnroll;

namespace Ohirok.SoftAssert.Core
{
    /// <summary>
    /// Marker stored in <see cref="ScenarioContext"/> by the generator-injected call before a
    /// <c>#softassert</c>-tagged step, and the context passed to <c>[OnSoftFailure]</c>/<c>[OnSoftFlush]</c> hooks.
    /// </summary>
    public sealed class SoftAssertContext
    {
        public string[] Hashtags { get; internal set; } = [];
        public string[] AllTags { get; internal set; } = [];

        /// <summary>Set for <c>[OnSoftFailure]</c>; the exception just captured for the current step.</summary>
        public Exception Error { get; internal set; }

        /// <summary>Set for <c>[OnSoftFlush]</c>; all soft failures accumulated so far in the scenario.</summary>
        public AggregateAssertionException Aggregate { get; internal set; }

        internal ScenarioContext CurrentScenarioContext { get; set; }

        internal static SoftAssertContext ForScenario(ScenarioContext sc, string[] hashtags = null)
        {
            var fc = sc.ScenarioContainer.Resolve<FeatureContext>();
            var featureTags = fc?.FeatureInfo?.Tags ?? [];
            var ruleTags = sc.RuleInfo?.Tags ?? [];
            var scenarioTags = sc.ScenarioInfo?.Tags ?? [];
            hashtags ??= [];

            var allTags = new string[featureTags.Length + ruleTags.Length + scenarioTags.Length + hashtags.Length];
            int idx = 0;
            featureTags.CopyTo(allTags, idx); idx += featureTags.Length;
            ruleTags.CopyTo(allTags, idx); idx += ruleTags.Length;
            scenarioTags.CopyTo(allTags, idx); idx += scenarioTags.Length;
            hashtags.CopyTo(allTags, idx);

            return new SoftAssertContext
            {
                Hashtags = hashtags,
                AllTags = allTags,
                CurrentScenarioContext = sc,
            };
        }
    }
}
