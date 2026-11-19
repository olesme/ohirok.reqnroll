<#
.SYNOPSIS
  Asserts that each smoke project's generated code really contains every plugin's injected call.

.DESCRIPTION
  Run this after building the smoke projects. It exists because a passing test run is not always
  enough to prove a plugin participated.

  A plugin can be completely inert and still leave a green test. SoftAssert is the clearest case: its
  only observable runtime effect is capturing a failure, so a scenario where nothing fails passes
  identically whether the plugin ran or not. The generated code is where participation is unambiguous,
  because the injected marker call is only ever emitted by the plugin's own generator interceptor.

  Two real defects were found exactly this way:
    - a generator plugin reached as a transitive dependency never registered, because NuGet keeps
      build assets private by default (step-level hashtags silently did nothing);
    - several fluent interceptors shared one scheduler slot, so with more than one plugin installed
      only the highest-ordered plugin's injections survived.

  Both left tests green. Neither would be caught without checking the generated output.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$smokeRoot = $PSScriptRoot

# Marker -> the plugin whose generator interceptor is the only thing that emits it.
# XFail's step-level feature emits no code of its own (it consumes the hashtag StepHashtags emits and
# acts at runtime), so the XFail smoke projects verify it by asserting real interception behaviour; only
# its scenario tag (@xfail) has a marker of XFail's own.
$expectations = @(
    @{ Project = 'AllPluginsNUnitSmoke'; File = 'Features/AllPluginsSmoke.feature.cs'; Markers = @(
        'Ohirok.Retry.Manager.WrapTestMethodAsync'
        'Ohirok.StepHashtags.Manager.Set'
        'Ohirok.SoftAssert.Manager.SetSoftStepMarker'
    )}
    @{ Project = 'StepHashtagsNUnitSmoke'; File = 'Features/HashtagSmoke.feature.cs'; Markers = @(
        'Ohirok.StepHashtags.Manager.Set'
    )}
    @{ Project = 'SoftAssertNUnitSmoke'; File = 'Features/SoftAssertSmoke.feature.cs'; Markers = @(
        'Ohirok.SoftAssert.Manager.SetSoftStepMarker'
    )}
)

foreach ($runner in @('NUnit', 'MsTest', 'Xunit', 'TUnit')) {
    $expectations += @{ Project = "Retry${runner}Smoke"; File = 'Features/RetrySmoke.feature.cs'; Markers = @(
        'Ohirok.Retry.Manager.WrapTestMethodAsync'
    )}
    # The transitive-generator-plugin regression gate: StepHashtags arrives only through XFail here,
    # so this call is present if and only if its build props flowed and its generator plugin ran.
    $expectations += @{ Project = "XFail${runner}Smoke"; File = 'Features/XFailSmoke.feature.cs'; Markers = @(
        'Ohirok.StepHashtags.Manager.Set(testRunner, new string[] { "xfail:InvalidOperationException" })'
        # XFail's own generator plugin, wrapping the scenario tagged @xfail.
        'Ohirok.XFail.Manager.WrapScenarioAsync'
    )}
}

$failures = @()

foreach ($e in $expectations) {
    $path = Join-Path $smokeRoot (Join-Path $e.Project $e.File)
    if (-not (Test-Path $path)) {
        $failures += "$($e.Project): generated file not found at $($e.File) - was the project built?"
        continue
    }
    $content = Get-Content $path -Raw
    foreach ($marker in $e.Markers) {
        if ($content.Contains($marker)) {
            Write-Host "  OK   $($e.Project): $marker" -ForegroundColor DarkGray
        }
        else {
            $failures += "$($e.Project): generated code is missing '$marker'"
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host ''
    foreach ($f in $failures) { Write-Host "FAIL  $f" -ForegroundColor Red }
    throw "$($failures.Count) generated-code expectation(s) not met"
}

Write-Host ''
Write-Host "All generated-code expectations met ($($expectations.Count) project checks)." -ForegroundColor Green
