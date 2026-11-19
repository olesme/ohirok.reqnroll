## Summary

What this PR changes and why.

## Checklist

- [ ] `dotnet build Ohirok.Reqnroll.slnx -c Release` succeeds
- [ ] `./scripts/run-tests.ps1` passes (positive projects green, `*.Negative` projects red for their documented reasons)
- [ ] Behaviour that a test runner can influence is covered on every runner suite (NUnit, MSTest, xUnit v3, TUnit)
- [ ] `./scripts/test-packages.ps1` passes when packaging, build assets or plugin wiring changed
- [ ] README of every touched package describes the new behaviour
