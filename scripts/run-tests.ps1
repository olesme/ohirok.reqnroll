<#
.SYNOPSIS
  Runs the repository's test gate: every test project under tests/, each on its own test host.

.DESCRIPTION
  The single list of what "the tests pass" means, used by CI, publish.yml and locally.

  Test projects are discovered, not listed, so a new one is gated without editing any workflow:
    - tests/**/*.csproj that reference a test framework are "positive" projects - every test must pass
      (expected failures are expressed inside them with #xfail and therefore pass);
    - projects named *.Negative are "negative" projects - every test in them must FAIL. They hold the
      scenarios whose correct outcome is a red test and that cannot be wrapped in #xfail (for example
      because they test XFail itself, or fail after the last step). A negative project that turns green
      means a plugin started swallowing real failures, so it fails the gate too;
    - tests/PackagingSmokeTests is excluded; scripts/test-packages.ps1 owns it.

  xUnit v3 and TUnit projects run on Microsoft.Testing.Platform, whose built executable is the test host.
  NUnit and MSTest projects use the VSTest adapter and run through `dotnet test`.

.PARAMETER Configuration
  Build configuration. The projects must already be built unless -Build is given.

.PARAMETER Build
  Build Ohirok.Reqnroll.slnx first.

.PARAMETER Filter
  Only run projects whose name matches this wildcard (e.g. 'Retry*').

.PARAMETER Describe
  For negative projects, print each scenario's attempts, failing step error and log.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Build,
    [string]$Filter = '*',
    # Print what each scenario of a negative project actually did (attempts, error, log) - the input for
    # writing its expected-failures.json.
    [switch]$Describe
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$resultsRoot = Join-Path $repoRoot "TestResults/gate"

if ($Build) {
    dotnet build (Join-Path $repoRoot 'Ohirok.Reqnroll.slnx') -c $Configuration -nologo
    if ($LASTEXITCODE -ne 0) { throw 'build failed' }
}

if (Test-Path $resultsRoot) { Remove-Item $resultsRoot -Recurse -Force }

function Get-TrxCounters([string]$dir) {
    $trx = Get-ChildItem $dir -Recurse -Filter *.trx | Sort-Object LastWriteTime | Select-Object -Last 1
    if (-not $trx) { return $null }
    $counters = ([xml](Get-Content $trx.FullName -Raw)).TestRun.ResultSummary.Counters
    [pscustomobject]@{ Total = [int]$counters.total; Passed = [int]$counters.passed; Failed = [int]$counters.failed }
}

# Scenario names, attempts, final error and logged text, read from the Cucumber messages Reqnroll writes.
# This is runner-independent (test names and captured output differ per runner, the messages do not).
function Get-ScenarioOutcomes([string]$ndjson) {
    $pickles = @{}; $testCases = @{}; $started = @{}; $byPickle = [ordered]@{}
    foreach ($line in [System.IO.File]::ReadLines($ndjson)) {
        $e = $line | ConvertFrom-Json
        if ($e.pickle) { $pickles[$e.pickle.id] = $e.pickle.name }
        elseif ($e.testCase) { $testCases[$e.testCase.id] = $e.testCase.pickleId }
        elseif ($e.testCaseStarted) {
            $pickleId = $testCases[$e.testCaseStarted.testCaseId]
            $started[$e.testCaseStarted.id] = $pickleId
            if (-not $byPickle.Contains($pickleId)) {
                $byPickle[$pickleId] = [pscustomobject]@{ Name = $pickles[$pickleId]; Attempts = 0; Error = ''; Log = '' }
            }
            $byPickle[$pickleId].Attempts++
            $byPickle[$pickleId].Error = ''
        }
        elseif ($e.testStepFinished) {
            $r = $e.testStepFinished.testStepResult
            if ($r.status -notin 'PASSED', 'SKIPPED' -and -not $byPickle[$started[$e.testStepFinished.testCaseStartedId]].Error) {
                $msg = if ($r.exception) { "$($r.exception.type): $($r.exception.message)" } else { "$($r.status): $($r.message)" }
                $byPickle[$started[$e.testStepFinished.testCaseStartedId]].Error = $msg
            }
        }
        elseif ($e.attachment -and $e.attachment.testCaseStartedId -and $e.attachment.contentEncoding -eq 'IDENTITY') {
            $byPickle[$started[$e.attachment.testCaseStartedId]].Log += "$($e.attachment.body)`n"
        }
    }
    $byPickle.Values
}

function ConvertTo-Key([string]$s) { ($s -replace '[^A-Za-z0-9]', '').ToLowerInvariant() }

