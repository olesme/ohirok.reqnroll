using Reqnroll;

namespace Ohirok.XFail.ReqnrollPlugin.Execution
{
    /// <summary>
    /// Reqnroll only accepts output while a step or hook is running. A scenario's XFail verdict is reached after its
    /// last step, so the report signal is queued here and written from <see cref="InternalXFailHooks"/>.
    /// </summary>
    internal static class PendingXFailSignals
    {
        // A plugin-specific type: ScenarioContext keys values by type name, so a bare list type could collide with
        // another plugin's queue.
        private sealed class SignalList : List<Action<IReqnrollOutputHelper>>;

        public static void Enqueue(ScenarioContext sc, Action<IReqnrollOutputHelper> action)
        {
            if (!sc.TryGetValue<SignalList>(out var list))
            {
                list = new SignalList();
                sc.Set(list);
            }
            list.Add(action);
        }

        public static void Flush(ScenarioContext sc)
        {
            if (!sc.TryGetValue<SignalList>(out var list) || list.Count == 0) return;
            var output = sc.ScenarioContainer.Resolve<IReqnrollOutputHelper>();
            foreach (var action in list) action(output);
            list.Clear();
        }
    }

    [Binding]
    internal sealed class InternalXFailHooks(ScenarioContext sc)
    {
        [AfterScenario(Order = int.MaxValue)]
        public void FlushXFailSignals() => PendingXFailSignals.Flush(sc);
    }
}
