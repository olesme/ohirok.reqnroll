using System.Reflection;
using Reqnroll.BoDi;

namespace Ohirok.RuntimeMiddleware
{
    /// <summary>The links registered for one service interface in one container.</summary>
    internal sealed class ChainRegistry(Type interfaceType, Type baseType)
    {
        readonly Type _interfaceType = interfaceType;
        readonly Type _baseType = baseType;
        readonly List<ChainLink> _links = [];
        readonly object _lock = new();

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
            }
        }

        /// <summary>Builds the chain; called once per container by BoDi, which keeps the result.</summary>
        public object Build(IObjectContainer container)
        {
            ChainLink[] sorted;
            lock (_lock) sorted = [.. _links.OrderByDescending(x => x.Order)];

            object current = CreateBase(container);
            foreach (var link in sorted)
            {
                current = link.Factory != null
                    ? link.Factory(current, container)
                    : CreateInstance(link.Type, current, container);
            }
            return current;
        }

        // The parent's implementation (Reqnroll's default or another plugin's replacement) when the parent has one;
        // a chain in a top-level container starts from TBase.
        object CreateBase(IObjectContainer container)
        {
            var parent = (container as ObjectContainer)?.BaseContainer;
            return parent != null && parent.IsRegistered(_interfaceType)
                ? parent.Resolve(_interfaceType)
                : container.Resolve(_baseType);
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
