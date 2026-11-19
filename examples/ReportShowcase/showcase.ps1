<#
.SYNOPSIS
  Runs the showcase test projects as a CI pipeline would over several builds and produces every Ohirok report
  they lead to, plus a landing page: output/index.html.

.DESCRIPTION
  Everything comes from real test runs of the four showcase projects (four different test runners):

    Showcase.WebShop.NUnit        the main suite - every status, plugin and kind of attachment
    Showcase.Payments.Xunit       a second test project of the same product
    Showcase.SingleFeature.TUnit  the edge case: a report with one feature
    Showcase.Large.MSTest         2000 tests

  Pipeline, per build 1..N (OHIROK_SHOWCASE_BUILD): run WebShop and Payments. The formatter keeps run history in
  a file, so each build's own report shows the builds before it. In build 4 the pipeline reruns the failed
  health scenarios (OHIROK_SHOWCASE_RERUN=1) under the same pipelineRunId, so history keeps one row for that build.

  Then Ohirok.Reporting.Combine merges all builds of both projects - build 4 as base run + rerun - into one
  report, rendered once per built-in theme. The single-feature and the large suite are rendered per theme too.

.PARAMETER Builds
  Number of simulated builds (at least 4, so that the rerun build exists). Default 5.

.PARAMETER Configuration
  Build configuration of the test projects.
#>
[CmdletBinding()]
param(
    [ValidateRange(4, 20)][int]$Builds = 5,
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $root '..\..')).Path
$out = Join-Path $root 'output'
$runs = Join-Path $out 'runs'
# Built-in themes by name, plus 'custom': a theme and a template of your own, loaded by file path (Customization/).
$themes = @('default', 'cucumber-green', 'cyberpunk-purple', 'midnight-blue', 'custom')
$rerunBuild = 4

$projects = [ordered]@{
    webshop = @{ Name = 'Showcase.WebShop.NUnit'; Host = 'dotnet-test'; Title = 'Web shop' }
    payments = @{ Name = 'Showcase.Payments.Xunit'; Host = 'xunit'; Title = 'Payments' }
    single = @{ Name = 'Showcase.SingleFeature.TUnit'; Host = 'tunit'; Title = 'Single feature' }
    large = @{ Name = 'Showcase.Large.MSTest'; Host = 'dotnet-test'; Title = '2000 tests' }
}

function Get-BinDir($p) { Join-Path $root "$($p.Name)/bin/$Configuration/net10.0" }

function Invoke-Project($key, [int]$build, [switch]$Rerun, [string]$Filter) {
    $p = $projects[$key]
    $bin = Get-BinDir $p
    $env:OHIROK_SHOWCASE_BUILD = $build
    $env:OHIROK_SHOWCASE_RERUN = if ($Rerun) { '1' } else { '0' }
    $label = "build $build$(if ($Rerun) { ' rerun' })"
    Write-Host "  $($p.Title) - $label" -ForegroundColor Cyan
    # The suites contain failing scenarios on purpose, so a red exit code is expected; only a missing report fails.
    Push-Location $bin
    try {
        switch ($p.Host) {
            'dotnet-test' {
                $testArgs = @((Join-Path $root "$($p.Name)/$($p.Name).csproj"), '--no-build', '-c', $Configuration, '-nologo', '--logger', 'console;verbosity=quiet')
                if ($Filter) { $testArgs += @('--filter', $Filter) }
                dotnet test @testArgs | Out-Null
            }
            'xunit' { & (Join-Path $bin "$($p.Name).exe") -noLogo | Out-Null }
            'tunit' { & (Join-Path $bin "$($p.Name).exe") --no-banner | Out-Null }
        }
    }
    finally { Pop-Location }

    $ndjson = Join-Path $bin 'report/ohirok-report.ndjson'
    if (-not (Test-Path $ndjson)) { throw "$($p.Name) produced no report" }
    $dir = Join-Path $runs "build-$build"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $stem = Join-Path $dir ($key + $(if ($Rerun) { '-rerun' }))
    Copy-Item $ndjson "$stem.ndjson" -Force
    Copy-Item (Join-Path $bin 'report/ohirok-report.html') "$stem.html" -Force
    # The history dots of a build's own report link to the archived reports of earlier builds, next to it.
    $archive = Join-Path $bin 'report/history-archive'
    if (Test-Path $archive) {
        $target = Join-Path $dir 'history-archive'
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        Copy-Item (Join-Path $archive '*') $target -Force
    }
    "$stem.ndjson"
}

