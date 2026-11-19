using System;

namespace Ohirok.PluginChains.Common
{
    /// <summary>
    /// Holds a storage implementation that may not be known yet at DI-registration time (e.g. a CustomStorage
    /// plugin hands it over later via UseStorage) - shared by Ohirok.PersistentCleanup's ICleanupStorageManager
    /// and Ohirok.Reporting.History's IReportHistoryStorageManager, which each wrap one of these around their
    /// own public-facing proxy type.
    /// </summary>
    public sealed class LateBoundStorageProxy<TStorage> where TStorage : class
    {
        private TStorage _current;

        public LateBoundStorageProxy(TStorage initial = null)
        {
            _current = initial;
        }

        public TStorage Current => _current;

        public void UseStorage(TStorage storage, StorageReplaceMode mode)
        {
            if (_current == null)
            {
                _current = storage;
                return;
            }

            switch (mode)
            {
                case StorageReplaceMode.ThrowIfExists:
                    throw new InvalidOperationException($"A {typeof(TStorage).Name} implementation is already configured.");
                case StorageReplaceMode.Replace:
                    _current = storage;
                    break;
                case StorageReplaceMode.IgnoreIfExists:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
    }
}
