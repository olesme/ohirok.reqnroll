<#
.SYNOPSIS
  One command for the packaging smoke tests: pack every src/ package, build every smoke project against the
  packages, run each one on its own test host, then prove every plugin actually took part in code generation.

.DESCRIPTION
  The smoke projects reference Ohirok only through PackageReference, so this is the check that the packages a
  consumer installs from NuGet work - a green `dotnet test` on the main solution proves nothing about packaging.

  Building any smoke project also packs the feed on its own when it is stale (see
  tests/PackagingSmokeTests/Directory.Build.targets), so the IDE and plain `dotnet build` stay in sync too. This
  script forces a fresh pack so a run never depends on that staleness check.

.PARAMETER Configuration
  Build configuration for both the packages and the smoke projects.

.PARAMETER Version
  Version of the packages under test. Defaults to the pinned local smoke version.

.PARAMETER FeedSource
  Folder with already-packed .nupkg files to test instead of packing src/ (publish.yml passes its release
  artifacts here, so the exact files about to be pushed are the ones tested).
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Version = '0.0.0-smoke',
    [string]$FeedSource
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$smokeRoot = Join-Path $repoRoot 'tests/PackagingSmokeTests'
$feed = Join-Path $smokeRoot '.localfeed'
$solution = Join-Path $smokeRoot 'PackagingSmokeTests.slnx'

if ($FeedSource) {
    if (Test-Path $feed) { Remove-Item $feed -Recurse -Force }
    New-Item -ItemType Directory -Path $feed | Out-Null
    Copy-Item (Join-Path $FeedSource '*.nupkg') $feed
    Get-ChildItem $smokeRoot -Recurse -Filter *.feature.cs |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Remove-Item -Force
    # Same fresh-clone reset pack-local-feed.ps1 does (see the reason there).
    Get-ChildItem $smokeRoot -Directory |
        ForEach-Object { Join-Path $_.FullName 'bin'; Join-Path $_.FullName 'obj' } |
        Where-Object { Test-Path $_ } | ForEach-Object { Remove-Item $_ -Recurse -Force }
    $cache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }
    Get-ChildItem $cache -Directory -Filter 'ohirok.*' -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName $Version } | Where-Object { Test-Path $_ } |
        ForEach-Object { Remove-Item $_ -Recurse -Force }
}
else {
    & (Join-Path $smokeRoot 'pack-local-feed.ps1') -Version $Version -Configuration $Configuration -Force
}

# Every repack reuses the same version, and a long-lived MSBuild node keeps the generator plugin assemblies it
# loaded from the previous pack - so generation could silently run the old plugin bits. Start from fresh nodes.
dotnet build-server shutdown | Out-Null

$buildArgs = @("-c", $Configuration, "-p:OhirokSmokeVersion=$Version", "-p:OhirokSkipSmokePack=true", "-nologo", "-nodeReuse:false")

dotnet restore $solution "-p:OhirokSmokeVersion=$Version" "-p:OhirokSkipSmokePack=true" -nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw 'restore of the smoke solution failed' }

dotnet build $solution --no-restore @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'build of the smoke solution failed' }

$failed = @()
foreach ($project in Get-ChildItem $smokeRoot -Recurse -Filter *.csproj | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) {
    $name = $project.BaseName
    Write-Host "`n=== $name ===" -ForegroundColor Cyan

    # xUnit v3 and TUnit run on Microsoft.Testing.Platform; their built executable is the test host.
    # NUnit and MSTest projects here use the VSTest adapter and run through `dotnet test`.
    $csproj = Get-Content $project.FullName -Raw
    if ($csproj -match 'Include="(TUnit|xunit\.v3)"') {
        $exe = Join-Path $project.DirectoryName "bin/$Configuration/net10.0/$name"
        if ($IsWindows -or $env:OS -eq 'Windows_NT') { $exe += '.exe' }
        & $exe
    }
    else {
        dotnet test $project.FullName --no-build -c $Configuration "-p:OhirokSmokeVersion=$Version" -nologo
    }
    if ($LASTEXITCODE -ne 0) { $failed += $name }
}

& (Join-Path $smokeRoot 'verify-generated-code.ps1')

if ($failed.Count -gt 0) {
    throw "packaging smoke tests failed: $($failed -join ', ')"
}
Write-Host "`nAll packaging smoke tests passed against $Version." -ForegroundColor Green
