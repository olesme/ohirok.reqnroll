namespace Ohirok.RuntimeMiddleware
{
    internal sealed class ChainRegistryHolder
    {
        readonly Dictionary<Type, ChainRegistry> _registries = [];
        readonly object _lock = new();
        bool _inherited;

        public ChainRegistry GetOrCreate(Type interfaceType, Type baseType)
        {
            lock (_lock)
            {
                if (!_registries.TryGetValue(interfaceType, out var registry))
                {
                    registry = new ChainRegistry(baseType);
                    _registries[interfaceType] = registry;
                }
                return registry;
            }
        }

        public void InheritFrom(ChainRegistryHolder parent)
        {
            lock (_lock)
            {
                if (_inherited) return;
                foreach (var kvp in parent._registries)
                {
                    if (!_registries.TryGetValue(kvp.Key, out var existing))
                        _registries[kvp.Key] = kvp.Value;
                    else
                        existing.MergeFrom(kvp.Value);
                }
                _inherited = true;
            }
        }
    }
}
