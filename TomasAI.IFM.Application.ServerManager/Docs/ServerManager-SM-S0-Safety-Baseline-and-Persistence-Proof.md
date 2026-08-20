# IFM Server Manager SM-S0 Safety Baseline and PostgreSQL Persistence Proof

**Document type:** Implementation gate record

**Status:** Complete for entry into SM-S1

**Version:** 1.1

**Date:** 2026-08-20

**Owner:** IFM engineering

## 1. Gate decision

SM-S0 is complete as the implementation baseline for Server Manager modernization. This approval permits SM-S1
development; it does not install a service, create or enable a schedule, or authorize paper/live trading.

Evidence completed in this gate:

- scheduled-task executable inventory and assessment of historical schedule-shaped code;
- Windows service identity, local operator groups, pipe/file ACL, and PostgreSQL role decisions;
- pinned Quartz.NET, hosted-service, serializer, and Npgsql package versions;
- a .NET 10 configuration contract test; and
- a disposable PostgreSQL 17.2 integration proof that a Quartz job and trigger survive scheduler shutdown and
  recreation.

The user confirmed that IFM has no operational scheduler and no schedule data to migrate. The Reference-domain
schedule classes are dormant historical code artifacts only. SM-S0 therefore has no database export or schedule
migration prerequisite.

## 2. Approved scope and prohibitions

The authoritative functional design is
`TomasAI.IFM.Application.ServerManager/Docs/ServerManager-Scheduled-Task-Supervision-Specification.md` version 1.5.
It is accepted as the baseline for staged implementation.

SM-S0 establishes technology and safety decisions only. It does not permit:

- running Quartz inside the WPF Server Manager;
- using RAMJobStore, SQLite, ScyllaDB, or the Reference projection as trigger authority;
- adding arbitrary executables through the operator UI;
- creating or enabling schedules;
- installing or starting Scheduler Host as a Windows Service;
- changing the current API/UI process launcher; or
- executing a scheduled operation against a paper or live environment.

## 3. Executable inventory

### 3.1 Scheduler candidates

| Catalog candidate | Current target/runtime | Evidence | SM-S0 disposition |
| --- | --- | --- | --- |
| `TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose` | .NET 10 Worker | Current NATS command client; submits the database-backup workflow; externally scheduled one-shot process | First adoption candidate, but remains disabled until its SM-S4 task gate |
| `TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen` | .NET 7 Worker | Legacy REST client references, stale binary hints, unchecked terminal result, and forced process termination | Blocked until .NET 10/current messaging/cancellation modernization |
| `TomasAI.IFM.Application.ScheduledTask.SetClosingPrice` | .NET 7 Worker | Legacy REST dependencies, RestSharp 106, fixed close-time assumptions, and forced process termination | Blocked until calendar, messaging, cancellation, and completion behavior is repaired |
| `SceduledTask.TrainFuturesItiPredictiveModel` | .NET 8 Worker | Misspelled legacy project path, removed REST-client project references, and stale assembly hints | Blocked until behavior is specified and the executable is rebuilt on .NET 10 |
| `TomasAI.ScheduledTasks` | .NET Core 3.1 Worker | Empty/placeholder legacy host | Excluded permanently; do not deploy, catalog, or import |

The inventory searched the dedicated scheduled-task project roots, the legacy `TomasAI.ScheduledTasks` tree, Server
Manager configuration, Reference-domain schedule contracts/projections, and solution executable projects.

### 3.2 Explicitly non-cataloged executables

Other executable projects—including database-backup consoles, projection migration, benchmarks, test applications,
API/UI hosts, and vendor samples—are not scheduler candidates merely because they are console applications. Adding one
requires a separate catalog proposal that defines arguments, identity, dependencies, timeout, overlap, idempotency,
completion evidence, output bounds, and safety environment.

### 3.3 Admission rule

The catalog key identifies an approved immutable task definition; it is not a raw path. The first production catalog
will be compiled/configured by administrators and will contain no enabled entry by default. Executable paths are
resolved beneath an approved deployment root and validated again before every launch.

## 4. Historical schedule-shaped code

### 4.1 Located code artifacts

The legacy Reference-domain schedule model contains:

- `ScheduledJobReadModel` with job ID/name, `Daily` schedule type, anchor date/time, interval, task name, enabled flag,
  audit timestamps/users, and optional day-of-week flags;
- add/change/remove commands and corresponding events;
- `IReferenceDbReadContext.GetScheduledJobsAsync`;
- Reference projection CQL for scheduled-job and day-of-week rows; and
- ScyllaDB projection write/recovery code.