# Checks every scenario of a negative project against expected-failures.json (looked up next to the project,
# then in its parent folder so the runner twins of one suite share a single file). Each rule:
#   { "scenario": "<name prefix>", "error": "<regex>", "attempts": "3" | "1-4", "log": "<regex>", "noLog": "<regex>" }
# Every scenario must match a rule and satisfy it, and every rule must match at least one scenario.
function Test-ExpectedFailures([System.IO.FileInfo]$project, [string]$configuration) {
    $rulesFile = @((Join-Path $project.DirectoryName 'expected-failures.json'),
                   (Join-Path (Split-Path $project.DirectoryName -Parent) 'expected-failures.json')) |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $rulesFile) { return @() }

    $ndjson = Get-ChildItem (Join-Path $project.DirectoryName "bin/$configuration") -Recurse -Filter reqnroll_report.ndjson |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if (-not $ndjson) { return @("no Cucumber messages file (reqnroll_report.ndjson) found to check $rulesFile against") }

    $rules = Get-Content $rulesFile -Raw | ConvertFrom-Json
    $used = @{}
    $problems = @()
    foreach ($s in Get-ScenarioOutcomes $ndjson.FullName) {
        $key = ConvertTo-Key $s.Name
        $rule = $rules | Where-Object { $key.StartsWith((ConvertTo-Key $_.scenario)) } |
            Sort-Object { (ConvertTo-Key $_.scenario).Length } -Descending | Select-Object -First 1
        if (-not $rule) { $problems += "'$($s.Name)': no rule in expected-failures.json"; continue }
        $used[$rule.scenario] = $true

        # A failure raised outside the steps (a scenario-level wrapper) leaves no step error; such a rule says so by
        # giving only a log pattern.
        if (-not $s.Error -and ($rule.error -or -not $rule.log)) { $problems += "'$($s.Name)': passed, expected a failure"; continue }
        if ($rule.error -and $s.Error -notmatch $rule.error) { $problems += "'$($s.Name)': error '$($s.Error)' does not match /$($rule.error)/" }
        if ($rule.attempts) {
            $lo, $hi = "$($rule.attempts)" -split '-'
            if (-not $hi) { $hi = $lo }
            if ($s.Attempts -lt [int]$lo -or $s.Attempts -gt [int]$hi) { $problems += "'$($s.Name)': $($s.Attempts) attempt(s), expected $($rule.attempts)" }
        }
        if ($rule.log -and $s.Log -notmatch $rule.log) { $problems += "'$($s.Name)': log does not match /$($rule.log)/" }
        if ($rule.noLog -and $s.Log -match $rule.noLog) { $problems += "'$($s.Name)': log unexpectedly matches /$($rule.noLog)/" }
    }
    foreach ($r in $rules) {
        if (-not $used[$r.scenario]) { $problems += "rule '$($r.scenario)' matched no scenario" }
    }
    $problems
}

# Checks the tests of a positive project against expected-tests.json (next to the project, then in its parent
# folder), using the Cucumber messages:
#   [ { "scenario": "<name prefix>", "tags": [ ["@a", "@b"], ["@a", "@c"] ] } ]
# Every pickle must have run exactly once, there must be as many pickles run as tests in the TRX (so every test
# reported its own pickle), every pickle must match a rule, and for each rule the tag sets of its pickles (order
# ignored) must be exactly the listed ones.
function Test-ExpectedTests([System.IO.FileInfo]$project, [string]$configuration, $counters) {
    $rulesFile = @((Join-Path $project.DirectoryName 'expected-tests.json'),
                   (Join-Path (Split-Path $project.DirectoryName -Parent) 'expected-tests.json')) |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $rulesFile) { return @() }

    $ndjson = Get-ChildItem (Join-Path $project.DirectoryName "bin/$configuration") -Recurse -Filter reqnroll_report.ndjson |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if (-not $ndjson) { return @("no Cucumber messages file (reqnroll_report.ndjson) found to check $rulesFile against") }

    $pickles = @{}; $testCases = @{}; $runs = @{}
    foreach ($line in [System.IO.File]::ReadLines($ndjson.FullName)) {
        $e = $line | ConvertFrom-Json
        if ($e.pickle) {
            $pickles[$e.pickle.id] = [pscustomobject]@{ Name = $e.pickle.name; Tags = @($e.pickle.tags | ForEach-Object name | Sort-Object) }
        }
        elseif ($e.testCase) { $testCases[$e.testCase.id] = $e.testCase.pickleId }
        elseif ($e.testCaseStarted) { $runs[$testCases[$e.testCaseStarted.testCaseId]]++ }
    }

    $problems = @()
    foreach ($id in $runs.Keys) {
        if ($runs[$id] -ne 1) { $problems += "'$($pickles[$id].Name)': ran $($runs[$id]) times, expected once" }
    }
    if ($counters -and $runs.Count -ne $counters.Total) {
        $problems += "$($counters.Total) tests ran but only $($runs.Count) pickles were reported: tests share pickles"
    }

    $rules = Get-Content $rulesFile -Raw | ConvertFrom-Json
    $byRule = @{}
    foreach ($id in $runs.Keys) {
        $p = $pickles[$id]
        $key = ConvertTo-Key $p.Name
        $rule = $rules | Where-Object { $key.StartsWith((ConvertTo-Key $_.scenario)) } |
            Sort-Object { (ConvertTo-Key $_.scenario).Length } -Descending | Select-Object -First 1
        if (-not $rule) { $problems += "'$($p.Name)': no rule in expected-tests.json"; continue }
        if (-not $byRule.Contains($rule.scenario)) { $byRule[$rule.scenario] = @() }
        $byRule[$rule.scenario] += , ($p.Tags -join ' ')
    }
    foreach ($r in $rules) {
        $expected = @($r.tags | ForEach-Object { (@($_) | Sort-Object) -join ' ' } | Sort-Object)
        $actual = @($byRule[$r.scenario] | Sort-Object)
        if (($expected -join '|') -ne ($actual -join '|')) {
            $problems += "'$($r.scenario)': pickle tags [$($actual -join '] [')] differ from the expected [$($expected -join '] [')]"
        }
    }
    $problems
}

