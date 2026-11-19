namespace Ohirok.PersistentCleanup.CosmosDb.ReqnrollPlugin
{
    public sealed class CosmosDbCleanupConfig : CleanupConfigBase
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseId { get; set; } = "CleanupDb";
        public string ContainerId { get; set; } = "CleanupRecords";
    }
}
