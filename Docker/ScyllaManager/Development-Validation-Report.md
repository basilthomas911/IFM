# Scylla Manager Development Validation Report

**Validated:** 2026-08-13
**Scope:** Windows development workstation only

## Result

Scylla Manager 3.4.2 successfully backed up an isolated keyspace from the application ScyllaDB 6.2.2 node to the
E-drive-backed MinIO bucket, restored its schema and table data into a separate ScyllaDB 6.2.2 node, and returned the
known validation row. Existing application keyspaces were never restore targets.

## Migration and rollback evidence

- Original container image reference: `scylladb/scylla:latest`; resolved image ID begins `8085cb9d`.
- Adopted image: `ifm/scylla-with-manager-agent:6.2.2-3.4.2`.
- Persistent application volume: `docker_scylla`, unchanged across container recreation.
- Host ID before and after: `5b7bb542-c879-43cd-8804-ec2e3b6ad4d4`.
- Logical load before and after: 1.41 GB.
- Application keyspaces before validation keyspace creation: 14.
- Schema table count after migration: 193, matching the pre-migration inventory.
- Original node address: `172.20.0.2`; Manager-reachable address after migration: `172.30.20.10`.
- Offline safety archive: `E:\IFM\DatabaseBackup\scylla-manager\safety\20260813-181029\docker_scylla.tar`.
- Archive size: 31,158,132,736 bytes.
- Archive SHA-256: `B4960709CCF9A3E9315C40E0FDA80E71307EB3E2D8A6BA811D6E5F704A56C2C2`.
- The same directory contains `ifm-scylladb-original-inspect.json` for container-definition rollback.

## Manager topology

| Role | Manager name | Address | Scylla | Agent |
| --- | --- | --- | --- | --- |
| Application source | `ifm-development` | `172.30.20.10` | 6.2.2 | 3.4.2 |
| Fresh restore target | `ifm-restore-validation` | `172.30.20.11` | 6.2.2 | 3.4.2 |

Both clusters were registered with `--without-repair`. Manager reported CQL, REST, and Agent UP for both nodes.

## Backup and restore proof

- Validation keyspace/table: `ifm_manager_validation.restore_probe`.
- Probe ID: `11111111-1111-1111-1111-111111111111`.
- Expected/restored value: `scylla-manager-backup-restore-ok`.
- Location: `s3:ifm-development`.
- Snapshot tag: `sm_20260813222512UTC`.
- Snapshot listing: 279.444 KiB across one node; validation table data was 5.239 KiB.
- `backup/validation-backup`: DONE in 25.7 seconds.
- `restore/validation-schema-restore`: DONE.
- `restore/validation-table-restore`: DONE.
- `validate_backup/validation-integrity`: DONE with no deletion option enabled.
- Matching Linux `sctool` 3.4.2 exported to `E:\IFM\DatabaseBackup\tools\scylla-manager\sctool`.
- Temporary validation keyspaces were dropped from source and target after proof; the source returned to 193 schema
  tables. The restore target was stopped while retaining its separate identity volume for the next drill.
- The Database Backup Host's E-drive mount executed that client and reported client/server 3.4.2 successfully.
- The updated backup host and its Manager 3.4 CLI contract built successfully with `dotnet build`; 15 related
  non-Docker integration tests passed. After two transient `mcr.microsoft.com` TLS timeouts, the pinned .NET images
  downloaded successfully, the development image rebuilt, and the host became healthy with
  `IFM_SCYLLA_BACKUP_ENABLED=true`.

## Development-only infrastructure decisions

Manager Agent 3.4.2 accepts S3, GCS, and Azure locations; it does not accept `localstorage:`. The development stack
therefore uses a pinned single-node MinIO endpoint. MinIO stores objects under
`E:\IFM\DatabaseBackup\scylla-manager\object-storage`. This is a functional development layout, not the production
availability or encryption design.

Docker Desktop also required the temporary Linux-VM `fs.aio-max-nr` ceiling to be raised to 262,144 while three
Scylla nodes were active. The privileged `aio-init` one-shot service applies that development-only setting. Production
Linux provisioning must configure and monitor its own AIO limit without this helper.

Production SATA backup drives can be formatted and mounted directly by Linux. PostgreSQL tools can write directly to
the Linux filesystem; Scylla Manager should use a supported object-storage endpoint whose data path resides on those
Linux-mounted disks, or an external supported object store. Windows and the E-drive layout are not part of that design.