Write-Host 'Building the showcase...' -ForegroundColor Green
foreach ($p in $projects.Values) {
    dotnet build (Join-Path $root "$($p.Name)/$($p.Name).csproj") -c $Configuration -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "build of $($p.Name) failed" }
}
dotnet build (Join-Path $repoRoot 'src/Reporting/Ohirok.Reporting.Combine') -c $Configuration -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'build of Ohirok.Reporting.Combine failed' }
$combineDll = Join-Path $repoRoot "src/Reporting/Ohirok.Reporting.Combine/bin/$Configuration/net10.0/Ohirok.Reporting.Combine.dll"

# A fresh history for every invocation.
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
foreach ($p in $projects.Values) {
    $bin = Get-BinDir $p
    Remove-Item (Join-Path $bin '*-history.ndjson') -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $bin 'report') -Recurse -Force -ErrorAction SilentlyContinue
}

$combineArgs = @()
for ($b = 1; $b -le $Builds; $b++) {
    Write-Host "Build $b" -ForegroundColor Green
    $web = Invoke-Project 'webshop' $b
    $pay = Invoke-Project 'payments' $b
    $combineArgs += @('--name', "Build $b - Web shop", '--ndjson', $web)
    if ($b -eq $rerunBuild) {
        $rerun = Invoke-Project 'webshop' $b -Rerun -Filter 'FullyQualifiedName~BehaviourAcrossBuilds&(FullyQualifiedName~Flaky|FullyQualifiedName~Regressed|FullyQualifiedName~Broken)'
        $combineArgs += @('--ndjson', $rerun)
    }
    $combineArgs += @('--name', "Build $b - Payments", '--ndjson', $pay)
}
Write-Host 'Single feature and large suite' -ForegroundColor Green
$single = Invoke-Project 'single' $Builds
$large = Invoke-Project 'large' $Builds

function Invoke-Combine([string]$theme, [string]$title, [string[]]$inputArgs, [string]$target) {
    $cfg = Get-Content (Join-Path $root 'Showcase.WebShop.NUnit/reqnroll.json') -Raw | ConvertFrom-Json
    $cfg.reporting.title = $title
    if ($theme -eq 'custom') {
        $cfg.reporting | Add-Member -NotePropertyName css -NotePropertyValue (Join-Path $root 'Customization/tea-house.css') -Force
        $cfg.reporting | Add-Member -NotePropertyName template -NotePropertyValue (Join-Path $root 'Customization/custom-template.html') -Force
    }
    elseif ($theme -ne 'default') { $cfg.reporting | Add-Member -NotePropertyName css -NotePropertyValue $theme -Force }
    $cfgPath = Join-Path $out "reqnroll.$theme.json"
    $cfg | ConvertTo-Json -Depth 20 | Set-Content $cfgPath -Encoding UTF8
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    dotnet $combineDll @inputArgs --out $target --reqnroll-config $cfgPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "combine failed for $target" }
}

Write-Host 'Combining' -ForegroundColor Green
foreach ($theme in $themes) {
    $dir = Join-Path $out "themes/$theme"
    Invoke-Combine $theme "Tea shop - $Builds builds, 2 projects" $combineArgs (Join-Path $dir 'pipeline.html')
    Invoke-Combine $theme 'Gift cards - single feature' @('--name', 'Single feature', '--ndjson', $single) (Join-Path $dir 'single-feature.html')
    Invoke-Combine $theme 'Tea shop price matrices - 2000 tests' @('--name', '2000 tests', '--ndjson', $large) (Join-Path $dir 'large.html')
}

