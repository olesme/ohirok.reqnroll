namespace Ohirok.PersistentCleanup.Redis.ReqnrollPlugin
{
    internal sealed class RedisCleanupConfig : CleanupConfigBase
    {
        public string ConnectionString { get; set; } = "localhost:6379";
        public string KeyPrefix { get; set; } = "cleanup:";
        public int Database { get; set; } = 0;
    }

    internal static class RedisConfigHelper
    {
        private static RedisCleanupConfig _cached;
        public static RedisCleanupConfig Config =>
            _cached ??= CleanupConfigHelper.GetConfig<RedisCleanupConfig>();
    }
}
