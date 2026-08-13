# Database Backup Host

The Database Backup Host has two Docker compositions with deliberately different purposes:

- `docker-compose.yml` is the repeatable Ubuntu qualification environment. It creates its own NATS server, uses named
  volumes, and leaves native database access disabled.
- `docker-compose.development.yml` is the normal workstation service. It connects to the existing IFM NATS server and
  bind-mounts all durable backup data beneath `E:\IFM\DatabaseBackup` by default.

Development storage does not require BitLocker. Encryption at rest remains mandatory for production backup storage,
credentials, and manifest signing keys. Development operators should still avoid copying production data or production
secrets onto an unencrypted workstation drive.

## Development directory layout

The development composition uses this layout:

```text
E:\IFM\DatabaseBackup\
  journal\
  online-vault\
  offline-media\
  native\postgresql\
  native\scylla\
  restore\workspace\
  restore\postgresql\
  restore\scylla\
  secrets\
  tools\scylla-manager\
  validation\
```

Set `IFM_BACKUP_ROOT` to another absolute Docker-compatible path when `E:` is unavailable. All service-owned state is
below that one root, so changing drives requires one environment variable rather than editing application settings.

Create the directories from PowerShell:

```powershell
$env:IFM_BACKUP_ROOT = 'E:/IFM/DatabaseBackup'
$directories = @(
  'journal', 'online-vault', 'offline-media', 'native/postgresql', 'native/scylla',
  'restore/workspace', 'restore/postgresql', 'restore/scylla', 'secrets',
  'tools/scylla-manager', 'validation'
)
$directories | ForEach-Object {
  New-Item -ItemType Directory -Force -Path (Join-Path 'E:\IFM\DatabaseBackup' $_) | Out-Null
}
```

The signing-key files must be named `ifm-manifest-private.pem` and `ifm-manifest-public.pem` beneath `secrets`. They are
mounted read-only and must never be committed. Database credentials are supplied only through environment variables.
For the image's non-root UID, generate and restrict development-only keys with:

```powershell
docker run --rm --mount type=bind,source=E:\IFM\DatabaseBackup\secrets,target=/keys postgres:17.2 `
  openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out /keys/ifm-manifest-private.pem
docker run --rm --mount type=bind,source=E:\IFM\DatabaseBackup\secrets,target=/keys postgres:17.2 `
  openssl pkey -in /keys/ifm-manifest-private.pem -pubout -out /keys/ifm-manifest-public.pem
docker run --rm --mount type=bind,source=E:\IFM\DatabaseBackup\secrets,target=/keys postgres:17.2 `
  sh -c 'chown 1654:1654 /keys/*.pem && chmod 0400 /keys/*.pem'
```

## Starting the development service

The current development PostgreSQL container runs PostgreSQL 17.2, so the backup-host image installs the matching
PostgreSQL 17 native tools. Set a connection string that uses `host.docker.internal`, because `localhost` inside the
backup-host container is the backup host itself:

```powershell
$env:IFM_BACKUP_ROOT = 'E:/IFM/DatabaseBackup'
$env:IFM_NATS_URL = 'nats://host.docker.internal:4222'
$env:IFM_POSTGRES_BACKUP_CONNECTION = 'Host=host.docker.internal;Port=5432;Database=postgres;Username=postgres;Password=<development-password>;SSL Mode=Disable;Pooling=false'
$env:IFM_POSTGRES_BACKUP_ENABLED = 'true'
$env:IFM_SCYLLA_BACKUP_ENABLED = 'false'

