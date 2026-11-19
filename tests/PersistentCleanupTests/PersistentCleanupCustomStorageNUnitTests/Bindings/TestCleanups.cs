using Ohirok.PersistentCleanup;

namespace PersistentCleanupCustomStorageNUnitTests.Bindings
{
    [Binding]
    public class TestCleanups
    {
        // Cleanups run concurrently (maxConcurrency > 1), so all shared state is guarded by one lock.
        private static readonly object Gate = new();
        private static readonly Dictionary<string, bool> _executed = [];
        private static readonly List<string> _executionOrder = [];
        private static readonly Dictionary<string, int> _attemptCounts = [];

        public static void Reset()
        {
            lock (Gate)
            {
                _executed.Clear();
                _executionOrder.Clear();
                _attemptCounts.Clear();
            }
        }

        public static bool WasExecuted(string resourceId) { lock (Gate) return _executed.GetValueOrDefault(resourceId); }
        public static List<string> GetExecutionOrder() { lock (Gate) return [.. _executionOrder]; }
        public static int GetAttemptCount(string resourceId) { lock (Gate) return _attemptCounts.GetValueOrDefault(resourceId); }

        [Cleanup("CleanupUser")]
        public static void CleanupUser(string userId) => MarkExecuted(userId);

        [Cleanup("CleanupFailingUser")]
        public static void CleanupFailingUser(string userId)
        {
            lock (Gate) _attemptCounts[userId] = _attemptCounts.GetValueOrDefault(userId) + 1;
            throw new InvalidOperationException($"Simulated failure for {userId}");
        }

        [Cleanup]
        public void CleanupUserWithDefaultKey(string userId) => MarkExecuted(userId);

        private static void MarkExecuted(string userId)
        {
            lock (Gate)
            {
                _executed[userId] = true;
                _executionOrder.Add(userId);
            }
        }
    }
}
