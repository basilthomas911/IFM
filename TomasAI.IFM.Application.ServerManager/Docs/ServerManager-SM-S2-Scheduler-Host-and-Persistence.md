# IFM Server Manager SM-S2 Scheduler Host and Persistence Gate

**Document type:** Implementation gate record

**Status:** Complete for entry into SM-S3

**Version:** 1.0

**Date:** 2026-08-20

**Owner:** IFM engineering

## 1. Gate decision

SM-S2 is complete. IFM now has an independent Scheduler Host that can run as a console application or Windows
Service, owns the persistent Quartz runtime, uses PostgreSQL for authoritative scheduler state, recovers incomplete
runs without retrying ambiguous work, and serves the first read-only Server Manager scheduler dashboard.

The scheduler remains greenfield. Its schedule-definition table starts empty, and no Reference-domain schedule
export or import exists.

## 2. Implemented projects

| Project | Responsibility |
| --- | --- |
| `TomasAI.IFM.Application.ServerManager.Contracts` | Versioned protocol constants, request/response envelopes, dashboard DTOs, and scheduler enums only |
| `TomasAI.IFM.Application.ServerManager.SchedulerHost` | Host lifecycle, migrations, Quartz, catalog, run/attempt store, recovery, process execution, Job Objects, health, and named pipe |
| `TomasAI.IFM.Application.ServerManager` | Read-only pipe client and Applications/Scheduled Tasks/Task Runs/Logs/Scheduler Health views |

The contracts project has no WPF, Quartz, Npgsql, or process dependency.

## 3. Startup and failure boundary

Scheduler Host startup is ordered:

1. acquire the exclusive scheduler-host file lock;
2. acquire the PostgreSQL advisory migration lock;
3. apply create-only Quartz and IFM migrations;
4. snapshot the administrator task catalog;
5. convert incomplete `Planned`, `Starting`, `Running`, or `Cancelling` runs and attempts to `Abandoned`;
6. create Quartz in standby;
7. reconcile enabled PostgreSQL schedule definitions with Quartz and remove stale Quartz jobs;
8. start scheduling; and
9. publish `Ready` through the local pipe.

If PostgreSQL migration/bootstrap fails, the process remains available through the pipe with `Unhealthy` status and
does not start Quartz. There is no RAM, SQLite, or ScyllaDB fallback.

## 4. PostgreSQL model

Create-only versioned migrations create:

- `ifm_quartz.qrtz_*`, owned logically by Quartz;
- `ifm_scheduler.schema_migration`;
- `ifm_scheduler.task_catalog_snapshot`;
- `ifm_scheduler.schedule_definition`;
- `ifm_scheduler.task_run`;
- `ifm_scheduler.task_attempt`;
- `ifm_scheduler.audit_entry`; and
- `ifm_scheduler.outbox`.

A partial unique index permits at most one `Planned`, `Starting`, `Running`, or `Cancelling` run per schedule. Quartz
`DisallowConcurrentExecution` is therefore not the only overlap guard. A rejected concurrent occurrence is persisted
as `SkippedOverlap`.

Run transitions are checked by a monotonic state machine. Terminal states cannot return to active states. Recovery
marks ambiguous work `Abandoned` and never assumes that the underlying business operation did not happen.

## 5. Catalog and process safety

- Catalog keys are immutable safe path segments.
- Executables and working directories must remain beneath the approved deployment root.
- Optional SHA-256 hashes are verified before launch.
- Operator-entered arbitrary executable paths do not exist in this stage.
- Arguments use `ProcessStartInfo.ArgumentList`; no shell is used.
- Child environment inheritance is cleared and rebuilt from a small OS baseline plus the catalog allowlist.
- Run/occurrence/attempt/fire/origin/environment correlation variables are injected.
- stdout and stderr are drained concurrently to separate per-run files.
- each child is assigned to a kill-on-close Windows Job Object before the run becomes `Running`.
- timeout or host cancellation uses the configured graceful mechanism and then terminates the owned Job Object.

No real scheduled task is enabled by SM-S2. Real task adoption remains gated by SM-S4.

## 6. Local protocol and dashboard

The V1 pipe protocol is:

- local-only and current-user restricted for development/paper-trading console use;
- versioned;
- request-ID correlated;
- cancellation-aware;
- four-byte little-endian length-prefixed;
- limited to 1 MiB per frame; and
- serialized with `System.Text.Json` without polymorphic type activation.

SM-S2 exposes the read-only `scheduler.dashboard.get` operation. It returns health, catalog snapshots, schedule
summaries, and recent runs. Server Manager refreshes it periodically and provides tabs for Applications, Scheduled
Tasks, Task Runs, Logs, and Scheduler Health.

The Windows Service production/operator-group pipe ACL is an installation/security acceptance item in SM-S5. SM-S2
uses `CurrentUserOnly`, which is intentionally restrictive for development and paper-trading console operation.

## 7. Configuration

`SchedulerHost/appsettings.json` defines the environment, scheduler identity, pipe, task-run root, deployment root,
concurrency, shutdown bound, recent-run limit, and administrator task catalog. The PostgreSQL connection string has no
committed password; deployment supplies credentials through normal configuration precedence.

The initial catalog contains only `futures-market-close`, and its snapshot reports whether the executable is actually
deployed. There are no initial schedule definitions or Quartz triggers.

## 8. Validation evidence

Release validation includes:

```text
Scheduler Host build: 0 warnings, 0 errors
Server Manager build: 0 warnings, 0 errors
Server Manager unit tests: 18 passed
Server Manager integration tests: 11 passed
```

The integration suite proves:

- create-only migrations are idempotent on PostgreSQL 17.2;
- the greenfield schedule/run tables start empty;
- catalog snapshots persist;
- incomplete runs recover to `Abandoned`;
- the database overlap uniqueness rule and `SkippedOverlap` evidence;
- the production Scheduler Host starts persistent Quartz and publishes `Ready`;
- the real named-pipe client receives health/catalog/schedule/run data;
- Windows Job Object assignment plus stdout/stderr file capture;
- existing API/UI process supervision behavior remains intact; and
- forced and graceful process shutdown paths remain intact.

## 9. SM-S3 entry boundary

SM-S3 may add schedule validation and preview, CRUD, enable/disable, audit writes, idempotent mutating pipe requests,
manual run, cancellation, explicit retry, durable output paging/tailing, retention, and broader failure injection.

SM-S3 must continue to keep Server Manager out of PostgreSQL, start new schedules disabled, and reject arbitrary
executable paths.

## 10. References

- [Scheduled-task supervision specification](ServerManager-Scheduled-Task-Supervision-Specification.md)
- [Server Manager implementation details](ServerManager-Implementation-Details.md)
- [SM-S1 process/log supervision gate](ServerManager-SM-S1-Process-and-Log-Supervision.md)

## 11. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 1.0 | 2026-08-20 | Recorded the Scheduler Host, PostgreSQL/Quartz authority, catalog/run/attempt persistence, recovery, Job Object execution, local read protocol, dashboard, and validation evidence. |
