using Ohirok.StepHashtags.ReqnrollPlugin;
using Reqnroll;

namespace StepHashtagsSmoke.StepDefinitions
{
    [Binding]
    public sealed class HashtagSmokeSteps(ScenarioContext sc)
    {
        [When("I call the external service")]
        public void CallService()
        {
            var tags = sc.GetStepHashtags();
            if (!sc.HasStepHashtag("timeout:5000"))
                throw new Exception($"Expected timeout:5000 hashtag, got [{string.Join(",", tags)}]");
            if (!sc.HasStepHashtag("priority:high"))
                throw new Exception($"Expected priority:high hashtag, got [{string.Join(",", tags)}]");
        }
    }
}
