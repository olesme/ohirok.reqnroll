namespace Ohirok.Reporting.History.MongoDb.ReqnrollPlugin
{
    internal sealed class MongoDbHistoryConfig : ReportHistoryConfigBase
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = "ReportHistoryDb";
        public string CollectionName { get; set; } = "ReportHistoryRuns";
    }
}
