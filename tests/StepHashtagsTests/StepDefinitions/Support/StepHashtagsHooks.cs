using Ohirok.StepHashtags.ReqnrollPlugin;
using Shouldly;

namespace StepHashtagsNUnitTests.Support
{
    [Binding]
    public static class StepHashtagsHooks
    {
        // Kept per scenario, not in static fields: xUnit and TUnit run scenarios in parallel.
        private const string CapturedInBefore = "hooks:capturedInBefore";

        [BeforeStep]
        public static void BeforeStep(ScenarioContext ctx)
        {
            ctx[CapturedInBefore] = ctx.GetStepHashtags();
        }

        [AfterStep]
        public static void AfterStep(ScenarioContext ctx)
        {
            var capturedInAfter = ctx.GetStepHashtags();

            if (ctx.StepContext.StepInfo.Text.Contains("triggers hook validation"))
            {
                ((string[])ctx[CapturedInBefore]).ShouldContain("hook-test");
                capturedInAfter.ShouldContain("hook-test");
            }
        }

        [BeforeStep("bind1")]
        [BeforeStep("bind2")]
        public static void BeforeStepWithTag(ScenarioContext ctx, IReqnrollOutputHelper output)
        {
            output.WriteLine($"Before step scenario tags: {string.Join(", ", ctx.ScenarioInfo.Tags)}");
            output.WriteLine($"Before step combined tags: {string.Join(", ", ctx.ScenarioInfo.CombinedTags)}");
        }

        [AfterStep("bind1")]
        [AfterStep("bind2")]
        public static void AfterStepWithTag(ScenarioContext ctx, IReqnrollOutputHelper output)
        {
            output.WriteLine($"After step scenario tags: {string.Join(", ", ctx.ScenarioInfo.Tags)}");
            output.WriteLine($"After step combined tags: {string.Join(", ", ctx.ScenarioInfo.CombinedTags)}");
        }
    }
}
