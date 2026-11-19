using Catchy;
using Catchy.Sdk;
using Ohirok.CustomBindings;
using Ohirok.SoftAssert.Core;
using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.Infrastructure;
using Reqnroll.Tracing;

namespace Ohirok.SoftAssert.Execution
{
    internal sealed class SoftAssertBindingInvoker(IAsyncBindingInvoker inner) : IAsyncBindingInvoker
    {
        public async Task<object> InvokeBindingAsync(
            IBinding binding,
            IContextManager contextManager,
            object[] arguments,
            ITestTracer testTracer,
            DurationHolder durationHolder)
        {
            // Hooks go through the same invoker as steps. Only step definitions consume the soft marker.
            if (binding is not IStepDefinitionBinding)
                return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);

            var sc = contextManager.ScenarioContext;

            if (!sc.TryGetValue<SoftAssertContext>(out var ctx))
            {
                return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);
            }

            try
            {
                return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);
            }
            // An aggregate thrown by a step is a flush (SoftState.FlushIfNeeded): capturing it back
            // into the same state would swallow it.
            catch (Exception ex) when (ex is not AggregateAssertionException)
            {
                await CaptureAsync(sc, ctx, ex);
                return null;
            }
            finally
            {
                ClearContext(sc);
            }
        }

        private static async Task CaptureAsync(ScenarioContext sc, SoftAssertContext ctx, Exception ex)
        {
            if (!sc.ScenarioContainer.IsRegistered<StatefulAsserter>())
            {
                throw new InvalidOperationException(
                    "Ohirok.SoftAssert.ReqnrollPlugin requires Catchy.ReqnrollPlugin to also be registered " +
                    "(no StatefulAsserter is registered in the scenario container). Add the Catchy.ReqnrollPlugin " +
                    "NuGet package alongside this one.");
            }

            var asserter = sc.ScenarioContainer.Resolve<StatefulAsserter>();
            asserter.Soft.SoftState.Capture(ex);
            ctx.Error = ex;

            var provider = sc.ScenarioContainer.Resolve<ICustomBindingProvider>();
            await SoftAssertHooksExecutor.ExecuteAsync(provider, ctx, SoftAssertConstants.SoftFailureEventKey, sc.ScenarioContainer);
        }

        private static void ClearContext(ScenarioContext sc)
        {
            if (sc.TryGetValue<SoftAssertContext>(out _))
            {
                sc.Remove(typeof(SoftAssertContext).FullName);
            }
        }
    }
}
