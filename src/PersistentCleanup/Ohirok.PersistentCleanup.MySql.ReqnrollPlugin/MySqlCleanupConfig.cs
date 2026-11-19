namespace Ohirok.PersistentCleanup.MySql.ReqnrollPlugin
{
    internal sealed class MySqlCleanupConfig : CleanupConfigBase
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
