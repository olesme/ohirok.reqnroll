using System.Collections.Concurrent;
using Ohirok.PluginChains.Common.Reporting;
using Ohirok.Retry.Core;
using Ohirok.Retry.Execution;
using Ohirok.Retry.Extensions;
using Ohirok.Retry.Helpers;
using Reqnroll;

namespace Ohirok.Retry
{
    public static class Manager
    {
        #region Context Lookup

        private static readonly ConcurrentDictionary<ITestRunner, RetryContext> _runnerContexts = new();

        public static RetryContext GetRetryContext(ITestRunner testRunner)
        {
            _runnerContexts.TryGetValue(testRunner, out var ctx);
            return ctx;
        }

        #endregion

        #region ShouldRetry

        public static bool ShouldRetry(RetryContext ctx)
        {
            if (ctx == null) return false;
            if (ctx.Attempt >= ctx.MaxAttempts) return false;
            if (ctx.CheckTimeout()) return false;

            if (ctx.DecisionOverride.HasValue) return ctx.DecisionOverride.Value;

            var sc = ctx.CurrentScenarioContext;
            if (sc == null) return false;

            var status = sc.ScenarioExecutionStatus;
            // Only a real failure can turn out differently next time. A pending, undefined or ambiguous step fails the same
            // way on every attempt, so retrying it only repeats the same result.
            if (status == ScenarioExecutionStatus.OK ||
                status == ScenarioExecutionStatus.Skipped ||
                status == ScenarioExecutionStatus.UndefinedStep ||
                status == ScenarioExecutionStatus.StepDefinitionPending ||
                status == ScenarioExecutionStatus.BindingError)
                return false;
            if (RetryExecutor.IsDeterministicFailure(sc.TestError)) return false;

            return ConditionChecker.ShouldRetry(ctx.Conditions, sc.TestError);
        }

        #endregion

        #region Test Retry (wraps entire test method body)

        public static async Task WrapTestMethodAsync(ITestRunner testRunner, Func<Task> testBody)
        {
            var ctx = new RetryContext { Type = RetryType.Test };
            _runnerContexts[testRunner] = ctx;

            try
            {
                do
                {
                    ctx.ShouldContinueLoop = false;
                    ctx.LastException = null;

                    try
                    {
                        await testBody();
                    }
                    catch (Exception ex)
                    {
                        ctx.RecordError(ex);
                    }

                    if (ctx.Attempt == 0)
                    {
                        if (ctx.LastException != null)
                            throw ctx.LastException;
                        return;
                    }

                    if (ctx.LastException != null)
                    {
                        var decisionSc = ctx.CurrentScenarioContext;
                        if (decisionSc != null)
                            await RetryExecutor.OnDecisionAsync(ctx, decisionSc);
                    }

                    if (ctx.LastException != null && ShouldRetry(ctx))
                    {
                        var sc = ctx.CurrentScenarioContext;
                        if (sc != null)
                            await RetryExecutor.OnRestartedAsync(ctx, sc);

                        // Ends this attempt's scenario (its AfterScenario hooks run here). A failing AfterScenario hook
                        // must not stop the retry, nor hide: RetryBindingInvoker turns it into a warning on this attempt
                        // (see EndingRetriedAttempt). Anything else that still fails here is shown on the final attempt,
                        // since this attempt's output is closed by the time Reqnroll rethrows.
                        var endingAttempt = ctx.Attempt;
                        ctx.EndingRetriedAttempt = true;
                        try { await testRunner.OnScenarioEndAsync(); }
                        catch (Exception hookEx)
                        {
                            var real = (hookEx as System.Reflection.TargetInvocationException)?.InnerException ?? hookEx;
                            ctx.EarlierAttemptHookFailures.Add($"Ending attempt {endingAttempt} failed: {real.GetType().Name}: {real.Message}");
                        }
                        finally { ctx.EndingRetriedAttempt = false; }

                        ctx.ShouldContinueLoop = true;

                        if (ctx.Delay > TimeSpan.Zero)
                            await Task.Delay(ctx.Delay);
                    }
                    else if (ctx.LastException != null && ctx.Attempt < ctx.MaxAttempts && ctx.CheckTimeout())
                    {
                        // Out of time with attempts left: fail with the same TimeoutException the block and step
                        // retries raise, wrapping the last error instead of surfacing it alone.
                        ctx.LastException = RetryExecutor.CreateTimeout(ctx);
                    }
                } while (ctx.ShouldContinueLoop);

                // Finalization: runs BEFORE TearDown's OnScenarioEndAsync, so the completion signal
                // below is queued (DeferredOutput) rather than written directly - see its remarks.
                var finalSc = ctx.CurrentScenarioContext;
                if (finalSc != null)
                {
                    foreach (var failure in ctx.EarlierAttemptHookFailures)
                    {
                        var message = failure;
                        DeferredOutput.Enqueue(finalSc, output => output.EmbedWarning(System.Net.WebUtility.HtmlEncode(message)));
                    }

                    bool success = ctx.LastException == null;
                    if (ctx.Status != RetryStatus.TimedOut)
                        ctx.Complete(success ? RetryStatus.Success : RetryStatus.Failed);

                    await RetryExecutor.OnLoopCompletedAsync(ctx, finalSc);

                    if (ctx.Status == RetryStatus.TimedOut)
                    {
                        RetryExecutor.QueueTimedOutSignal(ctx, finalSc);
                    }
                    else if (ctx.Attempt > 1)
                    {
                        var attempt = ctx.Attempt;
                        var tags = finalSc.ScenarioInfo.Tags;

                        DeferredOutput.Enqueue(finalSc, output =>
                        {
                            output.WriteLine(success
                                ? $"[Retry] Test succeeded on attempt {attempt}"
                                : $"[Retry] Test failed after {attempt} attempts");

                            if (success)
                                output.ReportIssue(ReportIssueCategory.RetrySucceeded, "Ohirok.Retry",
                                    $"Passed after {attempt} attempts", tags: tags);
                        });
                    }
                }

                if (ctx.LastException != null)
                    throw ctx.LastException;
            }
            finally
            {
                ctx.CurrentScenarioContext?.Remove(typeof(RetryContext).FullName);
                ctx.Dispose();
                _runnerContexts.TryRemove(testRunner, out _);
            }
        }

