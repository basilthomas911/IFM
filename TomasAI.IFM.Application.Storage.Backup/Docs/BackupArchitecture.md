# Database Backup and Restore Architecture

Status: Revised first draft
Scope: PostgreSQL and ScyllaDB running in Docker
Last reviewed: 2026-07-31

## Purpose

This document proposes an asynchronous backup and restore service that can be called from a high-level actor, scheduled daily, and observed through structured status and log events.

The target architecture supports two backup types:

- **Full**: backs up the complete supported scope.
- **Partial**: backs up changes since the latest successful full or partial backup.

The backup process runs inside a dedicated Docker container. Backup artifacts and manifests are written to a mounted backup drive. The backup container must not modify active database source data.

## Delivery priority and paper-trading acceptance target

The first operational milestone is deliberately limited to reliable full backup and full restore. It must be running and exercised daily before paper trading starts producing persistent data. Partial backup and chain-based restore are the next phase and must build on the same manifests, verification, logging, and fresh-volume restore workflow.

The paper-trading milestone is complete only when all of the following are automated and repeatable:

- A daily full PostgreSQL backup completes and passes checksum and native backup verification.
- A daily full ScyllaDB backup includes schema and all configured application keyspaces and passes artifact verification.
- Both engines can be restored into new, isolated Docker volumes without deleting or overwriting the active volumes.
- Restored PostgreSQL and ScyllaDB containers start successfully and pass application-level schema and data validation queries.
- Backup, restore, validation, and failure status is available to the actor through NATS and to operators through structured logs.
- At least one automated restore drill runs on a regular schedule. Daily is preferred during paper trading; the frequency may be reduced only after the process has demonstrated sustained reliability.
- Restore-drill volumes are explicitly deleted only after successful validation and according to the test cleanup policy. Active volumes are never cleanup targets.

Incremental/partial backup, point-in-time recovery, long-term production retention, encryption, and off-site replication remain later phases. They must not delay the verified full-backup/full-restore milestone.

## Terminology

- **Full backup**: a self-contained backup of the configured physical PostgreSQL cluster or configured ScyllaDB keyspaces.
- **Partial backup**: changes since a valid parent full or partial backup. It is also called an incremental backup by the native database tools.
- **Full restore**: restores a self-contained full backup to the state captured by that backup.
- **Partial-chain restore**: restores the complete database state represented by a full backup plus every required partial backup through a selected recovery point.
- **Selective restore**: restores only selected databases, schemas, tables, or keyspaces. This is a separate feature and is not implied by 'Partial'. PostgreSQL physical incremental backups are cluster-wide.

A partial backup is not independently restorable and does not produce a database containing only changed data. A partial-chain restore always reconstructs a complete usable target database.

## Important engine constraints

### PostgreSQL

PostgreSQL partial backups are physical, cluster-wide incremental backups. PostgreSQL 17 supports them through 'pg_basebackup --incremental'. A partial backup depends on an earlier backup manifest, and restoration requires the complete chain to be reconstructed with 'pg_combinebackup'.

'pg_basebackup' cannot back up an individual database or table. Selective database and table backups can be produced with 'pg_dump', but those are complete logical exports rather than partial backups. A future 'LogicalExport' operation should represent that behavior instead of overloading 'Partial'.

The live PostgreSQL data directory must not be copied from a read-only Docker volume while PostgreSQL is running. The backup container connects to PostgreSQL over the Docker network using the replication protocol and writes a consistent backup directly to the backup drive.

### ScyllaDB

A ScyllaDB full backup consists of a schema export and snapshots of the selected keyspaces or tables. Snapshots are hard links to immutable SSTables created by the ScyllaDB process.

A ScyllaDB partial backup consists of newly flushed SSTables since the previous full or partial backup. Incremental backup must be enabled on the ScyllaDB node. Restoration requires the base snapshot and all dependent partial backups.

Scylla backup is a per-node operation. The initial implementation may target the current single-node Docker deployment. A multi-node deployment should use Scylla Manager for cluster-wide coordination, manifests, progress reporting, and restore handling.

