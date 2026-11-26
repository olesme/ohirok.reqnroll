using System;
using System.Collections.Generic;
using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.Bindings.Reflection;
using Reqnroll.BoDi;

namespace Ohirok.PluginChains.Common.Reporting
{
    /// <summary>
    /// Output that has to be written when Reqnroll would drop it. Reqnroll's message formatter accepts
    /// <see cref="IReqnrollOutputHelper"/> output only while a step or a hook is current; a verdict reached after the
    /// last step (a retry loop, an expected failure, a wrapper around the test method) has nowhere to write. Queue the
    /// write here instead: it runs in an <c>AfterScenario</c> hook that the SDK registers once (order
    /// <see cref="int.MaxValue"/>, so after your own hooks), with the scenario's output helper.
    /// </summary>
    public static class DeferredOutput
    {
        // ScenarioContext keys values by type name, so a type of our own keeps the slot from colliding with anyone's.
        private sealed class Queue : List<Action<IReqnrollOutputHelper>>;

        /// <summary>Queues <paramref name="write"/> to run in this scenario's final <c>AfterScenario</c> hook.</summary>
        public static void Enqueue(ScenarioContext scenarioContext, Action<IReqnrollOutputHelper> write)
        {
            if (scenarioContext == null) throw new ArgumentNullException(nameof(scenarioContext));
            if (write == null) throw new ArgumentNullException(nameof(write));
            if (!scenarioContext.TryGetValue<Queue>(out var queue))
            {
                queue = new Queue();
                scenarioContext.Set(queue);
            }
            queue.Add(write);
        }

        /// <summary>Runs and clears the queued writes now. Call it only while a step or hook is current.</summary>
        public static void Flush(ScenarioContext scenarioContext)
        {
            if (!scenarioContext.TryGetValue<Queue>(out var queue) || queue.Count == 0) return;
            var output = scenarioContext.ScenarioContainer.Resolve<IReqnrollOutputHelper>();
            var writes = queue.ToArray();
            queue.Clear();
            foreach (var write in writes) write(output);
        }

        /// <summary>True for the SDK's own flush hook, so a binding-invoker chain link (retry, timeouts, expected
        /// failures) can let it through untouched.</summary>
        public static bool IsFlushHook(IBinding binding)
            => binding.Method is RuntimeBindingMethod method && method.MethodInfo.DeclaringType == typeof(DeferredOutputHook);

        private sealed class Registered;

        internal static void RegisterFlushHook(IObjectContainer globalContainer)
        {
            if (globalContainer.IsRegistered<Registered>()) return;
            globalContainer.RegisterInstanceAs(new Registered());

            var registry = globalContainer.Resolve<IBindingRegistry>();
            var factory = globalContainer.Resolve<IBindingFactory>();
            registry.RegisterHookBinding(factory.CreateHookBinding(
                new RuntimeBindingMethod(typeof(DeferredOutputHook).GetMethod(nameof(DeferredOutputHook.FlushDeferredOutput))),
                HookType.AfterScenario, null, int.MaxValue));
        }
    }

    /// <summary>The flush hook <see cref="DeferredOutput"/> registers by hand (no [Binding] scanning needed).</summary>
    internal sealed class DeferredOutputHook(ScenarioContext scenarioContext)
    {
        public void FlushDeferredOutput() => DeferredOutput.Flush(scenarioContext);
    }
}
