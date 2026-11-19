using Gherkin.Ast;
using Ohirok.GenerationInterceptor.Contexts;

namespace Ohirok.GenerationInterceptor.Fluent
{
    public sealed class FeatureBackgroundBlock : BlockBase
    {
        public Background Background { get; }

        internal FeatureBackgroundBlock(in FeatureBackgroundStepsContext ctx, InjectionScheduler scheduler)
            : base(ctx.GenerationContext, ctx.Method, ctx.Steps, scheduler)
        {
            Background = ctx.Background;
        }

        public bool HasTag(string tag) => TagsContain(tag, GetFeatureTags());
    }
}
