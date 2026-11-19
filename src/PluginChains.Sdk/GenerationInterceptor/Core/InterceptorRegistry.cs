using Ohirok.GenerationInterceptor.Infrastructure;
using Reqnroll.BoDi;

namespace Ohirok.GenerationInterceptor.Core
{
    public static class InterceptorRegistry
    {
        private static readonly List<IGenerationInterceptor> _list = [];
        private static IGenerationInterceptor[] _sorted;
        private static readonly object _lock = new();

        public static void Register(IGenerationInterceptor interceptor)
        {
            lock (_lock)
            {
                _list.Add(interceptor ?? throw new ArgumentNullException(nameof(interceptor)));
                _sorted = null;
            }
        }

        public static void Register(IObjectContainer container, IGenerationInterceptor interceptor)
        {
            InterceptorInfrastructure.EnsureRegistered(container);
            Register(interceptor);
        }

        internal static IGenerationInterceptor[] GetOrdered()
        {
            lock (_lock)
            {
                return _sorted ??= [.. _list.OrderBy(x => x.Order)];
            }
        }

        internal static IGenerationInterceptor[] GetOrderedForScenario(IReadOnlyList<string> allTags)
        {
            lock (_lock)
            {
                if (_list.Count == 0) return [];
                return [.. _list.OrderBy(x => x.GetEffectiveOrder(allTags))];
            }
        }

        public static bool HasAny
        {
            get { lock (_lock) return _list.Count > 0; }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _list.Clear();
                _sorted = null;
            }
            Invoke.Invalidate();
        }
    }
}
