# Local Workstation Database Backup and Restore Phase 6 Validation Report

**Gate:** 6 - PostgreSQL LocalWorkstation capability

**Status:** Passed

**Date:** 2026-08-12

## Scope completed

- Added the real `PostgreSqlBackupCapability` behind the existing destination-neutral application port.
- Added a private native-process boundary that resolves only `pg_basebackup`, `pg_verifybackup`, and `pg_ctl` from a
  fixed configured tool directory, uses `ProcessStartInfo.ArgumentList`, disables shell execution, redirects bounded
  output, removes inherited `PG*` settings, and kills the process tree on cancellation or timeout.
- Kept credentials behind an environment-variable reference. The connection password is supplied only through the
  child process environment and is absent from actor contracts, native arguments, evidence, diagnostics, and logs.
- Added strict allowlists for protection sets and loopback-only fresh-target profiles/logical targets. Backup, restore,
  and protected database roots are canonicalized and cannot overlap.
- Implemented full physical capture in plain format with streamed WAL, fast checkpointing, progress, and SHA-256
  native manifest checksums.
- Parsed the native backup manifest into bounded system-identifier, timeline, start/end LSN, required WAL-segment,
  artifact-count, byte-count, elapsed-time, and throughput evidence. Both numeric and string system identifiers are
  accepted across supported PostgreSQL manifest versions.
- Added native `pg_verifybackup` validation before an in-progress capture is atomically promoted. Tampered backups
  remain ineligible and incomplete native captures fail closed for explicit reconciliation.
- Made capture and verification idempotent by operation identity. A restarted host reuses the completed native
  manifest/evidence instead of invoking a second base backup.
- Added lease heartbeats around long-running native capture, verification, and restore work so stale workers are
  fenced while valid workers retain their lease.
- Added fresh-target restore that copies an immutable verified source into a new operation-specific target, verifies
  the copy, boots the isolated target, compares its native system identifier, runs application validation, stops it,
  records bounded statistics/evidence, and never performs cutover.
- Added production-restore orchestration through validation and `ReadyForCutover`; restore drills end in completed
  state. Run-statistics events are persisted in the SQLite statistics table and durable outbox.
- Added host startup validation for native tool presence and matching major versions before readiness. Dry-run remains
  the default; the real capability is selected only when LocalWorkstation is enabled and dry-run is disabled.

The implementation follows PostgreSQL's supported physical-backup model: `pg_basebackup` captures the entire cluster
through the replication protocol with streamed WAL, and `pg_verifybackup` validates the native manifest, files,
checksums, and required WAL. Native verification complements rather than replaces an actual test restore, so Gate 6
performs both. See the official [pg_basebackup](https://www.postgresql.org/docs/current/app-pgbasebackup.html) and
[pg_verifybackup](https://www.postgresql.org/docs/current/app-pgverifybackup.html) documentation.

## Validation evidence

### Real PostgreSQL 17 physical restore

```text
dotnet test TomasAI.IFM.Framework.Storage.IntegrationTests/
  TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --no-build --no-restore \
  --filter "Category=Gate6NativeIntegration" --verbosity minimal

Passed: 1
Failed: 0
Skipped: 0
Duration: 43 seconds
```

The test creates a disposable PostgreSQL 17 source container, inserts a synthetic application row, checkpoints it,
and executes a real `pg_basebackup` with streamed WAL. It constructs a second capability instance to simulate host
restart and proves the native capture count remains one. It then runs real `pg_verifybackup`, copies the physical
cluster into an operation-specific fresh target, boots a second disposable PostgreSQL container, compares the source
and restored system identifiers, and queries the synthetic row successfully. Both uniquely named containers are
force-removed during test cleanup; a post-test Docker scan returned no Gate 6 containers.

### Deterministic adapter and journal integration

```text
dotnet test TomasAI.IFM.Framework.Storage.IntegrationTests/
  TomasAI.IFM.Framework.Storage.IntegratedTests.csproj --no-restore \
  --filter "FullyQualifiedName~DatabaseBackupJournalIntegrationTests|Category=Gate6Integration" \
  --verbosity minimal

Passed: 10
Failed: 0
Skipped: 0
```

The suite verifies exact native argument construction; password isolation; manifest and WAL-range parsing; capture
recovery after host restart; checksum-tamper rejection; idempotent restore replay; protection-set and fresh-target
allowlists; fenced-lease heartbeat renewal; statistics persistence; and the restore service-event sequence ending at
`ReadyForCutover`.

### Regression suites

```text
SystemAdmin unit tests:                  93 passed, 0 failed, 0 skipped
SystemAdmin BDD tests:                    3 passed, 0 failed, 0 skipped
SystemAdmin integration tests:            4 passed, 0 failed, 0 skipped
PostgreSQL projection integration tests:  4 passed, 0 failed, 0 skipped
Journal/adapter integration tests:        10 passed, 0 failed, 0 skipped
Native PostgreSQL restore tests:           1 passed, 0 failed, 0 skipped
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
Time elapsed: 37.28 seconds
```

## Gate result

Gate 6 passed because a real PostgreSQL physical backup with streamed WAL is natively verified; its bounded WAL,
identity, and run-statistics evidence survives host restart without repeated capture; the verified files restore only
into an isolated fresh target; native and application validation reproduce the synthetic data; long operations renew
their fenced journal lease; production recovery stops at `ReadyForCutover`; regression and advisory checks pass; test
infrastructure is removed; and the complete solution builds with zero warnings and errors.

Phase 7 (the real Scylla LocalWorkstation capture, verification, schema/dependency evidence, statistics, and
fresh-target restore capability) is the next pending implementation phase.
