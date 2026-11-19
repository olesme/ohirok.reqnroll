using Ohirok.GenerationInterceptor.Core;
using Ohirok.PluginChains.Common;
using Reqnroll.Generator.Plugins;
using Reqnroll.Infrastructure;
using Reqnroll.UnitTestProvider;

[assembly: GeneratorPlugin(typeof(Ohirok.SoftAssert.ReqnrollPlugin.SoftAssertGeneratorPlugin))]

namespace Ohirok.SoftAssert.ReqnrollPlugin
{
    public sealed class SoftAssertGeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(
            GeneratorPluginEvents generatorPluginEvents,
            GeneratorPluginParameters generatorPluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            PluginConfigProvider.Initialize(generatorPluginEvents);

            generatorPluginEvents.CustomizeDependencies += (_, args) =>
            {
                InterceptorRegistry.Register(args.ObjectContainer, new SoftAssertGeneratorInterceptor());
            };
        }
    }
}
