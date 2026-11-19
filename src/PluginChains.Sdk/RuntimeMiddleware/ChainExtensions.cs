using Reqnroll.BoDi;

namespace Ohirok.RuntimeMiddleware
{
    public static class ChainExtensions
    {
        public static void RegisterChainLink<TInterface, TBase, TLink>(
            this IObjectContainer container,
            int order = 0,
            DuplicateBehavior duplicateBehavior = DuplicateBehavior.Replace,
            Func<IObjectContainer, bool> when = null)
            where TInterface : class
            where TBase : class, TInterface
            where TLink : class, TInterface
        {
            var holder = GetOrCreateHolder(container);
            var registry = holder.GetOrCreate(typeof(TInterface), typeof(TBase));

            if (when == null)
            {
                registry.AddLink(typeof(TLink), order, duplicateBehavior);
            }
            else
            {
                registry.AddLink(
                    (inner, c) => when(c)
                        ? ChainRegistry.CreateInstance(typeof(TLink), inner, c)
                        : inner,
                    order,
                    typeof(TLink).FullName,
                    duplicateBehavior);
            }

            container.RegisterFactoryAs<TInterface>(c =>
                (TInterface)holder.GetOrCreate(typeof(TInterface), typeof(TBase)).Build(c));
        }

        public static void RegisterChainLink<TInterface, TBase>(
            this IObjectContainer container,
            Func<TInterface, IObjectContainer, TInterface> factory,
            int order = 0,
            string name = null,
            DuplicateBehavior duplicateBehavior = DuplicateBehavior.Replace,
            Func<IObjectContainer, bool> when = null)
            where TInterface : class
            where TBase : class, TInterface
        {
            var holder = GetOrCreateHolder(container);
            var registry = holder.GetOrCreate(typeof(TInterface), typeof(TBase));
            registry.AddLink(
                (inner, c) => when == null || when(c)
                    ? factory((TInterface)inner, c)
                    : inner,
                order, name, duplicateBehavior);

            container.RegisterFactoryAs<TInterface>(c =>
                (TInterface)holder.GetOrCreate(typeof(TInterface), typeof(TBase)).Build(c));
        }

        public static void RegisterChainLinkIfNotExists<TInterface, TBase, TLink>(
            this IObjectContainer container,
            int order = 0,
            Func<IObjectContainer, bool> when = null)
            where TInterface : class
            where TBase : class, TInterface
            where TLink : class, TInterface
            => container.RegisterChainLink<TInterface, TBase, TLink>(order, DuplicateBehavior.Ignore, when);

        public static void AddChainLink<TInterface, TBase, TLink>(
            this IObjectContainer container,
            int order = 0,
            Func<IObjectContainer, bool> when = null)
            where TInterface : class
            where TBase : class, TInterface
            where TLink : class, TInterface
            => container.RegisterChainLink<TInterface, TBase, TLink>(order, DuplicateBehavior.Allow, when);

        public static void InheritChainLinks(
            this IObjectContainer child,
            IObjectContainer parent)
        {
            var parentHolder = TryGetHolder(parent);
            if (parentHolder == null) return;
            GetOrCreateHolder(child).InheritFrom(parentHolder);
        }

        static ChainRegistryHolder GetOrCreateHolder(IObjectContainer container)
        {
            if (container.IsRegistered<ChainRegistryHolder>())
                return container.Resolve<ChainRegistryHolder>();
            var holder = new ChainRegistryHolder();
            container.RegisterInstanceAs(holder);
            return holder;
        }

        static ChainRegistryHolder TryGetHolder(IObjectContainer container)
        {
            try { return container.IsRegistered<ChainRegistryHolder>() ? container.Resolve<ChainRegistryHolder>() : null; }
            catch { return null; }
        }
    }
}
