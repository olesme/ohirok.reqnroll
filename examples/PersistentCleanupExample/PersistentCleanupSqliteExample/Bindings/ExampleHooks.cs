namespace PersistentCleanupSqliteExample.Bindings
{
    [Binding]
    public static class ExampleHooks
    {
        // The scenarios count attempts and failures from scratch. A failed record left in the database by an
        // earlier run would be retried by PerformAllAsync (that is the point of the plugin) and skew the counts,
        // so the example starts from an empty database. The file is only created on first use of the service,
        // which happens after this hook.
        [BeforeTestRun(Order = 0)]
        public static void StartWithEmptyDatabase()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
            {
                try { File.Delete(Path.GetFullPath("cleanup-example.db" + suffix)); }
                catch (IOException) { /* in use or already gone: the example still works */ }
            }
        }
    }
}
