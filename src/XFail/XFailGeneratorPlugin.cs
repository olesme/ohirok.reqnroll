using Ohirok.GenerationInterceptor.Core;
using Ohirok.PluginChains.Common;
using Reqnroll.Generator.Plugins;
using Reqnroll.Infrastructure;
using Reqnroll.UnitTestProvider;

[assembly: GeneratorPlugin(typeof(Ohirok.XFail.ReqnrollPlugin.XFailGeneratorPlugin))]

namespace Ohirok.XFail.ReqnrollPlugin
{
    /// <summary>Adds the scenario-level <c>@xfail</c> / <c>@afail</c> wrapper; the step hashtags need no generated code.</summary>
    public sealed class XFailGeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(
            GeneratorPluginEvents generatorPluginEvents,
            GeneratorPluginParameters generatorPluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            PluginConfigProvider.Initialize(generatorPluginEvents);
            generatorPluginEvents.CustomizeDependencies += (_, args) =>
                InterceptorRegistry.Register(args.ObjectContainer, new XFailGeneratorInterceptor());
        }
    }
}
