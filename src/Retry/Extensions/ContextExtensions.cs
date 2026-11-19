using Ohirok.Retry.Core;
using Reqnroll;

namespace Ohirok.Retry.Extensions
{
    public static class ContextExtensions
    {
        public static bool TryGetRetryContext(this ScenarioContext sc, out RetryContext ctx)
        {
            return sc.TryGetValue(out ctx);
        }

        public static RetryContext GetRetryContext(this ScenarioContext sc)
        {
            sc.TryGetValue<RetryContext>(out var ctx);
            return ctx;
        }

        public static void MigrateToNextAttempt(this ScenarioContext sc)
        {
            if (!sc.TryGetValue<RetryContext>(out var ctx)) return;

            ctx.ScenarioBackup ??= [];
            foreach (var kvp in sc)
                ctx.ScenarioBackup[kvp.Key] = kvp.Value;
        }

        public static void AddToNextAttempt<T>(this ScenarioContext sc, string key, T value)
        {
            if (!sc.TryGetValue<RetryContext>(out var ctx)) return;
            ctx.ScenarioBackup ??= [];
            ctx.ScenarioBackup[key] = value;
        }

        public static void SetInNextAttempt<T>(this ScenarioContext sc, T value)
        {
            if (!sc.TryGetValue<RetryContext>(out var ctx)) return;
            ctx.ScenarioBackup ??= [];
            ctx.ScenarioBackup[typeof(T).FullName] = value;
        }
    }
}