## Proposed architecture

~~~text
Daily scheduler / UI
        |
        v
SystemAdminCommandActor
        |
        | DatabaseBackupEvent over NATS
        v
Database Backup Service container
        |
        +-- PostgreSqlBackupEngine -> pg_basebackup / pg_verifybackup
        |
        +-- ScyllaBackupEngine -----> snapshot/flush APIs + SSTable copy
        |
        +-- Manifest and logs ------> mounted backup drive
        |
        +-- Progress/completed/failed events over NATS
                                      |
                                      v
                         Actor/UI backup log listener
~~~

The C# service executes database utilities inside its own container. It does not call 'docker exec' and does not mount the Docker socket. Docker socket access would give the container host-level control and is not required for backup execution.

## Backup semantics

| Engine | Full backup and restore | Partial backup and chain restore |
|---|---|---|
| PostgreSQL | Complete physical cluster backup restored directly into a fresh PostgreSQL volume | Changed blocks since the parent backup; reconstruct a synthetic full backup with the complete chain before restoring it into a fresh volume |
| ScyllaDB | Schema plus snapshot of configured keyspaces/tables restored into a fresh ScyllaDB volume with the same topology | Newly flushed SSTables plus required commit-log information since the base snapshot; restore the base and every dependent partial into a fresh volume |

Every partial backup records:

- 'BaseBackupId': the originating full backup.
- 'ParentBackupId': the immediately preceding full or partial backup.
- Database engine and version.
- Cluster identity and target scope.
- Selected keyspaces or tables where applicable.
- Start time, completion time, and consistency boundary.
- Artifact paths, sizes, and checksums.
- Backup command version and relevant options.

If no valid parent backup exists, the default policy is to create a full backup automatically.

## Cross-database consistency boundary

PostgreSQL and ScyllaDB cannot create one atomic snapshot across both engines. When an application operation writes related data to both databases, independently started backups may represent different application points in time.

For the paper-trading milestone, the coordinating actor must:

1. Enter backup maintenance mode and pause new ingestion and cross-database writers.
2. Wait for in-flight writes to complete.
3. Record a shared 'BackupSetId' and application checkpoint or last-ingested sequence.
4. Start the PostgreSQL and ScyllaDB backups and record their individual consistency boundaries.
5. Resume ingestion after both native snapshots have established their backup boundaries, or after both backups complete for the simplest initial implementation.

The manifest for each engine records the shared 'BackupSetId'. Restore validation must reject a combined restore when the two backups belong to different backup sets unless an operator explicitly requests that behavior.

## Artifact storage

Backup artifacts are stored uncompressed in their native file and directory layout. Compression is intentionally outside the proposed implementation so the first version can focus on reliable backup, verification, and restore behavior. It may be reconsidered later as a separately versioned artifact-storage feature.

The service must:

- Record file paths, sizes, and checksums in the operation manifest.
- Keep 'manifest.json', its checksum, and 'backup.log.jsonl' beside the native backup data.
- Write the complete operation into an '.inprogress' directory and atomically rename that directory only after native verification and checksum generation succeed.
- Perform a disk-space preflight for the retained backup, a fresh restored volume, and any additional partial-chain reconstruction staging.
- Preserve PostgreSQL files and ScyllaDB SSTable component sets exactly as produced or copied by the native backup process.

## Docker deployment

Add an 'ifm-database-backup' service containing:

- The .NET backup worker and internal API.
- PostgreSQL 17 client utilities.
- ScyllaDB-compatible administration and schema utilities.
- A non-root runtime user.
- A read-only container root filesystem.
- Temporary writable storage through 'tmpfs'.
- Network access to PostgreSQL, ScyllaDB, and NATS.
- No Docker socket.

Proposed mounts:

~~~yaml
volumes:
  - D:/IFM-Backups:/backups:rw
  - docker_scylla:/source/scylla:ro
~~~

The PostgreSQL data volume is intentionally not mounted. PostgreSQL backup traffic uses the replication protocol.

The database image versions should be pinned before backup implementation. Physical backup and restore formats are version-sensitive, and the current containers use floating 'latest' tags.

