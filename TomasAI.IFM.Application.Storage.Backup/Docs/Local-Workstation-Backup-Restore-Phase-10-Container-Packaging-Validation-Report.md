# Local Workstation Database Backup and Restore Phase 10 Container Packaging Validation Report

**Gate:** 10 - Ubuntu 24.04 Docker qualification, runtime validation, and legacy removal

**Slice:** Standalone container packaging and restart-persistence smoke

**Status:** Passed; superseded by the final Phase 10 report

**Date:** 2026-08-12

## Scope completed

- Packaged the existing Database Backup Worker with official .NET 10 SDK and ASP.NET runtime images based on Ubuntu
  24.04 (`noble`). Aspire is not part of this composition.
- Added a bounded Docker build context containing only the Worker and its exact project-reference closure.
- Added Linux container configuration for the SQLite journal, online/offline destinations, PostgreSQL and Scylla
  backup roots, restore workspaces, native tool locations, NATS JetStream, and external secret locations.
- Added standalone NATS JetStream and Worker composition with named persistent volumes.
- Ran the Worker as the image's non-root `app` user with a read-only root filesystem, all Linux capabilities dropped,
  `no-new-privileges`, and a bounded `/tmp` tmpfs.
- Exposed only loopback-bound liveness and readiness endpoints. No backup or restore command endpoint was added.
- Installed PostgreSQL 16 native backup/verification/control tools in the qualification image.
- Verified the SQLite execution journal is reused from the same persistent volume after restarting only the Worker.

The container configuration deliberately keeps `LocalWorkstation` disabled in dry-run mode. This slice does not
execute or claim a native backup or restore.

## Validation evidence

### Compose configuration and image build

```text
docker compose -f Docker/DatabaseBackup/docker-compose.yml config --quiet

Exit code: 0
```

```text
docker compose --progress plain -f Docker/DatabaseBackup/docker-compose.yml build database-backup-host

Image: ifm/database-backup-host:gate10
Build context: 1.55 MB on the first bounded build; 42.54 KB after local build-output exclusions
Exit code: 0
```

The in-image `dotnet publish` completed successfully. The second clean packaging run emitted no redundant host-package
warnings.

### Standalone startup and health

```text
docker compose --progress plain -f Docker/DatabaseBackup/docker-compose.yml \
  up --detach --build --wait --wait-timeout 120

nats: healthy
database-backup-host: healthy
Exit code: 0
```

Both probes returned `Healthy`:

```text
GET http://127.0.0.1:8088/health/live
GET http://127.0.0.1:8088/health/ready
```

### Runtime identity and native tools

```text
uid=1654(app) gid=1654(app) groups=1654(app)
Ubuntu 24.04.4 LTS (Noble Numbat)
pg_basebackup (PostgreSQL) 16.14
pg_verifybackup (PostgreSQL) 16.14
pg_ctl (PostgreSQL) 16.14
```

The Scylla Manager CLI is intentionally not downloaded into the image. Native Scylla qualification must mount the
approved `sctool` binary read-only at `/opt/scylla-manager/bin/sctool` and provide its credentials externally.

### Persistent journal restart smoke

Before restarting the Worker:

```text
/var/lib/ifm/database-backup/journal/execution-journal.db
device:inode:size = 2112:574903:77824
```

After `docker compose restart database-backup-host` and a successful readiness wait:

```text
device:inode:size = 2112:574903:77824
health/live = Healthy
health/ready = Healthy
```

Matching device, inode, and size demonstrate that the restarted Worker reopened the existing journal on the attached
named volume instead of creating a journal in its disposable container layer. Host-level encrypted-volume evidence is
still required before the separate encrypted-persistent-mount definition-of-done item can pass.

### Full solution build

```text
dotnet build TomasAI.IFM.sln --no-restore

Build succeeded.
0 Warning(s)
0 Error(s)
```

## Slice result and Phase 10 follow-up

The standalone Worker/Ubuntu 24.04 packaging smoke passed. The image builds, the Worker connects to the standalone
NATS JetStream service, Linux readiness and liveness are observable, the process runs without root privileges, and
the SQLite journal survives a Worker restart on persistent storage.

The later `Local-Workstation-Backup-Restore-Phase-10-Validation-Report.md` records successful pinned PostgreSQL and
Scylla native fresh-target restores, final regression execution, scheduled-task migration, and repository-wide legacy
API removal. Docker Desktop data resides beneath `D:\Docker\wsl\data` on the qualification host and that drive is not
managed by BitLocker. This is acceptable for the development qualification because no production data or production
secrets are used. The report does not claim encrypted storage; production deployment retains a separate mandatory
encryption-at-rest gate.
