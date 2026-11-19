using System.Reflection;
using Reqnroll.BoDi;

namespace Ohirok.RuntimeMiddleware
{
    internal sealed class ChainRegistry(Type baseType)
    {
        readonly Type _baseType = baseType;
        readonly List<ChainLink> _links = [];
        readonly object _lock = new();
        object _cached;

        public void AddLink(Type linkType, int order, DuplicateBehavior behavior)
        {
            ValidateOrder(order);
            lock (_lock)
            {
                var existing = _links.FirstOrDefault(l => l.Type == linkType);
                if (existing != null)
                {
                    switch (behavior)
                    {
                        case DuplicateBehavior.Replace: _links.Remove(existing); break;
                        case DuplicateBehavior.Allow: break;
                        case DuplicateBehavior.Throw: throw new InvalidOperationException($"Duplicate: {linkType.Name}");
                        case DuplicateBehavior.Ignore: return;
                    }
                }
                _links.Add(new ChainLink(linkType, order));
                _cached = null;
            }
        }

        public void AddLink(Func<object, IObjectContainer, object> factory, int order, string name, DuplicateBehavior behavior)
        {
            ValidateOrder(order);
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    var existing = _links.FirstOrDefault(l => l.Name == name);
                    if (existing != null)
                    {
                        switch (behavior)
                        {
                            case DuplicateBehavior.Replace: _links.Remove(existing); break;
                            case DuplicateBehavior.Allow: break;
                            case DuplicateBehavior.Throw: throw new InvalidOperationException($"Duplicate: {name}");
                            case DuplicateBehavior.Ignore: return;
                        }
                    }
                }
                _links.Add(new ChainLink(factory, order, name));
                _cached = null;
            }
        }

        public void MergeFrom(ChainRegistry other)
        {
            lock (_lock) lock (other._lock)
                {
                    foreach (var link in other._links)
                    {
                        if (link.Type != null && _links.Any(l => l.Type == link.Type)) continue;
                        if (!string.IsNullOrEmpty(link.Name) && _links.Any(l => l.Name == link.Name)) continue;
                        _links.Add(link);
                    }
                    _cached = null;
                }
        }

        public object Build(IObjectContainer container)
        {
            if (_cached != null) return _cached;
            lock (_lock)
            {
                if (_cached != null) return _cached;

                var sorted = _links.OrderByDescending(x => x.Order).ToArray();
                object current = CreateBase(container);

                for (int i = 0; i < sorted.Length; i++)
                {
                    var link = sorted[i];
                    current = link.Factory != null
                        ? link.Factory(current, container)
                        : CreateInstance(link.Type, current, container);
                }

                _cached = current;
                return current;
            }
        }

        object CreateBase(IObjectContainer container)
        {
            try { if (container.IsRegistered(_baseType)) return container.Resolve(_baseType); }
            catch 
            {
            // No
            }
            return CreateInstance(_baseType, null, container);
        }

        static internal object CreateInstance(Type type, object inner, IObjectContainer container)
        {
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length);

            foreach (var ctor in ctors)
            {
                var parms = ctor.GetParameters();
                var args = new object[parms.Length];
                bool innerUsed = inner == null;

                for (int i = 0; i < parms.Length; i++)
                {
                    if (!innerUsed && parms[i].ParameterType.IsInstanceOfType(inner))
                    {
                        args[i] = inner;
                        innerUsed = true;
                    }
                    else
                    {
                        args[i] = container.Resolve(parms[i].ParameterType);
                    }
                }

                if (innerUsed) return ctor.Invoke(args);
            }

            throw new InvalidOperationException($"Cannot create {type.Name}");
        }

        static void ValidateOrder(int order)
        {
            if (order == int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(order), "int.MaxValue is reserved");
        }
    }
}
