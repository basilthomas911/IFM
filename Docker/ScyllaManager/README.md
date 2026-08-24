# Scylla Manager Development Service

This composition pins Scylla Manager 3.4.2, the supported line for the current ScyllaDB 6.2.2 node. It includes a
dedicated ScyllaDB metadata service, loopback-only Manager API/metrics ports, an optional matching `sctool` export, and
a disposable Agent-enabled restore target.

Scylla Manager Agent 3.4.2 supports S3, GCS, and Azure backup locations; it does not implement a `localstorage:`
provider. Development therefore includes a pinned, single-node MinIO S3-compatible endpoint. MinIO writes its object
data to `E:\IFM\DatabaseBackup\scylla-manager\object-storage`, while the Agents use the supported
`s3:ifm-development` location. MinIO's API and console are bound to Windows loopback ports 19000 and 19001; the
container network continues to use standard ports 9000 and 9001.

Production physical SATA drives will be formatted and mounted by Linux, for example at
`/srv/ifm-backup/scylla` and `/srv/ifm-backup/postgresql`. PostgreSQL native tools can write directly to that filesystem.
For Scylla Manager 3.4, expose the Scylla backup disks through a supported S3-compatible service such as a production
MinIO deployment, or use external S3/GCS/Azure storage. This still bypasses Windows; MinIO stores the objects directly
on the Linux-mounted SATA filesystem. A single development MinIO container is not a production availability design.

## Safety and adoption sequence

1. Record Scylla version, schema/keyspaces, selected row probes, container command, network, and `docker_scylla` volume.
2. Stop application writers and stop `ifm-scylladb` cleanly.
3. Copy the entire stopped `docker_scylla` volume to the timestamped E-drive safety directory.
4. Build the Agent-enabled image and validate it without mounting the application volume.
5. Remove only the old container, then start `Docker/ScyllaDb/docker-compose.yml`, which mounts the same external volume.
6. Validate application data and Agent health. If validation fails, stop the new container and recreate the captured
   original container definition against `docker_scylla`.

Manager registration initially uses `--without-repair` so installing Manager does not introduce scheduled workload.
The backup/restore proof creates only `ifm_manager_validation`, backs it up to `s3:ifm-development`, restores
it into `ifm-scylla-restore-validation`, and verifies a known row. Existing application keyspaces are not restore
targets.

Never use `docker compose down --volumes` for ScyllaDB or Manager metadata.

Initialize the E-drive directories and generate the development-only Agent token without printing it:

```powershell
& Docker/ScyllaManager/Initialize-Development.ps1
```

The initializer is idempotent and does not overwrite an existing Agent configuration.

Start the persistent development services:

```powershell
docker compose -f Docker/ScyllaManager/docker-compose.yml up --detach --wait
docker compose -f Docker/ScyllaDb/docker-compose.yml up --detach --wait
```

If the E-drive is unavailable or Windows reports that it needs repair, do not keep writing through the bind mount and
do not run a filesystem repair while database services are active. Preserve the E-drive contents and use the explicit
Docker-managed Development fallback until a maintenance window:

```powershell
docker compose -f Docker/ScyllaManager/docker-compose.yml `
  -f Docker/ScyllaManager/docker-compose.safe-storage.yml `
  up --detach --wait scylla-backup-s3 scylla-backup-s3-init
```

The fallback replaces only MinIO's `/data` mount and does not modify or delete the E-drive directory. It is suitable
for Development qualification, not as the sole long-term backup location.

The restore target is intentionally opt-in. Start it only for a restore drill, and stop it afterward without deleting
its volume so its Manager registration and host identity remain stable:

```powershell
docker compose -f Docker/ScyllaManager/docker-compose.yml --profile validation up --detach --wait scylla-restore-validation
docker compose -f Docker/ScyllaManager/docker-compose.yml --profile validation stop --timeout 120 scylla-restore-validation
```

Gates 11-12 use separate two-node source and restore clusters so node completeness can be qualified without touching
the application cluster. Docker Desktop needs the higher temporary AIO limit while all four isolated nodes run:

```powershell
$env:IFM_SCYLLA_AIO_MAX_NR = '1048576'
docker compose -f Docker/ScyllaManager/docker-compose.yml --profile gate11 `
  up --detach --wait scylla-gate11-source-1 scylla-gate11-source-2 `
  scylla-gate12-restore-1 scylla-gate12-restore-2

docker compose -f Docker/ScyllaManager/docker-compose.yml --profile gate11 `
  stop --timeout 120 scylla-gate12-restore-1 scylla-gate12-restore-2
```

The Gate 11 source volumes and Manager metadata are persistent. Never use `down --volumes`. A Manager snapshot is
eligible only when its native manifest contains every node recorded in the signed live topology.

Export the client binary consumed by the Database Backup Host whenever the Manager version changes:

```powershell
docker compose -f Docker/ScyllaManager/docker-compose.yml --profile tools run --rm sctool-export
```

Both development clusters are registered with `--without-repair`: `ifm-development` is the source and
`ifm-restore-validation` is the fresh-target profile. See `Development-Validation-Report.md` for the completed
backup/restore proof and rollback evidence.

## Docker Desktop AIO prerequisite

Docker Desktop's Linux VM defaults `fs.aio-max-nr` to 65,536, which is insufficient when the application Scylla node,
Manager metadata node, and isolated Gate 11-12 source/restore nodes run together. The `aio-init` one-shot service
raises the shared Docker VM limit to 1,048,576 before either Manager-owned Scylla service starts. It requires Docker's
`privileged` mode, changes only the temporary Linux VM kernel maximum, does not preallocate that capacity, and resets
when Docker Desktop restarts. Override `IFM_SCYLLA_AIO_MAX_NR` only when the development VM has a separately qualified
limit.

This service is a Windows/Docker Desktop development workaround. Do not deploy it in production. Set the production
Linux host's AIO limit through the operating-system provisioning and monitoring configuration instead.
