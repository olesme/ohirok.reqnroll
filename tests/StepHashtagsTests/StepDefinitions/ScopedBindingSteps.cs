using Shouldly;

namespace StepHashtagsNUnitTests.StepDefinitions
{
    [Binding]
    public class ScopedBindingSteps(ScenarioContext ctx)
    {
        // Scoped: bind1
        [Scope(Tag = "bind1")]
        [Given("this step is associated with bind1 scope")]
        public void StepForBind1()
        {
            ctx["scoped"] = "bind1";
        }

        // Scoped: bind2
        [Scope(Tag = "bind2")]
        [Given("this step is associated with bind2 scope")]
        public void StepForBind2()
        {
            ctx["scoped"] = "bind2";
        }

        // Fallback - should never called
        [Given("this step is associated with bind1 scope")]
        [Given("this step is associated with bind2 scope")]
        public static void Fallback()
        {
            throw new ShouldAssertException("Fallback step definition MUST NOT be executed when scoped ones match!");
        }
    }
}
