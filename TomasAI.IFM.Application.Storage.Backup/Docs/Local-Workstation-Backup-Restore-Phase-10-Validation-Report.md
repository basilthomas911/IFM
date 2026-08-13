# Local Workstation Database Backup and Restore Phase 10 Validation Report

**Gate:** 10 — Ubuntu 24.04 Docker qualification, runtime validation, and legacy removal

**Status:** Implementation and runtime qualification passed; gate blocked on one external host-encryption proof

**Date:** 2026-08-13

## Result

The PostgreSQL and Scylla native paths, Ubuntu 24.04 Worker image, persistent journal restart, durable integration
workflow, UI/system behavior, and legacy removal all passed. Gate 10 is not declared fully green because the available
Windows session is not elevated and therefore cannot read the BitLocker state of the drive backing Docker Desktop.

## Native engine qualification

The native tests use pinned images rather than floating tags:

| Engine | Qualification image | Result |
| --- | --- | --- |
| PostgreSQL | `postgres:16.14` | Physical base backup with streamed WAL, native verification, host-restart idempotency, isolated fresh-target boot, and restored-row validation passed. |
| Scylla | `scylladb/scylla:6.2.2` | Native snapshot and SSTable evidence, verification, host-restart idempotency, fresh-node restore, and restored-row validation passed. |

The PostgreSQL 16 qualification found a real compatibility difference: its `backup_manifest` does not supply the
`System-Identifier` field expected by the prior reader. The capability now obtains a missing identifier from the
allowlisted `pg_controldata` tool while retaining the manifest value when present. Tool discovery, validation, Docker
test execution, and the Ubuntu image now cover `pg_basebackup`, `pg_verifybackup`, `pg_ctl`, and `pg_controldata`.

Final combined command:

```text
dotnet test TomasAI.IFM.Framework.Storage.IntegrationTests/TomasAI.IFM.Framework.Storage.IntegratedTests.csproj \
  --no-build --no-restore --filter "Category=Gate10NativeIntegration"

Passed: 2, Failed: 0, Skipped: 0, Duration: 1m 18s
```

Only uniquely named disposable PostgreSQL and Scylla containers and their temporary data were created by these tests.

## Ubuntu Worker qualification

The final composition was rebuilt and started with:

```text
docker compose -f Docker/DatabaseBackup/docker-compose.yml config --quiet
docker compose -f Docker/DatabaseBackup/docker-compose.yml up --detach --build --wait --wait-timeout 120
```

Results:

- both NATS JetStream and the Database Backup Host became healthy;
- liveness and readiness returned `Healthy`;
- the Worker ran as `uid=1654(app) gid=1654(app)`, not root;
- `pg_controldata` reported PostgreSQL 16.14 in the rebuilt image; and
- the journal identity was `2112:574903:77824` before and after Worker restart.

The composition was stopped with `docker compose ... down` without deleting its persistent named volumes.

## Legacy removal and scheduled task migration

The deprecated per-database SystemAdmin workflow was removed after the replacement actor workflow had passed earlier
gates. Removal includes its command/query/event contracts, actors and state, client APIs, HTTP routes, UI model and
view-model, event-listener callbacks, storage methods, tests, benchmarks, and dependency registrations.

The Futures Market Close Worker now:

- targets .NET 10;
- uses the shared NATS producer and typed durable command APIs;
- requests application shutdown and validates command acceptance;
- submits configured protection sets rather than querying database names;
- sends `RequestDatabaseBackupCommand` with scheduled-task authorization context; and
- does not use a Core NATS listener as durable completion state.

Its standalone project build passed. The project remains outside `TomasAI.IFM.sln`, so it is validated separately.

The repository audit below returned no matches in source, JSON configuration, or project files (generated output and
historical documentation were excluded):

```text
rg "BackupDatabaseCommand|DatabaseBackupType|\bDatabaseBackupId\b|DatabaseBackupNames|\
DatabaseBackupInfoMessageEvent|BackupDatabaseAsync|GetDatabaseNamesAsync|GetDatabaseNamesQuery|\
SystemAdminCommandActor|SystemAdminQueryActor|SystemAdminUriPath|SystemAdminQueryUriPath|\
ISystemAdminCommandApi|ISystemAdminQueryApi|SystemAdminCommandApi|SystemAdminQueryApi|\
/api/systemadmin/(backup|databasenames)"

Matches: 0
```

## Regression evidence

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Domain.SystemAdmin unit | 28 | 0 | 0 |
| Domain.SystemAdmin BDD | 1 | 0 | 0 |
| Storage Gate 6/7/8 integration | 15 | 0 | 0 |
| Domain.SystemAdmin Gate 5 integration | 2 | 0 | 0 |
| UI presentation unit | 126 | 0 | 0 |
| UI Gate 9 system | 1 | 0 | 0 |
| Gate 10 native integration | 2 | 0 | 0 |

Build validation also passed:

```text
dotnet build TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose/TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose.csproj --no-restore
Build succeeded. 0 warnings, 0 errors.

dotnet build TomasAI.IFM.sln --no-restore -m:1 -p:NuGetAudit=false
Build succeeded. 0 warnings, 0 errors.
```

The solution is built serially because multiple unrelated DataBento projects share one native CMake output directory;
parallel solution builds can race while reconfiguring that directory.

## Encrypted persistent-volume evidence

Persistence is proven: Docker reports the named journal volume as a local volume rooted beneath
`/var/lib/docker/volumes`, and the journal retained the same device, inode, and size across Worker restart. The Windows
drive is healthy NTFS storage and Docker Desktop reports `/var/lib/docker` as its Docker root.

Encryption is not yet proven. `manage-bde -status C:` returned access denied because BitLocker status requires an
elevated administrator session. An operator must run this exact read-only command from an elevated terminal and attach
the result:

```powershell
manage-bde -status C:
```

Gate 10 can be marked passed when that output shows the backing volume is fully encrypted and protection is on. No
code change or additional native qualification is pending.
