# Ohirok.Reporting.ReqnrollPlugin

Renders a standalone HTML report from a Reqnroll run.

This package is the formatter entry point for the reporting package set. It writes the live `.ndjson` stream and produces the final HTML report in the same folder.

## Installation

```bash
dotnet add package Ohirok.Reporting.ReqnrollPlugin
```

Enable the formatter in `reqnroll.json`:

```json
{
  "formatters": {
    "ohirokReport": { "outputFilePath": "report/ohirok-report.ndjson" }
  }
}
```

`outputFilePath` names the raw Cucumber Messages `.ndjson` file. Once the run finishes, the standalone HTML report is written next to it with a `.html` extension.

All formatter settings are optional. If omitted, the Reqnroll formatter default is used unless you explicitly set the path.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `formatters.ohirokReport.outputFilePath` | No explicit project default; framework default applies when the formatter is not configured | Path to the generated `.ndjson` stream that the HTML report is generated from. |

The formatter itself is enabled by including `ohirokReport` in the `formatters` section. The project does not define a custom fallback value beyond the standard Reqnroll formatter behavior.

## Related docs

- [Ohirok.Reporting](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/README.md): the `reporting` and `reportingHistory` settings
- [Ohirok.Reporting.Core](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.Core/README.md)
- [Ohirok.Reporting.History.Core](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.History.Core/README.md)
