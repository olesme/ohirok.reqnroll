using Gherkin.Ast;
using Ohirok.GenerationInterceptor.Contexts;

namespace Ohirok.GenerationInterceptor.Fluent
{
    public sealed class RuleBackgroundBlock : BlockBase
    {
        public Background Background { get; }
        public Rule Rule { get; }
        public Scenario CurrentScenario { get; }

        internal RuleBackgroundBlock(in RuleBackgroundStepsContext ctx, InjectionScheduler scheduler)
            : base(ctx.GenerationContext, ctx.Method, ctx.Steps, scheduler)
        {
            Background = ctx.Background;
            Rule = ctx.Rule;
            CurrentScenario = ctx.CurrentScenario;
        }

        public string[] GetRuleTags() => Rule?.Tags?
            .Select(t => t.Name.TrimStart('@')).ToArray() ?? [];

        public bool HasTag(string tag) => TagsContain(tag, GetFeatureTags(), GetRuleTags());
    }
}
