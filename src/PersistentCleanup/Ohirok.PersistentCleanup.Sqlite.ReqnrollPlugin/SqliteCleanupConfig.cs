namespace Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin
{
    /// <summary>
    /// Configuration for SQLite storage
    /// </summary>
    internal sealed class SqliteCleanupConfig : CleanupConfigBase
    {
        public string DbPath { get; set; } = "cleanup.db";
    }
}
