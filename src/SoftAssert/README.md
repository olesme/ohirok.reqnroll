# Ohirok.SoftAssert.ReqnrollPlugin

Soft-assert support for Reqnroll steps: capture a step failure without aborting the scenario, then fail the scenario at the end with the aggregated result.

This package is built on top of [Catchy](https://www.nuget.org/packages/Catchy.ReqnrollPlugin)'s soft-assertion engine. The implementation is centered around the custom hook lifecycle and soft-assert flush behavior.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md) — a plugin-authoring SDK and a set of Reqnroll plugins.

## Installation

```bash
dotnet add package Ohirok.SoftAssert.ReqnrollPlugin
```

This also installs `Catchy.ReqnrollPlugin`, which owns the scenario-scoped `StatefulAsserter`. Add it explicitly only if you want to pin or update its version independently.

## Usage

Annotate a step with `#softassert`:

```gherkin
Scenario: Multiple checks
    Given the page has loaded
    #softassert
    Then the header is correct
    #softassert
    Then the footer is correct
    Then the page is still usable
```

If a `#softassert`-tagged step fails, the exception is captured and execution continues to the next step. After the last step all captured failures are aggregated and the scenario is reported as failed — exactly as if you had used `StatefulAsserter.Soft` directly, but without touching your step code. `#softassert` works in scenarios, `Background` and `Rule` backgrounds.

## Flushing

Captured failures are flushed automatically after the last step, for every scenario — including failures recorded directly with `StatefulAsserter.Soft` in your own step code. If there is something to flush, a step named "the collected soft assertion failures are reported" appears at the end of the report and fails with the aggregated result; otherwise nothing is added. It is a regular step, so the scenario fails inside the test body and plugins such as Retry see it like any other failure.

The step is not added when the scenario has already failed or was skipped (Reqnroll would only skip it). Catchy's end-of-scenario hooks still cover that case.

If you replace `SoftState.FlushAction` (for example to only report instead of throwing), the flush step passes and the failures are only reported.

### Flushing manually (checkpoints)

To fail early, for example before a step with side effects, write a step that flushes:

```csharp
[When("I verify the collected checks")]
public Task Verify() => asserter.Soft.SoftState.FlushIfNeeded();
```

Do not mark such a step with `#softassert`: it is meant to fail, not to be captured. Describe it with a plain comment instead (`# flushes soft failures`; a space after `#` keeps it a comment, not a hashtag). If a flush is thrown from a `#softassert` step anyway, it is not captured again (it is recognised as a flush), so the scenario still fails at that step.

If you need to handle such steps separately in step hooks, use a hashtag of your own (for example `#flushsoft`) and a step hook; do not reuse `#softassert` for it.

An automatic "flush before this step" hashtag is intentionally not provided: a failure that blocks the flow fails the scenario by itself, and flushing at the end already covers the rest. If you have a case that custom steps and hooks cannot cover, please open an issue.

## Lifecycle hooks

```csharp
[Binding]
public class MySoftHooks
{
    [OnSoftFailure]
    public void OnFailure(SoftAssertContext ctx)
    {
        Console.WriteLine($"Captured: {ctx.Error.Message}");
    }

    [OnSoftFlush]
    public void OnFlush(SoftAssertContext ctx)
    {
        Console.WriteLine($"{ctx.Aggregate.InnerExceptions.Count} soft failures flushed.");
    }
}
```

`[OnSoftFailure]` fires once per captured step exception. `[OnSoftFlush]` fires when the collected failures are flushed: inside the flush step, on a manual flush, or in Catchy's end-of-scenario fallback. A flush happens once per scenario. Both accept the same tag/hashtag/exception-type filters as the rest of the Ohirok custom-hook infrastructure, e.g. `[OnSoftFailure("ex:TimeoutException")]`.

## Configuration

Settings are read through the shared plugin configuration (see the `PluginChains.Sdk` README) from the `softassert` section. All values have defaults, so you only set what you change.

| Key | Default | Meaning |
| --- | --- | --- |
| `softassert.generationInterceptorOrder` | `100` | Order of the generator interceptor (where the flush call is placed relative to other plugins' generated code). |
| `softassert.bindingInvokerOrder` | `-1000` | Position in the binding-invoker chain; lower is more outer. |

The tag name (`softassert`) and the flush step text are fixed. If you need a custom `SoftAssertTag` (or several alternative tags) or a different `FlushStepText`, please open an issue.

## Working with other plugins

Ohirok plugins do not know about each other; an absent plugin simply has no effect.

- **Retry.** The flush is a step inside the test body, so a scenario that fails only because of soft failures is retried by `@retry`. For `#retry` + `#softassert` on the same step, Retry runs first and SoftAssert captures only what is left after the retries (see [Ordering](#ordering)).
  Step and block retry react to thrown exceptions only, so a soft failure recorded directly in step code does not trigger them (test-level retry does). If a retried step records soft failures and then throws, discard the failed attempt's failures with Catchy's `Checkpoint()`/`Revert()` in Retry lifecycle hooks (see the Retry README).

### Ordering

Two settings decide how SoftAssert is placed relative to Retry. The defaults are right for the combination; change them only if you know why.

- **Runtime (`bindingInvokerOrder`).** SoftAssert wraps step execution from the outside, and Retry sits inside it. A failing `#retry` + `#softassert` step therefore throws into Retry first, which retries it; SoftAssert captures the exception only if the last attempt still fails. With the order reversed, SoftAssert would capture the first failure, the step would look successful to Retry, and nothing would be retried.
- **Generation (`generationInterceptorOrder`).** The flush call is generated after the last step. Retry's scenario-level `@retryblock` wraps all steps in a block that closes after the last step, so the flush has to be generated inside it, otherwise a soft-only failure would be thrown after the block has ended and would not be retried. A higher generation order puts the call closer to the steps.

## See also

- `src/PluginChains.Sdk/README.md` for the shared custom-hook, configuration and runtime composition model.
- `src/Reporting/README.md` for the report layer that surfaces soft-failure outcomes in HTML output.
- `src/StepHashtags/README.md` for step-local metadata that can drive soft-assert and retry behavior.
