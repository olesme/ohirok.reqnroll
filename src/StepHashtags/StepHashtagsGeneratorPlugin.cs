using Ohirok.PluginChains.Common;
using Ohirok.GenerationInterceptor.Core;
using Ohirok.StepHashtags.ReqnrollPlugin;
using Reqnroll.Generator.Plugins;
using Reqnroll.Infrastructure;
using Reqnroll.UnitTestProvider;

[assembly: GeneratorPlugin(typeof(StepHashtagsGeneratorPlugin))]

namespace Ohirok.StepHashtags.ReqnrollPlugin
{
    public class StepHashtagsGeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(
            GeneratorPluginEvents generatorPluginEvents,
            GeneratorPluginParameters generatorPluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            PluginConfigProvider.Initialize(generatorPluginEvents);

            generatorPluginEvents.CustomizeDependencies += (_, args) =>
            {
                if (!StepHashtagsConfig.Enabled) return;
                InterceptorRegistry.Register(args.ObjectContainer, new StepHashtagInterceptor());
            };
        }
    }
}