## PostgreSQL prerequisites

PostgreSQL partial backups require:

- PostgreSQL 17 or later.
- 'summarize_wal=on'.
- A 'wal_summary_keep_time' that covers the maximum interval between dependent backups.
- A dedicated backup account with 'REPLICATION' permission.
- Appropriate 'pg_hba.conf' access from the backup container.
- Sufficient 'max_wal_senders' for the backup and WAL stream.

Continuous WAL archiving and point-in-time recovery are not required for the first version. They may be added as a separate capability later.

## C# contracts

The public actor-facing contract is engine-neutral:

~~~csharp
public interface IDatabaseBackupApi
{
    Task<BackupOperation> StartBackupAsync(
        BackupRequest request,
        CancellationToken cancellationToken);

    Task<RestoreOperation> StartRestoreAsync(
        RestoreRequest request,
        CancellationToken cancellationToken);

    Task<BackupOperation?> GetOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken);

    IAsyncEnumerable<BackupLogEntry> ListenAsync(
        Guid operationId,
        CancellationToken cancellationToken);

    Task CancelAsync(
        Guid operationId,
        CancellationToken cancellationToken);
}
~~~

Proposed internal abstractions:

- 'IDatabaseBackupEngine'
- 'IPostgreSqlBackupEngine'
- 'IScyllaBackupEngine'
- 'IBackupProcessRunner'
- 'IBackupManifestStore'
- 'IBackupLogListener'
- 'IBackupOperationStore'
- 'IRestoreCoordinator'
- 'IBackupTargetRegistry'

The process runner uses 'ProcessStartInfo.ArgumentList', redirects both standard output and standard error, kills the process tree on cancellation, and never constructs a shell command from user-controlled strings.

## Operation state

Each backup or restore has a unique 'OperationId' and transitions through these states:

~~~text
Queued
Validating
Preparing
Running
Verifying
Completed
Failed
Cancelled
~~~

Only one backup or restore may run for the same physical database target at a time. A restore takes an exclusive lock for the entire engine target.

## Actor and event integration

The repository already contains:

- 'BackupDatabaseCommand'
- 'DatabaseBackupEvent'
- 'DatabaseBackupInfoMessageEvent'
- 'DatabaseBackupCompleteEvent'
- 'DatabaseBackupFailEvent'
- 'SystemAdminCommandActor'
- A UI backup event listener

The proposed integration is:

1. A scheduler or UI sends 'BackupDatabaseCommand'.
2. 'SystemAdminCommandActor' validates the request and emits 'DatabaseBackupEvent'.
3. The backup service consumes the event and queues an operation.
4. The operation returns immediately with an 'OperationId'.
5. The backup engine emits structured progress and log entries.
6. The service publishes progress, completion, failure, or cancellation events over NATS.
7. The high-level actor and UI listener consume those events.

Required contract changes:

- Rename 'DatabaseBackupType.Diff' to 'Partial' while preserving its serialized numeric value.
- Add 'OperationId', 'BaseBackupId', and 'ParentBackupId' where applicable.
- Add 'DatabaseRestoreCommand' and matching requested/progress/completed/failed events.
- Normalize the actor subject used by 'DatabaseBackupInfoMessageEvent' and 'SystemAdminUIEventConsumer'.
- Replace the static database-name list with an engine-aware target registry.
- Add structured progress properties instead of relying only on free-form messages.

## Logging and status propagation

'IBackupLogListener' receives every process and orchestration entry:

- Operation ID.
- Timestamp.
- Engine and target.
- State and severity.
- Progress percentage where supported.
- Current table, keyspace, or backup stage.
- Bytes processed and total bytes where supported.
- Process exit code.
- Standard output or standard error message.

Initial sinks:

- Structured 'ILogger' output for Docker logs.
- Append-only 'backup.log.jsonl' beside the backup manifest.
- NATS progress events for the actor and UI.
- Current-operation state stored in the service for queries.

Backup metadata must be stored on the backup drive rather than solely in either database being protected.

## PostgreSQL command strategy

