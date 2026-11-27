# Third-party notices

Ohirok is MIT-licensed (see [LICENSE](LICENSE)). It contains the following third-party code and assets under their
own licenses. Packages that Ohirok merely depends on through NuGet are not listed: they ship with their own licenses.

## Code adapted from Reqnroll

`Ohirok.Reqnroll.PluginChains.Sdk` contains modified copies of Reqnroll generator classes, so it can weave code into the
generated tests without replacing the unit test providers. The copies live in
`src/PluginChains.Sdk/GenerationInterceptor/Generation/` (`ScenarioPartHelperInt.cs`, `UnitTestFeatureGeneratorInt.cs`,
`UnitTestFeatureGeneratorProviderInt.cs`, `UnitTestMethodGeneratorInt.cs`) and are compiled into the SDK assembly.
`scripts/sync-upstream.ps1` checks them against upstream Reqnroll.

```text
BSD 3-Clause License

Copyright (c) 2024-2025, Reqnroll

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## Report assets

`Ohirok.Reporting.Core` embeds these in the HTML reports it renders. Their full license texts ship next to them in
`src/Reporting/Ohirok.Reporting.Core/Rendering/Assets/` and inside the package's assembly.

| Asset | Used by | License | Copyright |
| --- | --- | --- | --- |
| [Lucide](https://lucide.dev) icons (a subset, inlined in `report.js`) | every report | ISC (`Lucide-LICENSE.txt`) | Lucide Icons and Contributors; portions from Feather (MIT), Cole Bemis |
| [Geist](https://vercel.com/font) and Geist Mono (`Fonts/Geist-Variable.woff2`, `Fonts/GeistMono-Variable.woff2`) | the `ultraviolet` theme | SIL Open Font License 1.1 (`Fonts/Geist-OFL.txt`) | Vercel, in collaboration with basement.studio |
| [Barlow](https://github.com/jpt/barlow) (`Fonts/Barlow-400/600/700.woff2`) | the `midnight-blue` theme | SIL Open Font License 1.1 (`Fonts/Barlow-OFL.txt`) | The Barlow Project Authors |

Build-time only, not shipped: [NUglify](https://github.com/trullock/NUglify) minifies the report's JavaScript and CSS
in Release builds (`build/AssetMinifier`).
