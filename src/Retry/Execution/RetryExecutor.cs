using Ohirok.CustomBindings;
using Ohirok.PluginChains.Common.Reporting;
using Ohirok.Retry.Config;
using Ohirok.Retry.Core;
using Ohirok.Retry.Events;
using Reqnroll;
using Reqnroll.Events;

namespace Ohirok.Retry.Execution
{
    internal static class RetryExecutor
    {
        #region Step Retry

        internal static async Task<object> ExecuteStepAsync(
            RetryContext ctx,
            Func<Task<object>> executeStep,
            ScenarioContext sc)
        {
            var container = sc.ScenarioContainer;
            var output = container.Resolve<IReqnrollOutputHelper>();

            ctx.StartLoop();
            if (RetryConfig.PublishEvents)
            {
                await container.Resolve<ITestThreadExecutionEventPublisher>().PublishEventAsync(new RetryLoopStartedEvent(ctx));
            }
            if (CustomBindingsConfig.AllowCustomBindings)
            {
                await HooksExecutor.ExecuteAsync(container.Resolve<ICustomBindingProvider>(), ctx, RetryConstants.RetryLoopStartedEventKey, container);
            }

            object result = null;

            try
            {
                while (ctx.Attempt < ctx.MaxAttempts)
                {
                    ThrowIfTimedOut(ctx);
                    ctx.StartAttempt();

                    try
                    {
                        result = await executeStep();
                        ctx.Complete(RetryStatus.Success);
                        break;
                    }
                    catch (Exception ex)
                    {
                        ctx.RecordError(ex);
                        await OnDecisionAsync(ctx, sc);

                        if (!CanRetry(ctx, ex))
                        {
                            ctx.Complete(RetryStatus.Failed);
                            throw;
                        }

                        ThrowIfTimedOut(ctx);

                        output.WriteLine($"[Retry] Step attempt {ctx.Attempt} failed: {ex.Message}");

                        if (CustomBindingsConfig.AllowCustomBindings)
                        {
                            await HooksExecutor.ExecuteAsync(container.Resolve<ICustomBindingProvider>(), ctx, RetryConstants.RetryRestartedEventKey, container);
                        }

                        if (RetryConfig.PublishEvents)
                        {
                            await container.Resolve<ITestThreadExecutionEventPublisher>().PublishEventAsync(new RetryRestartedEvent(ctx));
                        }

                        if (ctx.Delay > TimeSpan.Zero)
                            await Task.Delay(ctx.Delay);
                    }
                }
                return result;
            }
            finally
            {
                if (CustomBindingsConfig.AllowCustomBindings)
                {
                    await HooksExecutor.ExecuteAsync(container.Resolve<ICustomBindingProvider>(), ctx, RetryConstants.RetryLoopCompletedEventKey, container);
                }
                if (RetryConfig.PublishEvents)
                {
                    await container.Resolve<ITestThreadExecutionEventPublisher>().PublishEventAsync(new RetryLoopCompletedEvent(ctx));
                }
                LogCompletion(output, ctx, sc);
            }
        }

        #endregion

        #region Block Retry

        internal static async Task ExecuteBlockStepAsync(
            RetryContext ctx,
            Func<Task<object>> executeStep,
            ScenarioContext sc)
        {
            var container = sc.ScenarioContainer;
            var output = container.Resolve<IReqnrollOutputHelper>();

            if (ctx.HasFailedInCurrentAttempt)
            {
                output.WriteLine("[Retry] -> skipped: block failure");
                return;
            }

            ThrowIfTimedOut(ctx);

            try
            {
                await executeStep();
            }
            catch (Exception ex)
            {
                ctx.RecordError(ex);
                await OnDecisionAsync(ctx, sc);

                if (CanRetry(ctx, ex))
                {
                    // Out of time: fail this step with the timeout (Reqnroll records it on the step, so the report
                    // shows it), rather than letting the block loop throw after every step already looks passed.
                    ThrowIfTimedOut(ctx);
                    ctx.ShouldContinueLoop = true;
                    output.WriteLine($"[Retry] Block step failed: {ex.Message}");
                }
                else
                {
                    ctx.ShouldContinueLoop = false;
                    throw;
                }
            }
        }

        #endregion

        #region Test Retry Events

        internal static async Task OnLoopStartedAsync(RetryContext ctx, ScenarioContext sc)
        {
            var container = sc.ScenarioContainer;
            if (RetryConfig.PublishEvents)
            {
                await container.Resolve<ITestThreadExecutionEventPublisher>().PublishEventAsync(new RetryLoopStartedEvent(ctx));
            }

            if (CustomBindingsConfig.AllowCustomBindings)
            {
                await HooksExecutor.ExecuteAsync(container.Resolve<ICustomBindingProvider>(), ctx, RetryConstants.RetryLoopStartedEventKey, container);
            }
        }

