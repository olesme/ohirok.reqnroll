# Examples

- **[`ReportShowcase/`](ReportShowcase/README.md)** - the place to evaluate the Ohirok report and see every plugin at
  work. Four test projects on four runners (NUnit, xUnit v3, TUnit, MSTest) run over several simulated CI builds;
  one command produces every report that leads to: each theme, a single-feature edge case, a 2000-test suite, run
  history, two projects combined, and a build combined with the rerun of its failed scenarios. The features are
  grouped by capability, so they double as usage examples of Retry, XFail, StepHashtags, SoftAssert and the reporting
  embeds.
- **[`PersistentCleanupExample/`](PersistentCleanupExample/README.md)** - `Ohirok.PersistentCleanup` with its SQLite
  and custom storage backends, including wiring a custom storage once in `[BeforeTestRun]`.

The repository's own tests are under `tests/`; they verify behaviour and are not meant as samples.