Illustrative full backup:

~~~text
pg_basebackup
  --dbname=<replication connection>
  --pgdata=<run>/data
  --format=plain
  --wal-method=stream
  --progress
  --verbose
  --manifest-checksums=SHA256

pg_verifybackup <run>/data
~~~

Illustrative partial backup:

~~~text
pg_basebackup
  --dbname=<replication connection>
  --pgdata=<run>/data
  --format=plain
  --wal-method=stream
  --incremental=<parent>/postgres-backup-manifest
  --progress
  --verbose

pg_verifybackup <run>/data
~~~

Full restore preparation:

~~~text
verify operation manifest and checksums
pg_verifybackup <full>/data
copy the verified cluster into a new PostgreSQL volume
start an isolated validation container
run native and application validation
~~~

Partial-chain restore preparation:

~~~text
verify the full and every required partial operation
pg_verifybackup <each-chain-member>/data

pg_combinebackup
  --output=<staging>
  <full> <partial-1> ... <partial-n>

pg_verifybackup <staging>
~~~

The verified combined backup is restored into a new PostgreSQL data volume and started as a validation container before cutover. Missing, corrupt, unordered, or engine-incompatible chain members fail the restore before a target volume is populated.

## ScyllaDB command strategy

### Full backup

1. Export schema, roles, permissions, and service levels.
2. Flush and snapshot each selected keyspace.
3. Copy the tagged snapshot files from the read-only Scylla volume.
4. Generate checksums and a manifest.
5. Verify every copied SSTable component.
6. Clear only the snapshot tag created by this operation after the copy succeeds.

Illustrative commands:

~~~text
cqlsh -e "DESC SCHEMA WITH INTERNALS AND PASSWORDS"
nodetool snapshot -t <operation-tag> <keyspace>
~~~

The command adapter may use Scylla's administration REST endpoints rather than starting 'nodetool' if that is more reliable in the backup container.

### Partial backup

1. Confirm incremental backup is enabled.
2. Flush the selected keyspaces to establish the backup boundary.
3. Find SSTables in each table's 'backups' directory that are absent from the parent chain.
4. Copy the complete component set for each new SSTable.
5. Preserve the commit-log segment range required to recover from the base snapshot through this partial recovery point.
6. Record table UUIDs, file names, sizes, checksums, and commit-log boundaries.
7. Retain the base snapshot and complete partial and commit-log chain.

The direct single-node implementation must not claim that a ScyllaDB partial backup is restorable until commit-log retention has been implemented and verified. If reliable segment coordination cannot be demonstrated, partial backup must use Scylla Manager or remain disabled.

### Full restore

For the same topology:

1. Verify the operation manifest, schema, and every SSTable component set.
2. Reject missing, additional, or checksum-mismatched files before creating the restore target.
3. Create a new empty ScyllaDB volume and validation container with the backed-up topology and compatible pinned version.
4. Recreate the schema, excluding restored materialized-view and secondary-index SSTables where the engine guidance requires rebuilding them.
5. Stop the validation ScyllaDB process before direct SSTable placement.
6. Populate the new volume, correct ownership and permissions, and start the validation node.
7. Rebuild materialized views and secondary indexes where required, then validate schema and data.
8. Run repair when appropriate.

### Partial-chain restore

1. Resolve the selected partial backup to its base full backup and complete ordered parent chain.
2. Verify the base snapshot plus every required partial operation.
3. Reject missing, duplicate, corrupt, mismatched-topology, or incompatible-version chain members.
4. Restore the base snapshot and then all dependent incremental SSTables and required commit-log information into a new volume.
5. Continue with the same startup, rebuild, repair, and application validation used by a full restore.

A topology change should use Scylla Manager or 'sstableloader' rather than direct file placement.

## Artifact layout

~~~text
/backups/
  postgres/cluster/<operation-id>/
    manifest.json
    manifest.sha256
    postgres-backup-manifest
    backup.log.jsonl
    data/
  scylla/<target>/<operation-id>/
    manifest.json
    manifest.sha256
    backup.log.jsonl
    schema/
    snapshots/
    incrementals/
    commitlogs/
