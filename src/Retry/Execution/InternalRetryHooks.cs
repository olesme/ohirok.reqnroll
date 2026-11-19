using Reqnroll;

namespace Ohirok.Retry.Execution
{
    // Registered for binding discovery via RetryRuntimePlugin's ConfigurationDefaults handler (added
    // to AdditionalStepAssemblies), since this plugin assembly is not normally a step assembly. This is
    // the only genuinely Reqnroll-tracked hook in the Retry plugin; scenario-start setup is handled
    // directly by RetryRuntimePlugin's ScenarioStartedEvent subscription instead, to avoid depending on
    // that discovery for anything except this flush.
    [Binding]
    internal sealed class InternalRetryHooks(ScenarioContext sc)
    {
        [AfterScenario(Order = int.MaxValue)]
        public void AfterScenarioFlushRetrySignals()
        {
            PendingRetrySignals.Flush(sc);
        }
    }
}
