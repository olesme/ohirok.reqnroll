using Ohirok.PluginChains.Common;

namespace Ohirok.PersistentCleanup
{
    /// <summary>
    /// Base configuration for PersistentCleanup plugins.
    /// Storage-specific plugins extend this with their own fields.
    /// </summary>
    public class CleanupConfigBase
    {
        /// <summary>Scope value that makes a service see (and perform) records of every scope.</summary>
        public const string AllScopes = "*";

        /// <summary>Name of the environment variable that overrides <see cref="Scope"/>.</summary>
        public const string ScopeEnvironmentVariable = "OHIROK_CLEANUP_SCOPE";

        public bool Enabled { get; set; } = true;
        public bool AutoCreateDb { get; set; } = true;
        public bool AutoPerformOnStart { get; set; } = true;
        public int MaxConcurrency { get; set; } = 5;
        public int DefaultMaxRetries { get; set; } = 3;

        /// <summary>
        /// Completed records are kept this many days and then deleted by the next PerformAllAsync.
        /// 0 = delete a record right after it completes.
        /// </summary>
        public int DeleteCompletedAfterDays { get; set; } = 7;

        /// <summary>Orphan records are deleted after this many days. 0 = never.</summary>
        public int DeleteOrphansAfterDays { get; set; } = 30;

        /// <summary>
        /// A record stuck in Processing for longer than this is considered abandoned
        /// (its run died) and is executed again.
        /// </summary>
        public int ProcessingTimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Scope of this test process. Empty = no scope. "*" = every scope (for a dedicated cleanup step).
        /// Can be overridden by the OHIROK_CLEANUP_SCOPE environment variable.
        /// </summary>
        public string Scope { get; set; } = string.Empty;

        public LogLevel LogLevel { get; set; } = LogLevel.Detailed;
    }

    /// <summary>
    /// Helper to get storage-specific config from reqnroll.json
    /// </summary>
    public static class CleanupConfigHelper
    {
        public static TConfig GetConfig<TConfig>(string sectionName = "persistentCleanup")
            where TConfig : CleanupConfigBase, new()
        {
            return PluginConfigProvider.GetSection<TConfig>(sectionName);
        }
    }

    public enum LogLevel
    {
        None = 0,
        Brief = 1,
        Detailed = 2
    }
}
