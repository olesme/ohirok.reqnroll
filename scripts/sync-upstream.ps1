#!/usr/bin/env pwsh
# sync-upstream.ps1 — Check the SDK's shadow copies of Reqnroll generator internals for upstream drift.
#
# The tracked files and the hash of each one at the last sync point are listed in
# src/PluginChains.Sdk/GenerationInterceptor/Generation/.upstream-sync.json.
# Both the baseline and the ref being checked are downloaded from GitHub, so nothing has to be kept locally.
#
# Usage:
#   ./scripts/sync-upstream.ps1                         -- compare baseline vs Reqnroll main
#   ./scripts/sync-upstream.ps1 -Ref v3.3.5             -- compare baseline vs a release tag
#   ./scripts/sync-upstream.ps1 -Ref v3.3.5 -ShowDiff   -- also print the upstream diff of drifted files
#   ./scripts/sync-upstream.ps1 -Ref v3.3.5 -UpdateBaseline
#                                                       -- after porting changes, record -Ref as the new baseline
#
# Workflow when Reqnroll releases a new version:
#   1. ./scripts/sync-upstream.ps1 -Ref v3.3.5 -ShowDiff
#   2. Port relevant changes into the *Int.cs files (preserving the #region INTERCEPT patches).
#   3. ./scripts/sync-upstream.ps1 -Ref v3.3.5 -UpdateBaseline, then commit the .json together with the Int changes.
#
# Exit code: 0 when everything is in sync, 1 when drift was found (so CI can use it as a scheduled check).

param(
    [string]$Ref = "main",
    [switch]$ShowDiff,
    [switch]$UpdateBaseline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root      = Split-Path $PSScriptRoot -Parent
$stateFile = Join-Path $root "src/PluginChains.Sdk/GenerationInterceptor/Generation/.upstream-sync.json"
$state     = Get-Content $stateFile -Raw | ConvertFrom-Json

function Get-NormalizedHash([string]$path) {
    # GitHub serves LF; normalize so the hash does not depend on the platform.
    $text  = (Get-Content $path -Raw) -replace "`r`n", "`n" -replace "`r", "`n"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    $sha   = [System.Security.Cryptography.SHA256]::Create()
    ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("X2") }) -join ""
}

function Get-UpstreamFile([string]$gitRef, [string]$repoPath) {
    $tmp = [System.IO.Path]::GetTempFileName() + ".cs"
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/reqnroll/Reqnroll/$gitRef/$repoPath" -OutFile $tmp -UseBasicParsing
    $tmp
}

Write-Host ""
Write-Host "Reqnroll SDK upstream sync" -ForegroundColor Cyan
Write-Host "  Baseline ref : $($state.baselineRef) (last sync: $($state.lastSyncDate))"
Write-Host "  Checking ref : $Ref"
Write-Host ""

$anyDrift = $false

foreach ($entry in $state.files) {
    $label = Split-Path $entry.upstreamRepoPath -Leaf

    try {
        $upstream = Get-UpstreamFile $Ref $entry.upstreamRepoPath
    } catch {
        Write-Host "  [ERROR] $label — could not fetch $($entry.upstreamRepoPath) at $Ref`n    $_" -ForegroundColor Red
        $anyDrift = $true
        continue
    }

    $upstreamHash = Get-NormalizedHash $upstream

    if ($upstreamHash -eq $entry.baselineHashLF) {
        Write-Host "  [OK]    $label — unchanged since baseline" -ForegroundColor Green
        Remove-Item $upstream -Force
        continue
    }

    $anyDrift = $true
    Write-Host "  [DRIFT] $label — changed since baseline ($($state.baselineRef))" -ForegroundColor Yellow
    Write-Host "          SDK file: $($entry.sdkFile)" -ForegroundColor DarkYellow

    if ($ShowDiff) {
        $baseline = Get-UpstreamFile $state.baselineRef $entry.upstreamRepoPath
        if ((Get-NormalizedHash $baseline) -ne $entry.baselineHashLF) {
            Write-Host "  [WARN]  baseline ref '$($state.baselineRef)' no longer matches the recorded hash (moving branch?)" -ForegroundColor DarkYellow
        }
        Write-Host "`n  --- Reqnroll changes ($($state.baselineRef) → $Ref) ---" -ForegroundColor Cyan
        git diff --no-index --unified=5 $baseline $upstream 2>&1 | Select-Object -Skip 4 | ForEach-Object {
            $color = if ($_ -match '^\+') { 'Green' } elseif ($_ -match '^-') { 'Red' } else { 'Gray' }
            Write-Host "    $_" -ForegroundColor $color
        }
        Remove-Item $baseline -Force
    }

    if ($UpdateBaseline) {
        $entry.baselineHashLF = $upstreamHash
    }

    Remove-Item $upstream -Force
}

if ($UpdateBaseline) {
    $state.baselineRef  = $Ref
    $state.lastSyncDate = (Get-Date -Format "yyyy-MM-dd")
    $state | ConvertTo-Json -Depth 5 | Set-Content $stateFile -Encoding UTF8
    Write-Host "`nBaseline recorded as $Ref in $stateFile" -ForegroundColor Green
    exit 0
}

if ($anyDrift) {
    Write-Host "`nDrift found — see above." -ForegroundColor Yellow
    exit 1
}

Write-Host "All shadow copies are in sync with upstream ($Ref)." -ForegroundColor Green