~~~

The PostgreSQL native 'backup_manifest' is copied to 'postgres-backup-manifest' so a later partial backup can pass it to 'pg_basebackup' without inspecting the parent data directory. Only directories applicable to the backup type are present; for example, a full ScyllaDB backup has schema and snapshot directories, while a partial has incremental and commit-log directories. An operation writes into an '.inprogress' directory. The directory is atomically renamed to its final name only after native verification and checksum generation succeed. Incomplete directories are never eligible for restore.

## Restore safety

Restore is intentionally more restrictive than backup:

- Production restore and cutover are never scheduled automatically. Isolated restore drills may be scheduled.
- Restore requires an explicit operation and target.
- Backup chain and checksums are verified before any target change.
- The preferred target is a new writable volume and validation container.
- In-place restore requires an additional explicit confirmation and maintenance mode.
- Cutover is a separate confirmed operation after validation.
- The backup container never receives permanent write access to active database volumes.

Managing container lifecycle requires a trusted host-side deployment controller. The backup service should not receive Docker socket access merely to perform cutover.

## Scheduling and retention

Paper-trading policy:

- Run a full backup daily for both engines as one coordinated backup set.
- Run an automated restore drill daily when capacity and duration permit; otherwise run it frequently enough that a restore failure is detected before several backup generations accumulate.
- Keep at least the latest validated full backup plus the previous validated full backup during rollover.
- Never prune the last successfully restore-tested backup.
- Alert on missed schedules, failed verification, insufficient space, excessive duration, and overdue restore drills.

Later partial-backup policy:

- Run a full backup weekly.
- Run a partial backup daily.
- Run a full backup automatically if a valid partial parent is unavailable.
- Prevent overlapping backup and restore operations for the same engine target.
- Never delete a full backup while a retained partial depends on it.
- Prune complete chains rather than individual backup directories.
- Retain failed-operation manifests and logs for diagnosis without treating their data as restorable.

The scheduler publishes the same SystemAdmin command used for a manual backup. Scheduling remains outside the backup engine so scheduled and manual operations follow one execution path. Retention calculations use the actual native artifact sizes and reserve capacity for a fresh restore plus partial-chain staging where applicable.

## Security requirements

- Use Docker secrets or mounted credential files rather than command-line passwords.
- Use a dedicated PostgreSQL replication account.
- Use a least-privilege Scylla administration account where supported.
- Validate targets against an allowlist; do not accept arbitrary host names, command arguments, or paths.
- Reject path traversal in operation IDs and artifact names.
- Run the container as non-root with dropped Linux capabilities.
- Do not mount the Docker socket.
- Restrict the backup network to the required database and NATS services.
- Generate and verify checksums for every completed backup.
- Consider backup encryption and off-host replication as a later phase.

## Testing strategy

### Unit tests

- Backup state transitions.
- Full and partial chain selection.
- Chain-aware retention.
- Command argument construction.
- Process output parsing.
- Cancellation and timeout handling.
- Manifest serialization and checksum validation.
- Target and path validation.

### Docker integration tests

For each engine:

1. Start a disposable database and volume.
2. Create representative schemas and data.
3. Take a full backup.
4. Insert, update, and delete data.
5. Take a partial backup.
6. Restore into a fresh volume and container.
7. Compare schemas, row counts, selected values, and checksums.

The first implementation runs steps 1 through 3 and then immediately performs a full restore and validation. Partial-chain test coverage is added only after the full path is reliable.

### Paper-trading restore drill

1. Select the latest completed coordinated backup set.
2. Verify manifests, file checksums, and native backup integrity.
3. Restore both engines into new uniquely named volumes and isolated validation containers.
4. Verify native engine startup and recovery completion.
5. Run application schema checks, checkpoint checks, representative reads, row/count summaries where practical, and cross-database 'BackupSetId' validation.
6. Publish a signed-off drill result containing the backup IDs, timings, restored sizes, validation results, and logs.
7. Stop the validation containers and apply the explicit test-volume cleanup policy only after the result has been persisted.

