namespace Ohirok.Reporting.History.PostgreSql.ReqnrollPlugin
{
    internal sealed class PostgreSqlHistoryConfig : ReportHistoryConfigBase
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
