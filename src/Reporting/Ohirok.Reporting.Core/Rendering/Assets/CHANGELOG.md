# Base report assets changelog

`report-template.html`, `report.css`, and `report.js` in this folder are the single source of truth
for the built-in report. Most customization should go through the override points documented in
`ReportHtmlGenerator.cs` instead of forking these files - in particular `reporting.css` (CSS appended
after the base `report.css`, cascading over `:root`/`[data-theme]` variables) stays in sync with every
change below automatically, since it never copies the base files. `reporting.css`/`template`/`js` may
each be set to either a literal file path or the name of a built-in asset (e.g.
`"css": "ultraviolet"` - see `Themes/` in this folder for the full built-in catalog).

`reporting.template` and `reporting.js` are full forks of `report-template.html`/`report.js` with
**no automatic sync** - if you maintain one of these, use this log to decide whether a given change
affects your fork. Entries start 2026-11-02; earlier changes are not individually listed here.

## 2026-11-26 — attachment expansion, reruns, chocolate-brown

- `report.js`: attachments no longer carry a server-side `expanded` flag; whether one is shown open is decided
  client-side from the embed's own `expanded="true|false"` attribute, else from the new `display.expandAttachments`
  list (`reporting.expandAttachments`; default: callouts open). Callouts are therefore no longer folded behind a
  summary that repeated their whole text.
- `report.js`: the reruns switch and the `?reruns=on|off` parameter are gone: merged reruns stay visible as extra attempts
  labelled *rerun N*, like retries.
- `report.js`: everything on screen follows the search and filters - the header counts, the tree and detail statuses
  (`visibleStatus`) and the detail child list (nodes with nothing visible are left out); while one is active the header
  says "Showing N of M scenarios" with a Clear button.
- `report.js`: run start/finish times use the common date formatter instead of the raw ISO string.
- `Themes/chocolate-brown.css`: new built-in theme.
- `report-template.html`: the SVG favicon was invalid XML (a duplicated `href` on two `<use>` elements), so browsers
  dropped it and showed no icon; fixed. The header credit no longer names an author (`data-author` /
  `data-author-url` removed; `report.js` still honours them in a template that sets them).
- `report.js`/`report.css`: the top root shows a front-page summary (`.root-summary`): a status donut and the five features
  with the most failed scenarios (summed per feature name across runs, with "in N of M runs"; a click opens the latest
  occurrence), computed client-side and respecting the active filters. Other nodes are unchanged.
- `Themes/midnight-blue.css`: Barlow (bundled, straight DIN-like strokes) replaces Georgia.
- `report.css`: the popover shadow (`--shadow-popover`) is two soft layers in light mode and gains a faint edge ring in dark
  mode.
- `report.js`/`report.css`: the splitter is a 1px line (like the header's) inside a 9px grab area; on hover the line
  widens to 3px in place, so nothing moves. While the tree is hidden it has no resize cursor, no hover and no drag. The
  tree's scrollbar is a slim pill thumb on no track, padded off the rows. The show/hide button stays at the bottom of
  the splitter, now with Lucide chevrons, `aria-label`/`aria-expanded` and a contrast border.
- `report.js`: every control icon comes from one Lucide set (`LUCIDE` / `lucide(name, size)`, ISC, see
  `Lucide-LICENSE.txt`): toolbar, search, tree chevrons and node kinds, subtree and reset buttons, media actions,
  history. The status badges and the Cucumber mark keep their own filled shapes. A `js` fork that reads `ICON.*` or the
  `*_ICON` constants keeps working; their content changed.
- `Themes/cyberpunk-purple.css` is renamed `Themes/ultraviolet.css` (theme name `ultraviolet`; the old name is gone, nothing was published under it). It gets an indigo-to-magenta accent gradient (`--accent-gradient`) on the header line, the selected
  tree node's bar and the report title; its fonts are now Geist (prose) and Geist Mono (title, keywords, code), bundled in
  `Fonts/` and inlined through the new `url("ohirok-asset:Fonts/...")` reference.

## 2026-11-02 — theme CSS cascade token

- `report-template.html`: added a second `<style>{{ohirok:themeCss}}</style>` block immediately after
  the base `<style>{{ohirok:css}}</style>` block. A `template` fork written before this entry must add
  the same block (even if left empty) for `css` to have any effect when combined with that fork.
- No other structural changes to `report-template.html`/`report.css`/`report.js` in this entry.

## 2026-11-02 — config rename, name-or-path resolution, title/theme tokens

- **Config rename**: `reporting.templatePath`/`themeCssPath`/`jsPath` renamed to `reporting.template`/
  `css`/`js` ("Path" became misleading once a value can also be a bare built-in-asset name). A
  `reqnroll.json` written against the old keys must be updated - the old keys are no longer read.
- All three (`template`/`css`/`js`) now share the same name-or-path resolution: a configured value is
  checked against that override's built-in-name catalog (case-insensitive) first, then treated as a
  literal file path. `css` ships 3 built-in themes (`cyberpunk-purple`, `cucumber-green`,
  `midnight-blue` - see `Themes/*.css` in this folder); `template` and `js` have no built-in catalog
  entries yet, but share the identical mechanism for consistency.
- `reporting.title` now supports `${ENV_VAR_NAME}` substitution (undefined variables resolve to an
  empty string, matching the `envsubst` convention) - lets a CI pipeline inject e.g. a build number
  into the title without any report-specific templating language. Applies identically to both the
  page's `<title>`/visible header and the `ReportRun.Name` embedded in the JSON payload (used as the
  tree root node's label) - both must resolve the same text.
- `report-template.html`: new `{{ohirok:defaultTheme}}` token in the bootstrap script, driven by new
  `reporting.defaultTheme` config (`"light"`/`"dark"`, unset = browser `prefers-color-scheme`). A
  `template` fork written before this entry has no way to honor a configured default theme until it
  adds the same token.
