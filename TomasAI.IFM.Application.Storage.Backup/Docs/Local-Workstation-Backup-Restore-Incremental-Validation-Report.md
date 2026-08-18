# Local Workstation Incremental Backup Validation Report

Date: 2026-08-18

Status: Implemented and validated

## Delivered behavior

- One source-neutral `Full | Automatic | Incremental` request flows from UI, Console, or ScheduledTask through the
  DatabaseBackup actor and standalone host.
- The local chain planner selects only a verified parent available on every required replica. It enforces maximum
  chain depth and base age. `Automatic` falls back to full; explicit `Incremental` fails.
- PostgreSQL 17 captures with `pg_basebackup --incremental=<parent manifest>`, verifies each native backup, stages the
  entire dependency chain oldest-first, reconstructs it with `pg_combinebackup`, verifies the synthetic full backup,
  and boots/queries a fresh target.
- Scylla Manager restore points are treated as logically complete and physically deduplicated. Lineage is recorded for
  selection/audit without publishing a false IFM artifact dependency chain.
- Signed manifest schema version 2, catalogs, operation/restore-point projections, service/domain events, and UI state
  retain requested/resolved mode, native kind, base, parent, depth, and bounded native identity.
- Existing version-1 manifests are read as legacy full backups. Backup artifacts remain uncompressed.

## Validation evidence

| Area | Result |
| --- | --- |
| Chain planner policy (no parent, common parent, replica gap/content mismatch, depth/base-age limits, Scylla semantics) | 8 passed |
| Deterministic PostgreSQL full/tamper/incremental-combine tests | 4 passed |
| Disposable PostgreSQL 17 native full and full→incremental→combined fresh-target restore | 2 passed |
| Disposable Scylla native snapshot/evidence/fresh-target restore | 1 passed |
| DatabaseBackup contract, actor state, console mode, API/projector/query tests | 30 passed |
| SystemAdmin PostgreSQL projection/schema tests, including lineage JSON round trip | 6 passed |
| UI presentation tests | 181 passed |
| UI system tests | 13 passed |

The broad legacy `Application.Storage.IntegrationTests` invocation exceeded its five-minute command window without a
test result. Its focused `DatabaseBackupProjectionSchemaTests` suite completed successfully. This timeout is retained
as test-run evidence and is not represented as a passing run.

## Operational prerequisites

- PostgreSQL incremental capture requires source PostgreSQL 17 or later, matching PostgreSQL 17-or-later native tools,
  `summarize_wal=on`, retained WAL summaries, and the selected parent evidence from the same system identifier.
- The configured defaults enable incremental planning, limit a chain to six incremental descendants, and require a
  base no older than seven days.
- Scylla Manager retains 30 snapshots by default so physical deduplication can operate across useful history.
- Restore remains fresh-target only; no active database is overwritten.
