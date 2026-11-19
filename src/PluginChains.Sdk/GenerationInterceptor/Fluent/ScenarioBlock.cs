using Gherkin.Ast;
using Ohirok.GenerationInterceptor.Contexts;

namespace Ohirok.GenerationInterceptor.Fluent
{
    public sealed class ScenarioBlock : BlockBase
    {
        public Scenario Scenario { get; }
        public Rule ParentRule { get; }

        internal ScenarioBlock(in ScenarioOwnStepsContext ctx, InjectionScheduler scheduler)
            : base(ctx.GenerationContext, ctx.Method, ctx.Steps, scheduler)
        {
            Scenario = ctx.Scenario;
            ParentRule = ctx.ParentRule;
        }

        public string[] GetScenarioTags() => Scenario?.Tags?
            .Select(t => t.Name.TrimStart('@')).ToArray() ?? [];

        public string[] GetRuleTags() => ParentRule?.Tags?
            .Select(t => t.Name.TrimStart('@')).ToArray() ?? [];

        public string[] GetAllTags() => [.. GetFeatureTags(), .. GetRuleTags(), .. GetScenarioTags()];

        public bool HasTag(string tag) => TagsContain(tag, GetFeatureTags(), GetRuleTags(), GetScenarioTags());
    }
}
