namespace Ohirok.Reporting.History.Redis.ReqnrollPlugin
{
    internal sealed class RedisHistoryConfig : ReportHistoryConfigBase
    {
        public string ConnectionString { get; set; } = "";
        public string KeyPrefix { get; set; } = "reporthistory:";
        public int Database { get; set; } = 0;
    }
}
