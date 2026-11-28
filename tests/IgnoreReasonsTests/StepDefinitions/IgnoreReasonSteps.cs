using System.Reflection;
using System.Text.RegularExpressions;
using Reqnroll;

namespace IgnoreReasonsTests.StepDefinitions;

[Binding]
public class IgnoreReasonSteps
{
    [When(@"this step must not run(.*)")]
    public void MustNotRun(string _) => throw new InvalidOperationException("A scenario that should be skipped ran.");

    [Then("this scenario ran")]
    public void Ran() { }

    [Then(@"the test ""(.*)"" is skipped with the reason ""(.*)""")]
    public void SkippedWithReason(string scenario, string reason)
    {
        var skips = SkipTexts(TestMethod(scenario));
        if (!skips.Contains(reason))
            throw new InvalidOperationException($"'{scenario}' is skipped with [{string.Join(", ", skips)}], expected '{reason}'.");
    }

    [Then(@"the test ""(.*)"" is not skipped")]
    public void NotSkipped(string scenario)
    {
        var skips = SkipTexts(TestMethod(scenario));
        if (skips.Length > 0)
            throw new InvalidOperationException($"'{scenario}' is skipped with [{string.Join(", ", skips)}].");
    }

    /// <summary>The generated test method of a scenario: Reqnroll names it after the title, without spaces.</summary>
    static MethodInfo TestMethod(string scenario)
    {
        var key = Key(scenario);
        return typeof(IgnoreReasonSteps).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SingleOrDefault(m => Key(m.Name) == key)
            ?? throw new InvalidOperationException($"No generated test method for '{scenario}'.");
    }

    /// <summary>The texts the runner skips the test with: the message of an [Ignore] or [Skip] attribute, or the Skip of
    /// xUnit's [Fact] / [Theory].</summary>
    static string[] SkipTexts(MethodInfo method) => method.GetCustomAttributesData()
        .SelectMany(a =>
            a.AttributeType.Name is "IgnoreAttribute" or "SkipAttribute"
                ? a.ConstructorArguments.Select(c => c.Value as string ?? "")
                : a.NamedArguments.Where(n => n.MemberName == "Skip").Select(n => n.TypedValue.Value as string ?? ""))
        .ToArray();

    static string Key(string s) => Regex.Replace(s, "[^A-Za-z0-9]", "").ToLowerInvariant();
}
