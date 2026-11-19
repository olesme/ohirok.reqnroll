using Ohirok.PersistentCleanup;

namespace PersistentCleanupCustomStorageExample.Bindings
{
    [Binding]
    public class CleanupActions
    {
        [Cleanup("DeleteTempUser")]
        public void DeleteTempUser(string userId) { }
    }
}
