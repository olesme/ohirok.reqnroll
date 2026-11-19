using Ohirok.GenerationInterceptor.Core;
using Ohirok.PluginChains.Common;
using Reqnroll.Generator.Plugins;
using Reqnroll.Infrastructure;
using Reqnroll.UnitTestProvider;

[assembly: GeneratorPlugin(typeof(Ohirok.Retry.ReqnrollPlugin.RetryGeneratorPlugin))]

namespace Ohirok.Retry.ReqnrollPlugin
{
    public sealed class RetryGeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(
            GeneratorPluginEvents generatorPluginEvents,
            GeneratorPluginParameters generatorPluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            PluginConfigProvider.Initialize(generatorPluginEvents);

            generatorPluginEvents.CustomizeDependencies += (_, args) =>
            {
                InterceptorRegistry.Register(args.ObjectContainer, new RetryGeneratorInterceptor());
            };
        }
    }
}
