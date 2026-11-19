using Gherkin.Ast;
using Ohirok.GenerationInterceptor.Contexts;
using Ohirok.GenerationInterceptor.Fluent;

namespace Ohirok.XFail.ReqnrollPlugin
{
    /// <summary>
    /// Wraps the generated test method of a scenario tagged <c>@xfail</c> / <c>@afail</c> (on the scenario, its rule, its
    /// feature or an Examples block) in <see cref="Manager.WrapScenarioAsync"/>. Runs before Retry's wrapper (lower
    /// order), so a retried scenario is judged on its final outcome.
    /// </summary>
    internal sealed class XFailGeneratorInterceptor : FluentInterceptor
    {
        private const string MethodWrapKey = "__ohirokXFailMethodWrap__";

        public override int Order => XFailConfig.GenerationInterceptorOrder;

        protected override IReadOnlyList<string> TagPrefixes => [.. XFailTags.XFail, .. XFailTags.AFail];

        public override void BeforeScenarioMethod(in ScenarioMethodContext ctx)
        {
            if (!XFailConfig.Enabled) return;
            var tags = CollectMethodTags(ctx);
            if (tags.Any(t => string.Equals(t, "ignore", StringComparison.OrdinalIgnoreCase))) return;
            if (!tags.Any(t => XFailTags.TryParse(t, out _, out _))) return;

            ctx.GenCtx.CustomData[MethodWrapKey] = true;
            ctx.Add("await Ohirok.XFail.Manager.WrapScenarioAsync(testRunner, async () =>\n{");
        }

        public override void AfterScenarioMethod(in ScenarioMethodContext ctx)
        {
            if (ctx.GenCtx.CustomData.Remove(MethodWrapKey))
                ctx.Add("});");
        }

        private static List<string> CollectMethodTags(in ScenarioMethodContext ctx)
        {
            var list = new List<string>(8);
            AddTags(list, ctx.Feature.Tags);
            AddTags(list, ctx.RuleTags);
            AddTags(list, ctx.ScenarioTags);
            if (ctx.IsOutline)
                foreach (var ex in ctx.Examples)
                    AddTags(list, ex.Tags);
            return list;
        }

        private static void AddTags(List<string> list, IEnumerable<Tag>? tags)
        {
            if (tags == null) return;
            foreach (var t in tags)
            {
                var name = t.Name.TrimStart('@');
                if (name.Length > 0) list.Add(name);
            }
        }
    }
}
