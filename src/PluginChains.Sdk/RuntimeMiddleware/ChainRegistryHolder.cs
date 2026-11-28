namespace Ohirok.RuntimeMiddleware
{
    /// <summary>The chains of one container, one per service interface.</summary>
    internal sealed class ChainRegistryHolder
    {
        readonly Dictionary<Type, ChainRegistry> _registries = [];
        readonly object _lock = new();

        public ChainRegistry GetOrCreate(Type interfaceType, Type baseType)
        {
            lock (_lock)
            {
                if (!_registries.TryGetValue(interfaceType, out var registry))
                {
                    registry = new ChainRegistry(interfaceType, baseType);
                    _registries[interfaceType] = registry;
                }
                return registry;
            }
        }
    }
}
