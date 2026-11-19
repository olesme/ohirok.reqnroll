using Ohirok.CustomBindings;
using Ohirok.Reporting.Config;
using Ohirok.Reporting.Model;

namespace Ohirok.Reporting.Parsing
{
    /// <summary>
    /// Matches a scenario against the configured reporting.filters groups, reusing Ohirok.CustomBindings.FilterHelper
    /// verbatim (same tag/hashtag-state construction as Retry/SoftAssert's HooksExecutor.BuildStates). Every
    /// configured group always yields exactly one ReportFilterMatch per scenario - either the first option whose
    /// pattern matches, or the group's implicit "Other" bucket when none do.
    /// </summary>
    public static class ReportFilterMatcher
    {
        public static List<ReportFilterMatch> Match(ReportScenario scenario, List<ReportFilterGroup> groups)
        {
            var result = new List<ReportFilterMatch>();
            if (groups == null || groups.Count == 0)
                return result;

            var states = FilterHelper.GenerateStates(["", "@"], [.. scenario.Tags])
                .Concat(FilterHelper.GenerateStates(["#"], [.. scenario.Hashtags]))
                .ToArray();

            foreach (var group in groups)
            {
                var option = group.Options.FirstOrDefault(o => states.Any(s => FilterHelper.Matches(s, o.Match)));

                result.Add(option != null
                    ? new ReportFilterMatch { Group = group.Name, Label = option.Label, Bg = option.Bg, Fg = option.Fg }
                    : new ReportFilterMatch { Group = group.Name, Label = group.OtherLabel, Bg = group.OtherBg, Fg = group.OtherFg });
            }

            return result;
        }
    }
}