Failure scenarios include:

- Missing parent backup.
- Corrupted manifest or artifact.
- Insufficient backup disk space.
- Invalid credentials.
- Database unavailable.
- Process timeout.
- Cancellation during backup.
- Backup container restart during an operation.
- Attempted restore from an incomplete chain.

Restore tests must use disposable volumes and must never target the active development databases.

## Delivery phases

1. **Capability and configuration**
   - Pin database image versions.
   - Select Scylla REST/snapshot coordination or Scylla Manager.
   - Define the backup drive, staging capacity, full-backup retention, and restore-drill schedule.
   - Define application maintenance mode and the shared backup-set checkpoint.

2. **Contracts and event model**
   - Add operation IDs and 'Partial' terminology.
   - Add structured operation state and progress.
   - Add restore commands and events.
   - Introduce an engine-aware target registry.

3. **Backup service foundation**
   - Implement process execution, operation state, manifests, locking, checksums, disk-space preflight, and log listeners.
   - Add NATS command consumption and status publication.

4. **PostgreSQL full milestone**
   - Implement full backup, native verification, fresh-volume restore preparation, and validation.

5. **ScyllaDB full milestone**
   - Implement schema export, full snapshots, verification, fresh-volume restore preparation, and validation.

6. **Docker packaging**
   - Add the hardened backup image, compose service, secrets, mounts, networking, and health checks.

7. **Actor integration**
   - Connect the SystemAdmin events to the backup worker.
   - Publish progress, completion, failure, and cancellation events.
   - Update the existing UI listener.

8. **Paper-trading restore readiness**
   - Coordinate full backups with an ingestion pause and shared 'BackupSetId'.
   - Restore into fresh volumes and validate restored containers.
   - Automate the recurring restore drill and alerting.
   - Add a separate confirmed cutover process.

9. **Partial backup and chain restore**
   - Enable PostgreSQL WAL summaries and implement incremental backup, chain reconstruction, and restore validation.
   - Enable and implement ScyllaDB incremental SSTable and commit-log handling, chain reconstruction, and restore validation.
   - Add chain-aware retention.

10. **Automated verification and hardening**
   - Add unit, Docker integration, capacity, failure, and recovery tests.
   - Exercise clean-host recovery and document the complete runbook.

## Decisions required before implementation

1. Confirm the application maintenance-mode and shared-checkpoint mechanism used to coordinate PostgreSQL and ScyllaDB.
2. Decide whether the initial single-node Scylla implementation should use direct snapshot coordination or introduce Scylla Manager immediately.
3. Select the host backup and staging paths, capacity, daily-full retention, and restore-drill cleanup policy.
4. Define the maximum acceptable backup and restore duration.
5. Define the application validation queries and success criteria that make a restored backup eligible for cutover.
6. Confirm that selective PostgreSQL logical exports and selective restore are outside the first version.
7. After the full milestone is proven, select the full and partial schedule, with weekly full and daily partial as the proposed later default.

## References

- [PostgreSQL 17: pg_basebackup](https://www.postgresql.org/docs/17/app-pgbasebackup.html)
- [PostgreSQL 17: pg_verifybackup](https://www.postgresql.org/docs/17/app-pgverifybackup.html)
- [PostgreSQL 17: pg_combinebackup](https://www.postgresql.org/docs/17/app-pgcombinebackup.html)
- [PostgreSQL 17: SQL dump and restore](https://www.postgresql.org/docs/17/backup-dump.html)
- [PostgreSQL 17: continuous archiving and recovery](https://www.postgresql.org/docs/17/continuous-archiving.html)
- [ScyllaDB: backup your data](https://docs.scylladb.com/manual/stable/operating-scylla/procedures/backup-restore/backup.html)
- [ScyllaDB: restore from backup](https://docs.scylladb.com/manual/stable/operating-scylla/procedures/backup-restore/restore.html)
- [ScyllaDB: snapshots](https://docs.scylladb.com/manual/stable/kb/snapshots.html)
- [Scylla Manager: backup](https://manager.docs.scylladb.com/stable/backup/)
