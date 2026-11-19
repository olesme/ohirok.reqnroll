using Ohirok.GenerationInterceptor.Contexts;
using Reqnroll.Generator;

namespace Ohirok.GenerationInterceptor.Fluent
{
    public abstract class FluentInterceptor : Core.GenerationInterceptor
    {
        private const string KeyPrefix = "__FluentSched__";

        // Scoped to the concrete interceptor type, never shared. A single shared key would let each
        // fluent interceptor overwrite the scheduler stored by the one before it, so with several
        // fluent plugins installed together only the last one to run BeforeScenarioOwnSteps would
        // still have its scheduler in place by the time BeforeStep reads it - every other plugin's
        // injections would be silently dropped.
        private readonly string _key;

        protected FluentInterceptor() => _key = KeyPrefix + GetType().FullName;

        #region Per-scenario order override (--order:N)

        protected virtual IReadOnlyList<string> TagPrefixes => [];

        public override int GetEffectiveOrder(IReadOnlyList<string> allTags)
        {
            var prefixes = TagPrefixes;
            if (prefixes.Count == 0) return Order;

            foreach (var tag in allTags)
            {
                var dd = IndexOfDoubleDash(tag);
                if (dd < 0) continue;

                var basePart = tag.Substring(0, dd);
                int colon = basePart.IndexOf(':');
                var prefix = colon >= 0 ? basePart.Substring(0, colon) : basePart;

                bool match = false;
                foreach (var p in prefixes)
                    if (EqIgnCase(prefix, p)) { match = true; break; }
                if (!match) continue;

                var meta = tag.Substring(dd + 2);
                if (StartsWithOrderColon(meta) && int.TryParse(meta.Substring(6), out int n))
                    return n;
            }
            return Order;
        }

        private static int IndexOfDoubleDash(string s)
        {
            for (int i = 0; i < s.Length - 1; i++)
                if (s[i] == '-' && s[i + 1] == '-') return i;
            return -1;
        }

        private static bool StartsWithOrderColon(string s) =>
            s.Length >= 7 &&
            (s[0] | 0x20) == 'o' && (s[1] | 0x20) == 'r' &&
            (s[2] | 0x20) == 'd' && (s[3] | 0x20) == 'e' &&
            (s[4] | 0x20) == 'r' && s[5] == ':';

        private static bool EqIgnCase(string a, string b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if ((a[i] | 0x20) != (b[i] | 0x20)) return false;
            return true;
        }

        #endregion

        private void Store(TestClassGenerationContext ctx, InjectionScheduler s) =>
            ctx.CustomData[_key] = s;

        private void Clear(TestClassGenerationContext ctx) =>
            ctx.CustomData.Remove(_key);

        private InjectionScheduler Get(TestClassGenerationContext ctx) =>
            ctx.CustomData.TryGetValue(_key, out var obj) && obj is InjectionScheduler s ? s : null;

        #region Test Class

        public sealed override void AfterTestClass(in TestClassContext ctx) =>
            OnTestClass(in ctx);

        protected virtual void OnTestClass(in TestClassContext ctx) { }

        #endregion

        #region Scenario

        public sealed override void BeforeScenarioOwnSteps(in ScenarioOwnStepsContext ctx)
        {
            var scheduler = new InjectionScheduler();
            Store(ctx.GenerationContext, scheduler);
            OnScenario(new ScenarioBlock(in ctx, scheduler));
        }

        public sealed override void AfterScenarioOwnSteps(in ScenarioOwnStepsContext ctx) =>
            Clear(ctx.GenerationContext);

        protected virtual void OnScenario(ScenarioBlock scenario) { }

        #endregion

        #region Feature Background

        public sealed override void BeforeFeatureBackgroundSteps(in FeatureBackgroundStepsContext ctx)
        {
            var scheduler = new InjectionScheduler();
            Store(ctx.GenerationContext, scheduler);
            OnFeatureBackground(new FeatureBackgroundBlock(in ctx, scheduler));
        }

        public sealed override void AfterFeatureBackgroundSteps(in FeatureBackgroundStepsContext ctx) =>
            Clear(ctx.GenerationContext);

        protected virtual void OnFeatureBackground(FeatureBackgroundBlock background) { }

        #endregion

        #region Rule Background

        public sealed override void BeforeRuleBackgroundSteps(in RuleBackgroundStepsContext ctx)
        {
            var scheduler = new InjectionScheduler();
            Store(ctx.GenerationContext, scheduler);
            OnRuleBackground(new RuleBackgroundBlock(in ctx, scheduler));
        }

        public sealed override void AfterRuleBackgroundSteps(in RuleBackgroundStepsContext ctx) =>
            Clear(ctx.GenerationContext);

        protected virtual void OnRuleBackground(RuleBackgroundBlock background) { }

        #endregion

        #region Step Injection

        public sealed override void BeforeStep(in StepContext ctx)
        {
            var s = Get(ctx.GenCtx);
            if (s == null) return;
            foreach (var code in s.GetBefore(ctx.Index))
                ctx.Add(code);
        }

        public sealed override void AfterStep(in StepContext ctx)
        {
            var s = Get(ctx.GenCtx);
            if (s == null) return;
            foreach (var code in s.GetAfter(ctx.Index))
                ctx.Add(code);
        }

        #endregion
    }
}
