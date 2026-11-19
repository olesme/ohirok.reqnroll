namespace SoftAssertTests.Hooks
{
    /// <summary>No-op BeforeStep/AfterStep hooks scoped by tag. They only exist so that
    /// hooks pass through the binding invoker chain in scenarios tagged @withhook.</summary>
    [Binding]
    public sealed class StepHookGuards
    {
        [BeforeStep("withhook")]
        public void NoopBefore() { }

        [AfterStep("withhook")]
        public void NoopAfter() { }
    }
}
