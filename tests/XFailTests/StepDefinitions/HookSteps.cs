using Shouldly;
using Ohirok.XFail.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin.Attributes;

namespace XFailNUnitTests.StepDefinitions
{
    [Binding]
    public class RecordingHooks(ScenarioContext sc)
    {
        [OnXFail]
        public void OnX(XFailScenarioContext ctx) => sc["hook:xfail"] = ctx.Exception?.GetType().Name ?? "none";

        [OnAFail]
        public void OnA(XFailScenarioContext ctx) => sc["hook:afail"] = ctx.Exception?.GetType().Name ?? "none";

        [OnAFail("threw:TimeoutException")]
        public void OnATimeout(XFailScenarioContext ctx) => sc["hook:filtered-timeout"] = "ran";
    }

    [Binding]
    public class HookAssertionSteps(ScenarioContext sc)
    {
        [Then("the hook {string} saw {string}")]
        public void HookSaw(string hook, string expected)
        {
            (sc.ContainsKey("hook:" + hook)).ShouldBeTrue($"hook '{hook}' did not run");
            (sc["hook:" + hook]).ShouldBe(expected);
        }

        [Then("the hook {string} did not run")]
        public void HookDidNotRun(string hook)
            => (sc.ContainsKey("hook:" + hook)).ShouldBeFalse($"hook '{hook}' ran unexpectedly");
    }
}
