# Packaging smoke tests

These projects verify that the plugins work **as published NuGet packages**, which is a different
question from whether the code works. Every other test project in this repository reaches the plugins
through `ProjectReference`, which takes the assemblies straight from the build output and bypasses
packaging entirely. That is convenient, and it hides an entire class of defect.

Each project here therefore uses **only `PackageReference`**, restoring from a local feed built out of
real `.nupkg` files. There is no `ProjectReference` anywhere, and adding one would defeat the purpose.

## Why this exists

Three defects only surfaced here, and each left every existing test green:

1. **A generator plugin reached as a transitive dependency never registered.** NuGet keeps build
   assets private by default (`PrivateAssets` defaults to `contentfiles;analyzers;build`), so a
   project installing only `Ohirok.XFail.ReqnrollPlugin` never imported
   `Ohirok.StepHashtags.ReqnrollPlugin`'s `build\*.props`. Step-level hashtags such as
   `#xfail:SomeException` were silently ignored - no error, just no interception. Fixed by marking the
   dependency `PrivateAssets="none"`, which is what Reqnroll's own first-party plugins do.

2. **Several fluent interceptors shared one scheduler slot.** `FluentInterceptor` stored its
   `InjectionScheduler` in `CustomData` under a single shared key, so each fluent plugin overwrote the
   previous one's scheduler and only the highest-ordered plugin's step injections survived. Fixed by
   scoping the key to the concrete interceptor type.

3. **A plugin built on an older SDK could switch off newer SDK features.** Every SDK-based generator
   plugin carries its own copy of the SDK, and Reqnroll's generator keeps the first one it loads. With
   Catchy.ReqnrollPlugin 0.0.5 (SDK 0.0.0) loaded first, Variants generated nothing, without an error.
   Fixed by the SDK package's `buildTransitive` targets, which load the SDK NuGet chose first.

None of these is reachable from a `ProjectReference` test; the second and third also need more than one
plugin installed at once - hence `AllPluginsNUnitSmoke` and `MixedSdkNUnitSmoke`.

## Layout

| Project | Covers |
| --- | --- |
| `RetryNUnitSmoke`, `RetryMsTestSmoke`, `RetryXunitSmoke`, `RetryTUnitSmoke` | Retry across all four runners |
| `XFailNUnitSmoke`, `XFailMsTestSmoke`, `XFailXunitSmoke`, `XFailTUnitSmoke` | XFail across all four runners, including the transitive StepHashtags generator plugin |
| `StepHashtagsNUnitSmoke` | Step hashtags standalone |
| `SoftAssertNUnitSmoke` | SoftAssert, including its external Catchy dependency arriving transitively |
| `AllPluginsNUnitSmoke` | StepHashtags, XFail, Retry, SoftAssert and Variants installed together, with overlapping shared dependencies; also checks that the generator-only Variants assembly stays out of the output |
| `MixedSdkNUnitSmoke` | Variants next to a plugin built on an older SDK (Catchy.ReqnrollPlugin 0.0.5), loaded first |

`Directory.Build.props` deliberately **shadows** the repository-root one and does not import it, so
these projects are evaluated without the repository's own build settings - the same way a third-party
consumer's project would be.

The projects have their own solution, `tests/PackagingSmokeTests/PackagingSmokeTests.slnx`, and are
intentionally **not** part of `Ohirok.Reqnroll.slnx`: they restore from the local feed, so a
whole-solution restore of the main solution must not depend on packing first.

## Running locally

The one-command entry point packs every `src/` project, builds the smoke solution against the
packages, runs each project on its own test host, and then runs the generated-code check:

```bash
pwsh ./scripts/test-packages.ps1
```

It accepts `-Configuration` (default `Release`), `-Version` (default `9999.0.0-smoke`) and `-FeedSource`, a
folder of already-packed `.nupkg` files to test instead of packing `src/` (the publish workflow passes
its release artifacts here, so the exact files about to be pushed are the ones tested).

The feed itself lives in `tests/PackagingSmokeTests/.localfeed` and is produced by
`pack-local-feed.ps1`, which packs **every** project under `src/`, so a newly added package is covered
without editing the script. The feed is wiped before each pack, and cached copies of the smoke version
are removed from the global NuGet cache.

You rarely need to call it yourself: building or restoring any smoke project (from the IDE,
`dotnet build` or `dotnet test`) runs `pack-local-feed.ps1 -IfStale` first, through
`Directory.Build.targets`, which repacks only when the feed is missing or older than the sources under
`src/`. Pass `-p:OhirokSkipSmokePack=true` to skip that step when the feed is supplied from outside.

```bash
dotnet test tests/PackagingSmokeTests/XFailNUnitSmoke/XFailNUnitSmoke.csproj
```

xUnit v3 and TUnit projects run on Microsoft.Testing.Platform, so build them and run the produced
executable directly instead:

```bash
dotnet build tests/PackagingSmokeTests/XFailTUnitSmoke/XFailTUnitSmoke.csproj
./tests/PackagingSmokeTests/XFailTUnitSmoke/bin/Debug/net10.0/XFailTUnitSmoke.exe
```

## Checking the generated code too

```bash
pwsh tests/PackagingSmokeTests/verify-generated-code.ps1
```

A green test run does not always prove a plugin participated. SoftAssert's only observable runtime
effect is capturing a failure, so a scenario in which nothing fails passes identically whether the
plugin ran or not - which is precisely how defect 2 above stayed hidden. The generated code is
unambiguous, because each injected marker call is only ever emitted by that plugin's own generator
interceptor. This script asserts those calls are present.
