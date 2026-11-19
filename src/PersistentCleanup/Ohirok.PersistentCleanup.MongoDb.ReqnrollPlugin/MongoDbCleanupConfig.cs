namespace Ohirok.PersistentCleanup.MongoDb.ReqnrollPlugin
{
    public sealed class MongoDbCleanupConfig : CleanupConfigBase
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = "CleanupDb";
        public string CollectionName { get; set; } = "CleanupRecords";
    }
}