$projects = Get-ChildItem (Join-Path $repoRoot 'tests') -Recurse -Filter *.csproj |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj|PackagingSmokeTests)[\\/]' } |
    Where-Object { $_.BaseName -like $Filter } |
    Where-Object { (Get-Content $_.FullName -Raw) -match 'Include="(NUnit|MSTest\.TestFramework|xunit\.v3|TUnit)"|Runner\.props' } |
    Sort-Object FullName

$summary = @()
foreach ($project in $projects) {
    $name = $project.BaseName
    $negative = $name -like '*.Negative'
    $results = Join-Path $resultsRoot $name
    Write-Host "`n=== $name$(if ($negative) { ' (negative: every test must fail)' }) ===" -ForegroundColor Cyan

    # Runner suites (see tests/Ohirok.RunnerSuite.props) take the runner from the project name.
    $csproj = Get-Content $project.FullName -Raw
    $isXunit = $csproj -match 'Include="xunit\.v3"' -or ($csproj -match 'Runner\.props' -and $name -match 'Xunit')
    $isTUnit = $csproj -match 'Include="TUnit"' -or ($csproj -match 'Runner\.props' -and $name -match 'TUnit')
    $exit = 0
    try {
        if ($isXunit -or $isTUnit) {
            $exe = Join-Path $project.DirectoryName "bin/$Configuration/net10.0/$name"
            if ($IsWindows -or $env:OS -eq 'Windows_NT') { $exe += '.exe' }
            if ($isXunit) {
                New-Item -ItemType Directory -Path $results -Force | Out-Null
                # -trx, not -result-trx: xUnit v3 3.x only knows -trx, and 4.x still accepts it (the CI also checks 3.x).
                & $exe -trx (Join-Path $results "$name.trx")
            }
            else {
                & $exe --report-trx --results-directory $results
            }
        }
        else {
            dotnet test $project.FullName --no-build -c $Configuration -nologo --logger trx --results-directory $results
        }
        $exit = $LASTEXITCODE
    }
    catch {
        # A host that cannot even start (not built, path too long, ...) fails this project, not the whole gate.
        Write-Host "  could not run: $($_.Exception.Message)" -ForegroundColor Red
        $exit = -1
    }
    $c = Get-TrxCounters $results

    $ok = if ($negative) {
        # Red exit code AND not a single passing test - a partially green negative project is a regression.
        $exit -ne 0 -and $c -and $c.Total -gt 0 -and $c.Passed -eq 0
    }
    else {
        $exit -eq 0
    }

    # A negative project is only "correctly red" when each scenario fails for the documented reason.
    if ($negative -and $Describe) {
        $nd = Get-ChildItem (Join-Path $project.DirectoryName "bin/$Configuration") -Recurse -Filter reqnroll_report.ndjson |
            Sort-Object LastWriteTime | Select-Object -Last 1
        if ($nd) {
            Get-ScenarioOutcomes $nd.FullName | ForEach-Object {
                Write-Host ("  {0} | attempts={1} | {2}" -f $_.Name, $_.Attempts, $_.Error) -ForegroundColor Yellow
            }
        }
    }

    $why = @()
    if ($negative -and $ok) {
        $why = @(Test-ExpectedFailures $project $Configuration)
        foreach ($p in $why) { Write-Host "  expected-failures: $p" -ForegroundColor Red }
        if ($why.Count -gt 0) { $ok = $false }
    }
    if (-not $negative -and $ok) {
        $why = @(Test-ExpectedTests $project $Configuration $c)
        foreach ($p in $why) { Write-Host "  expected-tests: $p" -ForegroundColor Red }
        if ($why.Count -gt 0) { $ok = $false }
    }

    $summary += [pscustomobject]@{
        Project = $name
        Kind    = if ($negative) { 'negative' } else { 'positive' }
        Total   = if ($c) { $c.Total } else { '?' }
        Passed  = if ($c) { $c.Passed } else { '?' }
        Failed  = if ($c) { $c.Failed } else { '?' }
        Gate    = if ($ok) { 'OK' } else { 'FAIL' }
    }
}

Write-Host ''
$summary | Format-Table -AutoSize | Out-String | Write-Host

$bad = @($summary | Where-Object Gate -eq 'FAIL')
if ($bad.Count -gt 0) {
    throw "test gate failed: $($bad.Project -join ', ')"
}
Write-Host "Test gate passed ($($summary.Count) projects)." -ForegroundColor Green
