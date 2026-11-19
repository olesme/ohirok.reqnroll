using System.Reflection;
using Ohirok.Retry.Attributes;
using Ohirok.Retry.Config;
using Ohirok.Retry.Core;
using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.Bindings.Reflection;
using Reqnroll.Infrastructure;
using Reqnroll.Tracing;

namespace Ohirok.Retry.Execution
{
    internal sealed class RetryBindingInvoker(IAsyncBindingInvoker inner) : IAsyncBindingInvoker
    {
        public async Task<object> InvokeBindingAsync(
            IBinding binding,
            IContextManager contextManager,
            object[] arguments,
            ITestTracer testTracer,
            DurationHolder durationHolder)
        {
            // The internal completion-flush hook must never be retry/timeout-tracked itself - it runs
            // once, after the retry loop (and its RetryContext) has already been finalized/disposed.
            if (binding.Method is RuntimeBindingMethod internalHookMethod &&
                internalHookMethod.MethodInfo.DeclaringType == typeof(InternalRetryHooks))
            {
                return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);
            }

            var sc = contextManager.ScenarioContext;

            if (sc == null)
            {
                return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);
            }

            if (!sc.TryGetValue<RetryContext>(out var ctx))
            {
                if (RetryConfig.AllowRetryStepAttribute && binding.Method is RuntimeBindingMethod rbm)
                {
                    var attr = rbm.MethodInfo.GetCustomAttribute<RetryStepAttribute>();
                    if (attr != null)
                    {
                        return await ExecuteWithStepRetry(attr, binding, contextManager, arguments, testTracer, durationHolder, sc);
                    }
                }

                return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);
            }

            return ctx.Type switch
            {
                RetryType.Step when binding is IStepDefinitionBinding =>
                    await ExecuteStepRetry(ctx, binding, contextManager, arguments, testTracer, durationHolder, sc),

                RetryType.Block when binding is IStepDefinitionBinding =>
                    await ExecuteBlockStep(ctx, binding, contextManager, arguments, testTracer, durationHolder, sc),

                RetryType.Test =>
                    await ExecuteWithErrorTracking(ctx, binding, contextManager, arguments, testTracer, durationHolder),

                _ => await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder)
            };
        }

        private async Task<object> ExecuteWithStepRetry(
            RetryStepAttribute attr,
            IBinding binding,
            IContextManager contextManager,
            object[] arguments,
            ITestTracer testTracer,
            DurationHolder durationHolder,
            ScenarioContext sc)
        {
            var stepCtx = attr.CreateContext(contextManager);
            RegisterContext(sc, stepCtx);

            try
            {
                return await RetryExecutor.ExecuteStepAsync(
                    stepCtx,
                    () => inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder),
                    sc);
            }
            finally
            {
                ClearContext(sc);
            }
        }

        private async Task<object> ExecuteBlockStep(
            RetryContext ctx,
            IBinding binding,
            IContextManager contextManager,
            object[] arguments,
            ITestTracer testTracer,
            DurationHolder durationHolder,
            ScenarioContext sc)
        {
            await RetryExecutor.ExecuteBlockStepAsync(
                ctx,
                () => inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder),
                sc);
            return null;
        }

        private async Task<object> ExecuteStepRetry(
            RetryContext ctx,
            IBinding binding,
            IContextManager contextManager,
            object[] arguments,
            ITestTracer testTracer,
            DurationHolder durationHolder,
            ScenarioContext sc)
        {
            try
            {
                return await RetryExecutor.ExecuteStepAsync(
                    ctx,
                    () => inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder),
                    sc);
            }
            finally
            {
                ClearContext(sc);
            }
        }

        private async Task<object> ExecuteWithErrorTracking(
            RetryContext ctx,
            IBinding binding,
            IContextManager contextManager,
            object[] arguments,
            ITestTracer testTracer,
            DurationHolder durationHolder)
        {
            RetryExecutor.ThrowIfTimedOut(ctx);

            try
            {
                return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);
            }
            catch (Exception ex)
            {
                ctx.RecordError(ex);
                throw;
            }
        }

        private static void RegisterContext(ScenarioContext sc, RetryContext ctx)
        {
            sc.ScenarioContainer.RegisterInstanceAs(ctx);
            sc.Set(ctx);
        }

        private static void ClearContext(ScenarioContext sc)
        {
            if (sc.TryGetValue<RetryContext>(out var ctx))
            {
                ctx.Dispose();
                sc.Remove(typeof(RetryContext).FullName);
            }
        }
    }
}
