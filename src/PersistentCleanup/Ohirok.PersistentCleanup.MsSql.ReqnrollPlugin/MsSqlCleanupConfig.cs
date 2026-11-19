namespace Ohirok.PersistentCleanup.MsSql.ReqnrollPlugin
{
    internal sealed class MsSqlCleanupConfig : CleanupConfigBase
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
