using Ohirok.GenerationInterceptor.Core;
using Ohirok.GenerationInterceptor.Generation;
using Reqnroll.BoDi;
using Reqnroll.Generator.UnitTestConverter;

namespace Ohirok.GenerationInterceptor.Infrastructure
{

    public static class InterceptorInfrastructure
    {
        public static void EnsureRegistered(IObjectContainer container)
        {
            if (container.IsRegistered<UnitTestFeatureGeneratorInt>()) return;

            // New project container — reset the global interceptor list so interceptors
            // from previous project builds don't accumulate (generator runs in a shared process).
            InterceptorRegistry.Clear();

            // Register under the concrete type as well (mirrors Reqnroll's own convention for
            // UnitTestFeatureGenerator) so IsRegistered<UnitTestFeatureGeneratorInt>() works.
            container.RegisterTypeAs<UnitTestFeatureGeneratorInt, UnitTestFeatureGeneratorInt>();
            container.RegisterTypeAs<UnitTestFeatureGeneratorInt, IFeatureGenerator>();
            container.RegisterTypeAs<UnitTestFeatureGeneratorProviderInt, IFeatureGeneratorProvider>("default");
        }
    }
}
