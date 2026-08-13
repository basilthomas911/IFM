# Current Development Container Inventory

**Captured:** 2026-08-13

This inventory records the services used by the workstation application before they are adopted by repository-owned
compose definitions. Secrets are deliberately excluded.

| Service | Container | Pinned runtime version | Persistent data |
| --- | --- | --- | --- |
| NATS JetStream | `ifm-nats-server` | NATS 2.12.0 | `natsjetstream_nats_data` at `/data` |
| PostgreSQL | `ifm_db` | PostgreSQL 17.2 | `docker_postgres_data` at active `PGDATA=/tmp/pgdata` |
| Redis | `redis` | Redis 7.0.9 | legacy anonymous volume at `/data` |
| ScyllaDB | `ifm-scylladb` | ScyllaDB 6.2.2 + Manager Agent 3.4.2 | `docker_scylla` at `/var/lib/scylla` |
| Scylla Manager | `ifm-scylla-manager` | Manager 3.4.2 | `ifm_scylla_manager_db_data` through its ScyllaDB metadata node |
| Scylla backup S3 | `ifm-scylla-backup-s3` | MinIO 2025-09-07 | `E:\IFM\DatabaseBackup\scylla-manager\object-storage` |
| Database Backup Host | generated compose name | IFM .NET 10 development image | bind mounts below `E:\IFM\DatabaseBackup` |

The original Scylla container used `--broadcast-rpc-address 127.0.0.1` and did not contain Scylla Manager Agent. Its
data directory measured 34 GB before migration. The controlled recreation was completed on 2026-08-13 using the same
volume and a network-reachable broadcast address; no data-format upgrade was performed. The original container inspect
record and a checksummed offline volume archive are retained under the E-drive safety directory documented in the
Scylla Manager validation report.

PostgreSQL also has an automatically created image volume at `/var/lib/postgresql/data`, but that is not the active
cluster because `PGDATA` is `/tmp/pgdata`. Redis's legacy anonymous volume remains referenced until a separately tested
cache-volume migration is useful.
