using Ohirok.GenerationInterceptor;
using Ohirok.GenerationInterceptor.Contexts;
using Ohirok.GenerationInterceptor.Fluent;
using Ohirok.SoftAssert.Config;
using Ohirok.SoftAssert.Core;

namespace Ohirok.SoftAssert.ReqnrollPlugin
{
    internal sealed class SoftAssertGeneratorInterceptor : FluentInterceptor
    {
        public override int Order => SoftAssertConfig.GenerationInterceptorOrder;

        protected override IReadOnlyList<string> TagPrefixes => [SoftAssertConstants.SoftAssertTag];
        public override void AfterAllScenarioSteps(in AllScenarioStepsContext ctx)
            => ctx.Add("await Ohirok.SoftAssert.Manager.FlushIfNeededAsync(testRunner);");
        protected override void OnScenario(ScenarioBlock scenario) => MarkSoftSteps(scenario.Steps);
        protected override void OnFeatureBackground(FeatureBackgroundBlock background) => MarkSoftSteps(background.Steps);
        protected override void OnRuleBackground(RuleBackgroundBlock background) => MarkSoftSteps(background.Steps);

        private static void MarkSoftSteps(StepBlockInfo steps)
        {
            foreach (var step in steps.Where(s => s.HasHashtagStartingWith(SoftAssertConstants.SoftAssertTag)))
            {
                var stepHashtags = step.Hashtags
                    .Where(h => h.StartsWith(SoftAssertConstants.SoftAssertTag, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                step.InjectBefore(
                    $"Ohirok.SoftAssert.Manager.SetSoftStepMarker(testRunner, {GeneratedCode.StringArray(stepHashtags)});");
            }
        }
    }
}