        internal static async Task OnDecisionAsync(RetryContext ctx, ScenarioContext sc)
        {
            var container = sc.ScenarioContainer;
            if (CustomBindingsConfig.AllowCustomBindings)
            {
                await HooksExecutor.ExecuteAsync(container.Resolve<ICustomBindingProvider>(), ctx, RetryConstants.RetryDecisionEventKey, container);
            }
            if (RetryConfig.PublishEvents)
            {
                await container.Resolve<ITestThreadExecutionEventPublisher>().PublishEventAsync(new RetryDecisionEvent(ctx));
            }
        }

        internal static async Task OnRestartedAsync(RetryContext ctx, ScenarioContext sc)
        {
            var container = sc.ScenarioContainer;
            if (CustomBindingsConfig.AllowCustomBindings)
            {
                await HooksExecutor.ExecuteAsync(container.Resolve<ICustomBindingProvider>(), ctx, RetryConstants.RetryRestartedEventKey, container);
            }
            if (RetryConfig.PublishEvents)
            {
                await container.Resolve<ITestThreadExecutionEventPublisher>().PublishEventAsync(new RetryRestartedEvent(ctx));
            }
        }

        internal static async Task OnLoopCompletedAsync(RetryContext ctx, ScenarioContext sc)
        {
            var container = sc.ScenarioContainer;

            if (CustomBindingsConfig.AllowCustomBindings)
            {
                await HooksExecutor.ExecuteAsync(container.Resolve<ICustomBindingProvider>(), ctx, RetryConstants.RetryLoopCompletedEventKey, container);
            }
            if (RetryConfig.PublishEvents)
            {
                await container.Resolve<ITestThreadExecutionEventPublisher>().PublishEventAsync(new RetryLoopCompletedEvent(ctx));
            }
        }

        #endregion

        #region Private Helpers

        private static bool CanRetry(RetryContext ctx, Exception ex)
        {
            if (ctx.DecisionOverride.HasValue)
                return ctx.DecisionOverride.Value && ctx.Attempt < ctx.MaxAttempts && !ctx.CheckTimeout();

            return ctx.Attempt < ctx.MaxAttempts &&
                   ConditionChecker.ShouldRetry(ctx.Conditions, ex);
        }

        internal static void ThrowIfTimedOut(RetryContext ctx)
        {
            if (!ctx.CheckTimeout()) return;
            throw CreateTimeout(ctx);
        }

        /// <summary>
        /// The exception every retry type (test, block, step) fails with when its time budget runs out: a
        /// <see cref="TimeoutException"/> whose message states the timeout and the last error, with that last error
        /// as <see cref="Exception.InnerException"/> - so neither the timeout nor the real failure is hidden.
        /// </summary>
        internal static TimeoutException CreateTimeout(RetryContext ctx)
        {
            ctx.Complete(RetryStatus.TimedOut);
            var last = ctx.LastFailure;
            return new TimeoutException(
                $"{TimeoutSummary(ctx)}" + (last == null ? "" : $". Last error: {last.GetType().Name}: {last.Message}"),
                last);
        }

        internal static string TimeoutSummary(RetryContext ctx) =>
            $"Retry {ctx.Type} timed out after {ctx.Elapsed.TotalSeconds:F1}s " +
            $"({ctx.Attempt} attempts, limit: {ctx.Timeout.TotalSeconds:F1}s)";

        /// <summary>Log line + report issue for a timed-out loop, identical for every retry type.</summary>
        internal static void QueueTimedOutSignal(RetryContext ctx, ScenarioContext sc)
        {
            var summary = TimeoutSummary(ctx);
            var last = ctx.LastFailure;
            var message = last == null ? summary : $"{summary}. Last error: {last.GetType().Name}: {last.Message}";
            var tags = sc.ScenarioInfo.Tags;
            PendingRetrySignals.Enqueue(sc, output =>
            {
                output.WriteLine($"[Retry] {message}");
                output.ReportIssue(ReportIssueCategory.RetryTimedOut, "Ohirok.Retry", message, tags: tags);
            });
        }

        private static void LogCompletion(IReqnrollOutputHelper output, RetryContext ctx, ScenarioContext sc)
        {
            if (ctx.Status == RetryStatus.TimedOut)
            {
                QueueTimedOutSignal(ctx, sc);
                return;
            }

            if (ctx.Attempt <= 1) return;

            output.WriteLine(ctx.IsSuccess
                ? $"[Retry] {ctx.Type} succeeded on attempt {ctx.Attempt}"
                : $"[Retry] {ctx.Type} failed after {ctx.Attempt} attempts");

            if (ctx.IsSuccess)
                output.ReportIssue(ReportIssueCategory.RetrySucceeded, "Ohirok.Retry",
                    $"{ctx.Type} succeeded on attempt {ctx.Attempt}", tags: sc.ScenarioInfo.Tags);
        }

        #endregion
    }
}
