# Contributing

## Prerequisites

- .NET SDK 10 or newer, to build the repo and run its tests. The plugin packages target `netstandard2.0` (some also `net48`); only the `Ohirok.Reporting.Combine` tool needs the .NET 10 runtime.
- PowerShell 7 (`pwsh`) for the scripts under `scripts/`.

## Everyday commands

| Command | What it does |
| --- | --- |
| `dotnet build Ohirok.Reqnroll.slnx -c Release` | Builds every package, test project and example. |
| `./scripts/run-tests.ps1` | The test gate CI runs: every project under `tests/`, each on its own test host. |
| `./scripts/run-tests.ps1 -Filter 'Retry*'` | Only the matching projects. |
| `./scripts/test-packages.ps1` | Packs every `src/` project and runs the packaging smoke tests against the packages. |
| `./scripts/sync-upstream.ps1 -Ref v3.3.5 -ShowDiff` | Shows what changed upstream in the Reqnroll generator files the SDK shadow-copies. |
| `./examples/ReportShowcase/showcase.ps1` | Runs the showcase test projects over simulated builds and renders every report into `examples/ReportShowcase/output/`. |

## Report assets

The report's `report.js`, `report.css` and themes live readable in `src/Reporting/Ohirok.Reporting.Core/Rendering/Assets/`; edit those. Release builds (and so the packages) embed minified copies made by `build/AssetMinifier` (NUglify, local names shortened) under the same resource names; Debug builds embed the readable files. `-p:OhirokMinifyAssets=false` turns minification off in Release too. The icons in `report.js` are a Lucide subset written by `build/lucide-icons.js` (usage in its header); bundled fonts live in `Rendering/Assets/Fonts/` with their licenses.

## How the tests are organised

**Runner parity.** Behaviour a test framework can influence (generated test methods, retries, hooks, attachments)
is tested on NUnit, MSTest, xUnit v3 and TUnit from one set of scenarios. For example `tests/RetryTests/Features/`
holds the Retry scenarios, and each `Retry<Runner>Tests` project only declares its test-framework packages and
links those files through `RetryTests.Runner.props`. Add a scenario once and it runs on every runner. Pure logic
that no runner can affect (history storage, report combining) is tested on NUnit only.

**Expected failures.** A scenario whose correct outcome is a failed step is written with a step hashtag
(`#xfail:SomeException`) inside a normal project, so it is green exactly when it fails the expected way.

**Negative projects.** Some failures cannot be expressed that way, for example a scenario that tests XFail itself,
or one that must fail after its last step (retry exhaustion, soft-assert flush). Those scenarios live in a
`*.Negative` project, where every test must fail. Next to the project (or in its parent folder, shared by the
runner twins) an `expected-failures.json` records why each scenario must fail:

```json
[
  { "scenario": "FAIL_Max_Attempts", "error": "Always fails", "attempts": "3", "log": "Test failed after 3 attempts" }
]
```

`scenario` is a name prefix (punctuation and case are ignored), `error` a regex for the failing step's
exception, `attempts` the number of test-case attempts (`"3"` or a range `"1-4"`), and `log` / `noLog` regexes over
the text the scenario wrote. `run-tests.ps1` checks them against the Cucumber messages Reqnroll writes, so the
check is the same on every runner. A negative scenario that passes, or fails for a different reason, fails the
gate.

**Expected tests.** When a positive project's correctness is in which tests it generates (Variants does), an
`expected-tests.json` next to it (or in its parent folder) lists, per scenario name prefix, the tag set of each test:

```json
[
  { "scenario": "One test per browser", "tags": [ [ "@Browser:Chrome" ], [ "@Browser:Firefox" ] ] }
]
```

`run-tests.ps1` checks it against the Cucumber messages: every pickle ran once, there are as many pickles as tests
(no two tests share a pickle), every pickle matches a rule, and each rule's tag sets are exactly the listed ones.

**Packaging smoke tests.** `tests/PackagingSmokeTests/` consumes the packages only through `PackageReference`, the
way a user does. Building any of those projects packs a fresh local feed when `src/` changed. Close Visual Studio
(or unload those projects) while running `test-packages.ps1`, because a design-time build regenerates their
code-behind in the background.

## Releasing

Packages are published by the **Publish to NuGet** workflow (Actions → Run workflow), through NuGet Trusted Publishing.

- **What.** `packages` is `all` or a comma-separated list of groups (the folders under `src/`: `PluginChains.Sdk`, `Retry`, `XFail`, `SoftAssert`, `StepHashtags`, `Variants`, `PersistentCleanup`, `Reporting`) and package ids. The in-repo packages a picked one depends on are always published with it, so every published package depends on versions that exist.
- **Version.** All packages share one release number, read from NuGet.org only (no tags, no tools): leave `version` empty for the next one, the highest version of any package + 1. Packages left out of a release keep their last version; nothing is republished without a reason.
- **Adding to a release.** Give an existing number to publish the picked packages that do not have it yet, for example a new package for the current release. Packages that already have it are skipped, and the run stops if their code differs from the commit that version was built from (NuGet.org records it in each package), because the new packages would then depend on what is not published. A number below the current release is refused.
- **A failed run.** Nothing is pushed before the build, the test gate and the packaging smoke tests pass, so such a failure takes no number. If pushing itself fails half-way, run again with that version and the same packages: the rest are pushed, the ones already there are skipped.
- Every run builds and tests the whole repository and runs the packaging smoke tests against all packages of that commit, whatever it publishes.
- **GitHub Release.** After publishing, the run creates the GitHub Release `vX.Y.Z` with the packages it pushed and the changes since the previous release, or appends the packages to it when they join an existing release. It is a page for people; nothing reads it or its tag back. Creating a release by hand does not publish anything.

## Pull requests

- Keep each package's README in sync with its behaviour; the README is also the package page on nuget.org.
- Run the checklist in the pull request template.
