using Gherkin.Ast;
using Ohirok.GenerationInterceptor.Contexts;
using Ohirok.GenerationInterceptor.Infrastructure;
using Reqnroll.Generator;
using Reqnroll.Parser;

namespace Ohirok.GenerationInterceptor.Core
{
    internal static class Invoke
    {
        private static List<IGenerationInterceptor> _interceptors;
        private static List<IGenerationInterceptor> _interceptorsReversed;
        private static readonly object _lock = new();

        internal static void Invalidate()
        {
            lock (_lock)
            {
                _interceptors = null;
                _interceptorsReversed = null;
            }
        }

        private static void EnsureInitialized()
        {
            if (_interceptors == null)
            {
                lock (_lock)
                {
                    if (_interceptors == null)
                    {
                        var ordered = InterceptorRegistry.GetOrdered();
                        _interceptorsReversed = [.. Enumerable.Reverse(ordered)];
                        _interceptors = [.. ordered];
                    }
                }
            }
        }

        /// <summary>The hashtags the registered <see cref="IStepHashtagSource"/> interceptors give a step, in order.</summary>
        public static IEnumerable<string> SourcedStepHashtags(Step step)
        {
            EnsureInitialized();
            foreach (var interceptor in _interceptors)
                if (interceptor is IStepHashtagSource source)
                    foreach (var hashtag in source.GetStepHashtags(step) ?? [])
                        yield return hashtag;
        }

        public static ReqnrollDocument TransformDocument(ReqnrollDocument document)
        {
            EnsureInitialized();
            foreach (var interceptor in _interceptors)
            {
                document = interceptor.TransformDocument(document)
                    ?? throw new InvalidOperationException($"{interceptor.GetType().FullName}.TransformDocument returned null.");
            }
            return document;
        }

        #region Tag Collection (@ tags + # step hashtags)

        private static IReadOnlyList<string> CollectAllTags(in ScenarioMethodContext ctx)
        {
            var list = new List<string>(16);
            AddGherkinTags(list, ctx.Feature.Tags);
            AddGherkinTags(list, ctx.RuleTags);
            AddGherkinTags(list, ctx.ScenarioTags);
            if (ctx.IsOutline)
                foreach (var ex in ctx.Examples)
                    AddGherkinTags(list, ex.Tags);
            var steps = ctx.IsOutline ? ctx.ScenarioOutline?.Steps : ctx.Scenario?.Steps;
            AddStepHashtags(list, ctx.GenCtx, steps);
            return list;
        }

        private static IReadOnlyList<string> CollectAllTags(in AllScenarioStepsContext ctx)
        {
            var list = new List<string>(16);
            var def = ctx.ScenarioDefinition;
            AddGherkinTags(list, ctx.GenerationContext.Feature.Tags);
            if (def.Rule != null) AddGherkinTags(list, def.Rule.Tags);
            IEnumerable<Step> steps;
            if (def.IsScenarioOutline)
            {
                AddGherkinTags(list, def.ScenarioOutline.Tags);
                foreach (var ex in def.ScenarioOutline.Examples)
                    AddGherkinTags(list, ex.Tags);
                steps = def.ScenarioOutline.Steps;
            }
            else
            {
                AddGherkinTags(list, def.Scenario?.Tags);
                steps = def.Scenario?.Steps;
            }
            AddStepHashtags(list, ctx.GenerationContext, steps);
            return list;
        }

        private static IReadOnlyList<string> CollectAllTags(in ScenarioOwnStepsContext ctx)
        {
            var list = new List<string>(16);
            var def = ctx.ScenarioDefinition;
            AddGherkinTags(list, ctx.GenerationContext.Feature.Tags);
            if (def.Rule != null) AddGherkinTags(list, def.Rule.Tags);
            if (def.IsScenarioOutline)
            {
                AddGherkinTags(list, def.ScenarioOutline.Tags);
                foreach (var ex in def.ScenarioOutline.Examples)
                    AddGherkinTags(list, ex.Tags);
            }
            else
                AddGherkinTags(list, def.Scenario?.Tags);
            AddStepHashtags(list, ctx.GenerationContext, ctx.Steps);
            return list;
        }

