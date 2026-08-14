# Integration Test Execution

All solution projects whose names end in `IntegrationTests` or
`IntegratedTests` must execute sequentially. These tests commonly share NATS,
PostgreSQL, ScyllaDB, Redis, fixed subjects, and hosted application state.

## Enforcement

`Directory.Build.props` applies the convention automatically:

- links `IntegrationTests.AssemblyInfo.cs` into every matching test assembly;
- disables xUnit collection parallelization in compiled code;
- copies `xunit.integration.runner.json` with one worker and assembly and
  collection parallelization disabled;
- sets `TestTfmsInParallel` to `false`;
- selects `integration.runsettings`, whose test-host CPU count is one.

This protects individual projects regardless of whether tests are started from
an IDE, `dotnet test`, or CI.

## Running the complete solution integration suite

Use the repository runner when executing every integration-test project:

```powershell
.\scripts\Run-IntegrationTests.ps1
```

The runner reads the projects from `TomasAI.IFM.sln`, selects both naming
conventions, sorts them, and invokes `dotnet test` for one project at a time. It
stops at the first failing project. Optional examples:

```powershell
.\scripts\Run-IntegrationTests.ps1 -Configuration Release
.\scripts\Run-IntegrationTests.ps1 -NoBuild
.\scripts\Run-IntegrationTests.ps1 -ListOnly
.\scripts\Run-IntegrationTests.ps1 -Filter "FullyQualifiedName~TickAggregation"
```

Do not use a solution-wide parallel test command for integration tests. Unit
test projects remain free to run in parallel.
