using Ohirok.StepHashtags.ReqnrollPlugin;
using Shouldly;

namespace StepHashtagsNUnitTests.StepDefinitions
{
    [Binding]
    public class MultipleHashtagsSteps(ScenarioContext ctx)
    {
        [Given("step validates hashtag count is {int}")]
        public void ValidateCount(int expected)
        {
            ctx.GetStepHashtags().Length.ShouldBe(expected);
        }

        [Given("step validates hashtag {string} has value {string}")]
        public void ValidateHashtagValue(string prefix, string expected)
        {
            var hashtags = ctx.GetStepHashtags();
            var tag = hashtags.FirstOrDefault(h => h.StartsWith($"{prefix}:"));
            tag.ShouldNotBeNull();
            var value = tag?.Split(':')[1];
            value.ShouldBe(expected);
        }
    }
}
