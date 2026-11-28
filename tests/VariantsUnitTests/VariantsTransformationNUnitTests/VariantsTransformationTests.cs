using System.Globalization;
using Gherkin.Ast;
using NUnit.Framework;
using Ohirok.Variants;
using Reqnroll.Parser;

namespace VariantsTransformationNUnitTests;

/// <summary>
/// What the runner suites cannot show: inputs that fail generation, and inputs that must come back untouched.
/// The expansion itself is tested end to end in tests/VariantsTests on every runner.
/// </summary>
public class VariantsTransformationTests
{
    static ReqnrollDocument Parse(string feature)
        => new ReqnrollGherkinParser(new CultureInfo("en-US"))
            .Parse(new StringReader(feature), new ReqnrollDocumentLocation("Sample.feature"));

    [Test]
    public void Without_configured_keys_the_document_is_returned_as_is()
    {
        var document = Parse("Feature: F\n@Browser:Chrome\nScenario: S\n  Given a step\n");

        Assert.That(new VariantsTransformation([]).Transform(document), Is.SameAs(document));
    }

    [Test]
    public void Without_variant_tags_the_document_is_returned_as_is()
    {
        var document = Parse("Feature: F\n@issue:12\nScenario: S\n  Given a step\n");

        Assert.That(new VariantsTransformation(["Browser"]).Transform(document), Is.SameAs(document));
    }

    [Test]
    public void A_variant_tag_without_a_value_fails_with_its_location()
    {
        var document = Parse("Feature: F\n\n@Browser:\nScenario: S\n  Given a step\n");

        var ex = Assert.Throws<SemanticParserException>(() => new VariantsTransformation(["Browser"]).Transform(document));
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("@Browser:"));
            Assert.That(ex.Location?.Line, Is.EqualTo(3));
        });
    }

    [Test]
    public void An_outline_repeats_each_examples_block_per_value_with_the_column_added()
    {
        var document = Parse("Feature: F\n@Browser:Chrome @Browser:Firefox\nScenario Outline: S\n  Given a step <n>\n\n  @rows\n  Examples: Rows\n    | n |\n    | 1 |\n    | 2 |\n");

        var outline = (Scenario)new VariantsTransformation(["Browser"]).Transform(document).ReqnrollFeature.Children.Single();
        var chrome = outline.Examples.First();

        Assert.Multiple(() =>
        {
            Assert.That(outline.Tags, Is.Empty, "the variant tags are consumed");
            Assert.That(outline.Examples.Select(e => e.Name), Is.EqualTo(new[] { "Rows (Browser: Chrome)", "Rows (Browser: Firefox)" }));
            Assert.That(chrome.Tags.Select(t => t.Name), Is.EqualTo(new[] { "@rows", "@Browser:Chrome" }));
            Assert.That(chrome.TableHeader.Cells.Select(c => c.Value), Is.EqualTo(new[] { "n", "Browser" }));
            Assert.That(chrome.TableBody.Select(r => string.Join(",", r.Cells.Select(c => c.Value))), Is.EqualTo(new[] { "1,Chrome", "2,Chrome" }));
        });
    }

    [Test]
    public void Scenario_becomes_an_outline_with_one_examples_block_per_value()
    {
        var document = Parse("Feature: F\n@smoke @Browser:Chrome @Browser:Firefox\nScenario: S\n  Given a step\n");

        var scenario = new VariantsTransformation(["Browser"]).Transform(document).ReqnrollFeature.Children.Single();

        Assert.That(scenario, Is.InstanceOf<ScenarioOutline>());
        var outline = (ScenarioOutline)scenario;
        Assert.Multiple(() =>
        {
            Assert.That(outline.Tags.Select(t => t.Name), Is.EqualTo(new[] { "@smoke" }));
            Assert.That(outline.Examples.Select(e => e.Name), Is.EqualTo(new[] { "Browser: Chrome", "Browser: Firefox" }));
            Assert.That(outline.Examples.Select(e => e.Tags.Single().Name), Is.EqualTo(new[] { "@Browser:Chrome", "@Browser:Firefox" }));
            Assert.That(outline.Examples.Select(e => e.TableBody.Single().Cells.Single().Value), Is.EqualTo(new[] { "Chrome", "Firefox" }));
        });
    }
}
