(function () {
    var payload = JSON.parse(document.getElementById('ohirok-report-data').textContent);

    // Shown inside a frame (an Azure DevOps extension tab, Jenkins HTML Publisher, ...), the report only gets the
    // frame's space. Keep the page as it arrived, so it can be reopened in a window of its own - before the script
    // below changes the DOM.
    var FRAMED = (function () { try { return window.self !== window.top; } catch (e) { return true; } })();
    var ORIGINAL_PAGE = FRAMED ? '<!DOCTYPE html>\n' + document.documentElement.outerHTML : null;

    // Reruns of failed scenarios inside one pipeline (merged in by Ohirok.Reporting.Combine: one --name, several
    // --ndjson) can be switched off to see the pipeline's own first pass. The choice is remembered and applied before
    // the tree is built: rerun attempts are dropped and each scenario's status is taken from its own attempts.
    // A ?reruns=on|off URL parameter wins over the remembered choice, so a link shows what its sender saw.
    var RERUNS_KEY = 'ohirok-report-include-reruns';
    var REPORT_HAS_RERUNS = false;
    var INCLUDE_RERUNS = true;
    try { INCLUDE_RERUNS = localStorage.getItem(RERUNS_KEY) !== '0'; } catch (e) { /* storage unavailable */ }
    try {
        var rerunsParam = new URLSearchParams(location.search).get('reruns');
        if (rerunsParam === 'off') INCLUDE_RERUNS = false;
        else if (rerunsParam === 'on') INCLUDE_RERUNS = true;
    } catch (e) { /* no URL API - the remembered choice stands */ }
    (function applyRerunChoice() {
        function applyTo(s) {
            var attempts = s.attempts || [];
            if (!attempts.some(function (a) { return a.rerun; })) return;
            REPORT_HAS_RERUNS = true;
            if (INCLUDE_RERUNS) return;
            var own = attempts.filter(function (a) { return !a.rerun; });
            if (own.length === 0) return;
            s.attempts = own;
            var last = own[own.length - 1].status;
            s.status = last === 'Passed' && own.length > 1 ? 'PassWithIssues' : last;
        }
        // Scenarios sit in features, rules and outline example sets alike - anything with attempts is one.
        function visit(node) {
            if (Array.isArray(node)) { node.forEach(visit); return; }
            if (!node || typeof node !== 'object') return;
            if (Array.isArray(node.attempts)) { applyTo(node); return; }
            for (var k in node) if (k !== 'recentRuns') visit(node[k]);
        }
        (payload.runs || []).forEach(function (run) { visit(run.root); });
    })();

    // One formatter for every date/time in the report, so the generation time, run times and history entries
    // never mix clock styles or locales. reporting.timeFormat ("24h" default / "12h") and reporting.locale.
    var DISPLAY = payload.display || {};
    function formatDateTime(iso, options) {
        if (!iso) return 'unknown time';
        var d = new Date(iso);
        if (isNaN(d.getTime())) return String(iso);
        var opts = { hourCycle: DISPLAY.timeFormat === '12h' ? 'h12' : 'h23' };
        for (var k in options) opts[k] = options[k];
        try { return new Intl.DateTimeFormat(DISPLAY.locale || undefined, opts).format(d); }
        catch (e) { return new Intl.DateTimeFormat(undefined, opts).format(d); }
    }
    (function renderGeneratedDate() {
        var span = document.querySelector('.report-generated-date');
        if (!span || !payload.generatedAt) return;
        span.textContent = 'Generated ' + formatDateTime(payload.generatedAt, {
            weekday: 'short', year: 'numeric', month: 'short', day: 'numeric',
            hour: '2-digit', minute: '2-digit', second: '2-digit', timeZoneName: 'shortOffset'
        });
    })();
    var runs = payload.runs || [];
    // The server no longer computes scenario.health (see feedback_client_side_computation
    // memory); analyzeScenarioHealth() below derives it here from scenario.recentRuns, capped the same way
    // OhirokReportFormatter/ReportRunCombiner used to cap ReportHealthAnalyzer.Analyze's maxRealRuns. Sourced
    // from runs[0] since every run in one generated report shares the same static config (see
    // ReportRun.HistoryAnalysisWindow's own doc comment) - null when no history backend is configured.
    var historyAnalysisWindow = (runs[0] && runs[0].historyAnalysisWindow) || null;
    var filterGroups = (runs[0] && runs[0].filterGroups) || [];
    var otherLabelByGroup = {};
    filterGroups.forEach(function (g) { otherLabelByGroup[g.name] = g.otherLabel; });
    function isOtherMatch(m) { return !!m && otherLabelByGroup[m.group] === m.label; }
    var treeEl = document.getElementById('ohirok-report-tree');
    var headerTitleRow = document.getElementById('ohirok-report-header-title-row');
    var headerMetaRow = document.getElementById('ohirok-report-header-meta-row');
    var toolbar = document.getElementById('ohirok-report-toolbar');

    // Optional unobtrusive credit in the header's meta row (right-aligned, muted, small). Configure by filling
    // data-name / data-repo-url / data-author / data-author-url / data-hint on .report-credit in the template; stays hidden while empty.
    (function () {
        var credit = document.querySelector('.report-credit');
        if (!credit) return;
        var repo = credit.getAttribute('data-repo-url');
        var name = credit.getAttribute('data-name') || 'Ohirok.Reqnroll';
        var hint = credit.getAttribute('data-hint');
        if (hint) credit.title = hint;
        var author = credit.getAttribute('data-author');
        var authorUrl = credit.getAttribute('data-author-url');
        if (!repo && !author) return;
        function link(text, href) {
            var a = document.createElement('a');
            a.textContent = text;
            a.href = href;
            a.target = '_blank';
            a.rel = 'noopener noreferrer';
            return a;
        }
        if (repo) credit.appendChild(link(name, repo));
        else credit.appendChild(document.createTextNode(name));
        if (author) {
            credit.appendChild(document.createTextNode(' \u00b7 by '));
            if (authorUrl) credit.appendChild(link(author, authorUrl));
            else credit.appendChild(document.createTextNode(author));
        }
        credit.hidden = false;
    })();
    var filterPanelsEl = document.getElementById('ohirok-report-filter-panels');
    var breadcrumbEl = document.getElementById('ohirok-report-breadcrumb');
    var chartsContainer = document.getElementById('ohirok-report-charts');
    var detailContent = document.getElementById('ohirok-report-detail-content');
    var sidebarEl = document.getElementById('ohirok-report-sidebar');
    var splitterEl = document.getElementById('ohirok-report-splitter');

    var activeFilters = {};
    var activeStatusFilters = {};
    var searchText = '';
    var focusedNode = null;
    var rootNode = null;
    var selectedScenarioHistory = null;
    var analyticsMode = false;

    // ---------------------------------------------------------------- historical snapshots
    // A run-history entry ships the scenario's snapshot exactly as the C# model serialized it: PascalCase keys
    // ("Status", "Steps") and numeric enums ("Status": 1) - unlike the live tree, which is camelCase with string
    // statuses. The old code read snapshot.status / snapshot.steps directly, so every snapshot looked empty,
    // buildScenarioHistoryStrip() filtered every run out and the history row silently never rendered.
    // normalizeSnapshot() makes the snapshot look like a live scenario node (camelCase keys, string enums) so the
    // regular renderers (renderStep, renderHooksBlock, renderAttachmentsBlock, ...) can be reused unchanged.
    // Already-camelCase input passes through untouched, so fixing the serializer later is harmless.
    var STATUS_BY_NUMBER = ['Unknown', 'Passed', 'PassWithIssues', 'Skipped', 'Pending', 'Undefined', 'Ambiguous', 'Failed', 'NotRun'];
    var ISSUE_CATEGORY_BY_NUMBER = ['RetrySucceeded', 'SoftAssertUnflushed', 'XFailPassed', 'StepSkipped', 'Custom', 'RetryTimedOut', 'ExpectedFailure'];
    // Dictionaries keyed by user data (Examples-row parameter names) - their keys must never be re-cased.
    var VERBATIM_KEYS = { argumentValues: true, ArgumentValues: true };

    function normalizeSnapshotValue(value, keyName) {
        if (Array.isArray(value)) return value.map(function (v) { return normalizeSnapshotValue(v, keyName); });
        if (value && typeof value === 'object') {
            var out = {};
            Object.keys(value).forEach(function (k) {
                var nk = k.charAt(0).toLowerCase() + k.slice(1);
                out[nk] = VERBATIM_KEYS[k] ? value[k] : normalizeSnapshotValue(value[k], nk);
            });
            return out;
        }
        if (typeof value === 'number') {
            if (keyName === 'status') return STATUS_BY_NUMBER[value] || 'Unknown';
            if (keyName === 'category') return ISSUE_CATEGORY_BY_NUMBER[value] || 'Custom';
        }
        return value;
    }

    function parseHistoricalSnapshot(rawSnapshot) {
        if (!rawSnapshot) return null;
        var snapshot = rawSnapshot;
        if (typeof snapshot === 'string') {
            try { snapshot = JSON.parse(snapshot); } catch (e) { return null; }
        }
        if (!snapshot || typeof snapshot !== 'object') return null;
        if (snapshot.__normalized) return snapshot;
        snapshot = normalizeSnapshotValue(snapshot, '');

        var isMeaningful = !!(snapshot.status || snapshot.description || snapshot.comment || snapshot.errorMessage || snapshot.stackTrace ||
            (snapshot.steps && snapshot.steps.length) || (snapshot.attachments && snapshot.attachments.length) ||
            (snapshot.hooks && snapshot.hooks.length) || (snapshot.issues && snapshot.issues.length));
        if (!isMeaningful) return null;
        snapshot.__normalized = true;
        return snapshot;
    }

    // Parsing is cached per run entry - the strip asks for every entry on every render.
    // Parsing is cached per run entry - the strip asks for every entry on every render.
    function historicalSnapshotForRunSummary(runSummary) {
        if (!runSummary) return null;
        if (runSummary.__parsed === undefined) {
            var parsed = parseHistoricalSnapshot(runSummary.snapshot || runSummary.snapshotJson || null);
            try { Object.defineProperty(runSummary, '__parsed', { value: parsed, enumerable: false }); }
            catch (e) { return parsed; }
        }
        return runSummary.__parsed;
    }

    function clearSelectedScenarioHistory() {
        selectedScenarioHistory = null;
    }

    function setSelectedScenarioHistory(node, runSummary) {
        selectedScenarioHistory = { node: node, runSummary: runSummary, snapshot: historicalSnapshotForRunSummary(runSummary) };
    }

    function isSelectedScenarioHistory(node, runSummary) {
        return !!selectedScenarioHistory && !!node && !!runSummary && selectedScenarioHistory.node === node && selectedScenarioHistory.runSummary && selectedScenarioHistory.runSummary.runAt === runSummary.runAt && selectedScenarioHistory.runSummary.runUrl === runSummary.runUrl;
    }

    function isCurrentScenarioRun(node, runSummary) {
        if (!node || !node.scenario || !node.scenario.recentRuns || !node.scenario.recentRuns.length || !runSummary) return false;
        var current = node.scenario.recentRuns[0];
        return !!current && current.runAt === runSummary.runAt && (current.runUrl === runSummary.runUrl || (!current.runUrl && !runSummary.runUrl));
    }

    function setScenarioHistoryBanner(node) {
        var banner = document.getElementById('ohirok-report-context-banner');
        if (!banner) return;
        if (!selectedScenarioHistory || !node || selectedScenarioHistory.node !== node) {
            banner.classList.add('hidden');
            banner.innerHTML = '';
            return;
        }
        banner.innerHTML = '';
        banner.classList.remove('hidden');
        banner.appendChild(el('span', 'context-banner-label', 'Historical view'));
        banner.appendChild(document.createTextNode(' Viewing historical run ' + (selectedScenarioHistory.runSummary && selectedScenarioHistory.runSummary.runAt ? formatRunTimestamp(selectedScenarioHistory.runSummary.runAt) : 'selected') + ' for this scenario.'));
        var resetBtn = el('button', 'context-banner-reset-btn', 'Back to current');
        resetBtn.type = 'button';
        resetBtn.addEventListener('click', function () {
            clearSelectedScenarioHistory();
            renderDetail();
        });
        banner.appendChild(resetBtn);
    }

    function clearScenarioHistoryBanner() {
        var banner = document.getElementById('ohirok-report-context-banner');
        if (!banner) return;
        banner.classList.add('hidden');
        banner.innerHTML = '';
    }

    function resetScenarioHistorySelection() {
        clearSelectedScenarioHistory();
        clearScenarioHistoryBanner();
        renderDetail();
    }

    function toggleScenarioHistorySelection(node, runSummary) {
        if (!node || !runSummary) return;
        if (isSelectedScenarioHistory(node, runSummary)) {
            clearSelectedScenarioHistory();
            renderDetail();
            return;
        }
        setSelectedScenarioHistory(node, runSummary);
        renderDetail();
    }

    var searchInputWrap = null;
    var searchBox = null;
    var advancedSearchToggleBtn = null;
    var advancedSearchResetBtn = null;
    var customFilterToggleBtn = null;
    var customFilterResetBtn = null;
    var statusFilterToggleBtn = null;
    var statusFilterResetBtn = null;
    var globalResetBtn = null;
    // Per-field/per-group reset ("x") buttons - each entry's isActive() checks only ITS OWN field/
    // group's live state, so updateResetButtonStates() can recolor every one of these in one pass without each
    // field needing its own bespoke wiring at every place it mutates (they already all funnel through
    // applyFiltersAndSearch somewhere).
    var resetButtonStates = [];

    // ---------------------------------------------------------------- theme (D8)
    // The button always shows the theme it will switch *to* (click-to-switch-to semantics), with a matching
    // tooltip, so the current state and the action are both unambiguous rather than a plain glyph swap.
    var THEME_KEY = 'ohirok-report-theme';
    var themeBtn = null;
    /*! Icons: Lucide 1.69.0 (https://lucide.dev), ISC License, Copyright (c) 2026 Lucide Icons and Contributors;
        portions from Feather (MIT), Copyright (c) 2013-2022 Cole Bemis. Full text: Rendering/Assets/Lucide-LICENSE.txt. */
    // One family for every control (24px grid, 2px round strokes, outline); the status badges below stay filled on
    // purpose, because there the fill carries the meaning. lucide(name, size) returns ready inline SVG; the set is
    // generated by build/lucide-icons.js.
    var LUCIDE = {
        'chart-column': '<path d="M3 3v16a2 2 0 0 0 2 2h16" /><path d="M18 17V9" /><path d="M13 17V5" /><path d="M8 17v-3" />',
        'printer': '<path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" /><path d="M6 9V3a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v6" /><rect x="6" y="14" width="12" height="8" rx="1" />',
        'refresh-cw': '<path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8" /><path d="M21 3v5h-5" /><path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16" /><path d="M8 16H3v5" />',
        'hash': '<line x1="4" x2="20" y1="9" y2="9" /><line x1="4" x2="20" y1="15" y2="15" /><line x1="10" x2="8" y1="3" y2="21" /><line x1="16" x2="14" y1="3" y2="21" />',
        'square-arrow-out-up-right': '<path d="M21 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h6" /><path d="m21 3-9 9" /><path d="M15 3h6v6" />',
        'moon': '<path d="M20.985 12.486a9 9 0 1 1-9.473-9.472c.405-.022.617.46.402.803a6 6 0 0 0 8.268 8.268c.344-.215.825-.004.803.401" />',
        'sun': '<circle cx="12" cy="12" r="4" /><path d="M12 2v2" /><path d="M12 20v2" /><path d="m4.93 4.93 1.41 1.41" /><path d="m17.66 17.66 1.41 1.41" /><path d="M2 12h2" /><path d="M20 12h2" /><path d="m6.34 17.66-1.41 1.41" /><path d="m19.07 4.93-1.41 1.41" />',
        'sliders-horizontal': '<path d="M10 5H3" /><path d="M12 19H3" /><path d="M14 3v4" /><path d="M16 17v4" /><path d="M21 12h-9" /><path d="M21 19h-5" /><path d="M21 5h-7" /><path d="M8 10v4" /><path d="M8 12H3" />',
        'funnel': '<path d="M10 20a1 1 0 0 0 .553.895l2 1A1 1 0 0 0 14 21v-7a2 2 0 0 1 .517-1.341L21.74 4.67A1 1 0 0 0 21 3H3a1 1 0 0 0-.742 1.67l7.225 7.989A2 2 0 0 1 10 14z" />',
        'funnel-plus': '<path d="M13.354 3H3a1 1 0 0 0-.742 1.67l7.225 7.989A2 2 0 0 1 10 14v6a1 1 0 0 0 .553.895l2 1A1 1 0 0 0 14 21v-7a2 2 0 0 1 .517-1.341l1.218-1.348" /><path d="M16 6h6" /><path d="M19 3v6" />',
        'x': '<path d="M18 6 6 18" /><path d="m6 6 12 12" />',
        'chevron-left': '<path d="m15 18-6-6 6-6" />',
        'chevron-right': '<path d="m9 18 6-6-6-6" />',
        'panel-left-close': '<rect width="18" height="18" x="3" y="3" rx="2" /><path d="M9 3v18" /><path d="m16 15-3-3 3-3" />',
        'panel-left-open': '<rect width="18" height="18" x="3" y="3" rx="2" /><path d="M9 3v18" /><path d="m14 9 3 3-3 3" />',
        'maximize-2': '<path d="M15 3h6v6" /><path d="m21 3-7 7" /><path d="m3 21 7-7" /><path d="M9 21H3v-6" />',
        'download': '<path d="M12 15V3" /><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" /><path d="m7 10 5 5 5-5" />',
        'zoom-in': '<circle cx="11" cy="11" r="8" /><line x1="21" x2="16.65" y1="21" y2="16.65" /><line x1="11" x2="11" y1="8" y2="14" /><line x1="8" x2="14" y1="11" y2="11" />',
        'zoom-out': '<circle cx="11" cy="11" r="8" /><line x1="21" x2="16.65" y1="21" y2="16.65" /><line x1="8" x2="14" y1="11" y2="11" />',
        'file': '<path d="M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z" /><path d="M14 2v5a1 1 0 0 0 1 1h5" />',
        'external-link': '<path d="M15 3h6v6" /><path d="M10 14 21 3" /><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />',
        'history': '<path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" /><path d="M3 3v5h5" /><path d="M12 7v5l4 2" />',
        'settings': '<path d="M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915" /><circle cx="12" cy="12" r="3" />',
        'layers': '<path d="M12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83z" /><path d="M2 12a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 12" /><path d="M2 17a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 17" />',
        'play': '<path d="M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z" />',
        'folder': '<path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z" />',
        'shield': '<path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z" />',
        'rows-3': '<rect width="18" height="18" x="3" y="3" rx="2" /><path d="M21 9H3" /><path d="M21 15H3" />',
        'table': '<path d="M12 3v18" /><rect width="18" height="18" x="3" y="3" rx="2" /><path d="M3 9h18" /><path d="M3 15h18" />',
        'file-text': '<path d="M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z" /><path d="M14 2v5a1 1 0 0 0 1 1h5" /><path d="M10 9H8" /><path d="M16 13H8" /><path d="M16 17H8" />',
        'rotate-ccw': '<path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" /><path d="M3 3v5h5" />',
        'chevrons-up-down': '<path d="m7 15 5 5 5-5" /><path d="m7 9 5-5 5 5" />',
        'chevrons-down-up': '<path d="m7 20 5-5 5 5" /><path d="m7 4 5 5 5-5" />'
    };

    function lucide(name, size) {
        var s = size || 16;
        return '<svg viewBox="0 0 24 24" width="' + s + '" height="' + s + '" fill="none" stroke="currentColor" stroke-width="2"' +
            ' stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + (LUCIDE[name] || '') + '</svg>';
    }

    var MOON_ICON = lucide('moon', 18);
    var SUN_ICON = lucide('sun', 18);

    function currentTheme() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    function updateThemeButton() {
        if (!themeBtn) return;
        var next = currentTheme() === 'dark' ? 'light' : 'dark';
        themeBtn.innerHTML = next === 'dark' ? MOON_ICON : SUN_ICON;
        themeBtn.title = next === 'dark' ? 'Switch to dark theme' : 'Switch to light theme';
    }

    function toggleTheme() {
        var next = currentTheme() === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', next);
        try { localStorage.setItem(THEME_KEY, next); } catch (e) { /* storage unavailable - theme just won't persist */ }
        updateThemeButton();
        redrawTopRow();
    }

    // ---------------------------------------------------------------- dom helpers
    function el(tag, className, text) {
        var e = document.createElement(tag);
        if (className) e.className = className;
        if (text != null) e.textContent = text;
        return e;
    }

    // Left-positioned status glyphs - a dedicated icon carries "how did it go", fully decoupled
    // from the neutral kind-icon glyph ("what is this row") below. All circle-based, same convention as Allure's
    // circular status badges and stock LivingDoc/Reqnroll icon reports, so the shape is familiar out of the box.
    // currentColor + a .status-<Status> color class (see report.css) does the tinting, same technique as KIND_ICON_SVG.
    // Filled shape + background-colored ("cutout") glyph instead of an outline-only colored glyph -
    // a solid status-colored disk with the mark cut out reads far more contrastly at a glance (VS Test
    // Explorer convention) than a thin colored outline does. var(--bg-color) works as the cutout color in
    // both themes because the palette is already complementary: light theme's status colors are medium/dark
    // (legible on a near-white page), dark theme's are light/pastel (legible on a near-black page) - either
    // way the page background reads clearly against the filled status color.
    var STATUS_ICON_SVG = {
        Passed: '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="6.5" fill="currentColor"/><path d="M5 8.2 L7.2 10.4 L11.2 5.8" fill="none" stroke="var(--bg-color)" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        Failed: '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="6.5" fill="currentColor"/><path d="M5.5 5.5 L10.5 10.5 M10.5 5.5 L5.5 10.5" stroke="var(--bg-color)" stroke-width="1.5" stroke-linecap="round"/></svg>',
        PassWithIssues: '<svg viewBox="0 0 16 16"><path d="M8 2.2 L14 13 H2 Z" fill="currentColor" stroke="currentColor" stroke-width="1" stroke-linejoin="round"/><line x1="8" y1="6.6" x2="8" y2="9.4" stroke="var(--bg-color)" stroke-width="1.4" stroke-linecap="round"/><circle cx="8" cy="11.2" r="0.7" fill="var(--bg-color)"/></svg>',
        Skipped: '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="6.5" fill="currentColor"/><path d="M5.5 5 L8.2 8 L5.5 11" fill="none" stroke="var(--bg-color)" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/><path d="M9 5 L11.7 8 L9 11" fill="none" stroke="var(--bg-color)" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        Pending: '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="6.5" fill="currentColor"/><path d="M8 4.5 V8 L10.5 9.8" fill="none" stroke="var(--bg-color)" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        Undefined: '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="6.5" fill="currentColor"/><path d="M6.2 6.3 A1.9 1.7 0 1 1 8.6 8.7 C8.1 9 8 9.3 8 9.9" fill="none" stroke="var(--bg-color)" stroke-width="1.3" stroke-linecap="round"/><circle cx="8" cy="11.6" r="0.65" fill="var(--bg-color)"/></svg>',
        Ambiguous: '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="6.5" fill="currentColor"/><path d="M5.5 5 L8 8 L5.5 11 M10.5 5 L8 8 L10.5 11" fill="none" stroke="var(--bg-color)" stroke-width="1.3" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        NotRun: '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="6.5" fill="currentColor"/><line x1="5.2" y1="8" x2="10.8" y2="8" stroke="var(--bg-color)" stroke-width="1.5" stroke-linecap="round"/></svg>',
        Unknown: '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="6.5" fill="currentColor"/><circle cx="8" cy="8" r="4.3" fill="none" stroke="var(--bg-color)" stroke-width="1" stroke-dasharray="1.4 1.3"/></svg>'
    };

    function statusIcon(status) {
        var icon = el('span', 'status-icon status-' + status);
        icon.innerHTML = STATUS_ICON_SVG[status] || STATUS_ICON_SVG.Unknown;
        icon.title = status;
        return icon;
    }

    // Custom-filter matches render as small colored chips - same visual language as a Gherkin @tag
    // pill, just tinted with the match's configured bg/fg, instead of the old always-on unlabeled dot.
    // Black or white, whichever reads better on the given CSS colour (any syntax the browser understands).
    var readableTextCache = {};
    function readableTextOn(color) {
        if (!color) return '';
        if (readableTextCache[color]) return readableTextCache[color];
        var probe = document.createElement('span');
        probe.style.color = color;
        document.body.appendChild(probe);
        var rgb = (getComputedStyle(probe).color.match(/[\d.]+/g) || []).slice(0, 3).map(Number);
        document.body.removeChild(probe);
        var result = '#ffffff';
        if (rgb.length === 3) {
            var lum = rgb.map(function (v) { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); });
            var l = 0.2126 * lum[0] + 0.7152 * lum[1] + 0.0722 * lum[2];
            result = (l + 0.05) / 0.05 > 1.05 / (l + 0.05) ? '#000000' : '#ffffff';
        }
        return readableTextCache[color] = result;
    }

    function filterMatchChip(match) {
        var chip = el('span', 'tag filter-match-chip', match.label);
        chip.style.background = match.bg;
        chip.style.color = match.fg || readableTextOn(match.bg);
        chip.title = match.group ? (match.group + ': ' + match.label) : match.label;
        return chip;
    }

    // Structural "what is this row" glyphs for the tree sidebar, placed before the name - orthogonal to the
    // colored status dot (which answers "how did it go" and stays after the name). Monochrome/currentColor
    // since kind carries no health meaning. Self-contained inline SVG, same rationale as STATUS_ICON_SVG.
    var KIND_ICON_SVG = {
        run: lucide('play'),
        // The feature keeps the Cucumber mark in its own colours, like a logo; every other kind is a theme-coloured
        // Lucide outline.
        folder: lucide('folder'),
        // Official Cucumber logo mark (user-supplied) - a filled circle with a speech-bubble tail and
        // seven seeds, scaled to fit the 1rem box via .kind-icon svg's width/height:100% (viewBox stays its
        // original 415x480, no need to re-derive coordinates for a 16x16 grid).
        feature: '<svg viewBox="0 0 415 480"><path fill="#00A818" d="M170,415 L170,478 C260,470 330,420 372,345 A207,207 0 1 0 170,415 Z"/><g fill="#fff" transform="translate(207 207)"><ellipse cx="0" cy="-100" rx="24" ry="38" transform="rotate(-25)"/><ellipse cx="0" cy="-100" rx="24" ry="38" transform="rotate(25)"/><ellipse cx="0" cy="-100" rx="24" ry="38" transform="rotate(-65)"/><ellipse cx="0" cy="-100" rx="24" ry="38" transform="rotate(-114)"/><ellipse cx="0" cy="-100" rx="24" ry="38" transform="rotate(115)"/><ellipse cx="0" cy="-100" rx="24" ry="38" transform="rotate(-155)"/><ellipse cx="0" cy="-100" rx="24" ry="38" transform="rotate(155)"/></g></svg>',
        rule: lucide('shield'),
        scenarioOutline: lucide('rows-3'),
        exampleSet: lucide('table'),
        scenario: lucide('file-text'),
        attempt: lucide('rotate-ccw'),
        root: lucide('layers')
    };

    var KIND_LABELS = {
        run: 'Run',
        folder: 'Folder',
        feature: 'Feature',
        rule: 'Rule',
        scenarioOutline: 'Scenario Outline',
        exampleSet: 'Examples',
        scenario: 'Scenario',
        attempt: 'Attempt',
        root: 'All runs'
    };

    // Spliced into the scenario glyph when a scenario has more than one attempt - a compact "retry" marker on the
    // scenario's own row, distinct from the per-attempt child rows (kind: 'attempt', see createAttemptTreeNode)
    // that let each attempt be focused independently. The cutout circle uses var(--bg-color) so the
    // loop-arrow mark reads correctly against either theme, matching the STATUS_ICON_SVG cutout-badge technique.
    var RETRY_BADGE_SVG = '<g transform="scale(1.5)" stroke-width="1" stroke-linecap="butt"><circle cx="12" cy="12" r="4.6" fill="var(--bg-color)"/><path d="M10.3 10.1 A2.6 2.6 0 1 1 9.8 13.6" fill="none" stroke="currentColor" stroke-width="1" stroke-linecap="round"/><path d="M10.3 8.3 L10.3 10.1 L12.1 10.1 Z" fill="currentColor"/></g>';

    function kindIcon(kind, retried) {
        var svg = KIND_ICON_SVG[kind];
        if (!svg) return null;
        if (retried) svg = svg.replace('</svg>', RETRY_BADGE_SVG + '</svg>');
        var icon = el('span', 'kind-icon kind-' + kind + (retried ? ' kind-retried' : ''));
        icon.innerHTML = svg;
        icon.title = KIND_LABELS[kind] + (retried ? ' (retried)' : '');
        return icon;
    }

    function tagsSpan(tags, extraClass) {
        var span = el('span', 'tags-group');
        (tags || []).forEach(function (t) { span.appendChild(el('span', 'tag' + (extraClass ? ' ' + extraClass : ''), t)); });
        return span;
    }

    // ---------------------------------------------------------------- Gherkin syntax highlighting
    function keywordSpan(text, secondary) {
        return el('span', secondary ? 'gh-keyword-secondary' : 'gh-keyword', text);
    }

    // Pragmatic approximation of Cucumber-expression captured-argument coloring, without expression-pattern
    // data on the wire: quoted substrings and standalone numbers in step text get highlighted.
    var STEP_PARAMETER_RE = /"[^"]*"|\b\d+(?:\.\d+)?\b/g;

    function appendStepText(container, text) {
        var idx = 0;
        var match;
        STEP_PARAMETER_RE.lastIndex = 0;
        while ((match = STEP_PARAMETER_RE.exec(text))) {
            appendPlainText(container, text.slice(idx, match.index));
            container.appendChild(el('span', 'gh-parameter', match[0]));
            idx = match.index + match[0].length;
        }
        appendPlainText(container, text.slice(idx));
    }

    // Same "#token" scan as StaticHashtagAssociator/HashtagExtractor (C#) - a run of non-whitespace starting
    // right after a '#', so description/comment text highlights hashtags the same way the tree/detail chips do.
    var DESCRIPTION_HASHTAG_RE = /#\S+/g;

    function appendTextWithHashtags(container, text, hashtagClass) {
        var idx = 0;
        var match;
        DESCRIPTION_HASHTAG_RE.lastIndex = 0;
        while ((match = DESCRIPTION_HASHTAG_RE.exec(text))) {
            appendPlainText(container, text.slice(idx, match.index));
            container.appendChild(el('span', hashtagClass, match[0]));
            idx = match.index + match[0].length;
        }
        appendPlainText(container, text.slice(idx));
    }

    function renderDescription(container, description) {
        if (!description) return;
        var div = el('div', 'description');
        appendTextWithHashtags(div, description, 'description-hashtag');
        container.appendChild(div);
    }

    // Feature-file comment lines immediately above a Feature/Rule/Scenario/Scenario Outline/Examples header or a
    // step - same hashtag highlighting as the description,
    // so a "#knownBug" note reads consistently everywhere it appears. Renders as <li> inside a step <ul>/<ol> (so it
    // stays valid list markup alongside the sibling step <li>s) and as a plain <div> everywhere else.
    function renderNodeComment(container, comment) {
        if (!comment) return;
        var tag = (container.tagName === 'UL' || container.tagName === 'OL') ? 'li' : 'div';
        var node = el(tag, 'node-comment');
        appendTextWithHashtags(node, comment, 'description-hashtag');
        container.appendChild(node);
    }

    // step.hashtags (bordered pills, like scenario.hashtags/tags elsewhere) and step.comment (the full raw
    // .feature-file comment text) come from the SAME source comment line(s) - step.hashtags is just the
    // #token substrings extracted from it server-side (StaticHashtagAssociator.ExtractHashtags). Rendering
    // both verbatim would show every hashtag token twice; stripping the token text out of the comment before
    // rendering it as prose leaves only genuine leftover prose (and leaves pure "#timeout:5000"-only comment
    // lines with nothing left to render at all, once their token is already shown as a pill above).
    function stripHashtagTokens(comment) {
        if (!comment) return '';
        return comment
            .split('\n')
            .map(function (line) { return line.replace(DESCRIPTION_HASHTAG_RE, '').trim(); })
            .filter(function (line) { return line.length > 0; })
            .join('\n');
    }

    // Bordered pills for a step's #hashtag annotations (Ohirok.StepHashtags.ReqnrollPlugin convention),
    // reusing the same .tag.hashtag chip style scenario-level hashtags already get via tagsSpan - previously
    // these were only ever shown as plain unbordered text inside the step's comment line, with no visual
    // anchor distinguishing them from prose. Rendered as its own <li> immediately before the step's own <li>
    // in the same list, gated by the same comments-hidden toggle (#hashtags are comment-line content).
    function renderStepHashtags(list, hashtags, hasComment) { /* superseded by buildStepHashtagsBadge */ }

    // Hashtags are part of the step comment: when the comment is shown they are only highlighted inline in it;
    // when comments are hidden a "N hashtag(s)" toggle (same pattern as the hooks badge) reveals them on demand.
    function buildStepHashtagsBadge(step, hasComment) {
        var tags = step.hashtags || [];
        if (!tags.length) return null;
        var label = tags.length + (tags.length === 1 ? ' hashtag' : ' hashtags');
        var cls = hasComment ? ' has-comment' : '';
        var badge = el('button', 'step-hooks-badge step-hashtags-toggle' + cls, label);
        badge.type = 'button';
        badge.title = label;
        var block = el('li', 'step-hashtags-block collapsed' + cls);
        tags.forEach(function (t) {
            block.appendChild(el('span', 'description-hashtag', t.charAt(0) === '#' ? t : '#' + t));
            block.appendChild(document.createTextNode(' '));
        });
        var expanded = false;
        badge.addEventListener('click', function () {
            expanded = !expanded;
            block.classList.toggle('collapsed', !expanded);
            badge.classList.toggle('active', expanded);
            badge.textContent = expanded ? 'Hide hashtags' : label;
        });
        return { badge: badge, block: block };
    }

    // ---------------------------------------------------------------- embed tags + attachments (D9)
    var EMBED_TAG_RE = /<ohirok-embed((?:\s+[\w-]+="[^"]*")*)\s*>([\s\S]*?)<\/ohirok-embed>/g;
    var ATTR_RE = /([\w-]+)="([^"]*)"/g;

    function unescapeHtml(value) {
        return value.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&amp;/g, '&');
    }

    function parseAttrs(attrsText) {
        var attrs = {};
        var m;
        ATTR_RE.lastIndex = 0;
        while ((m = ATTR_RE.exec(attrsText))) attrs[m[1]] = unescapeHtml(m[2]);
        return attrs;
    }

    function appendPlainText(container, text) {
        if (text) container.appendChild(document.createTextNode(text));
    }

    // Shared by real image attachments (renderAttachment) and image-url embeds (renderEmbedPayload).
    // Standard "media frame" pattern: a slim caption bar on the frame's own border carries the Expand and
    // Download icon buttons, so the actions never cover the thumbnail itself. Clicking the thumbnail also expands.
    var ICON = {
        expand: lucide('maximize-2'),
        download: lucide('download'),
        close: lucide('x'),
        zoomIn: lucide('zoom-in'),
        zoomOut: lucide('zoom-out'),
        file: lucide('file'),
        external: lucide('external-link')
    };

    function actionButton(svg, label, onClick, cls) {
        var b = el('button', 'media-action' + (cls ? ' ' + cls : ''));
        b.type = 'button';
        b.title = label;
        b.setAttribute('aria-label', label);
        b.innerHTML = svg;
        b.addEventListener('click', function (ev) { ev.stopPropagation(); onClick(ev); });
        return b;
    }

    // data:/blob: URLs download directly; remote URLs are fetched into a blob first (the `download` attribute is
    // ignored cross-origin), falling back to opening the URL in a new tab when the host blocks CORS.
    function downloadMedia(src, name) {
        var filename = name || 'attachment';
        function viaAnchor(href, blank) {
            var a = document.createElement('a');
            a.href = href;
            a.download = filename;
            if (blank) { a.target = '_blank'; a.rel = 'noopener'; }
            document.body.appendChild(a);
            a.click();
            a.remove();
        }
        if (/^(data:|blob:)/i.test(src)) { viaAnchor(src); return; }
        fetch(src)
            .then(function (r) { if (!r.ok) throw new Error('http ' + r.status); return r.blob(); })
            .then(function (blob) {
                var u = URL.createObjectURL(blob);
                viaAnchor(u);
                setTimeout(function () { URL.revokeObjectURL(u); }, 5000);
            })
            .catch(function () { viaAnchor(src, true); });
    }

    function formatBytes(n) {
        if (!n && n !== 0) return '';
        if (n < 1024) return n + ' B';
        if (n < 1048576) return (n / 1024).toFixed(1) + ' KB';
        return (n / 1048576).toFixed(1) + ' MB';
    }

    function buildMediaFrame(kind, title, actionsBuilder) {
        var frame = el('figure', 'media-frame media-frame-' + kind);
        var bar = el('figcaption', 'media-frame-bar');
        bar.appendChild(el('span', 'media-frame-title', title));
        var actions = el('span', 'media-frame-actions');
        actionsBuilder(actions);
        bar.appendChild(actions);
        frame.appendChild(bar);
        return frame;
    }

    function buildImageWithActions(className, src, alt, downloadName) {
        var title = downloadName || alt || 'Image';
        var frame = buildMediaFrame('image', title, function (actions) {
            actions.appendChild(actionButton(ICON.expand, 'Expand', function () { openAttachmentLightbox(src, alt, title); }));
            actions.appendChild(actionButton(ICON.download, 'Download', function () { downloadMedia(src, downloadName || alt); }));
        });
        var thumb = el('button', 'media-frame-thumb');
        thumb.type = 'button';
        thumb.title = 'Click to expand';
        thumb.setAttribute('aria-label', 'Expand image ' + title);
        var img = el('img', className);
        img.src = src;
        img.alt = alt || '';
        img.loading = 'lazy';
        img.addEventListener('error', function () {
            thumb.replaceWith(el('div', 'media-frame-error', 'Image could not be loaded' + (/^https?:/i.test(src) ? ' (offline or blocked?)' : '')));
        });
        thumb.appendChild(img);
        thumb.addEventListener('click', function () { openAttachmentLightbox(src, alt, title); });
        frame.appendChild(thumb);
        return frame;
    }

    function renderEmbedPayload(container, embed) {
        if (!embed || !embed.kind) return;
        if (embed.kind === 'image-url') {
            container.appendChild(buildImageWithActions('embed-image', embed.url, embed.alt, embed.alt));
        } else if (embed.kind === 'image-data') {
            // EmbedImage(bytes|path) inlines the image as a data: URI; anything else is not an image and is dropped.
            if (/^data:image\//i.test(embed.data || '')) {
                container.appendChild(buildImageWithActions('embed-image', embed.data, embed.alt, embed.alt));
            }
        } else if (embed.kind === 'callout') {
            var callout = el('div', 'embed-callout embed-callout-' + embed.level);
            callout.innerHTML = embed.message;
            container.appendChild(callout);
        } else if (embed.kind === 'raw-html') {
            var raw = el('div', 'embed-raw-html');
            raw.innerHTML = embed.html;
            container.appendChild(raw);
        }
    }

    // Mirrors IssueMarkerParser.Scan's tag-scanning loop, but only recognizes <ohirok-embed> - <ohirok-issue> tags
    // were already stripped server-side and never reach the client.
    //
    // Embeds (callout/raw-html/image-url/image-data) are already self-styled boxes - only the surrounding
    // plain-text runs get wrapped in the monospace <pre>, so an embed never ends up double-boxed inside it.
    function renderTextWithEmbeds(container, text) {
        var idx = 0;
        var match;
        var plainWrap = null;
        function flushPlain(segment) {
            if (!segment) return;
            if (!plainWrap) {
                plainWrap = el('pre', 'attachment-text');
                container.appendChild(plainWrap);
            }
            appendPlainText(plainWrap, segment);
        }
        EMBED_TAG_RE.lastIndex = 0;
        while ((match = EMBED_TAG_RE.exec(text))) {
            flushPlain(text.slice(idx, match.index));
            plainWrap = null; // next plain-text run (if any) gets its own <pre>, not glued to this embed

            var attrs = parseAttrs(match[1]);
            var kind = attrs.kind;
            if (kind === 'image-url') renderEmbedPayload(container, { kind: kind, url: attrs.url, alt: attrs.alt });
            else if (kind === 'image-data') renderEmbedPayload(container, { kind: kind, data: attrs.data, alt: attrs.alt });
            else if (kind === 'callout') renderEmbedPayload(container, { kind: kind, level: attrs.level, message: match[2] });
            else if (kind === 'raw-html') renderEmbedPayload(container, { kind: kind, html: match[2] });

            idx = match.index + match[0].length;
        }
        flushPlain(text.slice(idx));
    }

    // Short message shown by default - the stack trace is almost always noise until you actually need
    // it, so it stays behind an explicit toggle rather than always-visible or a separate attachment. Uses a plain
    // <details>/<summary> disclosure, matching the Before/After hooks convention (renderHooksBlock) instead of a
    // bespoke button+text-toggle.
    function renderErrorBlock(container, className, errorMessage, stackTrace) {
        if (!errorMessage) return;
        container.appendChild(el('div', className, errorMessage));
        if (!stackTrace) return;

        var details = document.createElement('details');
        details.className = className + '-trace-block';
        details.appendChild(el('summary', className + '-trace-toggle', 'Full trace'));
        details.appendChild(el('pre', className + '-trace', stackTrace));
        container.appendChild(details);
    }

    // Minimal standard image viewer: dark backdrop, top toolbar (zoom out / zoom % / zoom in / download / close),
    // a natively scrollable stage (scrollbars + drag-to-pan + Ctrl/Cmd+wheel zoom), keyboard support
    // (Esc closes, + / - / 0 zoom, arrows scroll), focus trap and focus restore. No external dependency - the
    // report must stay a single self-contained file.
    function openAttachmentLightbox(src, alt, name) {
        var opener = document.activeElement;
        var title = name || alt || 'Image';
        var overlay = el('div', 'lightbox');
        overlay.setAttribute('role', 'dialog');
        overlay.setAttribute('aria-modal', 'true');
        overlay.setAttribute('aria-label', title);

        var bar = el('div', 'lightbox-bar');
        bar.appendChild(el('span', 'lightbox-title', title));
        var tools = el('div', 'lightbox-tools');
        var zoomOutBtn = actionButton(ICON.zoomOut, 'Zoom out (-)', function () { setZoom(zoom / 1.25); }, 'lightbox-btn');
        var zoomLabel = el('button', 'lightbox-zoom-label', '100%');
        zoomLabel.type = 'button';
        zoomLabel.title = 'Fit to window (0) · double-click image for 100%';
        var zoomInBtn = actionButton(ICON.zoomIn, 'Zoom in (+)', function () { setZoom(zoom * 1.25); }, 'lightbox-btn');
        var dlBtn = actionButton(ICON.download, 'Download', function () { downloadMedia(src, name || alt); }, 'lightbox-btn');
        var closeBtn = actionButton(ICON.close, 'Close (Esc)', dismiss, 'lightbox-btn lightbox-close');
        [zoomOutBtn, zoomLabel, zoomInBtn, dlBtn, closeBtn].forEach(function (b) { tools.appendChild(b); });
        bar.appendChild(tools);
        overlay.appendChild(bar);

        var stage = el('div', 'lightbox-stage');
        stage.tabIndex = 0;
        var img = el('img');
        img.alt = alt || '';
        img.draggable = false;
        stage.appendChild(img);
        overlay.appendChild(stage);

        var zoom = 1;          // relative to "fit to window"
        var fit = 1;           // fit-to-window scale vs natural size (never upscales past 100%)
        var MIN_ZOOM = 0.25, MAX_ZOOM = 16;

        function apply() {
            if (!img.naturalWidth) return;
            img.style.width = Math.round(img.naturalWidth * fit * zoom) + 'px';
            zoomLabel.textContent = Math.round(fit * zoom * 100) + '%';
            stage.classList.toggle('pannable', stage.scrollWidth > stage.clientWidth + 1 || stage.scrollHeight > stage.clientHeight + 1);
        }
        function computeFit() {
            if (!img.naturalWidth) return;
            var cs = getComputedStyle(stage);
            var w = stage.clientWidth - parseFloat(cs.paddingLeft) - parseFloat(cs.paddingRight);
            var h = stage.clientHeight - parseFloat(cs.paddingTop) - parseFloat(cs.paddingBottom);
            fit = Math.min(1, w / img.naturalWidth, h / img.naturalHeight);
            apply();
        }
        function setZoom(z, clientX, clientY) {
            z = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, z));
            if (z === zoom) return;
            var r = stage.getBoundingClientRect();
            var ax = (clientX == null ? r.left + r.width / 2 : clientX) - r.left;
            var ay = (clientY == null ? r.top + r.height / 2 : clientY) - r.top;
            var rx = (stage.scrollLeft + ax - img.offsetLeft) / (img.offsetWidth || 1);
            var ry = (stage.scrollTop + ay - img.offsetTop) / (img.offsetHeight || 1);
            zoom = z;
            apply();
            stage.scrollLeft = img.offsetLeft + rx * img.offsetWidth - ax;
            stage.scrollTop = img.offsetTop + ry * img.offsetHeight - ay;
        }
        function resetFit() { zoom = 1; apply(); stage.scrollLeft = 0; stage.scrollTop = 0; }

        img.addEventListener('load', computeFit);
        img.addEventListener('error', function () { stage.replaceChildren(el('div', 'media-frame-error', 'Image could not be loaded')); });
        img.src = src;
        zoomLabel.addEventListener('click', resetFit);
        img.addEventListener('dblclick', function (ev) {
            if (Math.abs(fit * zoom - 1) < 0.01) resetFit(); else setZoom(1 / fit, ev.clientX, ev.clientY);
        });
        stage.addEventListener('wheel', function (ev) {
            if (!ev.ctrlKey && !ev.metaKey) return; // plain wheel = native scroll
            ev.preventDefault();
            setZoom(zoom * (ev.deltaY < 0 ? 1.15 : 1 / 1.15), ev.clientX, ev.clientY);
        }, { passive: false });

        var drag = null;
        stage.addEventListener('mousedown', function (ev) {
            if (ev.button !== 0 || !stage.classList.contains('pannable')) return;
            ev.preventDefault();
            drag = { x: ev.clientX, y: ev.clientY, l: stage.scrollLeft, t: stage.scrollTop };
            stage.classList.add('dragging');
        });
        function onMove(ev) {
            if (!drag) return;
            stage.scrollLeft = drag.l - (ev.clientX - drag.x);
            stage.scrollTop = drag.t - (ev.clientY - drag.y);
        }
        function onUp() { drag = null; stage.classList.remove('dragging'); }
        window.addEventListener('mousemove', onMove);
        window.addEventListener('mouseup', onUp);

        function onKey(ev) {
            if (ev.key === 'Escape') { ev.preventDefault(); dismiss(); return; }
            if (ev.key === '+' || ev.key === '=') { ev.preventDefault(); setZoom(zoom * 1.25); }
            else if (ev.key === '-' || ev.key === '_') { ev.preventDefault(); setZoom(zoom / 1.25); }
            else if (ev.key === '0') { ev.preventDefault(); resetFit(); }
            else if (ev.key === 'Tab') {
                var f = Array.prototype.slice.call(overlay.querySelectorAll('button, [tabindex="0"]'));
                if (!f.length) return;
                var first = f[0], last = f[f.length - 1];
                if (ev.shiftKey && document.activeElement === first) { ev.preventDefault(); last.focus(); }
                else if (!ev.shiftKey && document.activeElement === last) { ev.preventDefault(); first.focus(); }
            }
        }
        document.addEventListener('keydown', onKey);
        window.addEventListener('resize', computeFit);
        // Backdrop click (outside the image) closes, like every mainstream lightbox.
        stage.addEventListener('click', function (ev) { if (ev.target === stage) dismiss(); });

        function dismiss() {
            document.removeEventListener('keydown', onKey);
            window.removeEventListener('resize', computeFit);
            window.removeEventListener('mousemove', onMove);
            window.removeEventListener('mouseup', onUp);
            overlay.remove();
            if (opener && opener.focus) { try { opener.focus(); } catch (e) { /* element gone */ } }
        }

        document.body.appendChild(overlay);
        closeBtn.focus();
    }

    // A stdout-echoed hashtag line (e.g. "#knownBug") duplicates text already highlighted inline in the
    // step/scenario comment above it - matches only a bare "#token" line, not arbitrary
    // stdout that happens to contain '#', so unrelated log output is never dropped.
    function isCommentEchoAttachment(body, ownerComment) {
        if (!ownerComment) return false;
        var trimmed = body.trim();
        return /^#\S+$/.test(trimmed) && ownerComment.indexOf(trimmed) !== -1;
    }

    function attachmentSource(a) {
        if (a.url) return a.url;
        if (a.isBase64Encoded) return 'data:' + (a.mediaType || 'application/octet-stream') + ';base64,' + a.body;
        return a.body;
    }

    var TEXT_PREVIEW_MAX_BYTES = 256 * 1024;
    var TEXT_FILE_RE = /\.(txt|log|csv|tsv|json|xml|ya?ml|md|html?|ini|cfg|conf|properties)$/i;

    // The attachment's content as text when it is a reasonably small text file, else null.
    function attachmentText(a) {
        if (!a.body) return null;
        var type = a.mediaType || '';
        var textual = /^text\//.test(type) || /(json|xml|yaml|csv)/.test(type) || TEXT_FILE_RE.test(a.fileName || '');
        if (!textual || (attachmentByteSize(a) || 0) > TEXT_PREVIEW_MAX_BYTES) return null;
        if (!a.isBase64Encoded) return a.body;
        try {
            var binary = atob(a.body);
            var bytes = new Uint8Array(binary.length);
            for (var i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
            return new TextDecoder('utf-8', { fatal: true }).decode(bytes);
        } catch (e) {
            return null; // not valid UTF-8 after all: keep the file card
        }
    }

    function attachmentByteSize(a) {
        if (!a.body) return null;
        if (!a.isBase64Encoded) return a.body.length;
        var pad = /=*$/.exec(a.body)[0].length;
        return Math.max(0, Math.floor(a.body.length * 3 / 4) - pad);
    }

    // Reqnroll's native attachment API (AddAttachment(path) / EmbedImage) carries a file name + media type + bytes;
    // Ohirok adds the cucumber log channel (text + <ohirok-embed> tags) and an optional external `url`.
    // Every kind gets the same frame (title bar with Download, and Expand for images) so they read as one family.
    function renderAttachment(container, attachment, ownerComment) {
        var type = attachment.mediaType || '';
        var name = attachment.fileName || null;

        if (type.indexOf('image/') === 0 && (attachment.body || attachment.url)) {
            container.appendChild(buildImageWithActions('attachment-image', attachmentSource(attachment), name, name));
            return;
        }

        if (type === 'text/x.cucumber.log+plain' && attachment.body) {
            renderTextWithEmbeds(container, attachment.body);
            return;
        }

        var src = (attachment.body || attachment.url) ? attachmentSource(attachment) : null;

        if ((type.indexOf('video/') === 0 || type.indexOf('audio/') === 0) && src) {
            var isVideo = type.indexOf('video/') === 0;
            var mediaFrame = buildMediaFrame(isVideo ? 'video' : 'audio', name || type, function (actions) {
                actions.appendChild(actionButton(ICON.download, 'Download', function () { downloadMedia(src, name); }));
            });
            var player = document.createElement(isVideo ? 'video' : 'audio');
            player.controls = true;
            player.preload = 'metadata';
            player.src = src;
            player.className = 'media-frame-player';
            mediaFrame.appendChild(player);
            container.appendChild(mediaFrame);
            return;
        }

        // Text files are previewed in place - also when they come base64-encoded, as Reqnroll's AddAttachment sends
        // every file, and also when the extension is the only hint (".log" arrives as application/octet-stream).
        var text = attachmentText(attachment);
        if (text !== null) {
            var isHtml = type === 'text/html' || /\.html?$/i.test(name || '');
            var textFrame = buildMediaFrame('text', name || type, function (actions) {
                actions.appendChild(actionButton(ICON.download, 'Download', function () {
                    downloadMedia(src || ('data:' + type + ';charset=utf-8,' + encodeURIComponent(text)), name || 'attachment.txt');
                }));
            });
            if (isHtml) {
                // Sandboxed: an attached page is shown, never allowed to run scripts in or navigate the report.
                var page = el('iframe', 'attachment-html-frame');
                page.setAttribute('sandbox', '');
                page.title = name || 'HTML attachment';
                page.srcdoc = text;
                textFrame.appendChild(page);
            } else {
                textFrame.appendChild(el('pre', 'attachment-text attachment-text-framed', text));
            }
            container.appendChild(textFrame);
            return;
        }

        // Any other file (pdf, zip, office, ...) and URL-only attachments: a compact file card.
        var card = el('div', 'media-frame media-frame-file');
        var iconEl = el('span', 'file-card-icon');
        iconEl.innerHTML = attachment.url && !attachment.body ? ICON.external : ICON.file;
        card.appendChild(iconEl);
        var meta = el('span', 'file-card-meta');
        meta.appendChild(el('span', 'file-card-name', name || (attachment.url ? attachment.url : (type || 'attachment'))));
        var sub = [type, formatBytes(attachmentByteSize(attachment))].filter(Boolean).join(' · ');
        if (sub) meta.appendChild(el('span', 'file-card-sub', sub));
        card.appendChild(meta);
        if (src) {
            var actions2 = el('span', 'media-frame-actions');
            if (attachment.url && !attachment.body) {
                var open = el('a', 'media-action');
                open.href = attachment.url;
                open.target = '_blank';
                open.rel = 'noopener noreferrer';
                open.title = 'Open in new tab';
                open.setAttribute('aria-label', 'Open in new tab');
                open.innerHTML = ICON.external;
                actions2.appendChild(open);
            } else {
                actions2.appendChild(actionButton(ICON.download, 'Download', function () { downloadMedia(src, name); }));
            }
            card.appendChild(actions2);
        }
        container.appendChild(card);
    }

    // The first <ohirok-embed> tag of a log attachment, as {attrs, content}, or null for plain log text / files.
    function firstEmbed(a) {
        if (a.mediaType !== 'text/x.cucumber.log+plain' || !a.body) return null;
        EMBED_TAG_RE.lastIndex = 0;
        var match = EMBED_TAG_RE.exec(a.body);
        EMBED_TAG_RE.lastIndex = 0;
        return match ? { attrs: parseAttrs(match[1]), content: match[2] } : null;
    }

    // The kind reporting.expandAttachments names: info / warning / error (callouts), image, html, log (WriteLine
    // text), text (text files), media, file.
    function attachmentKind(a) {
        var type = a.mediaType || '';
        var embed = firstEmbed(a);
        if (embed) {
            if (embed.attrs.kind === 'callout') return embed.attrs.level || 'info';
            if (embed.attrs.kind === 'image-url' || embed.attrs.kind === 'image-data') return 'image';
            if (embed.attrs.kind === 'raw-html') return 'html';
        }
        if (type === 'text/x.cucumber.log+plain') return 'log';
        if (type.indexOf('image/') === 0) return 'image';
        if (type.indexOf('video/') === 0 || type.indexOf('audio/') === 0) return 'media';
        if (type === 'text/html' || /\.html?$/i.test(a.fileName || '')) return 'html';
        return attachmentText(a) !== null ? 'text' : 'file';
    }

    // An attachment is "always expanded" (rendered directly, with no "N attachments" disclosure wrapper) when its
    // embed says expanded="true", or - with no explicit expanded="true|false" - when reporting.expandAttachments
    // lists its kind (default: the three callout levels).
    var EXPAND_KINDS = Array.isArray(DISPLAY.expandAttachments) ? DISPLAY.expandAttachments : ['info', 'warning', 'error'];

    function isAlwaysExpanded(a) {
        var embed = firstEmbed(a);
        var explicit = embed && embed.attrs.expanded;
        if (explicit === 'true') return true;
        if (explicit === 'false') return false;
        return EXPAND_KINDS.indexOf('all') >= 0 || EXPAND_KINDS.indexOf(attachmentKind(a)) >= 0;
    }

    var CALLOUT_LABELS = { info: 'Info', warning: 'Warning', error: 'Error' };

    function shortLabel(text) {
        var line = (text || '').replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
        return line.length > 80 ? line.slice(0, 79) + '\u2026' : line;
    }

    // What a collapsed entry in a list of several attachments says about itself: the file name, or for log text the
    // kind of embed it carries (an info/warning/error callout with its message, an image, HTML) or its first words.
    function attachmentSummaryLabel(a) {
        if (a.fileName) return a.fileName;
        if (a.mediaType && a.mediaType.indexOf('image/') === 0) return 'Image';
        if (a.mediaType === 'text/x.cucumber.log+plain') {
            var embed = firstEmbed(a);
            if (embed) {
                var attrs = embed.attrs;
                if (attrs.kind === 'callout') return (CALLOUT_LABELS[attrs.level] || 'Note') + ': ' + shortLabel(embed.content);
                if (attrs.kind === 'image-url' || attrs.kind === 'image-data') return attrs.alt && attrs.alt !== 'embedded-image' ? 'Image: ' + attrs.alt : 'Image';
                if (attrs.kind === 'raw-html') return 'HTML: ' + shortLabel(embed.content);
            }
            return shortLabel(a.body) || 'Log';
        }
        return a.mediaType || 'Attachment';
    }

    // Always-expanded attachments render inline with no wrapper at all. The rest go behind one collapsed
    // "N attachments" disclosure (several -> each independently collapsible), so a scenario with many
    // attachments doesn't clutter the pane before the user asks for them.
    function renderAttachmentsBlock(container, attachments, ownerErrorMessage, ownerComment) {
        if (!attachments || attachments.length === 0) return;
        // Comment-echo attachments (a stdout "#knownBug" line duplicating the highlighted comment) are dropped
        // from both the list and its count.
        var visible = attachments.filter(function (a) {
            return !(a.mediaType === 'text/x.cucumber.log+plain' && a.body && isCommentEchoAttachment(a.body, ownerComment));
        });
        if (visible.length === 0) return;

        var inline = visible.filter(isAlwaysExpanded);
        var folded = visible.filter(function (a) { return !isAlwaysExpanded(a); });

        inline.forEach(function (a) {
            var holder = el('div', 'attachment-inline');
            renderAttachment(holder, a, ownerComment);
            container.appendChild(holder);
        });

        if (folded.length === 0) return;
        var details = document.createElement('details');
        details.className = 'attachments-block';
        details.appendChild(el('summary', null, folded.length + (folded.length === 1 ? ' attachment' : ' attachments')));
        var div = el('div', 'attachments-list');
        if (folded.length === 1) {
            renderAttachment(div, folded[0], ownerComment);
        } else {
            folded.forEach(function (a) {
                var item = document.createElement('details');
                item.className = 'attachment-item';
                item.appendChild(el('summary', 'attachment-item-summary', attachmentSummaryLabel(a)));
                var body = el('div', 'attachment-item-body');
                renderAttachment(body, a, ownerComment);
                item.appendChild(body);
                div.appendChild(item);
            });
        }
        details.appendChild(div);
        container.appendChild(details);
    }

    // ---------------------------------------------------------------- steps / issues
    function renderStepArgument(list, argument) {
        var li = el('li', 'step-argument');
        if (argument.kind === 'docstring') {
            var pre = el('pre', 'step-docstring gh-docstring');
            pre.textContent = argument.text || '';
            li.appendChild(pre);
        } else if (argument.kind === 'datatable') {
            var table = el('table', 'step-datatable gh-datatable');
            (argument.rows || []).forEach(function (row) {
                var tr = document.createElement('tr');
                row.forEach(function (cell) {
                    var td = el('td', null, cell);
                    tr.appendChild(td);
                });
                table.appendChild(tr);
            });
            li.appendChild(table);
        }
        list.appendChild(li);
    }

    // Real Examples table - a literal <thead> from the Examples block's own column headers
    // (examples.TableHeader.Cells) plus one <tbody> row per example, each cell looked up by header key from
    // that row's ArgumentValues. Reuses the existing step-datatable/gh-datatable styling
    // rather than inventing a new table style, but with a real <thead> instead of that pattern's
    // bolded-first-row fake header, since an Examples table always has a true header row in the source file.
    function renderExamplesTable(container, set) {
        var headers = set.headers || [];
        if (!headers.length) return;
        var table = el('table', 'step-datatable gh-datatable examples-table');
        var thead = document.createElement('thead');
        var headRow = document.createElement('tr');
        headers.forEach(function (h) { headRow.appendChild(el('th', null, h)); });
        thead.appendChild(headRow);
        table.appendChild(thead);
        var tbody = document.createElement('tbody');
        (set.scenarios || []).forEach(function (scenario) {
            var tr = document.createElement('tr');
            var values = scenario.argumentValues || {};
            headers.forEach(function (h) { tr.appendChild(el('td', null, values[h] != null ? values[h] : '')); });
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        container.appendChild(table);
    }

    // Small "header: value" chip row for one example row's own raw arguments - shown next to
    // the already-substituted scenario title so the literal placeholder values from the .feature file's
    // Examples row are visible without having to cross-reference the owning ExampleSet's table separately.
    function renderScenarioArguments(container, argumentValues) {
        if (!argumentValues) return;
        var keys = Object.keys(argumentValues);
        if (!keys.length) return;
        var row = el('div', 'scenario-arguments');
        keys.forEach(function (key) {
            var chip = el('span', 'argument-chip');
            chip.appendChild(el('span', 'argument-chip-key', key + ': '));
            chip.appendChild(document.createTextNode(argumentValues[key]));
            row.appendChild(chip);
        });
        container.appendChild(row);
    }

    // BeforeStep/AfterStep hooks render right around the step they wrap, not lumped into the attempt-level
    // Before/After hooks blocks - old SpecFlow LivingDoc precedent: hook output nests inside the step. Following the
    // stock-Reqnroll-report convention - a small inline "+N" badge sits right after the step's own text/tags
    // (not a separate right-aligned row), and toggles a full-width block directly below the step's <li>.
    function buildStepHooksBadge(step) {
        var before = step.beforeHooks || [];
        var after = step.afterHooks || [];
        var count = before.length + after.length;
        if (count === 0) return null;

        var anyIssue = before.concat(after).some(function (h) { return h.errorMessage; });
        var hookLabel = count + (count === 1 ? ' hook' : ' hooks');
        var badge = el('button', 'step-hooks-badge' + (anyIssue ? ' has-issue' : ''), hookLabel);
        badge.type = 'button';
        badge.title = hookLabel;

        var block = el('li', 'step-hooks-block collapsed');
        if (before.length) {
            block.appendChild(el('div', 'step-hooks-block-label', 'Before'));
            var beforeList = el('ul', 'hook-list');
            before.forEach(function (h) { renderHook(beforeList, h); });
            block.appendChild(beforeList);
        }
        if (after.length) {
            block.appendChild(el('div', 'step-hooks-block-label', 'After'));
            var afterList = el('ul', 'hook-list');
            after.forEach(function (h) { renderHook(afterList, h); });
            block.appendChild(afterList);
        }

        var expanded = anyIssue;
        block.classList.toggle('collapsed', !expanded);
        badge.classList.toggle('active', expanded);
        badge.textContent = expanded ? 'Hide hooks' : hookLabel;
        badge.addEventListener('click', function () {
            expanded = !expanded;
            block.classList.toggle('collapsed', !expanded);
            badge.classList.toggle('active', expanded);
            badge.textContent = expanded ? 'Hide hooks' : hookLabel;
        });

        return { badge: badge, block: block };
    }

    function renderStep(list, step) {
        // Full comment text (hashtags highlighted inline) when comments are shown; when hidden, only the
        // #hashtag pills remain (see .step-hashtags-fallback in the CSS).
        var hasComment = !!(step.comment && step.comment.trim());
        if (hasComment) {
            var cLi = el('li', 'node-comment step-comment');
            appendTextWithHashtags(cLi, step.comment.trim(), 'description-hashtag');
            list.appendChild(cLi);
        }
        var tagBadge = buildStepHashtagsBadge(step, hasComment);

        var hooks = buildStepHooksBadge(step);

        var li = el('li', 'step-item step-status-' + step.status);
        // Matches real LivingDoc's visible step-binding info (its generated output embeds the resolved
        // MethodName/StepDefinition per step) - step.pattern is already captured/shipped in the JSON (used
        // internally for pattern-aware search) but was never surfaced to the user. A native tooltip keeps it
        // out of the way for the common case while still making it discoverable, rather than adding a new
        // always-visible line to every step.
        if (step.pattern && step.pattern !== step.text) li.title = step.pattern;
        li.appendChild(statusIcon(step.status));
        li.appendChild(keywordSpan(step.keyword));
        li.appendChild(document.createTextNode(' '));
        appendStepText(li, step.text);
        var stepDuration = formatDuration(step.durationMs);
        if (stepDuration) li.appendChild(el('span', 'step-duration', stepDuration));
        if (hooks) li.appendChild(hooks.badge);
        if (tagBadge) li.appendChild(tagBadge.badge);
        list.appendChild(li);
        if (tagBadge) list.appendChild(tagBadge.block);

        renderErrorBlock(list, 'step-error', step.errorMessage, step.stackTrace);

        if (step.argument) renderStepArgument(list, step.argument);

        if (step.attachments && step.attachments.length) {
            var attLi = el('li', 'step-attachments');
            renderAttachmentsBlock(attLi, step.attachments, step.errorMessage, step.comment);
            list.appendChild(attLi);
        }

        if (hooks) list.appendChild(hooks.block);
    }

    // A Background has no independent pass/fail (Gherkin model) - rendered once per feature/rule as its own
    // collapsible block, not part of the filter/search node graph and not repeated per scenario. Its own
    // Name/Description are independent display metadata (Gherkin allows both on any header), separate from
    // the pass/fail-less-ness of its steps - previously dropped entirely, see report_ui_feedback.txt.
    function renderBackground(container, steps, name, description) {
        if ((!steps || steps.length === 0) && !name && !description) return;
        var details = document.createElement('details');
        details.className = 'background-block';
        var summary = document.createElement('summary');
        summary.appendChild(keywordSpan(name ? 'Background:' : 'Background'));
        // In a flex <summary> a bare text node loses its leading space, so the name is its own spaced element.
        if (name) summary.appendChild(el('span', 'background-name', name));
        details.appendChild(summary);
        renderDescription(details, description);
        var list = el('ul', 'step-list');
        steps.forEach(function (s) { renderStep(list, s); });
        details.appendChild(list);
        container.appendChild(details);
    }

    function renderHook(list, hook) {
        var li = el('li', 'hook-item hook-status-' + hook.status);
        li.appendChild(statusIcon(hook.status));
        li.appendChild(el('span', 'hook-type', hook.type));
        if (hook.sourceReference) li.appendChild(el('span', 'hook-source', hook.sourceReference));
        var hookDuration = formatDuration(hook.durationMs);
        if (hookDuration) li.appendChild(el('span', 'step-duration', hookDuration));
        list.appendChild(li);

        renderErrorBlock(list, 'hook-error', hook.errorMessage, hook.stackTrace);

        if (hook.attachments && hook.attachments.length) {
            var attLi = el('li', 'hook-attachments');
            renderAttachmentsBlock(attLi, hook.attachments, hook.errorMessage);
            list.appendChild(attLi);
        }
    }

    // Surfaces hook failures/output rather than swallowing them - a small collapsible block per attempt,
    // matching both reference reporters' behavior.
    function renderHooksBlock(container, hooks, label) {
        if (!hooks || hooks.length === 0) return;
        var details = document.createElement('details');
        details.className = 'hooks-block';
        var summary = el('summary', 'hooks-block-summary', label || 'Hooks');
        details.appendChild(summary);
        var list = el('ul', 'hook-list');
        hooks.forEach(function (h) { renderHook(list, h); });
        details.appendChild(list);
        container.appendChild(details);
    }

    // Splits a flat hook list by lifecycle phase so callers can render Before* hooks ahead of the step list and
    // After* hooks behind it - matches where they actually run relative to the scenario's steps, instead of
    // lumping every hook (regardless of phase) into one block. BeforeStep/AfterStep hooks land here too (grouped
    // by phase, not yet attributed to their specific step - the model has no per-step hook association yet).
    function splitHooksByPhase(hooks) {
        var before = [], after = [];
        (hooks || []).forEach(function (h) {
            if (h.type && h.type.indexOf('Before') === 0) before.push(h);
            else after.push(h);
        });
        return { before: before, after: after };
    }

    function renderIssues(container, issues) {
        if (!issues || issues.length === 0) return;
        var list = el('ul', 'issue-list');
        issues.forEach(function (i) {
            var li = el('li', 'issue-item issue-' + i.category);
            li.appendChild(el('span', 'issue-category', i.category));
            if (i.severity) li.appendChild(el('span', 'issue-severity', i.severity));
            li.appendChild(document.createTextNode(' ' + i.message));
            list.appendChild(li);
        });
        container.appendChild(list);
    }

    // Retry-group display: a compact chip-sequence summary above the attempts list shows every
    // attempt's outcome at a glance (e.g. Failed -> Failed -> Passed) without expanding each one. Only the
    // final attempt - the one that determines the scenario's overall status - is expanded by default; earlier
    // attempts stay collapsed but are one click away via their chip, which opens and scrolls to that attempt.
    function renderRetrySummary(container, attempts) {
        var wrap = el('div', 'retry-summary');
        wrap.appendChild(el('span', 'retry-summary-label', 'Attempts (' + attempts.length + '):'));
        attempts.forEach(function (a, idx) {
            var chip = el('button', 'retry-chip');
            chip.type = 'button';
            chip.appendChild(statusIcon(a.status));
            chip.appendChild(document.createTextNode('Attempt ' + a.attemptNumber + (a.rerun ? ' · rerun ' + a.rerun : '')));
            chip.addEventListener('click', function () {
                var target = document.getElementById('attempt-block-' + a.attemptNumber);
                if (!target) return;
                target.open = true;
                target.scrollIntoView({ block: 'nearest' });
            });
            wrap.appendChild(chip);
            if (idx < attempts.length - 1) wrap.appendChild(el('span', 'retry-arrow', '→'));
        });
        container.appendChild(wrap);
    }

    function renderAttempt(container, attempt, defaultOpen) {
        var details = document.createElement('details');
        details.id = 'attempt-block-' + attempt.attemptNumber;
        details.open = !!defaultOpen;
        var summary = el('summary', null);
        var label = 'Attempt ' + attempt.attemptNumber + (attempt.rerun ? ' · rerun ' + attempt.rerun : '') + (attempt.willBeRetried ? ' (retried)' : '');
        summary.appendChild(statusIcon(attempt.status));
        summary.appendChild(el('span', 'attempt-label', label));
        details.appendChild(summary);

        var attemptHookPhases = splitHooksByPhase(attempt.hooks);
        renderHooksBlock(details, attemptHookPhases.before, 'Before hooks');

        var steps = el('ul', 'step-list');
        (attempt.steps || []).forEach(function (s) { renderStep(steps, s); });
        details.appendChild(steps);
        renderAttachmentsBlock(details, attempt.attachments);
        renderHooksBlock(details, attemptHookPhases.after, 'After hooks');

        container.appendChild(details);
    }

    // ---------------------------------------------------------------- aggregate rollup status
    // Worst-status-wins across a container's descendant scenarios, so a folder/feature/rule/outline/example-set
    // shows at a glance whether anything under it needs attention - not just its own name. NotRun/Unknown (nothing
    // under this container ever ran) sits at the bottom of the priority order: it still propagates upward when a
    // container's descendants are *all* NotRun/Unknown, but any genuine run outcome (even a plain Passed) wins over it.
    var AGGREGATE_STATUS_PRIORITY = ['Failed', 'Ambiguous', 'Undefined', 'Pending', 'Skipped', 'PassWithIssues', 'Passed', 'NotRun', 'Unknown'];

    function worseStatus(a, b) {
        var ia = AGGREGATE_STATUS_PRIORITY.indexOf(a);
        var ib = AGGREGATE_STATUS_PRIORITY.indexOf(b);
        if (ia === -1) ia = AGGREGATE_STATUS_PRIORITY.length;
        if (ib === -1) ib = AGGREGATE_STATUS_PRIORITY.length;
        return ia <= ib ? a : b;
    }

    function computeAggregateStatus(node) {
        if (node.scenario) return node.scenario.status;
        var agg = null;
        node.children.forEach(function (c) {
            var s = computeAggregateStatus(c);
            agg = agg === null ? s : worseStatus(agg, s);
        });
        return agg || 'NotRun';
    }

    // ---------------------------------------------------------------- sidebar tree (compact: name + badges only)
    // Each render*Tree function builds a lightweight node {el, row, scenario, children, kind, data} alongside the
    // DOM. Filter/search visibility (D4/D5) and chart tallying (D7) walk this graph; clicking a row focuses that
    // node and renders its full content into the detail pane (I2) instead of always rendering everything inline.
    function makeChevron() {
        var btn = el('button', 'tree-chevron');
        btn.type = 'button';
        btn.setAttribute('aria-label', 'Toggle');
        btn.innerHTML = lucide('chevron-right', 12);
        return btn;
    }

    function buildRow(nameText, nameClass, hasChildren, kind, retried) {
        var row = el('div', 'tree-row');
        var chevron = hasChildren ? makeChevron() : el('span', 'tree-chevron placeholder');
        row.appendChild(chevron);
        var icon = kindIcon(kind, retried);
        if (icon) row.appendChild(icon);
        var nameEl = el('span', 'tree-name' + (nameClass ? ' ' + nameClass : ''), nameText);
        // Wraps nameEl alone so flex/ellipsis sizing on the name stays independent of the row's
        // other children (chevron, icons, subtree buttons).
        var nameBlock = el('div', 'tree-name-block');
        nameBlock.appendChild(nameEl);
        row.appendChild(nameBlock);
        return { row: row, chevron: chevron, icon: icon, nameBlock: nameBlock, nameEl: nameEl };
    }

    // A dedicated status icon sits to the left of the (always-neutral) kind-icon - the two glyphs
    // answer different questions ("what is this row" vs "how did it go") and no longer fight for the same slot.
    function setRowStatus(rowInfo, status) {
        var icon = statusIcon(status);
        icon.classList.add('tree-status-icon');
        rowInfo.row.insertBefore(icon, rowInfo.icon || rowInfo.nameBlock);
    }

    function focusNode(node) {
        if (selectedScenarioHistory && selectedScenarioHistory.node !== node) clearSelectedScenarioHistory();
        if (focusedNode && focusedNode.row) focusedNode.row.classList.remove('selected');
        focusedNode = node;
        if (node && node.row) node.row.classList.add('selected');
        renderDetail();
        renderBreadcrumb(node);
        updateUrlState();
    }

    function renderScenarioHistoryBanner(node) {
        setScenarioHistoryBanner(node);
    }

    function renderCurrentScenarioHistoryState(node) {
        if (!selectedScenarioHistory || !node || selectedScenarioHistory.node !== node) {
            clearScenarioHistoryBanner();
            return;
        }
        setScenarioHistoryBanner(node);
    }

    // ---------------------------------------------------------------- deep linking
    // Reflects the focused node + search text into the URL (replaceState, no history spam) so the address
    // bar can be copied and reopened straight to the same view - matches the Expressium LivingDoc-style
    // report's own query-param-driven filter restore (confirmed in its generated output), extended here to
    // also pin the exact node, not just a text filter. '›' (not '/') separates path segments since node
    // names may themselves legitimately contain '/'.
    function nodeSegment(node) {
        if (!node) return '';
        if (node.kind === 'attempt') return 'attempt:' + node.data.attemptNumber;
        var name = (node.data && node.data.name) || '';
        return node.kind + ':' + name;
    }

    function getNodePath(node) {
        var segments = [];
        var n = node;
        while (n && n !== rootNode && n.parent) {
            segments.unshift(nodeSegment(n));
            n = n.parent;
        }
        return segments.join('›');
    }

    // Same walk as getNodePath, but stops at (and excludes) the nearest ancestor 'run' node instead of
    // always going all the way to rootNode. Used for deep-linking a scenario's history-strip entry into
    // THAT historical run's own archived report file (which is always a single-run report with no 'run:'
    // wrapper segment), as opposed to this page's own URL state which may be inside a combined multi-run
    // report and legitimately needs the full path including the wrapper.
    function getScenarioPathWithinRun(node) {
        var segments = [];
        var n = node;
        while (n && n !== rootNode && n.parent && n.kind !== 'run') {
            segments.unshift(nodeSegment(n));
            n = n.parent;
        }
        return segments.join('›');
    }

    function findNodeByPath(path) {
        if (!path) return rootNode;
        var segments = path.split('›');
        var n = rootNode;
        for (var i = 0; i < segments.length; i++) {
            var found = null;
            for (var j = 0; j < n.children.length; j++) {
                if (nodeSegment(n.children[j]) === segments[i]) { found = n.children[j]; break; }
            }
            if (!found) return null;
            n = found;
        }
        return n;
    }

    function updateUrlState() {
        try {
            var params = new URLSearchParams();
            if (searchText) params.set('q', searchText);
            var path = getNodePath(focusedNode);
            if (path) params.set('node', path);
            if (REPORT_HAS_RERUNS && !INCLUDE_RERUNS) params.set('reruns', 'off');
            var qs = params.toString();
            history.replaceState(null, '', location.pathname + (qs ? '?' + qs : '') + location.hash);
        } catch (e) { /* file:// or sandboxed contexts may block replaceState - deep link is additive, non-fatal */ }
    }

    function restoreStateFromUrl() {
        var params;
        try { params = new URLSearchParams(location.search); } catch (e) { focusNode(rootNode); return; }
        var q = params.get('q');
        if (q) {
            searchText = q.toLowerCase();
            if (searchBox) searchBox.value = q;
        }
        var target = findNodeByPath(params.get('node'));
        if (target && target !== rootNode) {
            var n = target.parent;
            while (n) { if (n.setExpanded) n.setExpanded(true); n = n.parent; }
            focusNode(target);
            if (target.row) target.row.scrollIntoView({ block: 'center' });
        } else {
            focusNode(rootNode);
        }
        if (q) applyFiltersAndSearch();
    }

    // Tracks each child's parent so renderBreadcrumb can walk back up to the multi-run
    // overview from wherever the user drilled down to - nothing else needed this before breadcrumbs existed.
    function addChild(parent, child) {
        parent.children.push(child);
        child.parent = parent;
        return child;
    }

    // Visible path back to "All runs" - the report title click already did this silently;
    // this surfaces it as clickable ancestor links. Only meaningful once there's more than one run to get lost
    // among, and only once the user has drilled down below that combined root.
    function renderBreadcrumb(node) {
        if (!breadcrumbEl) return;
        breadcrumbEl.innerHTML = '';
        if (runs.length <= 1 || !node || node === rootNode) {
            breadcrumbEl.classList.add('hidden');
            return;
        }

        // Skip invisible pass-through wrapper nodes - renderFolderTree's "virtual root" branch (run.root.name is
        // always '') splices a run's own subfolders/features straight into the run node's children, so each run
        // has one of these with no row/click target of its own sitting between it and its real content.
        var chain = [];
        for (var n = node; n; n = n.parent) {
            if (n.row === null && n !== rootNode) continue;
            chain.unshift(n);
        }

        breadcrumbEl.classList.remove('hidden');
        chain.forEach(function (ancestor, idx) {
            if (idx > 0) breadcrumbEl.appendChild(el('span', 'breadcrumb-sep', '›'));
            var label = ancestor === rootNode ? 'All runs' : nodeDisplayName(ancestor);
            if (idx === chain.length - 1) {
                breadcrumbEl.appendChild(el('span', 'breadcrumb-current', label));
            } else {
                var link = el('a', 'breadcrumb-link', label);
                link.href = '#';
                link.addEventListener('click', function (e) { e.preventDefault(); focusNode(ancestor); });
                breadcrumbEl.appendChild(link);
            }
        });
    }

    // A scenario with more than one attempt gets its attempts as independent child tree rows - each
    // one focusable on its own, mirroring createContainerTreeNode's chevron/subtree-button pattern - instead of
    // attempts only being reachable by expanding the <details> blocks inside the scenario's own detail pane.
    function createScenarioTreeNode(container, scenario) {
        var attempts = scenario.attempts || [];
        var hasAttempts = attempts.length > 1;
        var rowInfo = buildRow(scenario.name, 'scenario-name', hasAttempts, 'scenario', hasAttempts);
        var li = el('li');
        li.appendChild(rowInfo.row);
        var childrenUl = null;
        if (hasAttempts) {
            childrenUl = el('ul', 'tree-children collapsed');
            li.appendChild(childrenUl);
        }
        container.appendChild(li);

        var node = { el: li, row: rowInfo.row, scenario: scenario, data: scenario, kind: 'scenario', children: [] };

        setRowStatus(rowInfo, scenario.status);

        if (hasAttempts) {
            attempts.forEach(function (a) { addChild(node, createAttemptTreeNode(childrenUl, scenario, a)); });

            var expanded = false;
            node.setExpanded = function (exp) {
                expanded = exp;
                rowInfo.chevron.classList.toggle('expanded', expanded);
                childrenUl.classList.toggle('collapsed', !expanded);
            };
            rowInfo.chevron.addEventListener('click', function (e) {
                e.stopPropagation();
                node.setExpanded(!expanded);
            });
            rowInfo.row.appendChild(buildSubtreeExpandButtons(node));
        }

        rowInfo.row.addEventListener('click', function () { focusNode(node); });

        return node;
    }

    // Leaf tree row for one retry attempt - no children/chevron of its own; clicking it focuses just
    // that attempt's steps/hooks/attachments (see renderAttemptDetail) rather than the scenario's full retry
    // history. `scenario` stays null (this isn't the scenario-status-tallying leaf computeStatusCounts/
    // computeAggregateStatus/updateVisibility look for) since those all short-circuit on the parent scenario node
    // before ever recursing into its children - an attempt node is only reached when it is itself the focused node.
    function createAttemptTreeNode(container, scenario, attempt) {
        var label = 'Attempt ' + attempt.attemptNumber + (attempt.rerun ? ' · rerun ' + attempt.rerun : '') + (attempt.willBeRetried ? ' (retried)' : '');
        var rowInfo = buildRow(label, 'scenario-name', false, 'attempt', false);
        var li = el('li');
        li.appendChild(rowInfo.row);
        container.appendChild(li);

        var node = { el: li, row: rowInfo.row, scenario: null, attempt: attempt, parentScenario: scenario, data: attempt, kind: 'attempt', children: [] };

        setRowStatus(rowInfo, attempt.status);

        rowInfo.row.addEventListener('click', function () { focusNode(node); });

        return node;
    }

    // Per-node +/- - scoped to this node's own subtree. The earlier global Expand/Collapse-all pair
    // was removed: every top-level row already gets one of these, so a whole-tree pair was redundant.
    function buildSubtreeExpandButtons(node) {
        var wrap = el('span', 'tree-subtree-toggle');
        var expandBtn = el('button', 'tree-subtree-btn tree-subtree-expand');
            expandBtn.innerHTML = lucide('chevrons-up-down', 12);
        expandBtn.type = 'button';
        expandBtn.title = 'Expand this subtree';
        expandBtn.addEventListener('click', function (e) { e.stopPropagation(); setAllExpanded(node, true); });
        var collapseBtn = el('button', 'tree-subtree-btn tree-subtree-collapse');
            collapseBtn.innerHTML = lucide('chevrons-down-up', 12);
        collapseBtn.type = 'button';
        collapseBtn.title = 'Collapse this subtree';
        collapseBtn.addEventListener('click', function (e) { e.stopPropagation(); setAllExpanded(node, false); });
        wrap.appendChild(expandBtn);
        wrap.appendChild(collapseBtn);
        return wrap;
    }

    function createContainerTreeNode(container, kind, data, nameText, nameClass, buildChildren) {
        var rowInfo = buildRow(nameText, nameClass, true, kind, false);
        var li = el('li');
        li.appendChild(rowInfo.row);
        var childrenUl = el('ul', 'tree-children collapsed');
        li.appendChild(childrenUl);
        container.appendChild(li);

        var node = { el: li, row: rowInfo.row, nameBlock: rowInfo.nameBlock, scenario: null, data: data, kind: kind, children: [] };
        buildChildren(childrenUl, node);

        var aggregateStatus = computeAggregateStatus(node);
        setRowStatus(rowInfo, aggregateStatus);

        var expanded = false;
        node.setExpanded = function (exp) {
            expanded = exp;
            rowInfo.chevron.classList.toggle('expanded', expanded);
            childrenUl.classList.toggle('collapsed', !expanded);
        };
        rowInfo.chevron.addEventListener('click', function (e) {
            e.stopPropagation();
            node.setExpanded(!expanded);
        });
        rowInfo.row.appendChild(buildSubtreeExpandButtons(node));
        rowInfo.row.addEventListener('click', function () { focusNode(node); });

        return node;
    }

    function renderExampleSetTree(container, set) {
        return createContainerTreeNode(container, 'exampleSet', set, set.name || 'Examples', 'scenario-name', function (childrenUl, node) {
            (set.scenarios || []).forEach(function (s) { addChild(node, createScenarioTreeNode(childrenUl, s)); });
        });
    }

    function renderScenarioOutlineTree(container, outline) {
        return createContainerTreeNode(container, 'scenarioOutline', outline, outline.name, 'scenario-name', function (childrenUl, node) {
            (outline.exampleSets || []).forEach(function (es) { addChild(node, renderExampleSetTree(childrenUl, es)); });
        });
    }

    function renderRuleTree(container, rule) {
        return createContainerTreeNode(container, 'rule', rule, rule.name, 'feature-name', function (childrenUl, node) {
            (rule.scenarios || []).forEach(function (s) { addChild(node, createScenarioTreeNode(childrenUl, s)); });
            (rule.scenarioOutlines || []).forEach(function (o) { addChild(node, renderScenarioOutlineTree(childrenUl, o)); });
        });
    }

    function renderFeatureTree(container, feature) {
        return createContainerTreeNode(container, 'feature', feature, feature.name, 'feature-name', function (childrenUl, node) {
            (feature.rules || []).forEach(function (r) { addChild(node, renderRuleTree(childrenUl, r)); });
            (feature.scenarios || []).forEach(function (s) { addChild(node, createScenarioTreeNode(childrenUl, s)); });
            (feature.scenarioOutlines || []).forEach(function (o) { addChild(node, renderScenarioOutlineTree(childrenUl, o)); });
        });
    }

    function renderFolderTree(container, folder) {
        if (folder.name) {
            return createContainerTreeNode(container, 'folder', folder, folder.name, 'folder-name', function (childrenUl, node) {
                (folder.subfolders || []).forEach(function (f) { addChild(node, renderFolderTree(childrenUl, f)); });
                (folder.features || []).forEach(function (f) { addChild(node, renderFeatureTree(childrenUl, f)); });
            });
        }

        var node = { el: null, row: null, scenario: null, data: folder, kind: 'root', children: [] };
        (folder.subfolders || []).forEach(function (f) { addChild(node, renderFolderTree(container, f)); });
        (folder.features || []).forEach(function (f) { addChild(node, renderFeatureTree(container, f)); });
        return node;
    }

    // One top-level tree node per combined-report run - reuses renderFolderTree's own
    // "virtual root" branch (run.root.name is always '') to splice the run's subfolders/features directly into
    // this node's own children with zero extra tree-building code. When a combined report holds more than one
    // run, nothing previously distinguished which one is the most recent/current - the row label was just the
    // run's own (often generic, e.g. "Run") name. isLatest adds a small badge, and the run's own finishedAt/
    // startedAt is appended as a muted suffix so every run row is self-describing without having to click into
    // it and read renderRunMeta's strip.
    function renderRunTree(container, run, isLatest) {
        var node = createContainerTreeNode(container, 'run', run, run.name || 'Run', 'feature-name', function (childrenUl, node) {
            addChild(node, renderFolderTree(childrenUl, run.root));
        });
        var when = run.finishedAt || run.startedAt;
        if (when) node.nameBlock.appendChild(el('span', 'run-tree-date', formatRunTimestamp(when)));
        if (isLatest) node.nameBlock.appendChild(el('span', 'run-latest-badge', 'Latest'));
        return node;
    }

    // Entry point for the whole tree. When there's exactly one run (the everyday case), renders that run's root
    // directly at the top level so the single-run report is visually unchanged from before multi-run combining
    // existed; only wraps runs in a visible 'Run' node when there's genuinely more than one to distinguish.
    function renderRootTree(container, runs) {
        if (runs.length <= 1) {
            return renderFolderTree(container, runs[0] ? runs[0].root : { name: '', subfolders: [], features: [] });
        }

        // Which run is "current" - the one with the latest finishedAt/startedAt - computed client-side from
        // data already shipped to every viewer, per this report's own client-side-computation convention; no
        // server-side flag is needed or added.
        var latestRun = runs.reduce(function (latest, run) {
            var runWhen = run.finishedAt || run.startedAt || '';
            var latestWhen = latest ? (latest.finishedAt || latest.startedAt || '') : '';
            return !latest || runWhen > latestWhen ? run : latest;
        }, null);

        // Real, visible, clickable "All runs" row - this used to be a rowless pass-through
        // node with each run spliced straight into the sidebar, leaving only the breadcrumb (once you'd drilled
        // down into one run) as a way back up. Reusing createContainerTreeNode gives it a chevron, aggregate
        // status, and a focusable row like every other container, so there's an actual node to navigate to/from.
        return createContainerTreeNode(container, 'root', { name: 'All runs' }, 'All runs', 'folder-name', function (childrenUl, node) {
            runs.forEach(function (run) { addChild(node, renderRunTree(childrenUl, run, run === latestRun)); });
        });
    }

    // ---------------------------------------------------------------- detail pane (I2)
    // Populated on click from the node clicked in the sidebar - reuses the same step/attachment/issue/hook
    // rendering helpers as before, just targeting the detail pane instead of always being rendered inline.
    function clearDetail() {
        detailContent.innerHTML = '';
        detailContent.classList.remove('viewing-history');
    }

    // Historical rendering reuses the live renderers on a normalized snapshot (see normalizeSnapshot above), so a
    // past run looks exactly like the current one: arguments, comment, description, issues, hooks, steps, attachments.
    function renderHistoricalScenarioSnapshot(container, snapshot) {
        if (!snapshot) {
            container.appendChild(el('div', 'detail-empty', 'Historical data for this run is unavailable or malformed.'));
            return;
        }
        renderScenarioArguments(container, snapshot.argumentValues);
        renderNodeComment(container, snapshot.comment);
        renderDescription(container, snapshot.description);
        renderIssues(container, snapshot.issues || []);

        var phases = splitHooksByPhase(snapshot.hooks || []);
        renderHooksBlock(container, phases.before, 'Before hooks');

        var steps = el('ul', 'step-list');
        (snapshot.steps || []).forEach(function (s) { renderStep(steps, s); });
        container.appendChild(steps);

        var stepHasError = (snapshot.steps || []).some(function (s) { return s.errorMessage; });
        if (!stepHasError) renderErrorBlock(container, 'step-error', snapshot.errorMessage, snapshot.stackTrace);

        renderAttachmentsBlock(container, snapshot.attachments, null, snapshot.comment);
        renderHooksBlock(container, phases.after, 'After hooks');
    }

    function formatRunTimestamp(iso) {
        return formatDateTime(iso, { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });
    }

    // Sticky notice at the very top of the scenario container while a past run is shown.
    function buildHistoryBanner(node, selection) {
        var run = selection.runSummary || {};
        var banner = el('div', 'scenario-history-context-banner');
        banner.setAttribute('role', 'status');
        banner.appendChild(el('span', 'context-banner-label', 'Historical view'));
        var text = el('span', 'context-banner-text');
        text.appendChild(el('span', null, 'Recorded state from run'));
        text.appendChild(el('strong', 'context-banner-when', formatRunTimestamp(run.runAt)));
        var outcome = run.outcome || (selection.snapshot && selection.snapshot.status) || 'Unknown';
        var outcomeEl = el('span', 'context-banner-outcome');
        outcomeEl.appendChild(statusIcon(outcome));
        outcomeEl.appendChild(el('span', null, outcome));
        text.appendChild(outcomeEl);
        banner.appendChild(text);
        var actions = el('span', 'context-banner-actions');
        if (run.runUrl) {
            var link = el('a', 'context-banner-btn', 'Full report');
            link.href = run.runUrl;
            link.target = '_blank';
            link.rel = 'noopener';
            link.insertAdjacentHTML('beforeend', ICON.external);
            actions.appendChild(link);
        }
        var back = el('button', 'context-banner-btn context-banner-btn-primary', 'Back to current run');
        back.type = 'button';
        back.addEventListener('click', function () { clearSelectedScenarioHistory(); renderDetail(); });
        actions.appendChild(back);
        banner.appendChild(actions);
        return banner;
    }

    function renderScenarioDetail(container, scenario, node) {
        var viewing = selectedScenarioHistory && selectedScenarioHistory.node === node ? selectedScenarioHistory : null;
        container.classList.toggle('viewing-history', !!viewing);
        if (viewing) container.appendChild(buildHistoryBanner(node, viewing));

        var header = el('div', 'detail-header');

        // One row: custom-filter chips, then Gherkin @tags, then #hashtags - all direct flex items of the same row.
        var hasFilterMatches = (scenario.filterMatches || []).some(function (m) { return !isOtherMatch(m); });
        var hasTags = (scenario.tags && scenario.tags.length) || (scenario.hashtags && scenario.hashtags.length);
        if (hasFilterMatches || hasTags) {
            var tagsRow = el('div', 'detail-header-tags-row');
            (scenario.filterMatches || []).forEach(function (m) { if (!isOtherMatch(m)) tagsRow.appendChild(filterMatchChip(m)); });
            if (scenario.tags && scenario.tags.length) tagsRow.appendChild(tagsSpan(scenario.tags, 'gh-tag'));
            if (scenario.hashtags && scenario.hashtags.length) tagsRow.appendChild(tagsSpan(scenario.hashtags, 'hashtag'));
            header.appendChild(tagsRow);
        }

        var titleRow = el('div', 'detail-header-title-row');
        titleRow.appendChild(statusIcon(viewing ? ((viewing.snapshot && viewing.snapshot.status) || 'Unknown') : scenario.status));
        var titleSpan = el('span', 'scenario-name detail-title');
        titleSpan.appendChild(keywordSpan('Scenario:'));
        titleSpan.appendChild(document.createTextNode(' ' + scenario.name));
        titleRow.appendChild(titleSpan);
        header.appendChild(titleRow);

        // Run history lives on its own row under the title (a distinct control, not a tag).
        var historyStrip = buildScenarioHistoryStrip(scenario.recentRuns, node);
        if (historyStrip) header.appendChild(historyStrip);

        container.appendChild(header);

        if (viewing) {
            renderHistoricalScenarioSnapshot(container, viewing.snapshot || historicalSnapshotForRunSummary(viewing.runSummary));
            return;
        }

        renderScenarioArguments(container, scenario.argumentValues);
        renderNodeComment(container, scenario.comment);
        renderDescription(container, scenario.description);
        renderIssues(container, scenario.issues);

        var attempts = scenario.attempts || [];
        if (attempts.length > 1) {
            renderRetrySummary(container, attempts);
            attempts.forEach(function (a, idx) { renderAttempt(container, a, idx === attempts.length - 1); });
        } else {
            var scenarioHookPhases = splitHooksByPhase(scenario.hooks);
            renderHooksBlock(container, scenarioHookPhases.before, 'Before hooks');

            var steps = el('ul', 'step-list');
            (scenario.steps || []).forEach(function (s) { renderStep(steps, s); });
            container.appendChild(steps);
            renderAttachmentsBlock(container, scenario.attachments, null, scenario.comment);
            renderHooksBlock(container, scenarioHookPhases.after, 'After hooks');
        }
    }

    // Detail view for one attempt tree node - a scenario-context header (so it's clear which scenario
    // this attempt belongs to) followed by the same renderAttempt block used inline in renderScenarioDetail,
    // opened by default since focusing an attempt node means the user wants to see it immediately.
    function renderAttemptDetail(container, node) {
        var scenario = node.parentScenario;
        var attempt = node.attempt;
        var header = el('div', 'detail-header');
        header.appendChild(statusIcon(attempt.status));
        var titleSpan = el('span', 'scenario-name detail-title');
        titleSpan.appendChild(keywordSpan('Scenario:'));
        titleSpan.appendChild(document.createTextNode(' ' + scenario.name));
        header.appendChild(titleSpan);
        container.appendChild(header);
        renderAttempt(container, attempt, true);
    }

    function childDetailLabel(node) {
        if (node.kind === 'exampleSet') return node.data.name || 'Examples';
        return node.data.name || '';
    }

    // Single source of truth for "what is currently focused" - used by both the detail-pane header and the
    // single chart title, so the two never disagree about what's on screen.
    function nodeDisplayName(node) {
        if (!node || node.kind === 'root') return 'Overall';
        if (node.kind === 'scenario') return node.scenario.name;
        if (node.kind === 'attempt') return node.parentScenario.name + ' — Attempt ' + node.attempt.attemptNumber;
        if (node.kind === 'run') return (node.data && node.data.name) || 'Run';
        return (node.data && node.data.name) || 'Examples';
    }

    // Primary Gherkin keywords (Feature/Rule/Scenario Outline) get the bold accent .gh-keyword treatment;
    // Examples is grouped with LivingDoc's own secondary/structural keywords (History/Attachments/Duration).
    var NODE_KEYWORDS = {
        feature: { text: 'Feature:', secondary: false },
        rule: { text: 'Rule:', secondary: false },
        scenarioOutline: { text: 'Scenario Outline:', secondary: false },
        exampleSet: { text: 'Examples:', secondary: true }
    };

    // ms -> compact human string ("342ms", "1.2s", "3m 4s", "1h 6m") - no Date object involved, matching the
    // rest of the file's preference for plain string/number formatting over Date parsing (see buildHistoryMatrix).
    function formatDuration(ms) {
        if (ms == null) return null;
        if (ms < 1000) return Math.round(ms) + 'ms';
        var totalSeconds = ms / 1000;
        if (totalSeconds < 60) return totalSeconds.toFixed(1) + 's';
        var totalMinutes = Math.floor(totalSeconds / 60);
        var seconds = Math.round(totalSeconds - totalMinutes * 60);
        if (totalMinutes < 60) return totalMinutes + 'm ' + seconds + 's';
        var hours = Math.floor(totalMinutes / 60);
        var minutes = totalMinutes - hours * 60;
        return hours + 'h ' + minutes + 'm';
    }

    // Started/finished/duration only ever make sense for a whole run - reuses the header's
    // own muted chip-strip look (.header-status-strip/.header-status-item) rather than introducing new CSS.
    function renderRunMeta(container, run) {
        var strip = el('div', 'header-status-strip run-meta-strip');
        if (run.startedAt) strip.appendChild(el('span', 'header-status-item', 'Started: ' + run.startedAt.replace('T', ' ')));
        if (run.finishedAt) strip.appendChild(el('span', 'header-status-item', 'Finished: ' + run.finishedAt.replace('T', ' ')));
        var duration = formatDuration(run.durationMs);
        if (duration) strip.appendChild(el('span', 'header-status-item', 'Duration: ' + duration));
        if (strip.children.length) container.appendChild(strip);
    }

    function renderContainerOrRootDetail(container, node) {
        var data = node.data || {};
        var isRoot = node.kind === 'root';
        var isRun = node.kind === 'run';

        var header = el('div', 'detail-header');

        // Tags row ABOVE the title - same reasoning as renderScenarioDetail: a real .feature file
        // puts tags on their own line above Feature:/Rule:/Scenario Outline:/Examples:, not trailing the name.
        if (!isRoot && data.tags && data.tags.length) {
            var tagsRow = el('div', 'detail-header-tags-row');
            tagsRow.appendChild(tagsSpan(data.tags, 'gh-tag'));
            header.appendChild(tagsRow);
        }

        var titleRow = el('div', 'detail-header-title-row');
        titleRow.appendChild(statusIcon(computeAggregateStatus(node)));
        var titleSpan = el('span', 'detail-title');
        var kw = NODE_KEYWORDS[node.kind];
        if (kw) {
            titleSpan.appendChild(keywordSpan(kw.text, kw.secondary));
            titleSpan.appendChild(document.createTextNode(' '));
        }
        titleSpan.appendChild(document.createTextNode(nodeDisplayName(node)));
        titleRow.appendChild(titleSpan);
        header.appendChild(titleRow);

        container.appendChild(header);
        if (isRun) renderRunMeta(container, data);

        appendContainerBody(container, node);
    }

    // Shared by the top-level container/root detail view and every recursively-expanded child row below - the
    // same description/background/children content renders in place at every depth, so there is
    // exactly one place that knows how to render "the body of a container node".
    function appendContainerBody(container, node) {
        var data = node.data || {};
        if (node.kind !== 'root') {
            renderNodeComment(container, data.comment);
            renderDescription(container, data.description);
            if (data.background || data.backgroundName || data.backgroundDescription) {
                renderBackground(container, data.background, data.backgroundName, data.backgroundDescription);
            }
            if (node.kind === 'exampleSet') renderExamplesTable(container, data);
        }
        if (node.kind === 'root' || node.kind === 'run') {
            var statsStrip = buildHealthStatsStrip(node);
            if (statsStrip) container.appendChild(statsStrip);
        }
        if (node.children.length) container.appendChild(buildDetailChildrenList(node.children));
    }

    // Every level - including scenario/attempt leaves - expands in place under its own row; the
    // detail pane is no longer a navigation surface (that's the left tree's job - see focusNode), only a
    // universally-expandable view of whatever is currently focused.
    function buildDetailChildrenList(children) {
        var list = el('ul', 'detail-children-list');
        children.forEach(function (child) { appendDetailChild(list, child); });
        return list;
    }

    // A run's own root folder is always name-less (mirrors the sidebar tree's virtual-root flattening in
    // renderFolderTree) - without this, it would render as its own unlabeled row here since childDetailLabel
    // has nothing to show for it.
    function appendDetailChild(list, child) {
        if (child.kind === 'root') {
            child.children.forEach(function (grandchild) { appendDetailChild(list, grandchild); });
            return;
        }
        renderDetailChildItem(list, child);
    }

    function renderDetailChildItem(list, child) {
        var li = el('li', 'detail-children-item');
        var row = el('div', 'detail-children-row');
        var chevron = makeChevron();
        row.appendChild(chevron);
        row.appendChild(statusIcon(computeAggregateStatus(child)));
        row.appendChild(el('span', 'detail-children-label', childDetailLabel(child)));
        li.appendChild(row);

        var expandedWrap = el('div', 'detail-children-expanded collapsed');
        li.appendChild(expandedWrap);

        var expanded = false;
        var built = false;
        row.addEventListener('click', function () {
            expanded = !expanded;
            chevron.classList.toggle('expanded', expanded);
            expandedWrap.classList.toggle('collapsed', !expanded);
            if (expanded && !built) {
                built = true;
                if (child.kind === 'scenario') renderScenarioDetail(expandedWrap, child.scenario, child);
                else appendContainerBody(expandedWrap, child);
            }
        });

        list.appendChild(li);
    }

    function renderDetailForNode(node) {
        clearDetail();
        if (!node) {
            detailContent.appendChild(el('div', 'detail-empty', 'Select an item from the tree to see details.'));
            return;
        }
        if (node.kind === 'scenario') renderScenarioDetail(detailContent, node.scenario, node);
        else if (node.kind === 'attempt') renderAttemptDetail(detailContent, node);
        else renderContainerOrRootDetail(detailContent, node);
    }

    // ---------------------------------------------------------------- analytics view
    // A single toggle, not LivingDoc-style duplicate Features/Scenarios/Steps flat-list tabs - the hierarchical
    // tree stays the only navigation structure. The whole-tree status breakdown that per-node browsing doesn't
    // already surface now lives in the chart(s) drawn in the detail-top-row (see redrawTopRow); this view only
    // supplies what the chart doesn't - the merged flat tables/history matrix.
    function renderAnalyticsView() {
        clearDetail();
        detailContent.appendChild(buildTablesSection());
    }

    // ---------------------------------------------------------------- flat tables view
    // LivingDoc-style flat Features/Scenarios/Steps catalogs plus feature-level KPI columns, as their own view
    // mode alongside Analytics rather than folded into it - built entirely from data the tree already computed
    // (node.el's 'hidden' class from updateVisibility, scenario status/health/recentRuns), no new payload needed.
    function isNodeVisible(node) {
        return !node.el || !node.el.classList.contains('hidden');
    }

    function collectScenarioNodes(node, out) {
        if (node.scenario) {
            if (isNodeVisible(node)) out.push(node);
            return;
        }
        (node.children || []).forEach(function (c) { collectScenarioNodes(c, out); });
    }

    function collectFeatureNodes(node, out) {
        if (node.kind === 'feature' && isNodeVisible(node)) out.push(node);
        (node.children || []).forEach(function (c) { collectFeatureNodes(c, out); });
    }

    function pctLabel(n) {
        return isFinite(n) ? Math.round(n) + '%' : '—';
    }

    // Generic click-header-to-sort table - shared by the Features/Scenarios/Steps catalogs so sorting logic
    // lives in exactly one place. `columns[].value` drives both sort comparison and the default text render;
    // `render` overrides the cell entirely (links, badges); `format` only overrides the displayed text.
    function buildSortableTable(className, columns, rows) {
        var table = el('table', 'sortable-table ' + className);
        var thead = el('thead');
        var headRow = el('tr');
        var tbody = el('tbody');
        var sortState = { colIndex: 0, asc: true };

        function renderRows() {
            tbody.innerHTML = '';
            var sorted = rows.slice().sort(function (a, b) {
                var col = columns[sortState.colIndex];
                var va = col.value(a), vb = col.value(b);
                if (va < vb) return sortState.asc ? -1 : 1;
                if (va > vb) return sortState.asc ? 1 : -1;
                return 0;
            });
            sorted.forEach(function (row) {
                var tr = el('tr');
                columns.forEach(function (col) {
                    var td = el('td', col.className);
                    if (col.render) col.render(td, row);
                    else td.textContent = col.format ? col.format(col.value(row)) : col.value(row);
                    tr.appendChild(td);
                });
                tbody.appendChild(tr);
            });
        }

        function updateIndicators() {
            Array.prototype.forEach.call(headRow.children, function (th, idx) {
                th.classList.toggle('sorted-asc', idx === sortState.colIndex && sortState.asc);
                th.classList.toggle('sorted-desc', idx === sortState.colIndex && !sortState.asc);
            });
        }

        columns.forEach(function (col, idx) {
            var th = el('th', 'sortable-col', col.label);
            th.addEventListener('click', function () {
                if (sortState.colIndex === idx) sortState.asc = !sortState.asc;
                else { sortState.colIndex = idx; sortState.asc = true; }
                updateIndicators();
                renderRows();
            });
            headRow.appendChild(th);
        });
        thead.appendChild(headRow);
        table.appendChild(thead);
        table.appendChild(tbody);

        updateIndicators();
        renderRows();
        return table;
    }

    function buildFeaturesTable(scope) {
        var features = [];
        collectFeatureNodes(scope, features);

        var rows = features.map(function (fn) {
            var scenarios = [];
            collectScenarioNodes(fn, scenarios);
            var total = scenarios.length;
            var failed = scenarios.filter(function (s) { return s.scenario.status === 'Failed'; }).length;
            var passed = scenarios.filter(function (s) { return s.scenario.status === 'Passed'; }).length;
            var flaky = scenarios.filter(function (s) { return analyzeScenarioHealth(filterExcludedRuns(s.scenario.recentRuns), historyAnalysisWindow) === 'Flaky'; }).length;
            // "Coverage" has no dedicated payload field - approximated as the share of this feature's scenarios that
            // have actually run at least once (any status beyond NotRun/Unknown), vs. ones only ever defined.
            var covered = scenarios.filter(function (s) { return s.scenario.status !== 'NotRun' && s.scenario.status !== 'Unknown'; }).length;
            return {
                name: fn.data.name,
                node: fn,
                usedCount: total,
                failurePct: total ? (failed / total) * 100 : 0,
                passRate: total ? (passed / total) * 100 : 0,
                flakyRate: total ? (flaky / total) * 100 : 0,
                coverage: total ? (covered / total) * 100 : 0
            };
        });

        return buildSortableTable('features-table', [
            {
                label: 'Feature', value: function (r) { return r.name; }, render: function (td, r) {
                    var link = el('a', 'table-row-link', r.name);
                    link.href = '#';
                    link.addEventListener('click', function (e) { e.preventDefault(); focusNode(r.node); });
                    td.appendChild(link);
                }
            },
            { label: 'Used', value: function (r) { return r.usedCount; } },
            { label: 'Failure %', value: function (r) { return r.failurePct; }, format: pctLabel },
            { label: 'Pass Rate', value: function (r) { return r.passRate; }, format: pctLabel },
            { label: 'Flaky Rate', value: function (r) { return r.flakyRate; }, format: pctLabel },
            { label: 'Coverage', value: function (r) { return r.coverage; }, format: pctLabel }
        ].filter(function (c) { return scopeHasHistory(scope) || c.label !== 'Flaky Rate'; }), rows);
    }

    function buildScenariosTable(scope) {
        var nodes = [];
        collectScenarioNodes(scope, nodes);

        var rows = nodes.map(function (n) {
            var scenario = n.scenario;
            // "Used" = included runs in which the scenario actually executed; absent / not-executed runs never count.
            var info = analyzeScenarioHistory(filterExcludedRuns(scenario.recentRuns), historyAnalysisWindow);
            var usedCount = info.considered || 1;
            var failedCount = info.considered ? info.fails : (scenario.status === 'Failed' ? 1 : 0);
            return {
                name: scenario.name,
                node: n,
                usedCount: usedCount,
                failurePct: usedCount ? (failedCount / usedCount) * 100 : 0,
                status: scenario.status,
                health: info.health
            };
        });

        return buildSortableTable('scenarios-table', [
            {
                label: 'Scenario', value: function (r) { return r.name; }, render: function (td, r) {
                    var link = el('a', 'table-row-link', r.name);
                    link.href = '#';
                    link.addEventListener('click', function (e) { e.preventDefault(); focusNode(r.node); });
                    td.appendChild(link);
                }
            },
            { label: 'Status', value: function (r) { return r.status; }, render: function (td, r) { td.appendChild(statusIcon(r.status)); } },
            { label: 'Used', value: function (r) { return r.usedCount; } },
            { label: 'Failure %', value: function (r) { return r.failurePct; }, format: pctLabel },
            {
                label: 'Verdict', value: function (r) { return r.health; }, render: function (td, r) {
                    var b = el('span', 'health-stat-pill', r.health === 'Regressed' ? 'New bug' : (r.health === 'Broken' ? 'Failing' : (r.health === 'New' ? 'First run' : (r.health === 'Unknown' ? 'Not run' : r.health))));
                    b.style.setProperty('--pill-bg', healthColor(r.health));
                    b.style.color = readableTextOn(healthColor(r.health));
                    td.appendChild(b);
                }
            }
        ].filter(function (c) { return scopeHasHistory(scope) || c.label !== 'Verdict'; }), rows);
    }

    function buildStepsTable(scope) {
        var nodes = [];
        collectScenarioNodes(scope, nodes);

        // Catalog keyed by the step's definition pattern when known (so every instantiated occurrence of the same
        // step definition collapses into one row), falling back to the literal text for steps with no pattern.
        var catalog = {};
        nodes.forEach(function (n) {
            (n.scenario.steps || []).forEach(function (s) {
                var key = s.pattern || s.text;
                var entry = catalog[key];
                if (!entry) entry = catalog[key] = { sample: s.text, usedCount: 0, failedCount: 0 };
                entry.usedCount++;
                if (s.status === 'Failed') entry.failedCount++;
            });
        });

        var rows = Object.keys(catalog).map(function (key) {
            var entry = catalog[key];
            return {
                text: entry.sample,
                usedCount: entry.usedCount,
                failurePct: entry.usedCount ? (entry.failedCount / entry.usedCount) * 100 : 0
            };
        });

        return buildSortableTable('steps-table', [
            { label: 'Step', value: function (r) { return r.text; } },
            { label: 'Used', value: function (r) { return r.usedCount; } },
            { label: 'Failure %', value: function (r) { return r.failurePct; }, format: pctLabel }
        ], rows);
    }

    // ---------------------------------------------------------------- analytics scope
    // Every analytics table / the History tab describes ONLY the node focused in the tree (the whole report when
    // nothing narrower is focused). They used to read from rootNode regardless, so browsing nodes kept showing
    // the same, mostly irrelevant, whole-report numbers.
    function analyticsScopeNode() {
        var n = focusedNode || rootNode;
        if (n && n.kind === 'attempt' && n.parent) return n.parent;
        return n;
    }

    // "Historical data exists" = at least one scenario in scope was recorded in a run OTHER than the current one
    // (recentRuns holds the current run plus earlier ones). Without it every history-derived element - the History
    // tab, verdicts, health pills, the flaky-rate column, the history strip - is simply not rendered.
    function scopeHasHistory(scope) {
        var nodes = [];
        collectScenarioNodes(scope, nodes);
        return nodes.some(function (n) { return (n.scenario.recentRuns || []).length >= 2; });
    }

    function scopeBreadcrumb(node) {
        var names = [];
        for (var n = node; n; n = n.parent) {
            if (n.kind === 'root') continue;
            names.unshift(nodeDisplayName(n));
        }
        return names.length ? names.join(' › ') : 'Overall';
    }

    function collectAllScenarioNodes(node, out) {
        if (node.scenario) { out.push(node); return; }
        (node.children || []).forEach(function (c) { collectAllScenarioNodes(c, out); });
    }

    // Every run that appears anywhere in the report, oldest first, with how many scenarios have an entry for it.
    function collectKnownRuns() {
        var all = [];
        collectAllScenarioNodes(rootNode, all);
        var map = {};
        all.forEach(function (n) {
            (n.scenario.recentRuns || []).forEach(function (r) {
                var k = runKey(r);
                if (!map[k]) map[k] = { key: k, runAt: r.runAt, runUrl: r.runUrl, scenarios: 0, run: r };
                map[k].scenarios++;
            });
        });
        return Object.keys(map).map(function (k) { return map[k]; }).sort(function (a, b) { return a.runAt < b.runAt ? -1 : 1; });
    }

    function buildHistoryMatrix(nodes, runs) {
        var table = el('table', 'history-matrix-table');
        var thead = el('thead');
        var headRow = el('tr');
        headRow.appendChild(el('th', 'history-matrix-scenario-col', 'Scenario'));
        var healthTh = el('th', 'history-matrix-health-col', 'Verdict');
        healthTh.title = 'Based on pass/fail changes across the included runs in which the scenario actually ran';
        headRow.appendChild(healthTh);
        runs.forEach(function (run) {
            var th = el('th', 'history-matrix-date-col' + (excludedHistoryRuns[run.key] ? ' history-matrix-date-excluded' : ''), formatRunShort(run.runAt));
            th.title = formatRunTimestamp(run.runAt) + (excludedHistoryRuns[run.key] ? ' (excluded from calculations)' : '');
            headRow.appendChild(th);
        });
        thead.appendChild(headRow);
        table.appendChild(thead);

        var rows = nodes.map(function (n) {
            var info = analyzeScenarioHistory(filterExcludedRuns(n.scenario.recentRuns), historyAnalysisWindow);
            return { node: n, info: info };
        });
        var priority = { Regressed: 0, Flaky: 1, Broken: 2, Fixed: 3, New: 4, Passing: 5, Unknown: 6 };
        rows.sort(function (a, b) {
            var d = priority[a.info.health] - priority[b.info.health];
            return d !== 0 ? d : (a.node.scenario.name < b.node.scenario.name ? -1 : 1);
        });

        var tbody = el('tbody');
        rows.forEach(function (row) {
            var n = row.node;
            var tr = el('tr');
            var nameTd = el('td', 'history-matrix-scenario-name');
            var link = el('a', 'table-row-link', n.scenario.name);
            link.href = '#';
            link.addEventListener('click', function (e) { e.preventDefault(); focusNode(n); });
            nameTd.appendChild(link);
            tr.appendChild(nameTd);

            var verdictTd = el('td', 'history-matrix-health-cell');
            var badge = el('span', 'health-stat-pill', row.info.health === 'Regressed' ? 'New bug' : (row.info.health === 'Broken' ? 'Failing' : (row.info.health === 'New' ? 'First run' : (row.info.health === 'Unknown' ? 'Not run' : row.info.health))));
            badge.style.setProperty('--pill-bg', healthColor(row.info.health));
            badge.style.color = readableTextOn(healthColor(row.info.health));
            badge.title = describeScenarioHistory(row.info);
            verdictTd.appendChild(badge);
            verdictTd.appendChild(el('span', 'history-matrix-ratio', row.info.fails + '/' + row.info.considered + ' failed'));
            tr.appendChild(verdictTd);

            var byKey = {};
            (n.scenario.recentRuns || []).forEach(function (r) { byKey[runKey(r)] = r; });
            runs.forEach(function (run) {
                var td = el('td', 'history-matrix-cell' + (excludedHistoryRuns[run.key] ? ' history-matrix-date-excluded' : ''));
                var r = byKey[run.key];
                if (!r) {
                    var absent = el('span', 'history-matrix-absent', '–');
                    absent.title = 'Not present in this run (' + formatRunTimestamp(run.runAt) + ') - not counted';
                    td.appendChild(absent);
                } else {
                    var real = isRealOutcome(r.outcome);
                    var retried = real && runRetriedToPass(r);
                    var dot = el('span', 'history-matrix-dot' + (real ? '' : ' is-unreal') + (retried ? ' is-retry' : ''));
                    dot.style.setProperty('--dot-color', statusColor(r.outcome));
                    dot.title = r.outcome + ' — ' + formatRunTimestamp(r.runAt) + (real ? '' : ' (did not really run - not counted)') + (retried ? ' (passed only after a retry)' : '');
                    td.appendChild(dot);
                }
                tr.appendChild(td);
            });
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        var scroller = el('div', 'history-matrix-scroll');
        scroller.appendChild(table);
        return scroller;
    }

    // ---------------------------------------------------------------- run statistics (Azure-DevOps-style)
    // One row per INCLUDED run for the scenarios in scope: how many executed / passed / failed / were "other"
    // (present but skipped, incomplete or ambiguous), the pass rate over executed scenarios, and how many
    // scenarios sat in each verdict at that run (new bug / failing / flaky / fixed). Deltas compare each run with
    // the previous included one. A scenario absent from a run contributes nothing to that run.
    var RUN_STAT_BUCKETS = ['Regressed', 'Broken', 'Flaky', 'Fixed'];

    function computeRunStats(nodes) {
        var perScenario = nodes.map(function (n) {
            return (filterExcludedRuns(n.scenario.recentRuns) || []).slice().sort(function (a, b) { return a.runAt < b.runAt ? -1 : 1; });
        });
        var byKey = {}, order = [];
        perScenario.forEach(function (entries) {
            entries.forEach(function (r) {
                var k = runKey(r);
                if (!byKey[k]) {
                    byKey[k] = { key: k, runAt: r.runAt, runUrl: r.runUrl, executed: 0, passed: 0, failed: 0, other: 0, buckets: { Regressed: 0, Broken: 0, Flaky: 0, Fixed: 0, New: 0, Passing: 0 } };
                    order.push(byKey[k]);
                }
            });
        });
        perScenario.forEach(function (entries) {
            var chron = [];
            entries.forEach(function (r) {
                var st = byKey[runKey(r)];
                if (!isRealOutcome(r.outcome)) { st.other++; return; }
                chron.push({ pass: isPassLikeOutcome(r.outcome), retried: runRetriedToPass(r) });
                var window = historyAnalysisWindow && chron.length > historyAnalysisWindow ? chron.slice(chron.length - historyAnalysisWindow) : chron;
                st.executed++;
                if (chron[chron.length - 1].pass) st.passed++; else st.failed++;
                var v = classifyChron(window);
                st.buckets[v] = (st.buckets[v] || 0) + 1;
            });
        });
        order.sort(function (a, b) { return a.runAt < b.runAt ? -1 : 1; });
        order.forEach(function (s) { s.passRate = s.executed ? (s.passed / s.executed) * 100 : null; });
        return order;
    }

    // polarity: +1 = more is better (passes, pass rate, fixed), -1 = more is worse (failures, flaky), 0 = neutral.
    function deltaChip(cur, prev, polarity, isPercent, label) {
        if (prev === undefined || prev === null || cur === null) return el('span', 'delta delta-none', '');
        var diff = cur - prev;
        var zero = isPercent ? Math.abs(diff) < 0.05 : diff === 0;
        var cls = 'delta ' + (zero ? 'delta-zero' : (polarity === 0 ? 'delta-neutral' : ((diff > 0) === (polarity > 0) ? 'delta-good' : 'delta-bad')));
        var num = isPercent ? Math.abs(diff).toFixed(1) + ' pp' : String(Math.abs(diff));
        var chip = el('span', cls, zero ? '±0' : ((diff > 0 ? '▲ +' : '▼ −') + num));
        chip.title = label + ': ' + (isPercent ? cur.toFixed(1) + '% vs ' + prev.toFixed(1) + '%' : cur + ' vs ' + prev) + ' in the previous included run';
        return chip;
    }

    var RUN_STAT_COLUMNS = [
        { key: 'executed', label: 'Executed', pol: 0, get: function (s) { return s.executed; } },
        { key: 'passed', label: 'Passed', pol: 1, get: function (s) { return s.passed; } },
        { key: 'failed', label: 'Failed', pol: -1, get: function (s) { return s.failed; } },
        { key: 'other', label: 'Not run', pol: 0, get: function (s) { return s.other; }, hint: 'Present in the run but skipped, incomplete or ambiguous - excluded from the pass rate' },
        { key: 'rate', label: 'Pass rate', pol: 1, pct: true, get: function (s) { return s.passRate; } },
        { key: 'Regressed', label: 'New bugs', pol: -1, get: function (s) { return s.buckets.Regressed; }, hint: 'Failed in this run after passing in every earlier executed run (or failed in their first run)' },
        { key: 'Broken', label: 'Still failing', pol: -1, get: function (s) { return s.buckets.Broken; }, hint: 'Failing for several runs in a row' },
        { key: 'Flaky', label: 'Flaky', pol: -1, get: function (s) { return s.buckets.Flaky; }, hint: 'Flipped between pass and fail, or needed a retry to pass' },
        { key: 'Fixed', label: 'Fixed', pol: 1, get: function (s) { return s.buckets.Fixed; }, hint: 'Failed earlier, passing now' }
    ];

    function fmtStat(col, v) { return v === null || v === undefined ? '—' : (col.pct ? v.toFixed(1) + '%' : String(v)); }

    function buildRunStatsView(nodes) {
        var stats = computeRunStats(nodes);
        var wrap = el('div', 'run-stats');
        if (!stats.length) {
            wrap.appendChild(el('div', 'run-trend-chart-empty', 'No runs are included - tick at least one run under "Runs".'));
            return wrap;
        }
        var latest = stats[stats.length - 1];
        var prev = stats.length > 1 ? stats[stats.length - 2] : null;

        var head = el('div', 'run-stats-head');
        head.appendChild(el('strong', null, 'Latest included run'));
        head.appendChild(el('span', 'run-stats-when', formatRunTimestamp(latest.runAt)));
        head.appendChild(el('span', 'run-stats-vs', prev ? 'compared with ' + formatRunTimestamp(prev.runAt) : 'no earlier included run to compare with'));
        wrap.appendChild(head);

        var cards = el('div', 'run-stats-cards');
        RUN_STAT_COLUMNS.forEach(function (col) {
            var card = el('div', 'run-stat-card');
            if (col.hint) card.title = col.hint;
            card.appendChild(el('div', 'run-stat-label', col.label));
            var row = el('div', 'run-stat-value-row');
            row.appendChild(el('span', 'run-stat-value', fmtStat(col, col.get(latest))));
            if (prev) row.appendChild(deltaChip(col.get(latest), col.get(prev), col.pol, !!col.pct, col.label));
            card.appendChild(row);
            cards.appendChild(card);
        });
        wrap.appendChild(cards);

        var table = el('table', 'run-stats-table');
        var thead = el('thead'), hr = el('tr');
        hr.appendChild(el('th', null, 'Run'));
        RUN_STAT_COLUMNS.forEach(function (col) { var th = el('th', null, col.label); if (col.hint) th.title = col.hint; hr.appendChild(th); });
        thead.appendChild(hr);
        table.appendChild(thead);
        var tbody = el('tbody');
        stats.slice().reverse().forEach(function (s, idx, arr) {
            var before = arr[idx + 1] || null;   // previous (older) included run
            var tr = el('tr');
            var when = el('td', 'run-stats-run');
            when.textContent = formatRunTimestamp(s.runAt);
            if (idx === 0) when.appendChild(el('span', 'run-stats-latest', 'latest'));
            tr.appendChild(when);
            RUN_STAT_COLUMNS.forEach(function (col) {
                var td = el('td', 'run-stats-cell');
                td.appendChild(el('span', 'run-stats-num', fmtStat(col, col.get(s))));
                if (before) td.appendChild(deltaChip(col.get(s), col.get(before), col.pol, !!col.pct, col.label));
                tr.appendChild(td);
            });
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        var scroller = el('div', 'history-matrix-scroll');
        scroller.appendChild(table);
        wrap.appendChild(scroller);
        return wrap;
    }

    function buildHistoryContent(scope) {
        var content = el('div', 'history-content');
        var nodes = [];
        collectScenarioNodes(scope, nodes);
        nodes = nodes.filter(function (n) { return (n.scenario.recentRuns || []).length > 0; });
        if (!nodes.length) {
            content.appendChild(el('div', 'detail-empty', 'No run history is recorded for the scenarios in this selection.'));
            return content;
        }

        var runMap = {};
        nodes.forEach(function (n) {
            n.scenario.recentRuns.forEach(function (r) { runMap[runKey(r)] = { key: runKey(r), runAt: r.runAt, runUrl: r.runUrl }; });
        });
        var runs = Object.keys(runMap).map(function (k) { return runMap[k]; }).sort(function (a, b) { return a.runAt < b.runAt ? -1 : 1; });
        var includedCount = runs.filter(function (r) { return !excludedHistoryRuns[r.key]; }).length;

        content.appendChild(buildRunStatsView(nodes));

        if (includedCount < 2) {
            content.appendChild(el('div', 'run-trend-chart-empty', 'Only ' + includedCount + ' included run' + (includedCount === 1 ? '' : 's') + ' - at least two are needed to detect status changes.'));
        }

        if (nodes.length > 1) {
            var allEntries = [];
            nodes.forEach(function (n) { filterExcludedRuns(n.scenario.recentRuns).forEach(function (r) { allEntries.push(r); }); });
            buildRunTrendChart(content, allEntries, 'Outcomes per run');
        } else {
            var info = analyzeScenarioHistory(filterExcludedRuns(nodes[0].scenario.recentRuns), historyAnalysisWindow);
            content.appendChild(el('p', 'history-explanation', describeScenarioHistory(info)));
        }

        content.appendChild(buildHistoryMatrix(nodes, runs));
        return content;
    }

    var ICON_GEAR = lucide('settings');

    // History tab: toolbar (scope + "N of M runs" + settings gear) over the content. The gear opens a list of every
    // run known to the report with a checkbox each; unticking a run removes it from all calculations.
    function buildHistoryTab(scope) {
        var wrap = el('div', 'history-tab');
        var toolbar = el('div', 'history-toolbar');
        var summary = el('span', 'history-toolbar-summary');
        toolbar.appendChild(summary);

        var gear = el('button', 'history-settings-btn');
        gear.type = 'button';
        gear.setAttribute('aria-haspopup', 'true');
        gear.setAttribute('aria-expanded', 'false');
        gear.title = 'Choose which runs are included in the calculations';
        gear.innerHTML = ICON_GEAR + '<span>Runs</span>';
        toolbar.appendChild(gear);

        var panel = el('div', 'history-settings-panel');
        panel.hidden = true;
        var panelList = el('div', 'history-settings-list');
        var panelHead = el('div', 'history-settings-head');
        panelHead.appendChild(el('strong', null, 'Runs included in calculations'));
        var quick = el('span', 'history-settings-quick');
        var allBtn = el('button', 'history-settings-link', 'All');
        var noneBtn = el('button', 'history-settings-link', 'None');
        [allBtn, noneBtn].forEach(function (b) { b.type = 'button'; quick.appendChild(b); });
        panelHead.appendChild(quick);
        panel.appendChild(panelHead);
        panel.appendChild(panelList);
        panel.appendChild(el('div', 'history-settings-note', 'Unticked runs are ignored for verdicts, counts and charts. A scenario missing from a run, or not really executed in it, is never counted for that run.'));
        toolbar.appendChild(panel);
        wrap.appendChild(toolbar);

        var content = el('div');
        wrap.appendChild(content);

        var known = collectKnownRuns();
        var newestKey = known.length ? known[known.length - 1].key : null;

        function refresh() {
            var included = known.filter(function (r) { return !excludedHistoryRuns[r.key]; }).length;
            summary.textContent = included + ' of ' + known.length + ' runs included in calculations';
            panelList.innerHTML = '';
            known.slice().reverse().forEach(function (run) {
                var label = el('label', 'history-settings-item');
                var cb = document.createElement('input');
                cb.type = 'checkbox';
                cb.checked = !excludedHistoryRuns[run.key];
                cb.addEventListener('change', function () { excludedHistoryRuns[run.key] = !cb.checked; refresh(); });
                label.appendChild(cb);
                label.appendChild(el('span', 'history-settings-when', formatRunTimestamp(run.runAt)));
                label.appendChild(el('span', 'history-settings-meta', run.scenarios + ' scenarios' + (run.key === newestKey ? ' · latest' : '')));
                panelList.appendChild(label);
            });
            content.replaceChildren(buildHistoryContent(scope));
        }
        allBtn.addEventListener('click', function () { excludedHistoryRuns = {}; refresh(); });
        noneBtn.addEventListener('click', function () { known.forEach(function (r) { excludedHistoryRuns[r.key] = true; }); refresh(); });

        function setOpen(open) {
            panel.hidden = !open;
            gear.setAttribute('aria-expanded', open ? 'true' : 'false');
            gear.classList.toggle('active', open);
        }
        gear.addEventListener('click', function (ev) { ev.stopPropagation(); setOpen(panel.hidden); });
        function onDocClick(ev) {
            if (!wrap.isConnected) { document.removeEventListener('click', onDocClick); document.removeEventListener('keydown', onKey); return; }
            if (!panel.hidden && !panel.contains(ev.target)) setOpen(false);
        }
        function onKey(ev) { if (ev.key === 'Escape' && !panel.hidden) { setOpen(false); gear.focus(); } }
        document.addEventListener('click', onDocClick);
        document.addEventListener('keydown', onKey);

        refresh();
        return wrap;
    }

    // Scenario/step/feature catalogs, scoped to the focused node.
    function scopeFeatureNodes(scope) {
        var f = [];
        collectFeatureNodes(scope, f);
        return f;
    }

    var TABLES_TABS = ['Features', 'Scenarios', 'Steps', 'History'];
    var tablesActiveTab = 'Features';

    function buildTablesSection() {
        var wrap = el('div', 'tables-view');
        var scope = analyticsScopeNode();

        var scopeNodes = [];
        collectScenarioNodes(scope, scopeNodes);
        var scopeBar = el('div', 'tables-scope');
        scopeBar.appendChild(el('span', 'tables-scope-label', 'Scope'));
        scopeBar.appendChild(el('span', 'tables-scope-path', scopeBreadcrumb(scope)));
        scopeBar.appendChild(el('span', 'tables-scope-count', scopeNodes.length + ' scenario' + (scopeNodes.length === 1 ? '' : 's')));
        wrap.appendChild(scopeBar);

        // Tabs that cannot say anything about this scope are dropped instead of showing unrelated or empty data:
        // no features at or below the scope (e.g. a single scenario) -> no Features tab.
        var visibleTabs = TABLES_TABS.filter(function (tab) {
            if (tab === 'Features') return scopeFeatureNodes(scope).length > 0;
            if (tab === 'History') return scopeHasHistory(scope);
            return true;
        });
        if (visibleTabs.indexOf(tablesActiveTab) === -1) tablesActiveTab = visibleTabs[0];

        var tabs = el('div', 'tables-tabs');
        var body = el('div', 'tables-body');

        function renderBody() {
            body.innerHTML = '';
            if (tablesActiveTab === 'Features') body.appendChild(buildFeaturesTable(scope));
            else if (tablesActiveTab === 'Scenarios') body.appendChild(buildScenariosTable(scope));
            else if (tablesActiveTab === 'Steps') body.appendChild(buildStepsTable(scope));
            else body.appendChild(buildHistoryTab(scope));
        }

        visibleTabs.forEach(function (tab) {
            var btn = el('button', 'tables-tab' + (tab === tablesActiveTab ? ' active' : ''), tab);
            btn.type = 'button';
            btn.addEventListener('click', function () {
                if (tab === tablesActiveTab) return;
                tablesActiveTab = tab;
                Array.prototype.forEach.call(tabs.querySelectorAll('.tables-tab'), function (b) { b.classList.toggle('active', b.textContent === tab); });
                renderBody();
            });
            tabs.appendChild(btn);
        });
        wrap.appendChild(tabs);
        renderBody();
        wrap.appendChild(body);
        return wrap;
    }

    // Single dispatch point so every caller (row clicks, filter/search changes, theme toggle, the analytics
    // toggle itself) stays consistent about what "the detail pane" currently shows.
    function renderDetail() {
        if (analyticsMode) renderAnalyticsView();
        else renderDetailForNode(focusedNode);
        redrawTopRow();
    }

    // ---------------------------------------------------------------- filters (D4) + search (D5)
    // Matching itself already happened server-side (ReportFilterMatcher) - this only reads each scenario's
    // precomputed filterMatches, it never re-runs glob matching client-side.
    function findMatch(scenario, groupName) {
        var matches = scenario.filterMatches || [];
        for (var i = 0; i < matches.length; i++) {
            if (matches[i].group === groupName) return matches[i];
        }
        return null;
    }

    function scenarioMatchesFilters(scenario) {
        var groupName;
        for (groupName in activeFilters) {
            var excl = activeFilters[groupName];
            var m1 = findMatch(scenario, groupName);
            var label1 = m1 ? m1.label : null;
            if (label1 && excl[label1] === 'exclude') return false;
        }
        for (groupName in activeFilters) {
            var sel = activeFilters[groupName];
            var hasIncludes = false;
            for (var l in sel) { if (sel[l] === 'include') { hasIncludes = true; break; } }
            if (!hasIncludes) continue;
            var m2 = findMatch(scenario, groupName);
            var label2 = m2 ? m2.label : null;
            if (!label2 || sel[label2] !== 'include') return false;
        }
        return true;
    }

    // Mirrors scenarioMatchesFilters' three-state include/exclude cycle, for parity between status filters and
    // custom filters - an empty set means "no status filtering applied" rather than "match nothing" (item 17).
    function scenarioMatchesStatusFilters(scenario) {
        var statuses = Object.keys(activeStatusFilters);
        if (!statuses.length) return true;
        if (activeStatusFilters[scenario.status] === 'exclude') return false;
        var hasIncludes = statuses.some(function (s) { return activeStatusFilters[s] === 'include'; });
        if (hasIncludes && activeStatusFilters[scenario.status] !== 'include') return false;
        return true;
    }

    function textListMatches(list, query) {
        return (list || []).some(function (t) { return (t || '').toLowerCase().indexOf(query) !== -1; });
    }

    // Cucumber-expression placeholder syntax ({word}, {int}, {string}, ...) in the typed query signals the user
    // wants to search the step/scenario DEFINITION rather than one instantiated run of it - route those queries
    // against step.pattern/scenario.nameTemplate instead of the substituted text.
    var SEARCH_PLACEHOLDER_RE = /\{[A-Za-z][\w-]*\}/;

    function isPlaceholderQuery(query) {
        return SEARCH_PLACEHOLDER_RE.test(query);
    }

    function singleTextMatches(literalText, patternText, query) {
        if (!query) return false;
        if (isPlaceholderQuery(query) && patternText) return patternText.toLowerCase().indexOf(query) !== -1;
        return !!(literalText && literalText.toLowerCase().indexOf(query) !== -1);
    }

    function scenarioNameMatches(scenario, query) {
        return singleTextMatches(scenario.name, scenario.nameTemplate, query);
    }

    function scenarioStepMatches(scenario, query) {
        if (!query) return false;
        return (scenario.steps || []).some(function (s) {
            if (singleTextMatches(s.text, s.pattern, query)) return true;
            return textListMatches(s.hashtags, query);
        });
    }

    function scenarioTagMatches(scenario, query) {
        if (!query) return false;
        return textListMatches(scenario.tags, query) || textListMatches(scenario.hashtags, query);
    }

    // `*` wildcard matching for the advanced-search Tags field's chip terms (item 8) - each term matches a
    // whole tag/hashtag (not a substring, unlike the plain search box's scenarioTagMatches), so a bare term
    // like "smoke" means "has the tag smoke exactly" and "check*" means "any tag starting with check".
    function wildcardToRegex(term) {
        var pattern = term.split('*').map(function (part) {
            return part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        }).join('.*');
        return new RegExp('^' + pattern + '$', 'i');
    }

    function scenarioTagMatchesWildcard(scenario, term) {
        if (!term) return false;
        var re = wildcardToRegex(term);
        return (scenario.tags || []).concat(scenario.hashtags || []).some(function (t) { return re.test(t || ''); });
    }

    function matchesAnyTerm(scenario, testFn, terms) {
        return (terms || []).some(function (term) { return testFn(scenario, term); });
    }

    // Advanced search panel - per-field include/exclude on top of the plain search box. An include
    // query narrows to scenarios where that field matches; an exclude query drops scenarios where it does.
    // Tags (item 8) is `multi: true` - its include/exclude are arrays of chip terms, OR'd against each other,
    // rather than the single include/exclude string the name/step fields use.
    var advancedSearch = {
        name: { include: '', exclude: '' },
        step: { include: '', exclude: '' },
        tags: { include: [], exclude: [] }
    };
    var ADVANCED_SEARCH_FIELDS = [
        { key: 'name', test: scenarioNameMatches },
        { key: 'step', test: scenarioStepMatches },
        { key: 'tags', test: scenarioTagMatchesWildcard, multi: true }
    ];

    function scenarioMatchesAdvancedSearch(scenario) {
        return ADVANCED_SEARCH_FIELDS.every(function (f) {
            var cfg = advancedSearch[f.key];
            if (f.multi) {
                if (cfg.exclude.length && matchesAnyTerm(scenario, f.test, cfg.exclude)) return false;
                if (cfg.include.length && !matchesAnyTerm(scenario, f.test, cfg.include)) return false;
                return true;
            }
            if (cfg.exclude && f.test(scenario, cfg.exclude)) return false;
            if (cfg.include && !f.test(scenario, cfg.include)) return false;
            return true;
        });
    }

    function scenarioMatchesSearch(scenario) {
        if (!scenarioMatchesAdvancedSearch(scenario)) return false;
        if (!searchText) return true;
        return scenarioNameMatches(scenario, searchText) || scenarioTagMatches(scenario, searchText) || scenarioStepMatches(scenario, searchText);
    }

    // Bottom-up: a container is visible iff at least one descendant scenario is visible.
    function updateVisibility(node) {
        if (node.scenario) {
            var visible = scenarioMatchesFilters(node.scenario) && scenarioMatchesSearch(node.scenario) && scenarioMatchesStatusFilters(node.scenario);
            if (node.el) node.el.classList.toggle('hidden', !visible);
            return visible;
        }

        var anyVisible = false;
        node.children.forEach(function (c) { if (updateVisibility(c)) anyVisible = true; });
        if (node.el) node.el.classList.toggle('hidden', !anyVisible);
        return anyVisible;
    }

    // Non-empty-filter-state marker (item 17) - distinct from each toggle's own open/closed `.active` class,
    // so a toggle can show "has active filters" even while its panel is collapsed.
    function hasAdvancedSearchActive() {
        return ADVANCED_SEARCH_FIELDS.some(function (f) {
            var cfg = advancedSearch[f.key];
            if (f.multi) return cfg.include.length > 0 || cfg.exclude.length > 0;
            return !!(cfg.include || cfg.exclude);
        });
    }

    function hasCustomFilterActive() {
        for (var g in activeFilters) { if (Object.keys(activeFilters[g]).length) return true; }
        return false;
    }

    function hasStatusFilterActive() {
        return Object.keys(activeStatusFilters).length > 0;
    }

    function updateFilterStateMarkers() {
        if (advancedSearchToggleBtn) advancedSearchToggleBtn.classList.toggle('filter-active', hasAdvancedSearchActive());
        if (customFilterToggleBtn) customFilterToggleBtn.classList.toggle('filter-active', hasCustomFilterActive());
        if (statusFilterToggleBtn) statusFilterToggleBtn.classList.toggle('filter-active', hasStatusFilterActive());
    }

    // Red only means something ("there is a filter to clear") when it's actually true - a reset
    // button that's always red regardless of state cries wolf. Covers the three panel-level Reset buttons, the
    // toolbar's global reset-all, and every individual per-field/per-group clear button via resetButtonStates.
    function updateResetButtonStates() {
        var advancedActive = hasAdvancedSearchActive();
        var customActive = hasCustomFilterActive();
        var statusActive = hasStatusFilterActive();
        if (advancedSearchResetBtn) advancedSearchResetBtn.classList.toggle('has-value', advancedActive);
        if (customFilterResetBtn) customFilterResetBtn.classList.toggle('has-value', customActive);
        if (statusFilterResetBtn) statusFilterResetBtn.classList.toggle('has-value', statusActive);
        if (globalResetBtn) globalResetBtn.classList.toggle('has-value', !!searchText || advancedActive || customActive || statusActive);
        resetButtonStates.forEach(function (entry) { entry.btn.classList.toggle('has-value', !!entry.isActive()); });
    }

    function applyFiltersAndSearch() {
        updateVisibility(rootNode);
        updateFilterStateMarkers();
        updateResetButtonStates();
        // The detail-top-row's counts/chart are filter/search-scoped too (N3/#59) - re-dispatch through
        // renderDetail rather than calling redrawTopRow directly, so a filter change updates it either way.
        renderDetail();
    }

    // Icon-only glyphs for the docked search-row toggles and their panel controls - same
    // self-contained-inline-SVG/currentColor convention as KIND_ICON_SVG/STATUS_ICON_SVG.
    var ADVANCED_SEARCH_ICON = lucide('sliders-horizontal');
    var FILTER_ICON = lucide('funnel');
    // Plain FILTER_ICON is now the *status* filter's glyph - the custom-filter toggle gets this funnel-plus-
    // badge variant instead (item 17) so the two toggles read as visually distinct at a glance.
    var CUSTOM_FILTER_ICON = lucide('funnel-plus');
    // A '#' glyph, not a chat bubble - Gherkin comments are literally '#'-prefixed, so this reads as
    // "toggle the # comment lines" rather than "write a message" (which a speech-bubble icon implies).
    var COMMENT_ICON = lucide('hash');

    function iconToggleButton(cssClass, svg, title) {
        var btn = el('button', cssClass);
        btn.type = 'button';
        btn.title = title;
        btn.innerHTML = svg;
        return btn;
    }

    // Small plain-text "Hide" link for a panel's header - deliberately not an icon button like
    // iconToggleButton's, so it reads as a low-emphasis housekeeping action rather than competing visually
    // with the funnel/advanced-search toggles. Reset controls use resetIconButton instead.
    // Small "x" icon button for Reset controls - plain text glyph, matching the codebase's convention for
    // other small textual icon-buttons (e.g. the tree +/- toggles) rather than an inline SVG.
    function resetIconButton(title, extraClass) {
        var btn = el('button', 'icon-reset-btn' + (extraClass ? ' ' + extraClass : ''));
        btn.innerHTML = lucide('x', 12);
        btn.type = 'button';
        btn.title = title;
        btn.setAttribute('aria-label', title);
        return btn;
    }

    // Just "Reset" in a panel's header - there used to be a separate "Hide" button, but its own docked toggle
    // already closes the panel on a second click, so Hide was a second control doing the same thing.
    function buildPanelHeaderActions(panel, toggle) {
        var wrap = el('div', 'panel-header-actions');
        var resetBtn = resetIconButton('Reset');
        wrap.appendChild(resetBtn);
        return { wrap: wrap, resetBtn: resetBtn };
    }

    function buildSearchUi() {
        searchInputWrap = el('div', 'search-input-wrap');
        var box = el('input', 'search-box');
        box.type = 'search';
        box.placeholder = 'Search name, tags, or step text… (use {int}/{word}/etc. to match step/outline patterns)';
        box.addEventListener('input', function () {
            searchText = box.value.trim().toLowerCase();
            applyFiltersAndSearch();
            updateUrlState();
        });
        searchBox = box;
        searchInputWrap.appendChild(box);
        toolbar.appendChild(searchInputWrap);
        filterPanelsEl.appendChild(buildAdvancedSearchUi());
    }

    // Label + clear button share one row - the clear button used to be its own orphan row
    // below both inputs; it now sits opposite the label, at the row's right edge, with include/exclude as the
    // two rows beneath.
    function buildAdvancedSearchField(labelText, key, clearBtns) {
        var wrap = el('div', 'advanced-search-field');
        var header = el('div', 'advanced-search-field-header');
        header.appendChild(el('span', 'advanced-search-field-label', labelText));
        wrap.appendChild(header);

        var inputs = [];
        ['include', 'exclude'].forEach(function (mode) {
            var input = el('input', 'advanced-search-input advanced-search-' + mode);
            input.type = 'search';
            input.placeholder = mode === 'include' ? 'must contain…' : 'must not contain…';
            input.value = advancedSearch[key][mode];
            input.addEventListener('input', function () {
                advancedSearch[key][mode] = input.value.trim().toLowerCase();
                applyFiltersAndSearch();
            });
            inputs.push(input);
            wrap.appendChild(input);
        });

        var clearBtn = resetIconButton('Clear ' + labelText);
        clearBtn.addEventListener('click', function () {
            advancedSearch[key].include = '';
            advancedSearch[key].exclude = '';
            inputs.forEach(function (input) { input.value = ''; });
            applyFiltersAndSearch();
        });
        header.appendChild(clearBtn);
        clearBtns.push(clearBtn);
        resetButtonStates.push({ btn: clearBtn, isActive: function () { return !!(advancedSearch[key].include || advancedSearch[key].exclude); } });

        return wrap;
    }

    // One include/exclude chip box for the advanced-search Tags field (item 8) - typing a term and pressing
    // comma or Enter (or blurring the field) commits it as a removable chip instead of leaving it as free text,
    // since tags/hashtags are the one advanced-search field where "one of several exact/wildcard values" is a
    // more natural query shape than a single substring. `chips` is advancedSearch[key][mode] itself (not a
    // copy), so mutating it here mutates the live filter state directly.
    function buildTagsChipBox(mode, key) {
        var box = el('div', 'chip-input' + (mode === 'exclude' ? ' advanced-search-exclude' : ''));
        var chips = advancedSearch[key][mode];
        var input = el('input', 'chip-input-text');
        input.type = 'text';
        input.placeholder = mode === 'include' ? 'tag, tag*… (Enter/comma)' : 'must not have tag…';

        function renderChips() {
            Array.prototype.slice.call(box.querySelectorAll('.chip')).forEach(function (c) { c.remove(); });
            chips.forEach(function (term, index) {
                var chip = el('span', 'chip');
                chip.appendChild(document.createTextNode(term));
                var removeBtn = el('span', 'chip-remove', '×');
                removeBtn.addEventListener('click', function () {
                    chips.splice(index, 1);
                    renderChips();
                    applyFiltersAndSearch();
                });
                chip.appendChild(removeBtn);
                box.insertBefore(chip, input);
            });
        }

        function commitPending() {
            var term = input.value.trim();
            input.value = '';
            if (!term) return;
            chips.push(term);
            renderChips();
        }

        input.addEventListener('keydown', function (e) {
            if (e.key === ',' || e.key === 'Enter') {
                e.preventDefault();
                commitPending();
                applyFiltersAndSearch();
            } else if (e.key === 'Backspace' && !input.value && chips.length) {
                chips.pop();
                renderChips();
                applyFiltersAndSearch();
            }
        });
        input.addEventListener('blur', function () {
            var hadPending = !!input.value.trim();
            commitPending();
            if (hadPending) applyFiltersAndSearch();
        });

        box.appendChild(input);
        renderChips();

        return {
            box: box,
            clear: function () {
                chips.length = 0;
                input.value = '';
                renderChips();
            }
        };
    }

    function buildAdvancedSearchTagsField(clearBtns) {
        var wrap = el('div', 'advanced-search-field');
        var header = el('div', 'advanced-search-field-header');
        header.appendChild(el('span', 'advanced-search-field-label', 'Tags'));
        wrap.appendChild(header);

        var includeBox = buildTagsChipBox('include', 'tags');
        var excludeBox = buildTagsChipBox('exclude', 'tags');
        wrap.appendChild(includeBox.box);
        wrap.appendChild(excludeBox.box);

        var clearBtn = resetIconButton('Clear Tags');
        clearBtn.addEventListener('click', function () {
            includeBox.clear();
            excludeBox.clear();
            applyFiltersAndSearch();
        });
        header.appendChild(clearBtn);
        clearBtns.push(clearBtn);
        resetButtonStates.push({ btn: clearBtn, isActive: function () { return advancedSearch.tags.include.length > 0 || advancedSearch.tags.exclude.length > 0; } });

        return wrap;
    }

    // Docked, icon-only disclosure - the toggle sits flush against the search input's trailing
    // edge as one visual control, rather than a text <summary> below it; the panel itself only ever occupies a
    // fresh, full-width row of the (flex-wrap) toolbar once opened, so the collapsed toolbar stays compact.
    function buildAdvancedSearchUi() {
        var toggle = iconToggleButton('icon-toggle-btn advanced-search-toggle', ADVANCED_SEARCH_ICON, 'Advanced search');
        advancedSearchToggleBtn = toggle;
        searchInputWrap.appendChild(toggle);

        var panel = el('div', 'advanced-search-panel hidden');

        var header = el('div', 'advanced-search-panel-header');
        header.appendChild(el('span', 'advanced-search-panel-title', 'Advanced search'));
        var headerActions = buildPanelHeaderActions(panel, toggle);
        var resetAllBtn = headerActions.resetBtn;
        resetAllBtn.title = 'Reset all advanced search fields';
        advancedSearchResetBtn = resetAllBtn;
        header.appendChild(headerActions.wrap);
        panel.appendChild(header);

        var grid = el('div', 'advanced-search-grid');
        var clearBtns = [];
        grid.appendChild(buildAdvancedSearchField('Node name', 'name', clearBtns));
        grid.appendChild(buildAdvancedSearchField('Step text', 'step', clearBtns));
        grid.appendChild(buildAdvancedSearchTagsField(clearBtns));
        panel.appendChild(grid);

        resetAllBtn.addEventListener('click', function () { clearBtns.forEach(function (btn) { btn.click(); }); });

        toggle.addEventListener('click', function () {
            var open = panel.classList.toggle('hidden') === false;
            toggle.classList.toggle('active', open);
        });

        return panel;
    }

    // Collapsed by default behind an icon toggle docked next to the advanced-search one - one
    // full-width row per filter group instead of every group's pills wrapping together into a single paragraph.
    function buildFilterUi() {
        if (!filterGroups.length) return;

        var toggle = iconToggleButton('icon-toggle-btn filter-toggle', CUSTOM_FILTER_ICON, 'Custom filters');
        customFilterToggleBtn = toggle;
        searchInputWrap.appendChild(toggle);

        var panel = el('div', 'filter-panel hidden');

        var header = el('div', 'filter-panel-header');
        header.appendChild(el('span', 'filter-panel-title', 'Custom filters'));
        var headerActions = buildPanelHeaderActions(panel, toggle);
        var resetAllBtn = headerActions.resetBtn;
        resetAllBtn.title = 'Reset all custom filters';
        customFilterResetBtn = resetAllBtn;
        header.appendChild(headerActions.wrap);
        panel.appendChild(header);

        var allPills = [];

        filterGroups.forEach(function (group) {
            activeFilters[group.name] = {};
            var groupDiv = el('div', 'filter-group');
            groupDiv.appendChild(el('span', 'filter-group-name', group.name));

            var groupPills = [];
            var options = (group.options || []).concat([{ label: group.otherLabel, bg: group.otherBg, fg: group.otherFg }]);
            options.forEach(function (opt) {
                var pill = el('button', 'filter-pill neutral', opt.label);
                pill.type = 'button';
                pill.style.setProperty('--pill-bg', opt.bg);
                pill.style.setProperty('--pill-fg', opt.fg || readableTextOn(opt.bg));
                pill.addEventListener('click', function () {
                    var sel = activeFilters[group.name];
                    var cur = sel[opt.label];
                    var next = cur === undefined ? 'include' : (cur === 'include' ? 'exclude' : undefined);
                    if (next === undefined) delete sel[opt.label]; else sel[opt.label] = next;
                    pill.className = 'filter-pill ' + (next || 'neutral');
                    applyFiltersAndSearch();
                });
                allPills.push(pill);
                groupPills.push(pill);
                groupDiv.appendChild(pill);
            });

            // Per-group reset, pinned to this row's far-right end - previously only the whole
            // panel's header Reset existed, with no way to clear just one group (e.g. severity) without touching
            // the others (e.g. priority).
            var groupResetBtn = resetIconButton('Clear ' + group.name, 'filter-group-reset');
            groupResetBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                activeFilters[group.name] = {};
                groupPills.forEach(function (pill) { pill.className = 'filter-pill neutral'; });
                applyFiltersAndSearch();
            });
            groupDiv.appendChild(groupResetBtn);
            resetButtonStates.push({ btn: groupResetBtn, isActive: function () { return Object.keys(activeFilters[group.name]).length > 0; } });

            panel.appendChild(groupDiv);
        });

        resetAllBtn.addEventListener('click', function () {
            filterGroups.forEach(function (group) { activeFilters[group.name] = {}; });
            allPills.forEach(function (pill) { pill.className = 'filter-pill neutral'; });
            applyFiltersAndSearch();
        });

        toggle.addEventListener('click', function () {
            var open = panel.classList.toggle('hidden') === false;
            toggle.classList.toggle('active', open);
        });

        filterPanelsEl.appendChild(panel);
    }

    // Status filter panel (item 17) - one pill per STATUS_ORDER entry, always available (unlike buildFilterUi,
    // there's no "no groups defined" case to early-return on). Pills cycle the same three-state include/exclude
    // pattern as buildFilterUi - see scenarioMatchesStatusFilters.
    function buildStatusFilterUi() {
        var toggle = iconToggleButton('icon-toggle-btn status-filter-toggle', FILTER_ICON, 'Status filters');
        statusFilterToggleBtn = toggle;
        searchInputWrap.appendChild(toggle);

        var panel = el('div', 'filter-panel hidden');

        var header = el('div', 'filter-panel-header');
        header.appendChild(el('span', 'filter-panel-title', 'Status filters'));
        var headerActions = buildPanelHeaderActions(panel, toggle);
        var resetAllBtn = headerActions.resetBtn;
        resetAllBtn.title = 'Reset all status filters';
        statusFilterResetBtn = resetAllBtn;
        header.appendChild(headerActions.wrap);
        panel.appendChild(header);

        var groupDiv = el('div', 'filter-group');
        var allPills = [];

        STATUS_ORDER.forEach(function (status) {
            var pill = el('button', 'filter-pill neutral', status);
            pill.type = 'button';
            pill.style.setProperty('--pill-bg', statusColor(status));
            pill.style.setProperty('--pill-fg', '#ffffff');
            pill.addEventListener('click', function () {
                var cur = activeStatusFilters[status];
                var next = cur === undefined ? 'include' : (cur === 'include' ? 'exclude' : undefined);
                if (next === undefined) delete activeStatusFilters[status]; else activeStatusFilters[status] = next;
                pill.className = 'filter-pill ' + (next || 'neutral');
                applyFiltersAndSearch();
            });
            allPills.push(pill);
            groupDiv.appendChild(pill);
        });

        panel.appendChild(groupDiv);

        resetAllBtn.addEventListener('click', function () {
            activeStatusFilters = {};
            allPills.forEach(function (pill) { pill.className = 'filter-pill neutral'; });
            applyFiltersAndSearch();
        });

        toggle.addEventListener('click', function () {
            var open = panel.classList.toggle('hidden') === false;
            toggle.classList.toggle('active', open);
        });

        filterPanelsEl.appendChild(panel);
    }

    // Hide Gherkin comments (.node-comment), shown by default (an advantage over LivingDoc/
    // Expressium, which drop comments entirely - see project_reporting_library memory). Plain binary toggle,
    // no panel - mirrors buildSidebarCollapseToggle's persisted-boolean pattern rather than the three
    // open/close panels above, since there's nothing to configure beyond on/off. Toggles a class on
    // documentElement (same root the theme toggle already uses) which report.css's ".comments-hidden
    // .node-comment { display: none; }" rule keys off - pure client-side visibility,
    // no re-render, per feedback_client_side_computation.
    // Lives in the right-aligned header-icon-group next to the theme toggle, not among the
    // search/filter toggles - it's a display preference for the whole report, not a filter over its content.
    var COMMENTS_HIDDEN_KEY = 'ohirok-report-comments-hidden';

    function buildCommentsToggleUi(container) {
        var hidden = false;
        try { hidden = localStorage.getItem(COMMENTS_HIDDEN_KEY) === '1'; } catch (e) { /* storage unavailable - defaults to shown */ }
        document.documentElement.classList.toggle('comments-hidden', hidden);

        var toggle = iconToggleButton('header-icon-btn comments-toggle', COMMENT_ICON, hidden ? 'Show comments' : 'Hide comments');
        toggle.classList.toggle('active', hidden);
        toggle.addEventListener('click', function () {
            hidden = !hidden;
            document.documentElement.classList.toggle('comments-hidden', hidden);
            toggle.classList.toggle('active', hidden);
            toggle.title = hidden ? 'Show comments' : 'Hide comments';
            try { localStorage.setItem(COMMENTS_HIDDEN_KEY, hidden ? '1' : '0'); } catch (e) { /* storage unavailable - just won't persist */ }
        });
        container.appendChild(toggle);
    }

    // One-click clear-everything (item 17) - reuses each panel's own reset-all button rather than duplicating
    // their clearing logic, plus the plain search box which none of those three cover.
    function buildGlobalResetButton() {
        var btn = resetIconButton('Reset all search and filters', 'toolbar-reset-all');
        globalResetBtn = btn;
        btn.addEventListener('click', function () {
            if (searchBox) { searchBox.value = ''; searchText = ''; }
            if (advancedSearchResetBtn) advancedSearchResetBtn.click();
            if (customFilterResetBtn) customFilterResetBtn.click();
            if (statusFilterResetBtn) statusFilterResetBtn.click();
            applyFiltersAndSearch();
        });
        searchInputWrap.appendChild(btn);
    }

    // Print button - native window.print(); every browser's print dialog already offers
    // "Save as PDF", so no PDF library is needed. Lives beside the analytics/theme toggles, left of both.
    function buildPrintButton() {
        var btn = el('button', 'header-icon-btn print-toggle');
        btn.type = 'button';
        btn.title = 'Print report / save as PDF';
        btn.innerHTML = lucide('printer');
        btn.addEventListener('click', function () { window.print(); });
        return btn;
    }

    // Right-aligned icon cluster - analytics, print, and theme as one visual group
    // (margin-left:auto lives on the group, not on themeBtn alone), in that left-to-right order, instead of
    // three loose siblings in the title row where only theme ended up pinned to the far right.
    function buildToolbarExtras() {
        var group = el('div', 'header-icon-group');
        group.appendChild(buildAnalyticsToggleButton());
        group.appendChild(buildPrintButton());
        if (REPORT_HAS_RERUNS) group.appendChild(buildRerunsToggleButton());
        if (FRAMED) group.appendChild(buildOpenInWindowButton());
        buildCommentsToggleUi(group);

        themeBtn = el('button', 'theme-toggle');
        themeBtn.type = 'button';
        themeBtn.addEventListener('click', toggleTheme);
        group.appendChild(themeBtn);
        updateThemeButton();

        headerTitleRow.appendChild(group);
    }

    function buildOpenInWindowButton() {
        var btn = el('button', 'analytics-toggle open-in-window');
        btn.type = 'button';
        btn.title = 'Open the report in a window of its own';
        btn.innerHTML = lucide('square-arrow-out-up-right');
        btn.addEventListener('click', function () {
            var url = URL.createObjectURL(new Blob([ORIGINAL_PAGE], { type: 'text/html' }));
            window.open(url, '_blank');
        });
        return btn;
    }

    // Only present when the report contains reruns. It changes the data, not just the view, so unlike the icon-only
    // view toggles it carries a visible label and state. Applied at load time (see applyRerunChoice): a click stores
    // the choice, writes ?reruns=on|off into the URL (which keeps the selected node) and reloads.
    function buildRerunsToggleButton() {
        var btn = el('button', 'reruns-toggle' + (INCLUDE_RERUNS ? ' active' : ''));
        btn.type = 'button';
        btn.setAttribute('aria-pressed', INCLUDE_RERUNS ? 'true' : 'false');
        btn.title = INCLUDE_RERUNS
            ? 'Including reruns of failed scenarios - click to see the pipeline\'s first pass only'
            : 'Showing the pipeline\'s first pass only - click to include the reruns of failed scenarios';
        btn.innerHTML = lucide('refresh-cw', 14);
        btn.appendChild(el('span', 'reruns-toggle-label', INCLUDE_RERUNS ? 'Reruns: on' : 'Reruns: off'));
        btn.addEventListener('click', function () {
            var next = !INCLUDE_RERUNS;
            try { localStorage.setItem(RERUNS_KEY, next ? '1' : '0'); } catch (e) { /* the URL parameter carries it */ }
            var url = new URL(location.href);
            url.searchParams.set('reruns', next ? 'on' : 'off');
            location.href = url.toString();
        });
        return btn;
    }

    // Persistent header control - previously rebuilt fresh into the detail-top-row on every
    // redraw; moved into the header alongside print/theme since analyticsMode is a single global flag, not
    // per-node state, so one persistent button is all it ever needed. Toggles its own 'active' class directly
    // since redrawTopRow no longer rebuilds it on focus/filter changes.
    function buildAnalyticsToggleButton() {
        var btn = el('button', 'analytics-toggle' + (analyticsMode ? ' active' : ''));
        btn.type = 'button';
        btn.title = 'Toggle analytics view (whole-tree chart + flat tables)';
        btn.innerHTML = lucide('chart-column');
        btn.addEventListener('click', function () {
            analyticsMode = !analyticsMode;
            btn.classList.toggle('active', analyticsMode);
            renderDetail();
        });
        return btn;
    }

    // Per-node +/- subtree expand/collapse - walks the node.children graph computeAggregateStatus/
    // updateVisibility already use, calling the setExpanded closure createContainerTreeNode stashed on each
    // node (leaf scenario nodes have no setExpanded, so they're silently skipped by the `if` check below).
    function setAllExpanded(node, exp) {
        if (node.setExpanded) node.setExpanded(exp);
        (node.children || []).forEach(function (c) { setAllExpanded(c, exp); });
    }

    // ---------------------------------------------------------------- draggable sidebar/detail splitter
    var SIDEBAR_WIDTH_KEY = 'ohirok-report-sidebar-width';

    function buildSplitter() {
        if (!splitterEl || !sidebarEl) return;

        try {
            var saved = localStorage.getItem(SIDEBAR_WIDTH_KEY);
            if (saved) sidebarEl.style.flexBasis = saved;
        } catch (e) { /* storage unavailable - falls back to the CSS default */ }

        var dragging = false;

        splitterEl.addEventListener('mousedown', function (e) {
            if (e.target !== splitterEl) return; // ignore drags started on the collapse button
            if (sidebarEl.classList.contains('sidebar-hidden')) return; // nothing to resize while the tree is hidden
            dragging = true;
            splitterEl.classList.add('dragging');
            e.preventDefault();
        });

        document.addEventListener('mousemove', function (e) {
            if (!dragging) return;
            var rect = sidebarEl.parentNode.getBoundingClientRect();
            // Flat floor - search/filters moved into the header, so the sidebar no longer
            // holds a toolbar whose natural width this needs to protect; the old searchInputWrap.scrollWidth floor
            // now reports the HEADER's full toolbar width (hundreds of px), which clamped every drag to a fixed
            // rect.width*0.7 and made the splitter look broken (cursor showed col-resize, width never actually
            // changed). #ohirok-report-sidebar's own CSS min-width (12rem) is the real hard floor regardless.
            var minWidth = 160;
            var width = Math.min(Math.max(e.clientX - rect.left, minWidth), rect.width * 0.7);
            sidebarEl.style.flexBasis = width + 'px';
        });

        document.addEventListener('mouseup', function () {
            if (!dragging) return;
            dragging = false;
            splitterEl.classList.remove('dragging');
            try { localStorage.setItem(SIDEBAR_WIDTH_KEY, sidebarEl.style.flexBasis); } catch (e) { /* storage unavailable */ }
        });
    }

    // ---------------------------------------------------------------- sidebar collapse/expand (item 4)
    // Same "shows the action, not the current state" convention as the theme toggle above: a left-pointing
    // chevron while expanded (clicking it collapses the tree to the left), a right-pointing chevron while
    // collapsed (clicking it expands the tree back out to the right).
    var SIDEBAR_COLLAPSED_KEY = 'ohirok-report-sidebar-collapsed';
    var CHEVRON_LEFT_ICON = lucide('chevron-left');
    var CHEVRON_RIGHT_ICON = lucide('chevron-right');

    // The show/hide button sits at the bottom of the splitter line and stays there while the tree is hidden.
    function updateSidebarCollapseButton(btn, collapsed) {
        btn.innerHTML = collapsed ? CHEVRON_RIGHT_ICON : CHEVRON_LEFT_ICON;
        btn.title = collapsed ? 'Show scenario tree' : 'Hide scenario tree';
        btn.setAttribute('aria-label', btn.title);
        btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        btn.setAttribute('aria-controls', sidebarEl.id);
    }

    function buildSidebarCollapseToggle() {
        if (!splitterEl || !sidebarEl || !sidebarEl.parentNode) return;

        var collapsed = false;
        try { collapsed = localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === '1'; } catch (e) { /* storage unavailable - defaults to expanded */ }
        sidebarEl.classList.toggle('sidebar-hidden', collapsed);

        var btn = el('button', 'sidebar-collapse-btn');
        btn.type = 'button';
        updateSidebarCollapseButton(btn, collapsed);
        btn.addEventListener('click', function () {
            collapsed = !collapsed;
            sidebarEl.classList.toggle('sidebar-hidden', collapsed);
            updateSidebarCollapseButton(btn, collapsed);
            try { localStorage.setItem(SIDEBAR_COLLAPSED_KEY, collapsed ? '1' : '0'); } catch (e) { /* storage unavailable - just won't persist */ }
        });
        splitterEl.appendChild(btn);
    }
    // ---------------------------------------------------------------- charts (D7) - live in the detail pane (I3),
    // scoped to whatever node is currently focused.
    var STATUS_ORDER = ['Passed', 'PassWithIssues', 'Failed', 'Skipped', 'Pending', 'Undefined', 'Ambiguous', 'NotRun', 'Unknown'];

    // recentRuns.outcome is one of these 6 RunOutcome values (the "W-full" outcome granularity widened this from the
    // original 3 - PassWithIssues/Skipped/Ambiguous now map to their own distinct value instead of folding
    // into Passed/Incomplete) - distinct from STATUS_ORDER's 9-value ReportStatus vocabulary, which is what
    // scenario/attempt .status actually is. The two vocabularies deliberately reuse the same string names
    // where they overlap, so statusColor()/statusIcon() work unchanged for either one.
    var RUN_OUTCOME_ORDER = ['Passed', 'PassWithIssues', 'Failed', 'Skipped', 'Ambiguous', 'Incomplete'];

    function statusColor(status) {
        var suffix = status.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase();
        var value = getComputedStyle(document.documentElement).getPropertyValue('--color-' + suffix).trim();
        return value || '#9e9e9e';
    }

    // Health is derived client-side from scenario.recentRuns (the server no longer ships it). It is judged from the
    // scenario's own pass/fail history over the runs in which it ACTUALLY EXECUTED - runs where it is absent (no
    // entry) or did not really run (Skipped / Incomplete / Ambiguous) are ignored, never counted as pass or fail.
    //   Latest executed run FAILED:
    //     * New bug  - it passed in every earlier executed run, or it is brand new (first recorded run);
    //     * Flaky    - its history already flipped between pass and fail (2+ changes);
    //     * Failing  - otherwise (failing for several runs in a row: an existing failure, not a new one).
    //   Latest executed run PASSED:
    //     * Flaky    - it needed a retry to pass in some run, or flipped between pass and fail 2+ times;
    //     * Fixed    - it failed earlier and has passed since (one fail->pass change);
    //     * New      - first recorded run, passed;
    //     * Passing  - passed in every executed run.
    function isRealOutcome(outcome) {
        return outcome !== 'Incomplete' && outcome !== 'Skipped' && outcome !== 'Ambiguous';
    }

    function isPassLikeOutcome(outcome) {
        return outcome === 'Passed' || outcome === 'PassWithIssues';
    }

    function runKey(r) { return r.runId || r.runAt; }

    // Did the scenario pass only after retrying in this run? Historical snapshots carry the RetrySucceeded issue.
    function runRetriedToPass(r) {
        var snap = historicalSnapshotForRunSummary(r);
        return !!(snap && (snap.issues || []).some(function (i) { return i.category === 'RetrySucceeded'; }));
    }

    // chron: oldest -> newest [{ pass, retried }] of REAL runs only.
    function classifyChron(chron) {
        var n = chron.length;
        if (!n) return 'Unknown';
        var transitions = 0, fails = 0, retried = false, priorAllPass = true;
        chron.forEach(function (c, i) {
            if (i > 0 && c.pass !== chron[i - 1].pass) transitions++;
            if (!c.pass) fails++;
            if (c.retried) retried = true;
            if (i < n - 1 && !c.pass) priorAllPass = false;
        });
        if (!chron[n - 1].pass) {
            if (priorAllPass) return 'Regressed';
            return transitions >= 2 ? 'Flaky' : 'Broken';
        }
        if (fails === 0) return retried ? 'Flaky' : (n === 1 ? 'New' : 'Passing');
        return (transitions >= 2 || retried) ? 'Flaky' : 'Fixed';
    }

    function realChronology(entries, maxRealRuns) {
        var asc = (entries || []).slice().sort(function (a, b) { return a.runAt < b.runAt ? -1 : (a.runAt > b.runAt ? 1 : 0); });
        var real = asc.filter(function (r) { return isRealOutcome(r.outcome); });
        if (maxRealRuns && real.length > maxRealRuns) real = real.slice(real.length - maxRealRuns);
        return { all: asc, real: real };
    }

    function analyzeScenarioHistory(entries, maxRealRuns) {
        var c = realChronology(entries, maxRealRuns);
        var chron = c.real.map(function (r) { return { pass: isPassLikeOutcome(r.outcome), retried: runRetriedToPass(r), r: r }; });
        var info = {
            health: classifyChron(chron), considered: chron.length, ignored: c.all.length - chron.length, passes: 0, fails: 0,
            transitions: 0, retried: 0, priorPasses: 0, failingSince: null, fixedAt: null, streak: 0, latest: chron.length ? chron[chron.length - 1].r : null
        };
        chron.forEach(function (x, i) {
            if (x.pass) info.passes++; else info.fails++;
            if (x.retried) info.retried++;
            if (i > 0 && x.pass !== chron[i - 1].pass) info.transitions++;
        });
        if (chron.length) {
            var last = chron.length - 1;
            var k = last;
            while (k >= 0 && chron[k].pass === chron[last].pass) k--;
            info.streak = last - k;
            if (!chron[last].pass) { info.failingSince = chron[k + 1].r; info.priorPasses = chron.slice(0, k + 1).filter(function (x) { return x.pass; }).length; }
            else if (k >= 0) info.fixedAt = chron[k + 1].r;
        }
        return info;
    }

    // Kept for the existing callers (tables, root/run health strip): returns just the verdict.
    function analyzeScenarioHealth(runsNewestFirst, maxRealRuns) {
        return analyzeScenarioHistory(runsNewestFirst, maxRealRuns).health;
    }

    function describeScenarioHistory(info) {
        var parts = [];
        var n = info.considered;
        var runWord = function (c) { return c + ' run' + (c === 1 ? '' : 's'); };
        switch (info.health) {
            case 'Regressed':
                parts.push(n === 1
                    ? 'New bug: it failed in its first recorded run.'
                    : 'New bug: failed in the latest run (' + formatRunShort(info.latest.runAt) + ') after passing in all ' + runWord(info.priorPasses) + ' before it.');
                break;
            case 'Broken':
                parts.push('Failing for ' + runWord(info.streak) + ' in a row, since ' + formatRunShort(info.failingSince.runAt) + (info.passes ? ' (it passed before that)' : '') + ' - an existing failure, not a new one.');
                break;
            case 'Fixed': parts.push('Fixed: it failed earlier and has passed since ' + formatRunShort(info.fixedAt.runAt) + '.'); break;
            case 'Flaky': {
                var why = [];
                if (info.transitions >= 2) why.push('its result flipped between pass and fail ' + info.transitions + ' times across ' + runWord(n));
                if (info.retried) why.push('it needed a retry to pass in ' + runWord(info.retried));
                parts.push('Flaky: ' + why.join(' and ') + '.');
                break;
            }
            case 'New': parts.push('First recorded run (passed) - nothing to compare with yet.'); break;
            case 'Passing': parts.push('Passing in all ' + runWord(n) + '.'); break;
            default: parts.push('No executed run is included in the calculation.');
        }
        if (info.ignored > 0) parts.push(runWord(info.ignored) + ' not counted (skipped, incomplete, ambiguous or outside the analysis window).');
        return parts.join(' ');
    }

    // Runs the viewer unticked in the History tab's settings list are removed from every statistic (health, trend,
    // matrix verdicts). Keyed per RUN (runId, falling back to runAt) - the old per-calendar-date key merged
    // several runs of the same day into one column. In-memory only. The per-scenario history strip is never
    // filtered by it: that shows what happened, not a statistic.
    var excludedHistoryRuns = {};

    function isRunExcluded(r) { return !!excludedHistoryRuns[runKey(r)]; }

    function filterExcludedRuns(recentRuns) {
        var entries = recentRuns || [];
        if (!entries.length) return entries;
        var anyExcluded = false;
        for (var k in excludedHistoryRuns) { if (excludedHistoryRuns[k]) { anyExcluded = true; break; } }
        if (!anyExcluded) return entries;
        return entries.filter(function (r) { return !isRunExcluded(r); });
    }

    // A scenario only has real history when at least one prior run carries a usable historical snapshot,
    // not merely a summary row for the current run. The summary-only row from "recentRuns" is intentionally
    // not enough to render a historical detail switcher or to open a historical scenario view.
    function hasRealHistory(recentRuns) {
        var entries = recentRuns || [];
        return entries.some(function (r) { return !!historicalSnapshotForRunSummary(r); });
    }

    function anyScenarioHasRealHistory() {
        var nodes = [];
        collectScenarioNodes(rootNode, nodes);
        return nodes.some(function (n) { return hasRealHistory(n.scenario.recentRuns); });
    }

    // Tallies by each scenario's own derived status when walking a container's descendants - a scenario
    // contributes exactly one count, never per-attempt, since attempt child nodes are never reached here (the
    // scenario branch below returns before recursing into node.children). The `n.attempt` branch only fires when
    // an attempt node is itself the root of this walk - i.e. when the user has focused one attempt directly - so
    // that node's own chart/inline-counts reflect that specific attempt's status instead of the scenario's.
    function computeStatusCounts(node, respectFilters) {
        var counts = {};
        (function visit(n) {
            if (n.attempt) {
                if (respectFilters && n.el && n.el.classList.contains('hidden')) return;
                counts[n.attempt.status] = (counts[n.attempt.status] || 0) + 1;
                return;
            }
            if (n.scenario) {
                if (respectFilters && n.el && n.el.classList.contains('hidden')) return;
                counts[n.scenario.status] = (counts[n.scenario.status] || 0) + 1;
                return;
            }
            n.children.forEach(visit);
        })(node);
        return counts;
    }

    function buildDonut(counts, title) {
        var total = 0;
        STATUS_ORDER.forEach(function (s) { total += counts[s] || 0; });

        var wrap = el('div', 'donut-wrap');
        wrap.appendChild(el('div', 'donut-title', title));

        var size = 120, radius = 50, cx = size / 2, cy = size / 2;
        var circumference = 2 * Math.PI * radius;
        var svgNS = 'http://www.w3.org/2000/svg';

        var svg = document.createElementNS(svgNS, 'svg');
        svg.setAttribute('viewBox', '0 0 ' + size + ' ' + size);
        svg.setAttribute('class', 'donut-chart');

        var bg = document.createElementNS(svgNS, 'circle');
        bg.setAttribute('cx', cx); bg.setAttribute('cy', cy); bg.setAttribute('r', radius);
        bg.setAttribute('class', 'donut-bg');
        svg.appendChild(bg);

        var offset = 0;
        STATUS_ORDER.forEach(function (status) {
            var count = counts[status] || 0;
            if (count === 0 || total === 0) return;
            var len = (count / total) * circumference;
            var seg = document.createElementNS(svgNS, 'circle');
            seg.setAttribute('cx', cx); seg.setAttribute('cy', cy); seg.setAttribute('r', radius);
            seg.setAttribute('class', 'donut-seg');
            seg.style.stroke = statusColor(status);
            seg.setAttribute('stroke-dasharray', len + ' ' + (circumference - len));
            seg.setAttribute('stroke-dashoffset', String(-offset));
            seg.setAttribute('transform', 'rotate(-90 ' + cx + ' ' + cy + ')');
            svg.appendChild(seg);
            offset += len;
        });

        // A bare unlabeled total in the center read as meaningless out of context; show
        // passed/total (the two numbers that actually matter at a glance) plus a small "passed" caption,
        // instead of one ambiguous digit.
        var passed = counts['Passed'] || 0;

        var totalLabel = document.createElementNS(svgNS, 'text');
        totalLabel.setAttribute('x', cx); totalLabel.setAttribute('y', cy - 7);
        totalLabel.setAttribute('class', 'donut-total');
        totalLabel.textContent = passed + '/' + total;
        svg.appendChild(totalLabel);

        var totalSubLabel = document.createElementNS(svgNS, 'text');
        totalSubLabel.setAttribute('x', cx); totalSubLabel.setAttribute('y', cy + 11);
        totalSubLabel.setAttribute('class', 'donut-total-sub');
        totalSubLabel.textContent = 'passed';
        svg.appendChild(totalSubLabel);

        var body = el('div', 'donut-body');
        body.appendChild(svg);

        var legend = el('div', 'donut-legend');
        STATUS_ORDER.forEach(function (status) {
            var count = counts[status] || 0;
            if (count === 0) return;
            var item = el('span', 'donut-legend-item');
            var dot = el('span', 'donut-legend-dot');
            dot.style.background = statusColor(status);
            item.appendChild(dot);
            item.appendChild(document.createTextNode(status + ': ' + count + ' (' + pctLabel(total ? (count / total) * 100 : 0) + ')'));
            legend.appendChild(item);
        });
        body.appendChild(legend);
        wrap.appendChild(body);

        return wrap;
    }

    // Flattens every visible scenario's recentRuns history under `node` into one list - used both for
    // the whole-suite trend (rootNode) and the History tab's combined chart above the per-scenario matrix.
    function collectAggregateRecentRuns(node) {
        var scenarioNodes = [];
        collectScenarioNodes(node, scenarioNodes);
        var runs = [];
        scenarioNodes.forEach(function (n) { filterExcludedRuns(n.scenario.recentRuns).forEach(function (r) { runs.push(r); }); });
        return runs;
    }

    // A compact dot per prior run (oldest-to-newest, left-to-right) directly under a scenario's title.
    // Each dot click-through to that run's own archived report when a runUrl was shipped for it (deep-linked
    // straight to this same scenario within that report, not just its root - see getScenarioPathWithinRun).
    // Returns null (renders nothing) when there's no history to show, so reports with no history backend
    // configured stay exactly as history-free as before. Deliberately NOT filtered by excludedHistoryDates -
    // this strip shows what actually happened, not a statistic a viewer can mute.
    // Clock icon prefix so the strip reads as "history" at a glance rather than an unlabeled row of
    // marks. Marks are the same filled status icons used everywhere else in the report (a
    // plain color dash carries less information than the real status glyph), inside a thin rounded outline so
    // the whole strip reads as one distinct "history" section rather than loose marks floating in the layout.
    var HISTORY_STRIP_ICON = lucide('history');

    function buildScenarioHistoryStrip(recentRuns, node) {
        // recentRuns is NEWEST-FIRST on the wire (index 0 = the run this report was generated from). The strip shows
        // them oldest -> newest, left to right, and only appears when at least one earlier run exists.
        var entries = (recentRuns || []).filter(function (r) { return !!historicalSnapshotForRunSummary(r); });
        var currentRun = node && node.scenario && node.scenario.recentRuns && node.scenario.recentRuns.length ? node.scenario.recentRuns[0] : null;
        function same(a, b) { return !!a && !!b && a.runAt === b.runAt && (a.runUrl === b.runUrl || (!a.runUrl && !b.runUrl)); }
        // The current run is NOT drawn here: the banner's "Back to current run" button is the way back to it, so the
        // strip lists only the earlier runs and appears only when at least one exists.
        entries = entries.filter(function (r) { return !same(currentRun, r); });
        if (entries.length < 1) return null;

        var selectedRun = selectedScenarioHistory && selectedScenarioHistory.node === node ? selectedScenarioHistory.runSummary : null;

        var strip = el('div', 'scenario-history-strip');
        strip.setAttribute('role', 'group');
        strip.setAttribute('aria-label', 'Run history of this scenario - choose a run to view its recorded state');
        var icon = el('span', 'scenario-history-strip-icon');
        icon.innerHTML = HISTORY_STRIP_ICON;
        strip.appendChild(icon);
        strip.appendChild(el('span', 'scenario-history-strip-label', 'History'));

        var marks = el('span', 'scenario-history-marks');
        entries.slice().reverse().forEach(function (r) {
            var snap = historicalSnapshotForRunSummary(r);
            var outcome = r.outcome || (snap && snap.status) || 'Unknown';
            var isSelected = same(selectedRun, r);
            var when = formatRunTimestamp(r.runAt);

            var button = el('button', 'scenario-history-mark-button' + (isSelected ? ' is-selected' : ''));
            button.type = 'button';
            var label = outcome + ' — ' + when;
            button.title = label;
            button.setAttribute('aria-label', label);
            button.setAttribute('aria-pressed', isSelected ? 'true' : 'false');
            var mark = statusIcon(outcome);
            mark.removeAttribute('title');
            mark.classList.add('scenario-history-mark');
            button.appendChild(mark);
            button.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                if (isSelected) { clearSelectedScenarioHistory(); renderDetail(); return; }
                setSelectedScenarioHistory(node, r);
                renderDetail();
            });
            marks.appendChild(button);
        });
        strip.appendChild(marks);
        return strip;
    }

    // Azure-DevOps-style run-level health pills, tallied over every currently-visible
    // descendant scenario's Health verdict - computed here via analyzeScenarioHealth()
    // (the client-side port of ReportHealthAnalyzer) rather than read from a precomputed field, and respects
    // excludedHistoryDates (item 4c). Regressed/Broken read as "needs attention" labels since those are the
    // buckets a run reviewer actually acts on; Passing/Unknown contribute no pill since they're not actionable
    // signal. Returns null (renders nothing) when no descendant scenario has an actionable Health - the Y
    // gate, same reasoning as buildScenarioHistoryStrip.
    var HEALTH_STAT_ORDER = ['Regressed', 'Broken', 'Fixed', 'New', 'Flaky'];
    var HEALTH_STAT_LABELS = {
        Regressed: function (n) { return n + ' new bug' + (n === 1 ? '' : 's'); },
        Broken: function (n) { return n + ' still failing'; },
        Fixed: function (n) { return n + ' fixed'; },
        New: function (n) { return n + ' first-run'; },
        Flaky: function (n) { return n + ' flaky'; },
        Passing: function (n) { return n + ' stable'; }
    };
    var HEALTH_STAT_HINTS = {
        Regressed: 'Failed for the first time after passing in earlier runs',
        Broken: 'Failed in every run it executed',
        Fixed: 'Failed before, passing now',
        New: 'Only one executed run on record',
        Flaky: 'Switched between pass and fail at least twice',
        Passing: 'Passed in every run it executed'
    };
    var HEALTH_COLORS = { Regressed: '#c62828', Broken: '#7f1d1d', Fixed: '#2e7d32', New: '#1565c0', Flaky: '#e65100', Passing: '#546e7a', Unknown: '#757575' };
    function healthColor(health) { return HEALTH_COLORS[health] || '#757575'; }

    function countHealth(scenarioNodes) {
        var counts = {};
        scenarioNodes.forEach(function (n) {
            var health = analyzeScenarioHealth(filterExcludedRuns(n.scenario.recentRuns), historyAnalysisWindow);
            counts[health] = (counts[health] || 0) + 1;
        });
        return counts;
    }

    function buildHealthPills(counts, order, strip) {
        order.forEach(function (health) {
            var count = counts[health];
            if (!count) return;
            var pill = el('span', 'health-stat-pill', HEALTH_STAT_LABELS[health](count));
            pill.title = HEALTH_STAT_HINTS[health];
            pill.style.setProperty('--pill-bg', healthColor(health));
            pill.style.color = readableTextOn(healthColor(health));
            strip.appendChild(pill);
        });
    }

    function buildHealthStatsStrip(node) {
        if (!scopeHasHistory(node)) return null;   // no earlier runs recorded -> nothing historical to show
        var scenarioNodes = [];
        collectScenarioNodes(node, scenarioNodes);
        var counts = countHealth(scenarioNodes);
        if (!HEALTH_STAT_ORDER.some(function (h) { return counts[h] > 0; })) return null;
        var strip = el('div', 'health-stats-strip');
        buildHealthPills(counts, HEALTH_STAT_ORDER, strip);
        return strip;
    }

    function formatRunShort(iso) {
        return formatDateTime(iso, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
    }

    // Cross-run trend: ONE x position per RUN (not per calendar day - several runs on one day used to collapse
    // into a single point), y = how many of the scenarios in scope ended in each outcome in that run.
    // Dependency-free raw SVG, same convention as buildDonut.
    function buildRunTrendChart(container, recentRuns, title) {
        var wrap = el('div', 'run-trend-chart-wrap');
        if (title) wrap.appendChild(el('div', 'donut-title', title));

        var byRun = {}, runMeta = {};
        (recentRuns || []).forEach(function (r) {
            var k = runKey(r);
            runMeta[k] = r;
            if (!byRun[k]) byRun[k] = {};
            byRun[k][r.outcome] = (byRun[k][r.outcome] || 0) + 1;
        });
        var keys = Object.keys(byRun).sort(function (a, b) { return runMeta[a].runAt < runMeta[b].runAt ? -1 : 1; });
        if (keys.length < 2) {
            wrap.appendChild(el('div', 'run-trend-chart-empty', 'At least two included runs are needed to draw a trend.'));
            container.appendChild(wrap);
            return;
        }

        var maxCount = 1;
        keys.forEach(function (k) { RUN_OUTCOME_ORDER.forEach(function (s) { maxCount = Math.max(maxCount, byRun[k][s] || 0); }); });

        var svgNS = 'http://www.w3.org/2000/svg';
        var width = 360, height = 150, padLeft = 26, padBottom = 22, padTop = 10, padRight = 12;
        var plotW = width - padLeft - padRight, plotH = height - padTop - padBottom;
        var svg = document.createElementNS(svgNS, 'svg');
        svg.setAttribute('viewBox', '0 0 ' + width + ' ' + height);
        svg.setAttribute('class', 'run-trend-chart');
        svg.setAttribute('role', 'img');
        svg.setAttribute('aria-label', title || 'Run trend');

        function xAt(i) { return padLeft + (i / (keys.length - 1)) * plotW; }
        function yAt(count) { return padTop + plotH - (count / maxCount) * plotH; }
        function text(x, y, anchor, str) {
            var t = document.createElementNS(svgNS, 'text');
            t.setAttribute('x', x); t.setAttribute('y', y); t.setAttribute('text-anchor', anchor);
            t.setAttribute('class', 'run-trend-chart-axis-label');
            t.textContent = str;
            svg.appendChild(t);
        }
        text(padLeft - 4, padTop + 4, 'end', String(maxCount));
        text(padLeft - 4, padTop + plotH, 'end', '0');
        keys.forEach(function (k, i) {
            var tick = document.createElementNS(svgNS, 'line');
            tick.setAttribute('x1', xAt(i)); tick.setAttribute('x2', xAt(i)); tick.setAttribute('y1', padTop + plotH); tick.setAttribute('y2', padTop + plotH + 3);
            tick.setAttribute('class', 'run-trend-chart-tick');
            svg.appendChild(tick);
        });
        text(padLeft, height - 6, 'start', formatRunShort(runMeta[keys[0]].runAt));
        text(width - padRight, height - 6, 'end', formatRunShort(runMeta[keys[keys.length - 1]].runAt));

        var legend = el('div', 'donut-legend');
        RUN_OUTCOME_ORDER.forEach(function (status) {
            if (!keys.some(function (k) { return (byRun[k][status] || 0) > 0; })) return;
            var points = keys.map(function (k, i) { return xAt(i) + ',' + yAt(byRun[k][status] || 0); }).join(' ');
            var line = document.createElementNS(svgNS, 'polyline');
            line.setAttribute('points', points);
            line.setAttribute('class', 'run-trend-chart-line');
            line.style.stroke = statusColor(status);
            svg.appendChild(line);
            keys.forEach(function (k, i) {
                var count = byRun[k][status] || 0;
                if (!count) return;
                var dot = document.createElementNS(svgNS, 'circle');
                dot.setAttribute('cx', xAt(i)); dot.setAttribute('cy', yAt(count)); dot.setAttribute('r', 3);
                dot.setAttribute('class', 'run-trend-chart-dot');
                dot.style.fill = statusColor(status);
                var t = document.createElementNS(svgNS, 'title');
                t.textContent = status + ': ' + count + ' — ' + formatRunTimestamp(runMeta[k].runAt);
                dot.appendChild(t);
                svg.appendChild(dot);
            });
            var item = el('span', 'donut-legend-item');
            var legendDot = el('span', 'donut-legend-dot');
            legendDot.style.background = statusColor(status);
            item.appendChild(legendDot);
            item.appendChild(document.createTextNode(status));
            legend.appendChild(item);
        });
        wrap.appendChild(svg);
        wrap.appendChild(legend);
        container.appendChild(wrap);
    }

    // The normal-browsing counterpart to buildDonut's chart - same icon+count+percentage
    // information, just as a compact inline row instead of a chart, since a full chart is more weight than a
    // single focused node's breakdown needs; the chart is reserved for analytics mode's whole-tree view.
    function buildInlineStatusCounts(counts) {
        var total = 0;
        STATUS_ORDER.forEach(function (s) { total += counts[s] || 0; });

        var wrap = el('div', 'inline-status-counts');
        var any = false;
        STATUS_ORDER.forEach(function (status) {
            var count = counts[status] || 0;
            if (count === 0) return;
            any = true;
            var item = el('span', 'inline-status-count-item');
            item.appendChild(statusIcon(status));
            item.appendChild(document.createTextNode(count + ' (' + pctLabel(total ? (count / total) * 100 : 0) + ')'));
            wrap.appendChild(item);
        });
        if (!any) wrap.appendChild(el('span', 'inline-status-count-item muted', 'No scenarios'));
        return wrap;
    }

    // Right-panel top row - the analytics toggle now lives in the header, so this
    // row is just this node's inline status counts+percentages (normal browsing, filters/search respected) or
    // its donut chart with percentages (analytics mode) - the same slot answers "how did this go" either way.
    function redrawTopRow() {
        if (!chartsContainer || !rootNode) return;
        chartsContainer.innerHTML = '';
        var node = focusedNode || rootNode;
        // A leaf scenario/attempt is always exactly one item at 100% of itself - that breakdown carries no
        // information (it's not an aggregate of anything), so it's the phantom "1 (100%)" users kept asking
        // about. Only nodes with an actual subtree get the counts/donut.
        if (node.kind === 'scenario' || node.kind === 'attempt') return;

        var row = el('div', 'detail-top-row');
        var left = el('div', 'detail-top-row-left');
        if (analyticsMode) {
            left.appendChild(buildDonut(computeStatusCounts(node, true), nodeDisplayName(node)));
        } else {
            left.appendChild(buildInlineStatusCounts(computeStatusCounts(node, true)));
        }
        row.appendChild(left);
        chartsContainer.appendChild(row);
    }

    // Whole-run status counts in the header - deliberately unfiltered (respectFilters: false) and
    // built once at boot, since this is "how did the whole run go", not a view that should shift as the user
    // narrows the tree with search/filters (that scoped breakdown lives in the detail-top-row instead).
    function buildHeaderStatusStrip() {
        var counts = computeStatusCounts(rootNode, false);
        var total = 0;
        STATUS_ORDER.forEach(function (s) { total += counts[s] || 0; });
        var strip = el('span', 'header-status-strip');
        STATUS_ORDER.forEach(function (status) {
            var count = counts[status] || 0;
            if (count === 0) return;
            var item = el('span', 'header-status-item');
            item.appendChild(statusIcon(status));
            item.appendChild(document.createTextNode(count + ' (' + pctLabel(total ? (count / total) * 100 : 0) + ')'));
            strip.appendChild(item);
        });
        headerMetaRow.appendChild(strip);
    }

    // Clicking the report title returns to the combined "all runs" overview (item B) - the only other way to
    // see it is the one-time initial focusNode(rootNode) call at boot below, with no way back once you click
    // elsewhere. Targets the title span itself, not the whole headerTitleRow, since that row also holds the
    // theme toggle button and a click there shouldn't also navigate.
    function buildHeaderTitleNav() {
        var titleEl = headerTitleRow.querySelector('.report-title');
        titleEl.classList.add('report-title-clickable');
        titleEl.title = 'Back to overview (all runs)';
        titleEl.addEventListener('click', function () { focusNode(rootNode); });
    }

    // ---------------------------------------------------------------- boot
    buildSearchUi();
    buildStatusFilterUi();
    buildFilterUi();
    buildGlobalResetButton();
    buildToolbarExtras();
    buildSplitter();
    buildSidebarCollapseToggle();

    rootNode = renderRootTree(treeEl, runs);
    buildHeaderStatusStrip();
    buildHeaderTitleNav();
    restoreStateFromUrl();
})();
