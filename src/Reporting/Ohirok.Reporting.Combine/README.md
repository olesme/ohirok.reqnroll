# Ohirok.Reporting.Combine

Combines multiple `.ndjson` runs produced by the [Ohirok Reqnroll report plugin](https://www.nuget.org/packages/Ohirok.Reporting.ReqnrollPlugin) into a single standalone HTML report.

## Install

```bash
dotnet tool install --global Ohirok.Reporting.Combine
```

## Usage

```bash
ohirok-report-combine --name <name> --ndjson <path.ndjson> [--ndjson <rerun.ndjson> ...] [--name <name> --ndjson <path.ndjson> ...] --out <combined.html> [--reqnroll-config <reqnroll.json>]
```

Each `--name` starts a new run in the combined report. Every run is shown as its own sibling node, and the report's history view compares the scenarios across the runs you combined.

| Option | Meaning |
| --- | --- |
| `--name <name>` | Starts a new run with this display name. The next `--ndjson` is that run's base file. |
| `--ndjson <path>` | An `.ndjson` file written by the `ohirokReport` formatter. The first `--ndjson` after a `--name` is the run's base file; any further `--ndjson` before the next `--name` is a rerun of that run, merged onto it (see below). A first `--ndjson` with no `--name` before it starts a run named after the file. The file must exist. |
| `--out <path>` | Required. Path of the HTML report to write. |
| `--reqnroll-config <path>` | Optional. A `reqnroll.json` to read the `reporting` section (title, theme, filters, templates) and the `reportingHistory` windows from. Without it, the defaults are used. |
| `--help`, `-h` | Prints the usage. |

Any other argument is rejected with exit code 1.

Combine merges results in two different ways, chosen by how you pass the files.

### Several test projects or builds: one top-level node each

Give every input its own `--name`. Each becomes a separate top-level node of the report tree, and the history view
compares scenarios across them.

```bash
ohirok-report-combine \
  --name "Build 101 - API tests" --ndjson b101/api.ndjson \
  --name "Build 101 - UI tests" --ndjson b101/ui.ndjson \
  --name "Build 102 - API tests" --ndjson b102/api.ndjson \
  --out combined.html \
  --reqnroll-config reqnroll.json
```

### A rerun of failed scenarios in the same pipeline: merged into the same run

Rerunning only the failed tests is common practice (`dotnet test --filter ...` with the failed test names). List the
rerun files after the base file under the **same** `--name`, oldest first:

```bash
ohirok-report-combine \
  --name "Build 102" --ndjson build-102.ndjson --ndjson build-102-rerun-1.ndjson --ndjson build-102-rerun-2.ndjson \
  --out build-102.html
```

A scenario that a rerun ran again takes its result from that rerun; its earlier results stay visible as previous
attempts, labelled *Attempt 2 · rerun 1* and so on, so the run shows the combined outcome of the pipeline. A report that
contains reruns gets a reruns switch next to the analytics button: off shows the pipeline's first pass only, with
each scenario's status from its own attempts. A rerun scenario that does not exist in the base file is left out and
reported as a warning on standard error.

Because every `--ndjson` without its own `--name` joins the previous run, always give each independent run a `--name`.

## Combine and run history

Combine is stateless: it renders the files you give it and never reads or writes a history store. Persistent history
across builds comes from the `Ohirok.Reporting.History.*` packages, where `reportingHistory.pipelineRunId` (for example
`"${GITHUB_RUN_ID}"`) makes a base run and its rerun one history row instead of two. The two are complementary:
`pipelineRunId` keeps stored history to one entry per build, Combine produces one HTML report over several `.ndjson`
files. See the [reporting README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/README.md#reruns-of-failed-scenarios).

[`examples/ReportShowcase/showcase.ps1`](https://github.com/olesme/ohirok.reqnroll/blob/main/examples/ReportShowcase/showcase.ps1)
does both over five simulated builds of two test projects, with a rerun of the failed scenarios in build 4.
