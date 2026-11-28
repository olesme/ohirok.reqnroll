using Reqnroll.Generator.Plugins;
using Reqnroll.Infrastructure;
using Reqnroll.UnitTestProvider;

[assembly: GeneratorPlugin(typeof(Ohirok.PluginChains.Common.SdkGeneratorPlugin))]

namespace Ohirok.PluginChains.Common
{
    /// <summary>
    /// Does nothing; it exists so Reqnroll's generator can load the SDK assembly as a plugin, first, from the package
    /// NuGet chose for the project (see the package's <c>buildTransitive</c> targets).
    /// <para>
    /// Every generator plugin built on the SDK carries a copy of the SDK next to it, and Reqnroll's generator keeps
    /// the first copy it loads for all of them (they must share one: the interceptor registry lives in the SDK). Loaded
    /// first, NuGet's choice, the highest version any installed plugin needs, is that copy; otherwise a plugin built on
    /// an older SDK would hand its older copy to all the others and their newer features would silently not run.
    /// </para>
    /// </summary>
    public sealed class SdkGeneratorPlugin : IGeneratorPlugin
    {
        public void Initialize(
            GeneratorPluginEvents generatorPluginEvents,
            GeneratorPluginParameters generatorPluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
        }
    }
}
