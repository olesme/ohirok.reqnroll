# Third-party notices

The `Ohirok.Reporting.Core` package embeds the following third-party assets in the HTML reports it renders. Their
full license texts ship next to them in `src/Reporting/Ohirok.Reporting.Core/Rendering/Assets/` and inside the
package's assembly.

| Asset | Used by | License | Copyright |
| --- | --- | --- | --- |
| [Lucide](https://lucide.dev) icons (a subset, inlined in `report.js`) | every report | ISC (`Lucide-LICENSE.txt`) | Lucide Icons and Contributors; portions from Feather (MIT), Cole Bemis |
| [Geist](https://vercel.com/font) and Geist Mono (`Fonts/Geist-Variable.woff2`, `Fonts/GeistMono-Variable.woff2`) | the `ultraviolet` theme | SIL Open Font License 1.1 (`Fonts/Geist-OFL.txt`) | Vercel, in collaboration with basement.studio |
| [Barlow](https://github.com/jpt/barlow) (`Fonts/Barlow-400/600/700.woff2`) | the `midnight-blue` theme | SIL Open Font License 1.1 (`Fonts/Barlow-OFL.txt`) | The Barlow Project Authors |

Build-time only, not shipped: [NUglify](https://github.com/trullock/NUglify) minifies the report's JavaScript and CSS
in Release builds (`build/AssetMinifier`).
