using System.Runtime.CompilerServices;
using Ohirok.GenerationInterceptor.Contexts;
using Ohirok.GenerationInterceptor.Core;
using Ohirok.PluginChains.Common;
using Reqnroll.Generator.Plugins;
using Reqnroll.Infrastructure;
using Reqnroll.UnitTestProvider;

[assembly: GeneratorPlugin(typeof(Ohirok.IgnoreReasons.IgnoreReasonsGeneratorPlugin))]

namespace Ohirok.IgnoreReasons
{
    public sealed class IgnoreReasonsGeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(
            GeneratorPluginEvents generatorPluginEvents,
            GeneratorPluginParameters generatorPluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            PluginConfigProvider.Initialize(generatorPluginEvents);

            generatorPluginEvents.CustomizeDependencies += (_, args) =>
                InterceptorRegistry.Register(args.ObjectContainer, new IgnoreReasonsInterceptor());
        }
    }

    internal sealed class IgnoreReasonsInterceptor : Ohirok.GenerationInterceptor.Core.GenerationInterceptor
    {
        private static readonly ConditionalWeakTable<IgnoreReasonsSection, IgnoreReasons> _byConfig = new();

        private static IgnoreReasons Current =>
            _byConfig.GetValue(IgnoreReasonsConfig.Section, section => new IgnoreReasons(section.Tags));

        public override int Order => IgnoreReasonsConfig.Section.GenerationInterceptorOrder;

        public override Reqnroll.Parser.ReqnrollDocument TransformDocument(Reqnroll.Parser.ReqnrollDocument document)
            => Current.Transform(document);

        public override void AfterScenarioMethod(in ScenarioMethodContext ctx)
        {
            var reason = Current.ReasonFor(ctx.ScenarioTags, ctx.RuleTags, ctx.Feature?.Tags);
            if (reason != null) IgnoreReasons.SetReason(ctx.Method, reason);
        }
    }
}
