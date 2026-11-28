using System.Globalization;
using Gherkin.Ast;
using NUnit.Framework;
using Ohirok.AutoTags;
using Reqnroll.Parser;

namespace AutoTagsTransformationNUnitTests;

/// <summary>
/// What the runner suites cannot show: configuration that fails generation, and inputs that must come back untouched.
/// The tagging itself is tested end to end in tests/AutoTagsTests on every runner.
/// </summary>
public class AutoTagsTransformationTests
{
    static ReqnrollDocument Parse(string feature)
        => new ReqnrollGherkinParser(new CultureInfo("en-US"))
            .Parse(new StringReader(feature), new ReqnrollDocumentLocation("Sample.feature"));

    static AutoTagsTransformation For(string tag, params string[] patterns) => new(new Dictionary<string, string[]> { [tag] = patterns });

    [Test]
    public void Without_configured_tags_the_document_is_returned_as_is()
    {
        var document = Parse("Feature: F\nScenario: S\n  Given the mock server returns 1\n");

        Assert.That(new AutoTagsTransformation(null).Transform(document), Is.SameAs(document));
    }

    [Test]
    public void When_no_step_matches_the_document_is_returned_as_is()
    {
        var document = Parse("Feature: F\nScenario: S\n  Given a step\n");

        Assert.That(For("MockServer", "^the mock server").Transform(document), Is.SameAs(document));
    }

    [Test]
    public void An_invalid_pattern_fails_naming_the_pattern_and_its_tag()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => For("MockServer", "the (mock"));

        Assert.That(ex!.Message, Does.Contain("'the (mock'").And.Contain("@MockServer"));
    }

    [Test]
    public void A_pattern_matches_the_step_text_without_its_keyword()
    {
        var document = Parse("Feature: F\nScenario: S\n  Given the mock server returns 1\n");

        var scenario = (Scenario)For("@MockServer", "^the mock server").Transform(document).ReqnrollFeature.Children.Single();

        Assert.That(scenario.Tags.Select(t => t.Name), Is.EqualTo(new[] { "@MockServer" }));
    }

    [Test]
    public void An_outline_stays_an_outline_with_its_examples()
    {
        var document = Parse("Feature: F\nScenario Outline: S\n  Given the mock server returns <n>\n\n  Examples:\n    | n |\n    | 1 |\n");

        var outline = For("MockServer", "^the mock server").Transform(document).ReqnrollFeature.Children.Single();

        Assert.Multiple(() =>
        {
            Assert.That(outline, Is.InstanceOf<ScenarioOutline>());
            Assert.That(((Scenario)outline).Examples.Single().TableBody.Count(), Is.EqualTo(1));
        });
    }
}
