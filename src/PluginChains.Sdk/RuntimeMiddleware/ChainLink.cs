using Reqnroll.BoDi;

namespace Ohirok.RuntimeMiddleware
{
    internal sealed class ChainLink
    {
        public Type Type { get; }
        public string Name { get; }
        public Func<object, IObjectContainer, object> Factory { get; }
        public int Order { get; }

        public ChainLink(Type type, int order)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Order = order;
        }

        public ChainLink(Func<object, IObjectContainer, object> factory, int order, string name)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Name = name ?? string.Empty;
            Order = order;
        }
    }
}
