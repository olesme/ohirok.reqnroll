namespace Ohirok.Reporting.History.CosmosDb.ReqnrollPlugin
{
    internal sealed class CosmosDbHistoryConfig : ReportHistoryConfigBase
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseId { get; set; } = "ReportHistoryDb";
        public string ContainerId { get; set; } = "ReportHistoryRuns";
    }
}
