# Local Workstation Database Backup and Restore Phase 10 Validation Report

**Gate:** 10 — Ubuntu 24.04 Docker qualification, runtime validation, and legacy removal

**Status:** Passed for development workstation use

**Date:** 2026-08-13

## Result

The PostgreSQL and Scylla native paths, Ubuntu 24.04 Worker image, persistent journal restart, durable integration
workflow, UI/system behavior, and legacy removal all passed. Development workstation storage is not required to be
encrypted. Production data and production secrets must not be placed in this development backup location, and the
production deployment retains a mandatory encryption-at-rest gate.

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

## Development persistent-volume evidence

Persistence is proven: Docker reports the named journal volume as a local volume rooted beneath
`/var/lib/docker/volumes`, and the journal retained the same device, inode, and size across Worker restart. Docker
Desktop's `CustomWslDistroDir` is `D:\Docker\wsl\data`; the current data files include
`disk\docker_data.vhdx` and `main\ext4.vhdx`.

The operator ran this read-only command from an elevated terminal:

```powershell
manage-bde -status C:
```

The supplied result was:

```text
Volume: C: (OS Volume)
BitLocker Version: None
Conversion Status: Fully Decrypted
Percentage Encrypted: 0.0%
Encryption Method: None
Protection Status: Protection Off
Lock Status: Unlocked
Key Protectors: None Found
```

Because Docker Desktop data resides on `D:`, this `C:` result is retained only as non-applicable host evidence. The
operator then ran the applicable command from an Administrator PowerShell window:

```powershell
manage-bde -status D:
```

Windows returned:

```text
ERROR: The volume D: could not be opened by BitLocker.
This may be because the volume does not exist, or because it is not a valid BitLocker volume.
```

`D:` is a fixed local drive and the Docker VHDX files are present there, so the result means BitLocker does not manage
it. Encryption is not a development Gate 10 requirement. This evidence is retained to prevent the workstation from
being mistaken for an encrypted production target. A production deployment must use encrypted persistent storage and
repeat the journal restart, native backup, verification, and fresh-target restore checks in that environment.

## E-drive development validation

On 2026-08-13, the development layout was created beneath `E:\IFM\DatabaseBackup`, including journal, vault,
native-engine, restore-workspace, secrets, tools, and validation directories. Development-only P-256 manifest keys
were generated beneath `secrets`, restricted to container UID/GID `1654`, and mounted read-only.

The live development PostgreSQL server reports version 17.2. The Ubuntu backup-host image was therefore updated to
the PostgreSQL 17 tool line; its runtime reports `pg_basebackup 17.11`. PostgreSQL and Scylla native-source activation
can be selected independently. The development service is presently PostgreSQL-enabled and Scylla-disabled because a
Scylla Manager service and `sctool` are not installed.

The existing Gate 10 native tests were rerun with `TEMP` and `TMP` rooted at
`E:\IFM\DatabaseBackup\validation`. Both passed, and the final current-binary rerun passed in 1 minute 20 seconds:

| Engine | Backup and restore evidence |
| --- | --- |
| PostgreSQL | Disposable source row, physical base backup, native verification, fresh PostgreSQL target boot, matching system identifier, and restored-row query passed. |
| Scylla | Disposable source row, native snapshot/SSTables, verification, fresh Scylla node import, topology validation, and restored-row query passed. |

These tests intentionally use disposable source and restore containers. They validate both engine paths without
altering or overwriting `ifm_db` or `ifm-scylladb`; they are not represented as backups of the current application
data. The real actor-driven Scylla service remains unavailable until Scylla Manager is configured.

The final development composition connected to the existing NATS and PostgreSQL services, became healthy on
`127.0.0.1:8088`, and ran as `uid=1654(app)`. Its E-drive SQLite journal identity was
`73:53761720551735409:77824` before and after container restart, proving that the bind-mounted journal survived and was
reopened. A partial-engine admission check prevents a PostgreSQL-only host from journaling retained Scylla work, and
the development consumer begins at new events on first creation to avoid executing stale workstation commands.
