using Shouldly;

namespace XFailNUnitTests.StepDefinitions
{
    // The scenario-level hooks fire after the last step, so the assertion lives in an AfterScenario hook.
    [Binding]
    public class ScenarioHookChecks(ScenarioContext sc)
    {
        [AfterScenario("check-xfail-hook")]
        public void CheckXFailHook()
            => (sc.TryGetValue<string>("hook:xfail", out var v) ? v : "<hook did not run>").ShouldBe("ArgumentException");

        [AfterScenario("check-afail-hook-none")]
        public void CheckAFailHookNone()
            => (sc.TryGetValue<string>("hook:afail", out var v) ? v : "<hook did not run>").ShouldBe("none");
    }
}
