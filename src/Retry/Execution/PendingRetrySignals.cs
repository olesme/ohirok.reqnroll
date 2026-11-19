using Reqnroll;

namespace Ohirok.Retry.Execution
{
    // Reqnroll's message-formatter execution tracker only accepts WriteLine/AddAttachment while a
    // step or hook is "current" on its internal tracker stack. Test-level and block-level retry
    // completion logging fires after the scenario's/block's last step has already finished (the
    // stack is empty by then), so writing directly there is silently dropped on the wire - the
    // underlying PublishEventAsync call is fire-and-forget, so the resulting exception never
    // surfaces either. Queue the write here instead and flush it from within a genuinely tracked
    // [AfterScenario] hook (see InternalRetryHooks), where the tracker stack is non-empty again.
    internal static class PendingRetrySignals
    {
        // ScenarioContext.Set<T>/TryGetValue<T> key purely by typeof(T).FullName (see
        // ReqnrollContext.GetDefaultKey), so a bare List<Action<IReqnrollOutputHelper>> would collide
        // with any other plugin (e.g. XFail's PendingXFailSignals) queuing the same closed generic
        // type in the same scenario. Wrap it in a plugin-specific type to keep the storage slot unique.
        private sealed class SignalList : List<Action<IReqnrollOutputHelper>>
        {
        }

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
            if (!sc.TryGetValue<SignalList>(out var list) || list.Count == 0)
                return;

            var output = sc.ScenarioContainer.Resolve<IReqnrollOutputHelper>();
            foreach (var action in list)
                action(output);

            list.Clear();
        }
    }
}
