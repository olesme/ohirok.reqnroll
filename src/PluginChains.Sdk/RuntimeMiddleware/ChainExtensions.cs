using System.Runtime.CompilerServices;
using Reqnroll.BoDi;

namespace Ohirok.RuntimeMiddleware
{
    /// <summary>
    /// Chains of decorators ("links") around one Reqnroll service, so several plugins can each wrap it instead of the
    /// last registration replacing the others.
    /// <para>
    /// The chain lives in the container it is registered in. Its innermost object is what the <b>parent</b> container
    /// resolves for <c>TInterface</c> when the parent has a registration for it, otherwise a <c>TBase</c> created by
    /// the container. So register links one level below the container the service is registered in: Reqnroll
    /// registers <c>IAsyncBindingInvoker</c> in the global container and resolves it from the test-thread container,
    /// so its links go into the test-thread container (<c>CustomizeTestThreadDependencies</c>). The chain then wraps
    /// whatever any plugin registered globally, whatever order the plugins were loaded in.
    /// </para>
    /// </summary>
    public static class ChainExtensions
    {
        // One holder per container, looked up by the container itself: BoDi's IsRegistered/Resolve also search the
        // parents, which would make a child container share (and change) its parent's chains.
        static readonly ConditionalWeakTable<IObjectContainer, ChainRegistryHolder> Holders = new();

        public static void RegisterChainLink<TInterface, TBase, TLink>(
            this IObjectContainer container,
            int order = 0,
            DuplicateBehavior duplicateBehavior = DuplicateBehavior.Replace,
            Func<IObjectContainer, bool> when = null)
            where TInterface : class
            where TBase : class, TInterface
            where TLink : class, TInterface
        {
            var registry = GetRegistry<TInterface, TBase>(container);

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

            container.RegisterFactoryAs<TInterface>(c => (TInterface)registry.Build(c));
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
            var registry = GetRegistry<TInterface, TBase>(container);
            registry.AddLink(
                (inner, c) => when == null || when(c)
                    ? factory((TInterface)inner, c)
                    : inner,
                order, name, duplicateBehavior);

            container.RegisterFactoryAs<TInterface>(c => (TInterface)registry.Build(c));
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

        static ChainRegistry GetRegistry<TInterface, TBase>(IObjectContainer container)
            => Holders.GetValue(container, _ => new ChainRegistryHolder()).GetOrCreate(typeof(TInterface), typeof(TBase));
    }
}