This is historical code, not a scheduler engine or evidence of deployed schedule data. It has no complete Quartz
trigger, timezone, misfire, timeout, overlap, retry, process ownership, run-attempt, stdout/stderr retention, or
durable audit model.

### 4.2 Greenfield decision

The user confirmed that no scheduler currently exists and no schedule definitions need migration. Therefore:

- no PostgreSQL or ScyllaDB query/export is required for scheduler implementation;
- the initial Scheduler Host schemas start empty;
- there is no legacy schedule import path in the initial implementation;
- operators create all task definitions explicitly, and every definition starts disabled; and
- the dormant Reference-domain code does not become scheduling authority.

If operational schedule data is discovered later, it will be handled as a separate discovery and change request. It
is not assumed by this design and does not gate SM-S1, SM-S2, or later implementation stages.

## 5. Windows service and authorization decision

### 5.1 Service identity

| Item | Decision |
| --- | --- |
| Windows service name | `IFMSchedulerHost` |
| Display name | `IFM Scheduler Host` |
| Initial service account | Virtual service account `NT SERVICE\IFMSchedulerHost` |
| Startup | Automatic (Delayed Start) only after the SM-S5 installation gate |
| Interactive logon | Prohibited |
| Network/database identity | Dedicated PostgreSQL login/role supplied through the approved secret mechanism; never embedded in JSON or command arguments |
| Executable permissions | Read/execute only on approved task deployment roots |
| State/log permissions | Modify only on Scheduler Host state/output roots; no write permission to executable roots |

A managed service account may replace the virtual account if the deployment becomes domain/multi-host. That is a
reviewed deployment change, not a configuration toggle.

### 5.2 Local Windows groups

| Group | Capabilities |
| --- | --- |
| `IFM Scheduler Readers` | Connect to the read pipe; view health, definitions, history, audit, and redacted logs |
| `IFM Scheduler Operators` | Reader rights plus edit approved schedule timing, enable/disable within policy, run now, cancel, and explicit retry |
| `IFM Scheduler Administrators` | Operator rights plus install/upgrade/configure service, manage catalog, retention/export, service lifecycle, and production enablement |

Membership is explicit; nesting `Users` or `Authenticated Users` is prohibited. Local Administrators may install and
recover the service but are not silently treated as normal scheduler operators by application authorization.

### 5.3 IPC and filesystem ACLs

- Scheduler Host owns `IFM.ServerManager.Scheduler.v1`.
- The service identity has full pipe access; Readers receive read/query access; Operators receive approved command
  access; Administrators receive privileged command access.
- Authorization is checked per request after connection, not inferred solely from possession of the pipe name.
- Task output files grant write to the service identity, read to Readers/Operators/Administrators, and no general-user
  access.
- Executable roots grant no write access to the service identity or Operators.
- Secrets are never returned through IPC or inherited by children unless the catalog explicitly grants a named secret.

## 6. PostgreSQL identity and schema ownership

The scheduler uses the dedicated `ifm_scheduler` database with separate ownership:

| Role/schema | Purpose |
| --- | --- |
| `ifm_scheduler_owner` | Migration-only role; owns `ifm_quartz` and `ifm_scheduler`; not used by the running service |
| `ifm_scheduler_app` | Runtime login used by Scheduler Host; least-privilege DML/execute rights only |
| `ifm_quartz` | Quartz-owned tables and indexes |
| `ifm_scheduler` | IFM task catalog snapshot, schedule metadata, run/attempt, audit, retention, and schema version |

The WPF client receives no database credential and never reads these schemas directly. PostgreSQL unavailability is a
critical unhealthy state; there is no RAM or secondary-database trigger fallback.

## 7. Pinned package policy

| Package | Pinned version | Reason |
| --- | --- | --- |
| `Quartz` | `3.19.1` | Current stable Quartz.NET 3 line validated by the prototype and compatible with .NET 10 |
| `Quartz.Extensions.Hosting` | `3.19.1` | Generic-host lifecycle integration for the later Worker/Windows Service |
| `Quartz.Serialization.SystemTextJson` | `3.19.1` | Recommended JSON persistence for a new ADO job store |
| `Npgsql` | `10.0.3` | Matches the repository's existing .NET 10 PostgreSQL provider line |
| `Microsoft.Extensions.Hosting.WindowsServices` | `10.0.10` planned for SM-S2 | Matches the solution's .NET 10 Microsoft.Extensions patch line |

