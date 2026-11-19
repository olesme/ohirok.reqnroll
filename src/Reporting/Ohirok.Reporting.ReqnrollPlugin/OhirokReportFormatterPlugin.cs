using Ohirok.PluginChains.Common;
using Ohirok.Reporting.ReqnrollPlugin;
using Reqnroll.Formatters;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(OhirokReportFormatterPlugin))]

namespace Ohirok.Reporting.ReqnrollPlugin
{
    public sealed class OhirokReportFormatterPlugin : IRuntimePlugin
    {
        public void Initialize(
            RuntimePluginEvents runtimePluginEvents,
            RuntimePluginParameters runtimePluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            PluginConfigProvider.Initialize(runtimePluginEvents);

            runtimePluginEvents.RegisterGlobalDependencies += (_, args) =>
                args.ObjectContainer.RegisterTypeAs<OhirokReportFormatter, ICucumberMessageFormatter>("ohirokReport");
        }
    }
}
