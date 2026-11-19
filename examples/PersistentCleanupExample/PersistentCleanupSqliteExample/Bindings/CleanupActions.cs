using Ohirok.PersistentCleanup;

namespace PersistentCleanupSqliteExample.Bindings
{
    [Binding]
    public class CleanupActions
    {
        private static readonly List<string> _executionOrder = [];
        private static readonly Dictionary<string, int> _attemptCounts = [];

        public static List<string> ExecutionOrder => _executionOrder;
        public static int GetAttemptCount(string key) => _attemptCounts.GetValueOrDefault(key);

        [Cleanup("DeleteTempUser")]
        public void DeleteTempUser(string userId) => _executionOrder.Add(userId);

        [Cleanup("RemoveTempFile")]
        public static void RemoveTempFile(string path) => _executionOrder.Add(path);

        [Cleanup("DeleteUndeletableResource")]
        public static void DeleteUndeletableResource(string resourceId)
        {
            _attemptCounts[resourceId] = _attemptCounts.GetValueOrDefault(resourceId) + 1;
            throw new InvalidOperationException($"Simulated failure deleting {resourceId}");
        }
    }
}
