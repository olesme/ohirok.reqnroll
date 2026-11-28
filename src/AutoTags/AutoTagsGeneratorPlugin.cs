using Ohirok.GenerationInterceptor.Core;
using Ohirok.PluginChains.Common;
using Reqnroll.Generator.Plugins;
using Reqnroll.Infrastructure;
using Reqnroll.UnitTestProvider;

[assembly: GeneratorPlugin(typeof(Ohirok.AutoTags.AutoTagsGeneratorPlugin))]

namespace Ohirok.AutoTags
{
    public sealed class AutoTagsGeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(
            GeneratorPluginEvents generatorPluginEvents,
            GeneratorPluginParameters generatorPluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            PluginConfigProvider.Initialize(generatorPluginEvents);

            generatorPluginEvents.CustomizeDependencies += (_, args) =>
                InterceptorRegistry.Register(args.ObjectContainer, new AutoTagsInterceptor());
        }
    }

    internal sealed class AutoTagsInterceptor : Ohirok.GenerationInterceptor.Core.GenerationInterceptor
    {
        public override int Order => AutoTagsConfig.Section.GenerationInterceptorOrder;

        public override Reqnroll.Parser.ReqnrollDocument TransformDocument(Reqnroll.Parser.ReqnrollDocument document)
            => AutoTagsConfig.Transformation.Transform(document);
    }
}
