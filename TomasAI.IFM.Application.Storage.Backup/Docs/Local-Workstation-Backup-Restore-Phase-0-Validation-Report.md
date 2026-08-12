# Local Workstation Database Backup and Restore Phase 0 Validation Report

**Gate:** 0 — Baseline and naming cleanup

**Status:** Passed

**Date:** 2026-08-12

## Scope completed

- Renamed the misleading `TomasAI.IFM.Application.Storage.Backup` projection utility to
  `TomasAI.IFM.Application.Storage.ProjectionMigration`.
- Preserved the existing project GUID, command parsing, exit codes, migration targets, database behavior, and
  credential boundary.
- Updated the Scylla projection-migration runbook and command examples.
- Created compile-only boundaries for:
  - `TomasAI.IFM.Application.DatabaseBackup`;
  - `TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation`;
  - `TomasAI.IFM.Api.DatabaseBackup.Host`; and
  - `TomasAI.IFM.Application.DatabaseBackup.Console`.
- Added all renamed/new projects to `TomasAI.IFM.sln`.
- Updated backup architecture and implementation documents for the paper-trading deployment sequence:
  standalone .NET 10 Worker development, Ubuntu 24.04 Docker qualification after functional gates, and Aspire deferred
  to a separate full-system Linux production migration.
- Kept MarketData, TradeBroker, GeneralLedger, and other future capability hosts outside this implementation.

No backup command, actor contract, native backup capability, database schema, journal, Dockerfile, Aspire resource, or
production execution behavior was added in Phase 0.

## Baseline evidence

Before the rename:

```text
dotnet build TomasAI.IFM.Application.Storage.Backup/
  TomasAI.IFM.Application.Storage.Backup.csproj --no-restore --configuration Debug

Build succeeded.
0 Warning(s)
0 Error(s)
```

The original `--help` output exposed exactly four migration targets: `reference`, `securities`, `fund`, and `market`.

## Post-change evidence

### Renamed utility

```text
dotnet build TomasAI.IFM.Application.Storage.ProjectionMigration/
  TomasAI.IFM.Application.Storage.ProjectionMigration.csproj --no-restore --configuration Debug

Build succeeded.
0 Warning(s)
0 Error(s)
```

The renamed `--help` output still exposes exactly `reference`, `securities`, `fund`, and `market`; only the project path
in the usage examples changed.

### New project boundaries

```text
dotnet build TomasAI.IFM.Api.DatabaseBackup.Host/
  TomasAI.IFM.Api.DatabaseBackup.Host.csproj --configuration Debug

Build succeeded.
0 Warning(s)
0 Error(s)
```

```text
dotnet build TomasAI.IFM.Application.DatabaseBackup.Console/
  TomasAI.IFM.Application.DatabaseBackup.Console.csproj --configuration Debug

Build succeeded.
0 Warning(s)
0 Error(s)
```

The host build transitively compiled the application and LocalWorkstation adapter projects.

### Full solution

```text
dotnet build TomasAI.IFM.sln --no-restore --configuration Debug

Build succeeded.
0 Warning(s)
0 Error(s)
```

Elapsed time was 2 minutes 35 seconds.

## Test accounting

Phase 0 contains naming, documentation, and compile-only project scaffolding. It adds no executable backup behavior and
therefore adds zero backup tests. The bounded gate used pre/post utility behavior checks, targeted builds, solution
discovery, and the full solution build. Feature tests begin with the JetStream listener in Phase 1.

No database migration or backup/restore operation was executed.

## Gate result

Gate 0 passed because:

- the projection utility retained its observable command surface;
- the renamed and new projects compile;
- the full solution compiles with zero warnings and errors;
- the backup service remains an independent Worker boundary;
- no production behavior changed; and
- Docker and Aspire were not introduced prematurely.

Phase 1 must not begin until Gate 0 is reviewed and accepted.
