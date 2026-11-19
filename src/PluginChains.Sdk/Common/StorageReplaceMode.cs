namespace Ohirok.PluginChains.Common
{
    /// <summary>How a late-bound storage backend (e.g. a CustomStorage plugin) should behave when a storage
    /// implementation is already configured - shared by Ohirok.PersistentCleanup and Ohirok.Reporting.History.</summary>
    public enum StorageReplaceMode
    {
        ThrowIfExists,
        IgnoreIfExists,
        Replace
    }
}
