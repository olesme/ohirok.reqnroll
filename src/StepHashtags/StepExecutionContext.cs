using System.Linq.Expressions;
using System.Reflection;
using Reqnroll;

namespace Ohirok.StepHashtags.ReqnrollPlugin
{
    public class StepExecutionContext(ScenarioContext scenarioContext) : ReqnrollContext
    {
        private static readonly Action<ScenarioInfo, string[]> TagsSetter = CreateTagsSetter();

        private static Action<ScenarioInfo, string[]> CreateTagsSetter()
        {
            var prop = typeof(ScenarioInfo).GetProperty("CombinedTags", BindingFlags.Instance | BindingFlags.Public) ?? throw new InvalidOperationException("CombinedTags property not found");

            // Compiled expression for performance
            var param1 = Expression.Parameter(typeof(ScenarioInfo));
            var param2 = Expression.Parameter(typeof(string[]));
            var setterCall = Expression.Call(param1, prop.SetMethod, param2);
            return Expression.Lambda<Action<ScenarioInfo, string[]>>(setterCall, param1, param2).Compile();
        }

        public ScenarioContext ScenarioContext => scenarioContext;

        /// <summary>
        /// Gets step hashtags for the current step, or empty array if none.
        /// </summary>
        public string[] Hashtags { get; internal set; } = [];
        private readonly string[] _originalTags = scenarioContext.ScenarioInfo.CombinedTags;
        private Exception? _error;
        public string? SkipReason { get; internal set; } = null;
        public new Exception TestError
        {
            get => _error ?? scenarioContext.TestError;
            set => _error = value; 
        }

        public bool HasHashtag(string tag) =>
            Hashtags.Any(h => h.Equals(tag, StringComparison.OrdinalIgnoreCase));

        internal void Set(string[] hashtags)
        {
            var merged = _originalTags.Union(hashtags).ToArray();
            TagsSetter(scenarioContext.ScenarioInfo, merged);
            Hashtags = hashtags;

            // The StdOut echo of these hashtags is NOT written here: Set() runs (via the generation interceptor)
            // as the statement immediately before testRunner.Given/When/Then(...), i.e. before Reqnroll's formatter
            // pipeline has processed StepStartedEvent for this step. Any IReqnrollOutputHelper.WriteLine call made
            // this early throws inside Reqnroll's own (unawaited) event-processing pipeline - CurrentStep on an
            // empty step-tracker stack - and is silently lost. The echo is written from StepHashtagsRuntimePlugin's
            // own StepStartedEvent handler instead, which runs after that stack entry exists. The report itself
            // needs no such echo at all - it derives per-step hashtags by statically parsing the .feature file's
            // own comments (see Ohirok.Reporting.Core's StaticHashtagAssociator), never from runtime output.
        }

        internal void Reset()
        {
            TagsSetter(ScenarioContext.ScenarioInfo, _originalTags);
            Hashtags = [];
            SkipReason = null;
            base.Clear();
        }

        /// <summary>
        /// Marks the current step to be skipped. Call it from a BeforeStep hook or an event handler.
        /// Works regardless of <c>ignoreStep.enabled</c>, which only controls the built-in #ignore hashtag.
        /// </summary>
        public void Skip(string? reason = null) => SkipReason = reason ?? "user logic";

        /// <summary>
        /// Whether the step is marked to be skipped, by user logic or by the #ignore hashtag
        /// (the hashtag counts only while <c>ignoreStep.enabled</c> is true).
        /// </summary>
        public bool ShouldSkip(out string? reason)
        {
            if (SkipReason != null)
            {
                reason = SkipReason;
                return true;
            }
            if (IgnoreStepConfig.Enabled && HasHashtag("ignore"))
            {
                reason = "#ignore hashtag";
                return true;
            }
            reason = null;
            return false;
        }

        public bool ShouldSkip() => ShouldSkip(out _);
    }
}
