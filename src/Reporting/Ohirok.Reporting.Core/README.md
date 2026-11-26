# Ohirok.Reporting.Core

Shared model, parsing, and HTML rendering used by the Ohirok reporting formatter.

This package is the core implementation layer for the reporting package set. Install `Ohirok.Reporting.ReqnrollPlugin` to use it in a Reqnroll project:

```bash
dotnet add package Ohirok.Reporting.ReqnrollPlugin
```

## Report configuration

The report is configured in the `reporting` section of `reqnroll.json`. Every key is optional.

```json
{
  "reporting": {
    "title": "Checkout suite - build ${BUILD_NUMBER}",
    "css": "midnight-blue",
    "defaultTheme": "dark"
  }
}
```

| Key | Default | Meaning |
| --- | --- | --- |
| `title` | `"Ohirok Report"` | Report title. `${ENV_VAR}` references are replaced with the environment variable's value; an undefined variable becomes an empty string. |
| `css` | none | CSS appended after the built-in stylesheet. A built-in theme name (`cyberpunk-purple`, `cucumber-green`, `midnight-blue`, `chocolate-brown`) or a file path. |
| `template` | built-in template | File path of a full replacement for the HTML shell. |
| `js` | built-in script | File path of a full replacement for `report.js`. |
| `defaultTheme` | browser preference | `light` or `dark`: the theme the report opens with until the viewer toggles it. |
| `filters` | `[]` | Tag-based filter groups. |

A file path is resolved against the working directory of the process that renders the report; a value that matches neither a built-in name nor an existing file is ignored. A `template` replacement uses the tokens `{{ohirok:title}}`, `{{ohirok:generatedDate}}`, `{{ohirok:defaultTheme}}`, `{{ohirok:css}}`, `{{ohirok:themeCss}}`, `{{ohirok:js}}` and `{{ohirok:json}}`.

The full description, including filters and history settings, is in the [reporting README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/README.md#report-configuration).

## Related docs

- [Ohirok.Reporting](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/README.md)
- [Ohirok.Reporting.ReqnrollPlugin](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.ReqnrollPlugin/README.md)
- [Ohirok.Reporting.History.Core](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.History.Core/README.md)
- [Base report assets changelog](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.Core/Rendering/Assets/CHANGELOG.md)
