using Ohirok.GenerationInterceptor.Core;
using Ohirok.PluginChains.Common;
using Reqnroll.Generator.Plugins;
using Reqnroll.Infrastructure;
using Reqnroll.UnitTestProvider;

[assembly: GeneratorPlugin(typeof(Ohirok.Variants.VariantsGeneratorPlugin))]

namespace Ohirok.Variants
{
    public sealed class VariantsGeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(
            GeneratorPluginEvents generatorPluginEvents,
            GeneratorPluginParameters generatorPluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            PluginConfigProvider.Initialize(generatorPluginEvents);

            generatorPluginEvents.CustomizeDependencies += (_, args) =>
                InterceptorRegistry.Register(args.ObjectContainer, new VariantsInterceptor());
        }
    }

    internal sealed class VariantsInterceptor : Ohirok.GenerationInterceptor.Core.GenerationInterceptor
    {
        public override int Order => VariantsConfig.Section.GenerationInterceptorOrder;

        public override Reqnroll.Parser.ReqnrollDocument TransformDocument(Reqnroll.Parser.ReqnrollDocument document)
            => new VariantsTransformation(VariantsConfig.Section.Keys).Transform(document);
    }
}