# Landing page
$size = { param($f) '{0:N1} MB' -f ((Get-Item $f).Length / 1MB) }
$themeRows = ($themes | ForEach-Object {
    "<tr><th>$_</th><td><a href=""themes/$_/pipeline.html"">pipeline</a></td><td><a href=""themes/$_/single-feature.html"">single feature</a></td><td><a href=""themes/$_/large.html"">2000 tests</a> <small>($(& $size (Join-Path $out "themes/$_/large.html")))</small></td></tr>"
}) -join "`n"
$buildRows = (1..$Builds | ForEach-Object {
    $r = if ($_ -eq $rerunBuild) { ' &middot; <a href="runs/build-' + $_ + '/webshop-rerun.html">web shop rerun</a>' } else { '' }
    "<li>Build $_ &middot; <a href=""runs/build-$_/webshop.html"">web shop</a>$r &middot; <a href=""runs/build-$_/payments.html"">payments</a></li>"
}) -join "`n"
@"
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Ohirok report showcase</title>
<style>
:root { color-scheme: light dark; --bg: #fafafa; --fg: #1f2328; --muted: #57606a; --line: #d0d7de; --link: #0b5cad; }
@media (prefers-color-scheme: dark) { :root { --bg: #0d1117; --fg: #e6edf3; --muted: #9da7b3; --line: #30363d; --link: #6cb6ff; } }
body { margin: 0 auto; max-width: 960px; padding: 24px 16px 48px; background: var(--bg); color: var(--fg); font: 16px/1.5 system-ui, sans-serif; }
a { color: var(--link); } h1 { margin-bottom: 4px; } p.lead { color: var(--muted); margin-top: 0; }
table { border-collapse: collapse; width: 100%; margin: 8px 0 24px; } th, td { text-align: left; padding: 8px; border-bottom: 1px solid var(--line); }
small { color: var(--muted); } li { margin: 4px 0; }
</style></head><body>
<h1>Ohirok report showcase</h1>
<p class="lead">Generated $(Get-Date -Format 'yyyy-MM-dd HH:mm') from real runs of four test projects (NUnit, xUnit v3, TUnit, MSTest) over $Builds simulated builds.</p>
<h2>Every theme</h2>
<p><b>Pipeline</b>: all builds of the web shop and the payments project combined - run history and health across builds, build $rerunBuild as base run + rerun of its failures, every status, retries, expected/allowed failures, skipped steps, soft assertions, screenshots, HTML, callouts and links to bugs and backlog items, filters. Each report has its own light/dark switch.</p>
<table><thead><tr><th>Theme</th><th>Pipeline</th><th>Edge case</th><th>Large</th></tr></thead><tbody>
$themeRows
</tbody></table>
<h2>Where to look</h2>
<ul>
<li><b>Every kind of attachment</b> (Reqnroll files and text, inline images, image URLs, HTML, callouts, attachments from hooks): <a href="themes/default/pipeline.html?q=attachment">pipeline report filtered to it</a>.</li>
<li><b>Screenshots and links to bugs and backlog items</b>: <a href="themes/default/pipeline.html?q=receipt">checkout evidence</a>.</li>
<li><b>History and health</b>: <a href="themes/default/pipeline.html?q=across%20builds">behaviour across builds</a>, the History tab of the analytics view, and the history dots of each scenario.</li>
<li><b>Reruns</b>: build $rerunBuild of the web shop; switch reruns off and on in the report header.</li>
<li><b>Expected and allowed failures, skipped and incomplete steps, failing hooks</b>: <a href="themes/default/pipeline.html?q=resilience">resilience</a>.</li>
</ul>
<h2>Each build's own report</h2>
<p>What the formatter writes during the test run itself, with the history of the builds before it.</p>
<ul>
$buildRows
</ul>
</body></html>
"@ | Set-Content (Join-Path $out 'index.html') -Encoding UTF8

Remove-Item Env:\OHIROK_SHOWCASE_BUILD, Env:\OHIROK_SHOWCASE_RERUN -ErrorAction SilentlyContinue
Write-Host "`nOpen $(Join-Path $out 'index.html')" -ForegroundColor Green
