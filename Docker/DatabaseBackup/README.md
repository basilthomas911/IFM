# Database Backup Host Ubuntu Qualification

This composition packages the standalone Database Backup Host on the official .NET 10 Ubuntu 24.04 (`noble`) image.
It is the first Phase 10 qualification slice and deliberately starts with `LocalWorkstation` disabled in dry-run mode.
It proves Linux configuration, non-root startup, durable mount placement, NATS JetStream connectivity, and HTTP
liveness/readiness probes without executing native backup or restore work.

## Runtime boundaries

- The application runs as the image's non-root `app` user with all Linux capabilities dropped.
- The container root filesystem is read-only; `/tmp` is a bounded `tmpfs`.
- SQLite journal, online/offline vault, PostgreSQL/Scylla backup roots, and restore workspaces are named persistent
  volumes outside the disposable application layer.
- PostgreSQL 16 `pg_basebackup`, `pg_verifybackup`, `pg_ctl`, and `pg_controldata` tools are installed beneath
  `/usr/lib/postgresql/16/bin`.
- Native Scylla qualification must mount the approved `sctool` binary read-only at
  `/opt/scylla-manager/bin/sctool` before enabling the source. The qualification image does not download credentials
  or third-party binaries during startup.
- Manifest keys and database credentials are supplied through external secret mounts/environment references only when
  native mode is explicitly enabled.
- Only health endpoints are exposed: `GET /health/live` and `GET /health/ready`.

## Packaging smoke

From the repository root:

```powershell
docker compose -f Docker/DatabaseBackup/docker-compose.yml config --quiet
docker compose -f Docker/DatabaseBackup/docker-compose.yml build database-backup-host
docker compose -f Docker/DatabaseBackup/docker-compose.yml up -d --wait --wait-timeout 120
docker compose -f Docker/DatabaseBackup/docker-compose.yml ps
curl.exe --fail http://127.0.0.1:8088/health/live
curl.exe --fail http://127.0.0.1:8088/health/ready
docker compose -f Docker/DatabaseBackup/docker-compose.yml exec -T database-backup-host id
docker compose -f Docker/DatabaseBackup/docker-compose.yml restart database-backup-host
docker compose -f Docker/DatabaseBackup/docker-compose.yml up -d --wait --wait-timeout 120
```

Compare `stat -c '%d:%i:%s' /var/lib/ifm/database-backup/journal/execution-journal.db` through `docker compose exec`
before and after restart to prove the Worker reopens the same persistent journal. Use `docker compose ... down` to
stop the services. Do not use `down --volumes` during restart-survival qualification; the journal volume must remain
attached across host recreation. Full Gate 10 native backup, crash recovery, restore drill, fresh-target restore, and
encrypted-mount evidence are recorded in the Phase 10 validation report. Host encryption still requires an elevated
Windows BitLocker-status check before the encrypted-mount gate item can be certified.
