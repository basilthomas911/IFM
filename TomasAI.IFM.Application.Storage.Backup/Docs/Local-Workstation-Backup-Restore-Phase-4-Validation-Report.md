# Local Workstation Database Backup and Restore Phase 4 Validation Report

**Gate:** 4 - `SystemAdminDbContext` and projectors

**Status:** Passed

**Date:** 2026-08-12

## Scope completed

- Added the `system_admin` PostgreSQL schema with versioned recovery-operation, phase, run-statistics, restore-point,
  artifact-replica, recovery-error, policy, service-health, retention-state, projection-checkpoint, and durable
  projection-receipt tables.
- Added `SystemAdminSchemaDb` and registered both its connection and startup schema creation through the existing
  storage composition root. `SystemAdminDbConnection` can be configured independently and otherwise uses the Core
  event-source PostgreSQL connection.
- Replaced the temporary query context with a PostgreSQL `SystemAdminDbContext` implementing all 15 bounded
  DatabaseBackup query contracts.
- Added transactional projection application. Each authoritative domain event updates its read-model rows, writes an
  immutable event-hash receipt, and advances the projector checkpoint in one transaction.
- Added exact duplicate acceptance and same-revision/different-content conflict rejection at the projection target,
  plus monotonic `last_event_id`/source-revision fences on mutable rows.
- Added explicit database mappings for every persisted DatabaseBackup enum and carried restore class, restore-point,
  fresh-target, policy, retention, evaluation-boundary, and manifest-revision data through the service-to-domain event
  pipeline.
- Added `DatabaseBackupEventProjector`, registering every authoritative DatabaseBackup domain event exactly once with
  durable target-receipt idempotency.
- Wired projector start/stop into Command Actor lifecycle and event projection into repository denormalization.
- Added a projection-only rebuilder that clears derived SystemAdmin rows, sorts authoritative events by persisted
  event revision, replays them, and reports applied, duplicate, conflict, and final-checkpoint counts.
- Added typed projector completion/failure events and projector/rebuilder unit coverage.

Phase 4 does not introduce the SQLite execution journal or read from any service-owned journal. It does not execute a
native backup, restore, retention deletion, or database mutation outside the dedicated Core projection schema.

## Validation evidence

### Live PostgreSQL projection integration

```text
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/
  TomasAI.IFM.Application.Storage.IntegrationTests.csproj --no-build --no-restore \
  --filter DatabaseBackupProjection

Passed: 4
Failed: 0
Skipped: 0
```

The suite creates the real `system_admin` schema on PostgreSQL and verifies:

- all Gate 4 tables, durable receipt keys, and revision fences are present;
- rebuild SQL is confined to projection-owned tables and contains no event-source or journal access;
- applying the same persisted domain event twice yields `Applied` then `AlreadyApplied`, while the read model and
  checkpoint remain queryable; and
- an intentionally out-of-order authoritative event sequence is sorted and fully replayed into the latest operation
  state after clearing the derived projection.

### SystemAdmin regression suites

```text
SystemAdmin unit tests:        93 passed, 0 failed, 0 skipped
SystemAdmin BDD tests:          3 passed, 0 failed, 0 skipped
SystemAdmin integration tests:  2 passed, 0 failed, 0 skipped
```

The two new projector unit tests prove that all authoritative DatabaseBackup domain-event types have exactly one
`TargetReceipt` projection descriptor and that rebuild ordering and projection clearing are deterministic.

### Service-journal isolation scan

```text
rg -n -i "journal|Application.DatabaseBackup|service journal|sqlite" \
  TomasAI.IFM.Domain.SystemAdmin/DatabaseBackup \
  TomasAI.IFM.Application.Storage/SystemAdminDb

No matches.
```

### Full solution

```text
dotnet build TomasAI.IFM.sln --no-restore --configuration Debug --nologo

Build succeeded.
0 Warning(s)
0 Error(s)
```

Elapsed time for the final full solution build was 12.57 seconds.

## Gate result

Gate 4 passed because the Core PostgreSQL projection schema is real and registered; all 15 DatabaseBackup queries are
backed by bounded projection reads; authoritative domain events project transactionally with durable idempotency and
checkpoints; a complete projection can be destroyed and rebuilt from ordered authoritative events; live PostgreSQL,
unit, BDD, and domain integration suites pass; the actor and projection implementation contains no service-journal
dependency; and the full solution builds with zero warnings and errors.

Phase 5 (standalone host, SQLite journal/inbox/outbox/leases, lifecycle, health, JetStream plumbing, and fake native
capabilities) is the next pending implementation phase.
