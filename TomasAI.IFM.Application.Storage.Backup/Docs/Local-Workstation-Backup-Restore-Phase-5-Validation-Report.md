# Local Workstation Database Backup and Restore Phase 5 Validation Report

**Gate:** 5 - standalone host and SQLite execution journal

**Status:** Passed

**Date:** 2026-08-12

## Scope completed

- Added destination-neutral recovery processor, execution-journal, operation-executor, PostgreSQL capability, and
  Scylla capability contracts to the DatabaseBackup application layer.
- Added a strict LocalWorkstation processor registry. `BackupSource.None`, unknown values, and unregistered sources are
  rejected instead of ignored.
- Added a persistent SQLite execution journal using WAL mode, full synchronous writes, foreign keys, a bounded busy
  timeout, and startup integrity verification.
- Added versioned operation, inbox, checkpoint, artifact-replica, outbox, run-statistics, and reconciliation tables.
- Made execution-intent admission and the accepted service-event outbox write one transaction. Exact redelivery is
  idempotent; the same identity with different immutable content is rejected as a conflict.
- Added time-bounded leases with monotonically changing fencing tokens. Checkpoints and outbox writes require the
  current fence, preventing a stale worker from committing after a restart or lease takeover.
- Added allowlisted MessagePack service-event serialization with content hashes and deterministic service-event IDs.
- Added fake PostgreSQL and Scylla native capabilities and a LocalWorkstation processor that emits the canonical
  accepted, started, boundary-established, verified, and completed sequence without invoking native utilities.
- Added the standalone host composition root, journal initialization, startup reconciliation, execution dispatcher,
  durable outbox publisher, and JetStream inbound listener. Inbound acknowledgement occurs only after exact durable
  journal admission.
- Added readiness/liveness health checks for journal initialization, integrity, and startup reconciliation.
- Added path validation that keeps the SQLite journal on persistent storage and rejects placement inside protected
  database data roots.
- Selected `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5 explicitly; the resolved dependency graph passes the NuGet advisory
  audit.

Phase 5 uses fake database-native capabilities. It does not run `pg_basebackup`, restore PostgreSQL, capture Scylla
snapshots, delete retained data, or mutate a production database.

## Validation evidence

### SQLite journal and restart integration

```text
dotnet test TomasAI.IFM.Framework.Storage.IntegrationTests/
  TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --no-restore \
  --filter "FullyQualifiedName~DatabaseBackupJournalIntegrationTests" --verbosity minimal

Passed: 5
Failed: 0
Skipped: 0
```

The focused suite verifies exact admission/deduplication/conflict handling, restart recovery with ordered outbox
records, recovery after a simulated crash between an outbox write and its checkpoint, stale-fence rejection after
lease reacquisition, and strict processor-source registration.

### Real end-to-end host path

```text
dotnet test TomasAI.IFM.Domain.SystemAdmin.IntegrationTests/
  TomasAI.IFM.Domain.SystemAdmin.IntegrationTests.csproj --no-build --no-restore \
  --filter "Category=Gate5Integration" --verbosity minimal

Passed: 2
Failed: 0
Skipped: 0
```

The end-to-end test creates an authoritative Command Actor work order, publishes its execution intent through real
JetStream, durably admits it into SQLite, stops the first host listener, reopens the journal, completes the operation
through the fake processor, replays the durable service-event outbox through JetStream, translates those observations
through the Event Actor, and queries the completed PostgreSQL projection. The companion composition test verifies the
host's initialization, reconciliation, listener, dispatcher, and outbox-publisher startup order.

### Regression suites

```text
SystemAdmin unit tests:                  93 passed, 0 failed, 0 skipped
SystemAdmin BDD tests:                    3 passed, 0 failed, 0 skipped
SystemAdmin integration tests:            4 passed, 0 failed, 0 skipped
PostgreSQL projection integration tests:  4 passed, 0 failed, 0 skipped
SQLite journal integration tests:         5 passed, 0 failed, 0 skipped
```

### Dependency advisory audit

```text
dotnet list TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation/
  TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.csproj \
  package --vulnerable --include-transitive

No vulnerable packages found.
```

### Full solution

```text
dotnet build TomasAI.IFM.sln --no-restore --configuration Debug --nologo

Build succeeded.
0 Warning(s)
0 Error(s)
```

## Gate result

Gate 5 passed because execution intent crosses real JetStream and is durably admitted before acknowledgement; SQLite
retains exact inbox, checkpoint, lease, and outbox state across host restart; fenced processing resumes without
duplicating completed work; the service-event outbox replays in canonical order; Core actors consume those events and
produce a completed PostgreSQL projection; host lifecycle and health composition are verified; bounded regression and
dependency-advisory checks pass; and the full solution builds with zero warnings and errors.

Phase 6 (the real PostgreSQL LocalWorkstation backup, verification, WAL-continuity evidence, statistics, and
fresh-target restore capability) is the next pending implementation phase.