        private static void AddGherkinTags(List<string> list, IEnumerable<Tag> tags)
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

        private static void AddStepHashtags(List<string> list, TestClassGenerationContext genCtx, IEnumerable<Step> steps)
        {
            if (steps == null) return;
            foreach (var step in steps)
                list.AddRange(HashtagCache.GetForStep(genCtx, step.Location.Line));
        }

        #endregion

        public static void BeforeTestClass(in TestClassContext ctx)
        {
            EnsureInitialized();
            foreach (var i in _interceptors) i.BeforeTestClass(in ctx);
        }

        public static void AfterTestClass(in TestClassContext ctx)
        {
            EnsureInitialized();
            foreach (var i in _interceptorsReversed) i.AfterTestClass(in ctx);
        }

        public static void BeforeScenarioMethod(in ScenarioMethodContext ctx)
        {
            var ordered = InterceptorRegistry.GetOrderedForScenario(CollectAllTags(in ctx));
            foreach (var i in ordered) i.BeforeScenarioMethod(in ctx);
        }

        public static void AfterScenarioMethod(in ScenarioMethodContext ctx)
        {
            var ordered = InterceptorRegistry.GetOrderedForScenario(CollectAllTags(in ctx));
            for (int i = ordered.Length - 1; i >= 0; i--)
                ordered[i].AfterScenarioMethod(in ctx);
        }

        public static void BeforeFeatureBackgroundSteps(in FeatureBackgroundStepsContext ctx)
        {
            EnsureInitialized();
            foreach (var i in _interceptors) i.BeforeFeatureBackgroundSteps(in ctx);
        }

        public static void AfterFeatureBackgroundSteps(in FeatureBackgroundStepsContext ctx)
        {
            EnsureInitialized();
            foreach (var i in _interceptorsReversed) i.AfterFeatureBackgroundSteps(in ctx);
        }

        public static void BeforeRuleBackgroundSteps(in RuleBackgroundStepsContext ctx)
        {
            EnsureInitialized();
            foreach (var i in _interceptors) i.BeforeRuleBackgroundSteps(in ctx);
        }

        public static void AfterRuleBackgroundSteps(in RuleBackgroundStepsContext ctx)
        {
            EnsureInitialized();
            foreach (var i in _interceptorsReversed) i.AfterRuleBackgroundSteps(in ctx);
        }

        public static void BeforeAllScenarioSteps(in AllScenarioStepsContext ctx)
        {
            var ordered = InterceptorRegistry.GetOrderedForScenario(CollectAllTags(in ctx));
            foreach (var i in ordered) i.BeforeAllScenarioSteps(in ctx);
        }

        public static void AfterAllScenarioSteps(in AllScenarioStepsContext ctx)
        {
            var ordered = InterceptorRegistry.GetOrderedForScenario(CollectAllTags(in ctx));
            for (int i = ordered.Length - 1; i >= 0; i--)
                ordered[i].AfterAllScenarioSteps(in ctx);
        }

        public static void BeforeScenarioOwnSteps(in ScenarioOwnStepsContext ctx)
        {
            var ordered = InterceptorRegistry.GetOrderedForScenario(CollectAllTags(in ctx));
            foreach (var i in ordered) i.BeforeScenarioOwnSteps(in ctx);
        }

        public static void AfterScenarioOwnSteps(in ScenarioOwnStepsContext ctx)
        {
            var ordered = InterceptorRegistry.GetOrderedForScenario(CollectAllTags(in ctx));
            for (int i = ordered.Length - 1; i >= 0; i--)
                ordered[i].AfterScenarioOwnSteps(in ctx);
        }

        public static void BeforeStep(in StepContext ctx)
        {
            EnsureInitialized();
            foreach (var i in _interceptors) i.BeforeStep(in ctx);
        }

        public static void AfterStep(in StepContext ctx)
        {
            EnsureInitialized();
            foreach (var i in _interceptorsReversed) i.AfterStep(in ctx);
        }
    }
}