        #endregion

        #region Block Retry (@retryblock tag on scenario or #retryblock hashtag on steps)

        public static async Task WrapBlockAsync(ITestRunner testRunner, Func<Task> steps)
        {
            await WrapBlockAsync(testRunner, [], steps);
        }

        public static async Task WrapBlockAsync(ITestRunner testRunner, string[] hashtags, Func<Task> steps)
        {
            var sc = testRunner.ScenarioContext;

            if (sc.TryGetRetryContext(out _))
            {
                await steps();
                return;
            }

            var ctx = new RetryContext
            {
                Type = RetryType.Block
            };
            ctx.InitFromScenarioContext(sc, hashtags);
            RetryTagParser.ParseTags(ctx);
            ctx.StartLoop();

            sc.ScenarioContainer.RegisterInstanceAs(ctx);
            sc.Set(ctx);

            await RetryExecutor.OnLoopStartedAsync(ctx, sc);

            try
            {
                do
                {
                    ctx.ShouldContinueLoop = false;
                    ctx.HasFailedInCurrentAttempt = false;
                    ctx.LastException = null;
                    ctx.StartAttempt();

                    try
                    {
                        await steps();
                    }
                    catch
                    {
                        if (!ctx.ShouldContinueLoop) throw;
                    }

                    if (ctx.ShouldContinueLoop)
                    {
                        RetryExecutor.ThrowIfTimedOut(ctx);

                        await RetryExecutor.OnRestartedAsync(ctx, sc);

                        if (ctx.Delay > TimeSpan.Zero)
                            await Task.Delay(ctx.Delay);
                    }
                } while (ctx.ShouldContinueLoop);

                // Reqnroll records a failing step on the scenario instead of letting the exception escape the
                // block, so a block whose last attempt failed still leaves the loop normally. Report it as the
                // failure it is, not as a success.
                // A timeout raised before a block step (mid-attempt) is recorded the same way.
                if (ctx.HasFailedInCurrentAttempt || ctx.Status == RetryStatus.TimedOut)
                {
                    await CompleteBlockAsFailedAsync(ctx, sc);
                    return;
                }

                ctx.Complete(RetryStatus.Success);
                await RetryExecutor.OnLoopCompletedAsync(ctx, sc);

                if (ctx.Attempt > 1)
                {
                    var attempt = ctx.Attempt;
                    var type = ctx.Type;
                    var tags = sc.ScenarioInfo.Tags;

                    DeferredOutput.Enqueue(sc, output =>
                    {
                        output.WriteLine($"[Retry] {type} succeeded on attempt {attempt}");
                        output.ReportIssue(ReportIssueCategory.RetrySucceeded, "Ohirok.Retry",
                            $"{type} succeeded on attempt {attempt}", tags: tags);
                    });
                }
            }
            catch (Exception)
            {
                await CompleteBlockAsFailedAsync(ctx, sc);
                throw;
            }
            finally
            {
                ctx.Dispose();
                sc.Remove(typeof(RetryContext).FullName);
            }
        }

        private static async Task CompleteBlockAsFailedAsync(RetryContext ctx, ScenarioContext sc)
        {
            if (ctx.Status != RetryStatus.TimedOut)
                ctx.Complete(RetryStatus.Failed);

            await RetryExecutor.OnLoopCompletedAsync(ctx, sc);

            if (ctx.Status == RetryStatus.TimedOut)
            {
                RetryExecutor.QueueTimedOutSignal(ctx, sc);
            }
            else if (ctx.Attempt > 1)
            {
                var attempt = ctx.Attempt;
                var type = ctx.Type;
                DeferredOutput.Enqueue(sc, output =>
                    output.WriteLine($"[Retry] {type} failed after {attempt} attempts"));
            }
        }

        #endregion

        #region Step Retry (via hashtags injected by generator)

        public static void SetStepRetryContext(ITestRunner testRunner, string[] hashtags)
        {
            var sc = testRunner.ScenarioContext;

            if (sc.TryGetRetryContext(out _))
                return;

            var ctx = new RetryContext { Type = RetryType.Step };
            ctx.InitFromScenarioContext(sc, hashtags);
            RetryTagParser.ParseTags(ctx);
            ctx.StartLoop();

            sc.ScenarioContainer.RegisterInstanceAs(ctx);
            sc.Set(ctx);
        }

        #endregion
    }
}