docker compose -f Docker/DatabaseBackup/docker-compose.development.yml up --detach --build --wait
curl.exe --fail http://127.0.0.1:8088/health/ready
```

`.env.development.example` contains the same settings without real credentials. Copy it to the ignored local `.env`
file and replace its placeholders if persistent PowerShell environment variables are inconvenient.

PostgreSQL and Scylla can be enabled independently. Scylla remains disabled by default so this composition can start
when the optional Manager stack is down. The repository now supplies the compatible Manager service and exports
`sctool` to `E:\IFM\DatabaseBackup\tools\scylla-manager\sctool`. Start `Docker/ScyllaManager` and `Docker/ScyllaDb`,
then set `IFM_SCYLLA_BACKUP_ENABLED=true`. Development uses Manager cluster `ifm-development`, location
`s3:ifm-development`, and the eight current application keyspace families.

For a Scylla restore drill, explicitly start the `validation` profile from `Docker/ScyllaManager`. The allowlisted
fresh target is `ifm-restore-validation`; it is separate from `ifm-development` and the adapter never restores in
place. Stop the validation service after the drill without deleting its volume so its Manager identity remains stable.
The completed small backup, schema restore, table restore, and known-row verification are recorded in
`Docker/ScyllaManager/Development-Validation-Report.md`.

The development composition uses its own durable-consumer prefix and starts a newly created consumer at new events.
This prevents an initial workstation install from executing stale commands retained by an older development consumer;
after creation, JetStream durably resumes that same consumer across service restarts. An engine-specific host ignores
events for protection sets owned by a disabled engine before journal admission.

Stop the service without deleting the bind-mounted data:

```powershell
docker compose -f Docker/DatabaseBackup/docker-compose.development.yml down
```

## Operator console

`TomasAI.IFM.Application.DatabaseBackup.Console` is a NATS client for the actor workflow; it is not a direct wrapper
around `pg_basebackup`, `sctool`, or filesystem operations. The command and query actors and the Database Backup Host
must be running before console commands can complete.

Current verbs are:

```text
Queries:  status, list-operations, show-operation, list-restore-points, verify, reconcile, follow
Commands: backup, cancel, restore, restore-drill, approve-restore, approve-cutover,
          retention-evaluate, retention-execute
```

Restore and retention execution require `--confirm`. A restore always targets an allowlisted fresh target; it never
overwrites the live development database in place.

The implemented local capabilities currently create PostgreSQL physical base backups and Scylla snapshots. In the
public workflow these are full backups. There is no console `full`/`incremental` switch yet, and the console must not
claim incremental support. PostgreSQL incrementals require a PostgreSQL 17 backup-manifest chain and
`pg_combinebackup`; Scylla incrementals require a defined Manager-backed dependency chain. Add an explicit
`Automatic | Full | Incremental` contract only when both engines have defined fallback, retention, verification, and
chain-restore semantics. Until then, `backup` remains an unambiguous full backup request.

Useful next console additions, without changing backup semantics, are `cancel-restore`, `list-protection-sets`,
`show-policy`, `update-policy`, `show-backup-set`, `show-restore-point`, `show-restore`, `list-drills`, `rpo-status`,
`run-stats`, `retention-forecast`, `legal-hold`, and `release-legal-hold`.

## Qualification composition

The qualification image runs as the non-root `app` user, drops Linux capabilities, uses a read-only root filesystem,
and exposes only `GET /health/live` and `GET /health/ready`. From the repository root:

```powershell
docker compose -f Docker/DatabaseBackup/docker-compose.yml config --quiet
docker compose -f Docker/DatabaseBackup/docker-compose.yml build database-backup-host
docker compose -f Docker/DatabaseBackup/docker-compose.yml up -d --wait --wait-timeout 120
curl.exe --fail http://127.0.0.1:8088/health/live
curl.exe --fail http://127.0.0.1:8088/health/ready
docker compose -f Docker/DatabaseBackup/docker-compose.yml exec -T database-backup-host id
docker compose -f Docker/DatabaseBackup/docker-compose.yml restart database-backup-host
docker compose -f Docker/DatabaseBackup/docker-compose.yml up -d --wait --wait-timeout 120
```

Compare `stat -c '%d:%i:%s' /var/lib/ifm/database-backup/journal/execution-journal.db` before and after restart to prove
the Worker reopens the same journal. Use `docker compose ... down` without `--volumes` so qualification evidence is not
deleted accidentally.
