using Catchy;
using Ohirok.CustomBindings;
using Ohirok.PluginChains.Common.Reporting;
using Ohirok.SoftAssert.Core;
using Reqnroll;
using Reqnroll.BoDi;

namespace Ohirok.SoftAssert.Execution
{
    /// <summary>
    /// Manually registered as a BeforeScenario hook (see <c>SoftAssertRuntimePlugin</c>) rather than discovered via
    /// <c>[Binding]</c> scanning, mirroring Catchy's own <c>CatchyHooks</c> registration. Running as a hook (rather
    /// than from a <c>CustomizeScenarioDependencies</c> handler) guarantees Catchy's <see cref="StatefulAsserter"/>
    /// has already been registered into the scenario container, regardless of plugin load order.
    /// </summary>
    internal sealed class SoftAssertHooks(IObjectContainer container, ScenarioContext scenarioContext)
    {
        public void WireOnFlush()
        {
            if (!container.IsRegistered<StatefulAsserter>()) return;

            var asserter = container.Resolve<StatefulAsserter>();
            var provider = container.Resolve<ICustomBindingProvider>();
            var output = container.Resolve<IReqnrollOutputHelper>();
            var sc = scenarioContext;

            asserter.Soft.SoftState.OnFlush = [.. asserter.Soft.SoftState.OnFlush, (Func<AggregateAssertionException, Task>)(agg =>
            {
                // Fires before Catchy decides whether to rethrow the aggregate; whether this ultimately
                // becomes a "pass with issues" signal (vs. context on an already-failed scenario) is
                // derived later by the reporting formatter from the scenario's final outcome, not here.
                output.ReportIssue(ReportIssueCategory.SoftAssertUnflushed, "Ohirok.SoftAssert", agg.Message, tags: sc.ScenarioInfo.Tags);

                var ctx = SoftAssertContext.ForScenario(sc);
                ctx.Aggregate = agg;
                return SoftAssertHooksExecutor.ExecuteAsync(provider, ctx, SoftAssertConstants.SoftFlushEventKey, container);
            })];
        }
    }
}
