using Shouldly;
using static Ohirok.StepHashtags.ReqnrollPlugin.ScenarioContextExtensions;

namespace StepHashtagsNUnitTests.StepDefinitions
{
    [Binding]
    public class StepHashtagsSteps(ScenarioContext ctx)
    {
        [Given("step with hashtags validates {string}")]
        public void StepWithHashtags(string expected)
        {
            var hashtags = ctx.GetStepHashtags();
            var actual = string.Join(",", hashtags.Select(h => h.TrimStart('#')));
            actual.ShouldBe(expected);
        }

        [Given("step without hashtags validates none")]
        public void StepWithoutHashtags()
        {
            ctx.GetStepHashtags().ShouldBeEmpty();
        }

        [When("step triggers hook validation")]
        public static void StepTriggersHook()
        {
            // Hook will fire in afterstep
        }

        [Then("the step has no hashtags")]
        public void NoHashtags()
        {
            var tags = ctx.GetStepHashtags();
            if (tags.Length != 0)
                throw new InvalidOperationException("Unexpected hashtags: " + string.Join(", ", tags));
        }
    }
}

