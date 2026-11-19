using Shouldly;

namespace XFailNUnitTests.Negative.StepDefinitions
{
    [Binding]
    public static class Steps
    {
        [Given("step that throws ArgumentException")]
        public static void StepThrowsArgumentException()
            => throw new ArgumentException("Test argument exception");

        [Then("this step should never execute")]
        public static void StepShouldNeverExecute()
            => throw new ShouldAssertException("This step should never be reached - previous step should have thrown");
    }
}
