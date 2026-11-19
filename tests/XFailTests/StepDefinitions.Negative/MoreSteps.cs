namespace XFailNUnitTests.Negative.StepDefinitions
{
    [Binding]
    public static class MoreSteps
    {
        [Given("step that succeeds")]
        public static void StepThatSucceeds() { }

        [Given("step that throws InvalidOperationException")]
        public static void StepThrowsInvalidOperation()
            => throw new InvalidOperationException("Test invalid operation exception");
    }
}
