using Gherkin.Ast;
using Ohirok.GenerationInterceptor;
using Ohirok.GenerationInterceptor.Contexts;
using Ohirok.GenerationInterceptor.Fluent;
using Ohirok.Retry.Config;
using Ohirok.Retry.Core;
using Reqnroll.Generator.Generation;

namespace Ohirok.Retry.ReqnrollPlugin
{
    internal sealed class RetryGeneratorInterceptor : FluentInterceptor
    {
        private const string SpKey = "__ohirokRetrySP__";
        private const string MethodWrapKey = "__ohirokRetryMethodWrap__";

        public override int Order => RetryConfig.GenerationInterceptorOrder;

        protected override IReadOnlyList<string> TagPrefixes => ["retry", "retryblock"];

        #region Test retry (BeforeScenarioMethod / AfterScenarioMethod)

        public override void BeforeScenarioMethod(in ScenarioMethodContext ctx)
        {
            if (!RetryConfig.AllowRetryTag) return;
            var tags = CollectMethodTags(ctx);
            if (HasIgnoreTag(tags) || !HasRetryTag(tags) || HasRetryBlockScenarioTag(tags)) return;

            ctx.GenCtx.CustomData[MethodWrapKey] = true;
            ctx.Add("await Ohirok.Retry.Manager.WrapTestMethodAsync(testRunner, async () =>\n{");
        }

        public override void AfterScenarioMethod(in ScenarioMethodContext ctx)
        {
            if (ctx.GenCtx.CustomData.Remove(MethodWrapKey))
                ctx.Add("});");
        }

        #endregion

        #region Scenario block retry (@retryblock — BeforeAllScenarioSteps / AfterAllScenarioSteps)

        public override void BeforeAllScenarioSteps(in AllScenarioStepsContext ctx)
        {
            if (!RetryConfig.AllowRetryTag) return;
            var tags = CollectDefTags(ctx.ScenarioDefinition, ctx.GenerationContext.Feature.Tags);
            if (HasIgnoreTag(tags) || !HasRetryBlockScenarioTag(tags)) return;

            ctx.GenerationContext.CustomData[SpKey] = true;
            ctx.Add("await Ohirok.Retry.Manager.WrapBlockAsync(testRunner, async () => {");
        }

        public override void AfterAllScenarioSteps(in AllScenarioStepsContext ctx)
        {
            if (ctx.GenerationContext.CustomData.Remove(SpKey))
                ctx.Add("});");
        }

        #endregion

        #region Block / Step Retry (OnScenario)

        protected override void OnScenario(ScenarioBlock scenario) => ProcessSteps(scenario.Steps);
        protected override void OnFeatureBackground(FeatureBackgroundBlock background) => ProcessSteps(background.Steps);
        protected override void OnRuleBackground(RuleBackgroundBlock background) => ProcessSteps(background.Steps);

        private static void ProcessSteps(StepBlockInfo steps)
        {
            HandleBlockRetry(steps);
            if (RetryConfig.AllowRetryHashtag)
                HandleStepRetry(steps);
        }

        private static void HandleBlockRetry(StepBlockInfo steps)
        {
            foreach (var seq in steps.FindSequences(
                from: s => s.HasHashtagStartingWith(RetryConstants.RetryBlockTag),
                until: s => s.HasHashtag(RetryConstants.EndBlockTag)
                          || s.HasHashtagStartingWith(RetryConstants.RetryTag)))
            {
                // Every Retry setting on the opening step, not just #retryblock: #timeout, #condition and #delay on
                // the same step tune the block exactly like their tag counterparts tune a scenario.
                var blockHashtags = seq.First.Hashtags.Where(Helpers.RetryTagParser.IsRetrySetting).ToArray();
                seq.WrapSequence(
                    $"await Ohirok.Retry.Manager.WrapBlockAsync(testRunner, {GeneratedCode.StringArray(blockHashtags)}, async () => {{",
                    "});"
                );
            }
        }

        private static void HandleStepRetry(StepBlockInfo steps)
        {
            foreach (var step in steps.Where(s =>
                s.HasHashtagStartingWith(RetryConstants.RetryTag) &&
                !s.HasHashtagStartingWith(RetryConstants.RetryBlockTag)))
            {
                // Every Retry setting of the step, so #timeout / #condition / #delay next to #retry take effect.
                var stepHashtags = step.Hashtags.Where(Helpers.RetryTagParser.IsRetrySetting).ToArray();
                step.InjectBefore(
                    $"Ohirok.Retry.Manager.SetStepRetryContext(testRunner, {GeneratedCode.StringArray(stepHashtags)});"
                );
            }
        }

        #endregion

        #region Tag Resolution

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

        private static List<string> CollectDefTags(ScenarioDefinitionInFeatureFile def, IEnumerable<Tag> featureTags)
        {
            var list = new List<string>(8);
            AddTags(list, featureTags);
            AddTags(list, def.Rule?.Tags);
            if (def.IsScenarioOutline)
            {
                AddTags(list, def.ScenarioOutline.Tags);
                foreach (var ex in def.ScenarioOutline.Examples)
                    AddTags(list, ex.Tags);
            }
            else if (def.Scenario != null)
            {
                AddTags(list, def.Scenario.Tags);
            }
            return list;
        }

        private static void AddTags(List<string> list, IEnumerable<Tag> tags)
        {
            if (tags == null) return;
            foreach (var t in tags)
            {
                var name = t.Name;
                if (name.Length > 0 && (name[0] == '@' || name[0] == '#'))
                    name = name.Substring(1);
                if (name.Length > 0)
                    list.Add(name);
            }
        }

        private static bool HasIgnoreTag(List<string> tags)
        {
            foreach (var t in tags)
                if (EqIgnCase(t, RetryConstants.IgnoreTag)) return true;
            return false;
        }

        private static bool HasRetryTag(List<string> tags)
        {
            bool? result = null;
            foreach (var t in tags)
            {
                if (EqIgnCase(t, RetryConstants.NoRetryTag)) result = false;
                else if (StartsWithRetry(t) && !StartsWithRetryBlock(t)) result = true;
            }
            return result ?? false;
        }

        private static bool HasRetryBlockScenarioTag(List<string> tags)
        {
            foreach (var t in tags)
                if (StartsWithRetryBlock(t)) return true;
            return false;
        }


        #endregion

        #region String Helpers

        private static bool EqIgnCase(string a, string b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if ((a[i] | 0x20) != (b[i] | 0x20)) return false;
            return true;
        }

        private static bool StartsWithRetry(string s) =>
            s.Length >= 5 &&
            (s[0] | 0x20) == 'r' && (s[1] | 0x20) == 'e' &&
            (s[2] | 0x20) == 't' && (s[3] | 0x20) == 'r' &&
            (s[4] | 0x20) == 'y';

        private static bool StartsWithRetryBlock(string s) =>
            s.Length >= 10 && StartsWithRetry(s) &&
            (s[5] | 0x20) == 'b' && (s[6] | 0x20) == 'l' &&
            (s[7] | 0x20) == 'o' && (s[8] | 0x20) == 'c' &&
            (s[9] | 0x20) == 'k';

        #endregion
    }
}