All Quartz packages must remain on the same exact version. Updates require restore/build/test, schema-diff review,
persistence restart proof, release-note review, and a rollback note. Floating and prerelease versions are prohibited.

Quartz persistence policy is:

- `JobStoreTX`;
- `PostgreSQLDelegate`;
- Npgsql data source;
- `ifm_quartz.qrtz_` table prefix;
- System.Text.Json serializer;
- `useProperties=true`, restricting durable `JobDataMap` values to strings;
- non-clustered mode for the first release; and
- versioned create-only migrations derived from the matching Quartz release. Destructive sample schema scripts are
  prohibited in production.

## 8. .NET 10 PostgreSQL persistence proof

The project
`TomasAI.IFM.Application.ServerManager.SchedulerPrototype.IntegrationTests` is intentionally a gate prototype, not the
SM-S2 Scheduler Host implementation.

It proves:

1. the pinned package set restores and builds for `net10.0`;
2. configuration selects `JobStoreTX`, `PostgreSQLDelegate`, Npgsql, System.Text.Json, string properties, the dedicated
   schema prefix, and non-clustered operation;
3. the Quartz 3.19.1 PostgreSQL schema can be installed in a disposable PostgreSQL 17.2 database;
4. a durable job and future trigger can be written;
5. the scheduler can shut down and be recreated with the same scheduler name; and
6. the recreated scheduler reads the job, its string data, and its trigger before cleanup.

The integration fixture binds PostgreSQL only to a random loopback port, uses a disposable credential, and forcibly
removes its named container during teardown.

### 8.1 Recorded validation

```text
dotnet test ... --filter FullyQualifiedName~QuartzPostgresPrototypeConfigurationTests
Passed: 5, Failed: 0, Skipped: 0

dotnet test ... --filter FullyQualifiedName~QuartzPostgresPersistenceIntegrationTests
Passed: 1, Failed: 0, Skipped: 0
PostgreSQL image: postgres:17.2
Runtime: .NET SDK 10.0.302 / net10.0
```

## 9. SM-S0 exit checklist

| Requirement | Result | Evidence |
| --- | --- | --- |
| Specification and safety baseline accepted | Pass | User authorized SM-S0; design version 1.5 is the staged implementation baseline |
| Candidate executables inventoried | Pass | Section 3 |
| Historical schedule-shaped code assessed | Pass | Section 4 |
| Operational scheduler or schedule migration | Not applicable | User confirmed this is a greenfield scheduler implementation |
| Service identity and Windows groups decided | Pass | Section 5 |
| PostgreSQL roles/schema boundary decided | Pass | Section 6 |
| Quartz packages selected and pinned | Pass | Section 7 and prototype project |
| .NET 10 PostgreSQL persistence prototype | Pass | Six focused tests and Docker PostgreSQL restart proof |
| Any schedule created or enabled | Correctly not performed | Prohibited in SM-S0 |

## 10. Entry conditions for SM-S1

SM-S1 may now implement only the reusable API/UI process and log foundation. It must preserve these constraints:

- no Quartz runtime or Scheduler Host service is introduced in SM-S1;
- child processes use argument lists, explicit executable paths, asynchronous stdout/stderr drains, cancellation, and
  bounded shutdown;
- API/UI ownership remains interactive and separate from future unattended scheduled-task ownership;
- current unrelated process definitions are not silently launched; and
- the WPF UI remains responsive under output, process failure, and shutdown.

## 11. References

- [Server Manager scheduled-task supervision specification](ServerManager-Scheduled-Task-Supervision-Specification.md)
- [Server Manager implementation details](ServerManager-Implementation-Details.md)
- [Quartz.NET 3.19.1 NuGet package](https://www.nuget.org/packages/Quartz/3.19.1)
- [Quartz.NET configuration reference](https://www.quartz-scheduler.net/documentation/quartz-3.x/configuration/reference.html)
- [Quartz System.Text.Json persistence package](https://www.nuget.org/packages/Quartz.Serialization.SystemTextJson/3.19.1)
- [Quartz 3.19.1 PostgreSQL schema](https://github.com/quartznet/quartznet/blob/v3.19.1/database/tables/tables_postgres.sql)

## 12. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 1.1 | 2026-08-20 | Corrected the baseline to a greenfield scheduler: no live schedule export or migration prerequisite exists; Reference-domain schedule classes are dormant historical code only. |
| 1.0 | 2026-08-20 | Completed the SM-S0 inventory, identity/authorization decisions, package pins, PostgreSQL persistence prototype, validation evidence, and SM-S1 entry conditions. |
