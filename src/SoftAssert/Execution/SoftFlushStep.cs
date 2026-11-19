using Catchy;
using Ohirok.SoftAssert.Core;
using Reqnroll;

namespace Ohirok.SoftAssert.Execution
{
    [Binding]
    public sealed class SoftFlushStep(StatefulAsserter asserter)
    {
        // [StepDefinition] matches Given/When/Then, so it fits whatever block the last step was in.
        [StepDefinition(SoftAssertConstants.FlushStepText)]
        public Task Flush() => asserter.Soft.SoftState.FlushIfNeeded();
    }
}
