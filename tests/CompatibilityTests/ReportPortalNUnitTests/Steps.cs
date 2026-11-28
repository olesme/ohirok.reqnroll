using System.Reflection;
using NUnit.Framework;
using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.BoDi;

namespace ReportPortalNUnitTests;

[Binding]
public class Steps(IObjectContainer container)
{
    private static int _attempts;

    [Then("the binding invoker chain ends with SafeBindingInvoker under the Ohirok links")]
    public void ChainEndsWithReportPortal()
    {
        var chain = ChainOf(container.Resolve<IAsyncBindingInvoker>());

        Assert.Multiple(() =>
        {
            Assert.That(chain[^1], Is.EqualTo("SafeBindingInvoker"), string.Join(" -> ", chain));
            Assert.That(chain, Is.SupersetOf(new[]
            {
                "RetryBindingInvoker", "IgnoreStepBindingInvoker", "XFailBindingInvoker", "SoftAssertBindingInvoker",
            }), string.Join(" -> ", chain));
        });
    }

    [Given("a step that fails on its first attempt")]
    public void FailsOnce()
    {
        if (Interlocked.Increment(ref _attempts) == 1) throw new InvalidOperationException("first attempt fails");
    }

    [Given("a step that throws InvalidOperationException")]
    public void Throws() => throw new InvalidOperationException("expected failure");

    [Then("the {word} hook has run")]
    public void HookHasRun(string hook)
    {
        lock (HookProbe.Fired) Assert.That(HookProbe.Fired, Does.Contain(hook));
    }

    // Each link keeps the next one in a field; walk them down to the innermost invoker.
    private static string[] ChainOf(IAsyncBindingInvoker invoker)
    {
        var names = new List<string>();
        object? current = invoker;
        while (current != null)
        {
            names.Add(current.GetType().Name);
            current = current.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(f => typeof(IAsyncBindingInvoker).IsAssignableFrom(f.FieldType))
                ?.GetValue(current);
        }
        return [.. names];
    }
}
