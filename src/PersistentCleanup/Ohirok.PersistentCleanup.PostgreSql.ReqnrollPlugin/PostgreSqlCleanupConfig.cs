namespace Ohirok.PersistentCleanup.PostgreSql.ReqnrollPlugin
{
    internal sealed class PostgreSqlCleanupConfig : CleanupConfigBase
    {
        public string ConnectionString { get; set; } = string.Empty;
    }

    internal static class PostgreSqlConfigHelper
    {
        private static PostgreSqlCleanupConfig _cached;
        public static PostgreSqlCleanupConfig Config =>
            _cached ??= CleanupConfigHelper.GetConfig<PostgreSqlCleanupConfig>();
    }
}
