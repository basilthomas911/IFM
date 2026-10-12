# AWS Cloud Backup and Restore Operations Runbook

**Scope:** AWS `DatabaseBackup` source in Development, Staging, and Production.

## Safety boundary

- UI, Console, and ScheduledTask use only the shared DatabaseBackup commands and queries. They never call AWS directly.
- Treat `Disabled`, `Unavailable`, `Degraded`, `Pending`, and `Failed` as distinct states. A disabled or degraded AWS
  source must not make the LocalWorkstation source, Core Actor Host, or NATS unavailable.
- Use operation IDs only for correlation. Never copy credentials, session tokens, native command output, or customer
  data into alerts, dashboards, logs, or tickets.
- Never delete by bucket prefix. Retention execution requires an immutable plan ID, exact revision, independent approval,
  and revalidation of every bucket/key/version ID immediately before deletion.
- Never shorten Object Lock, remove a legal hold, weaken checksum/signature verification, or delete primary evidence to
  force replication convergence.

## Alert routing

| Alert or projected state | First response | Runbook |
| --- | --- | --- |
| Identity/credential rejection | Disable new AWS admission; preserve journaled work | Credential failure |
| Wrong account or Region | Stop before mutation | Wrong-account rejection |
| KMS denial or signing-key failure | Preserve artifacts as ineligible; do not bypass signing | KMS/key recovery |
| WAL gap or lag | Pause PITR eligibility for the affected timeline | WAL gap |
| Replication delay/failure | Keep the primary valid; mark recovery incomplete | Replication failure |
| Journal corruption/unavailability | Stop AWS dispatch; keep local source isolated | Journal PITR |
| Stale multipart upload | Reconcile checkpoint and exact upload ID | Multipart reconciliation |
| Catalog inconsistency | Rebuild only from signed immutable publication records | Catalog rebuild |
| Primary vault unavailable | Select and verify `aws-recovery` explicitly | Primary-vault loss |
| Recovery-only drill | Block primary credentials and rebuild from recovery | Recovery-only restore |
| Legal hold requested/changed | Use the separate legal-hold authorization | Legal hold |
| Retention plan drift/failure | Stop execution and create a new revision | Retention-plan failure |
| Restore target not fresh | Reject before native mutation | Fresh-target cleanup |

## Credential failure

1. Set AWS source admission off; do not stop LocalWorkstation processing.
2. Record the bounded failure category and credential expiry, never the credential value.
3. Verify workload identity, STS endpoint, trust policy, external ID, session duration, and host clock.
4. Re-authenticate or redeploy the workload role. Do not add permissions to the Core Actor Host.
5. Reconcile the DynamoDB operation and outbox before retrying.

## Wrong-account rejection

1. Compare STS account/ARN and configured workload, primary-vault, and recovery-vault allowlists.
2. Stop before S3, DynamoDB, or KMS mutation if any identity differs.
3. Correct deployment configuration through a reviewed change set; do not override the preflight.
4. Re-run read-only identity validation and retain its evidence.

## KMS/key recovery

1. Identify whether encrypt, decrypt, sign, or verify failed and which logical replica is affected.
2. Confirm key ARN, Region, key state, key usage, algorithm, policy, grant, and workload role.
3. Never substitute an AWS-managed key or a key from the other vault.
4. For signing rollover, keep the approved trust-bundle overlap until every retained signature remains verifiable.
5. Re-run exact-version verification before restoring eligibility.

## WAL gap

1. Mark the affected timeline/PITR interval ineligible and alert once per bounded incident.
2. Compare signed WAL records by timeline and segment name; never infer continuity from timestamps alone.
3. Reconcile the host spool, archive checkpoint, exact S3 versions, and source archive status.
4. Publish a missing segment only after its digest, timeline, and source completion evidence pass.
5. Re-run gap/fill/PITR qualification and record achieved RPO.

## Replication failure

1. Preserve the valid primary publication and show recovery as `Pending` or `Failed`.
2. Inspect S3 replication status, RTC metrics, destination ownership, KMS access, retention, and exact version identity.
3. Do not copy objects manually into an eligible prefix or delete/re-upload primary evidence.
4. Reconcile destination versions independently and re-run signed catalog resolution through the recovery role.
5. Escalate if the recovery objective is exceeded; pause new publication when policy requires both replicas.

## Journal PITR

1. Stop AWS admission and dispatch; keep evidence and immutable vault objects untouched.
2. Run the reviewed restore-to-new-table procedure with a unique Development/Staging target.
3. Validate key schema, `WorkQueueIndex`, tags, PITR, TTL/stream parity, checkpoints, leases, and outbox state.
4. Point a disposable qualification host at the restored table and reconcile before any cutover decision.
5. Retain the drill target and alarm for the approved evidence period.

## Multipart reconciliation

1. Resolve the DynamoDB checkpoint by bucket, generated key, upload ID, part count, and uploaded bytes.
2. List exact parts and require SHA-256 for every resumed part.
3. If completion response was lost, accept only one exact verified immutable object version.
4. Abort only an allowlisted stale upload older than policy; never abort published objects or fresh uploads.

## Catalog rebuild

1. Select one logical replica explicitly.
2. Enumerate signed immutable publication records under the generated environment schema.
3. Verify record and manifest signatures, exact versions, KMS key, checksum, retention, and dependencies.
4. Reject duplicate versions, missing sidecars, cycles, partial Scylla protection sets, and WAL gaps.
5. Compare rebuilt entries with the projected catalog and retain signed reconciliation evidence.

## Primary-vault loss and recovery-only restore

1. Block primary credentials/network access and assume only the recovery-read role.
2. Rebuild the recovery catalog independently; do not reuse cached primary catalog state.
3. Select `aws-recovery`, verify recovery ownership/KMS/retention/signatures, and stage exact versions.
4. Restore PostgreSQL or Scylla to an approved fresh isolated target.
5. Run native and application queries, record RPO/RTO, and keep cutover as a separate approval.

## Legal hold

1. Require the legal-hold role and a separately audited authorization reference.
2. Address only one exact bucket/key/version ID from verified catalog evidence.
3. Re-read hold status after the change and write immutable evidence.
4. Releasing a hold does not authorize deletion; a new retention plan is still required.

## Retention-plan failure

1. Stop on plan/policy revision mismatch, missing version, changed checksum/length, legal hold, unexpired retention,
   incomplete replica, dependency, wrong environment, or partial execution.
2. Reconcile completed exact-version actions without retrying the whole plan blindly.
3. Rebuild the catalog, create a new plan revision, obtain independent approval, and revalidate every object.
4. Prove at least one policy-compliant chain remains for every required engine and recovery class.

## Fresh-target cleanup

1. Reject non-empty, production, ambiguous, or non-allowlisted targets before native mutation.
2. Preserve diagnostic evidence for a failed drill until the result is recorded.
3. Delete only the disposable target named by the approved drill plan; never touch the source cluster/database.
4. Confirm the target, temporary credentials, containers, volumes, and network attachments are absent afterward.

## Gate 15 drill acceptance

- Every alert resolves to one section above and carries an operation ID plus bounded failure category.
- UI and Console projections agree on source, phase, replica, lineage, recoverable time, retention, and health.
- Expected disabled/after-hours states emit no repeated first-chance exception noise.
- Retry logging is bounded by the configured poll/backoff interval and one warning per failed attempt.

## Development credential refresh task

The current-user Windows task `IFM-Development-AwsBackupSessionRefresh` runs
`scripts/AwsBackup/Refresh-AwsDevelopmentBackupSession.ps1` after sign-in and every 20 minutes.
Install or update it with `scripts/AwsBackup/Install-AwsDevelopmentBackupSessionRefresh.ps1 -StartNow`.
The task runs hidden with limited privileges and stores no credentials in its definition. The refresh script
writes the temporary role session to the existing protected session file. Check only
`.artifacts/aws-backup-session/status.json` and the scheduled task's last result when diagnosing refresh;
never include the session file in logs or evidence.

This task requires the configured user to be signed in. It does not provide an unattended service identity
while that user is signed out. Installation and successful execution were verified on 2026-10-10;
an actual reboot/sign-in test has not been performed. AWS backup runtime admission remains disabled
until the outstanding qualification gates are complete.

## File-only PostgreSQL restore staging

AWS artifact manifests enumerate files, so staging must also recreate PostgreSQL's standard PGDATA
subdirectories, including empty runtime directories. `LocalDatabaseNativeRestoreArtifactSink` now
creates that layout before downloading files. Required files still undergo exact-length/hash checks
and `pg_verifybackup`; creating empty directories does not substitute for native verification.

The 2026-10-10 running-host restore drill exposed this issue: native verification passed, but the
first isolated startup failed with `could not open directory "pg_notify"`. Keep the failed test result
alongside corrective rerun evidence. Directory definitions follow the
[PostgreSQL 17 database file layout](https://www.postgresql.org/docs/17/storage-file-layout.html).

## Gate 14 read-only requalification � 2026-10-10

The real disposable candidate `3291d2ff9099419aa6c9b3e08ecde95b` and preserved backup
`b4c472227f044f419ac975f29a2463af` passed a fresh signed-catalog and full artifact-hash check
in both AWS vaults. All 32 exact object versions, including catalog entries, were observed.
Primary publication-object replication reports COMPLETED and destination versions report REPLICA.
The 47 deterministic gate tests, warning-free Release build, infrastructure/IAM scan, tracked-file
secret scan and dependency vulnerability audit also passed.

Gate 14 remains incomplete. The candidate retention expires at 2026-11-08T23:37:37.881Z.
Every exact-version legal-hold query returned `NoSuchObjectLockConfiguration`; this observation
is recorded as unknown, not OFF. No AWS backup object was changed or deleted, no executable
retention approval was issued, and runtime admission remains disabled. Fresh observations,
canonical plan signing and independent exact-revision approval are required before deletion.
Detailed evidence is in `.artifacts/aws-qualification-20261010`; the tracked status is
`deploy/aws/database-backup/environments/development/qualification-status-20261010.json`.

## Development admission decision � supersedes the preceding admission restrictions

On 2026-10-10 the user accepted the verified Development backup/restore functionality independently
of Gate 14 retention/deletion qualification. The Development compose overlay enables backup/restore
admission with destructive tests disabled. No legal-hold or retention change is authorized; the
candidate legal-hold-change proposal is withdrawn. Full qualification remains incomplete, and
Staging/Production configuration is unchanged. Refer to
`deploy/aws/database-backup/environments/development/DEVELOPMENT-BACKUP-RESTORE-ACCEPTANCE-20261010.md`.

## Development retention/deletion qualification completed — October 10, 2026

The owner-approved live test passed at 16:12 UTC. The normal signed-plan deletion executor deleted exactly **16 disposable object versions**, eight in each vault (81,192,266 bytes total). Every deleted version was checked absent. The newer backup `b4c472227f044f419ac975f29a2463af` was resolved through its signed catalog and its objects reverified in both vaults. Application backups and development databases were unchanged.

Before deletion, the approved candidate versions had legal hold explicitly set OFF and Governance retention shortened. The executor ran only after that retention expired, with fresh eligibility observations, and did not use a retention-bypass deletion header. This one-time owner exception does not change Production approval rules.

Both live integration tests passed: deletion/preservation and immutable audit publication. The canonical execution plans were KMS signed. Signed audit records were uploaded with 35-day Governance retention and read back successfully. See [execution result and exact AWS evidence references](../../deploy/aws/database-backup/environments/development/gate14-execution-result-20261010.json).

**Cleanup:** detach and delete the four temporary customer-managed policies listed above. Their permissions expire October 11 at 11:35:32 AM Eastern, but removing them completes cleanup.

This qualifies the standalone signed-plan executor in Development. Automatic retention and retention requests through the running backup host are still unwired; this result does not enable those operations or establish full Production qualification.
## Automatic Development retention through the backup host

The owner confirmed the four temporary qualification policies were removed on October 10, 2026.
Normal retention now uses the permanent retention-plan and retention-execution roles, assumed by the
Development backup upload role. It never removes legal holds, shortens Object Lock retention, or
uses Governance bypass.

The Development compose profile enables a daily retention check for `core-postgresql` and
`read-model-scylla`, independently in the primary and recovery vaults. The current age boundary is
35 days. A stable operation/plan ID per UTC scheduling interval prevents duplicate work after restart.

Flow: backup host schedule -> System Admin retention command -> persisted domain authorization
and execution event -> backup host DynamoDB journal -> fully verified catalog and signed immutable
plan -> fresh eligibility checks -> exact-version deletion -> signed audit evidence and service events.
The System Admin command actor must be running. If it is unavailable, the host logs a warning and
retries admission after five minutes; backup/restore processing remains available. Recording the
retention result does not create another execution request.

Retention preserves the newest verified restore point for each engine, its incremental dependencies,
protected versions, and verified retained backups in the other vault. Active backup/restore operations
defer retention. Any interrupted retention operation is marked failed for reconciliation rather than
blindly reexecuted. Partial deletion records the exact completed versions and reports failure.
An empty eligible plan is a successful no-op; no backup is deleted merely to test this schedule.

Configuration: `AutomaticRetentionEnabled`, `AutomaticRetentionInterval`,
`AutomaticRetentionApprovalReference`, `RetentionReadRoleArn`, and `RetentionExecutionRoleArn`
under `DatabaseBackup:Sources:AwsCloud`. Automatic execution is currently enabled only in Development.
Use normal durable host logs, System Admin operation results, DynamoDB checkpoints and immutable
AWS evidence to inspect each run. This runtime wiring does not establish Production qualification.

### Development replacement controller

The Linux native backup host prepares and validates restores and handles retention.
An independent Windows worker handles approved cutover while the API is stopped:
`scripts/AwsBackup/Start-DevelopmentDatabaseCutoverWorker.ps1` (health port 8089).
It uses the same actor execution events, expiration-aware temporary AWS role
credentials and independent DynamoDB execution journal. No credential is stored in
its startup receipt. Its local write-ahead recovery journal lives outside either
active database volume under `E:/IFM/DatabaseBackup/cutover-controller/journal`.

Approved cutover validates the exact operation, restore point, target and validation
revision, prepares cold restored data, stops repository application writers, preserves
the original named volume data in an operation-specific rollback volume, and replaces
data at the existing PostgreSQL or ScyllaDB endpoint. It then starts the database,
runs the API's read-only `--validate-restored-databases-only` provider check, and starts
the normal Development application through `Start-IFMDevelopment.ps1 -NoBuild`.
Completion requires native health, provider validation, application readiness and
cutover state reconciliation from the independent journal.

Failures before admission resumes attempt rollback and validate the preserved original
data with writers stopped. A crash during replacement or an ambiguous admission result
requires reconciliation rather than destructive automatic replay. Rollback volumes
are retained. The worker stop script refuses an unresolved replacement.

Existing local restore artifacts are reused only after exact signed length and SHA-256
verification; conflicting files are not overwritten. AWS transfer and verification
concurrency is bounded to four artifacts, with full verification preserved.

Qualification status remains in `qualification-status-20261010.json`; deployment
readiness and component tests are not a substitute for the pending live round trips.


### Isolated Scylla restore target preparation

A restored backup must replace the target contents, not overlap pre-existing rows.
The native Manager adapter now validates disposable-target permission and distinct
source/target CQL addresses before issuing a schema restore. After schema restore
completes, it clears all non-system base tables on the isolated target and verifies
that each is empty before Manager restores table data. System keyspaces and the
active source database are not truncated.

`ScyllaProtectionSetOptions.CqlContactPoints` names protected source IP addresses.
`ScyllaFreshTargetProfileOptions.CqlContactPoints` names isolated target IP addresses;
`DiscardExistingApplicationData` must explicitly be enabled for that disposable
profile. IPv4-mapped aliases cannot bypass endpoint overlap checks. The CQL driver
restricts query routing to target addresses and rejects discovered nodes outside
that set. Missing configuration fails before schema mutation.

Development uses source 172.30.20.10 and target 172.30.20.11 on the management
network. The qualification additionally verifies that a target-only marker row
is absent after restoration. Prior isolated volumes can be preserved by setting
`IFM_SCYLLA_RESTORE_VALIDATION_VOLUME` to an operation-specific new volume before
recreating the validation container. No active database volume is removed by this.

Five deterministic tests passed for preparation ordering, explicit permission and
source-address overlap rejection. Live native-path verification remains required.
See https://manager.docs.scylladb.com/stable/restore/restore-tables.html for the
requirement to clear existing table contents before loading restored data.

### Native PostgreSQL restore locale prerequisite (2026-10-10)

The Linux backup host must generate en_US.UTF-8 so it can boot the native Development PostgreSQL backup, whose postgresql.conf uses en_US.utf8 for lc_messages, lc_monetary, lc_numeric and lc_time. A missing locale causes isolated pg_ctl startup to fail after backup verification; it does not mean the backup changed the database schema. The host Dockerfile installs locales and runs localedef. Failed isolated staging is retained for diagnosis and reconciled before a new restore attempt. Active database data is unaffected by this isolated startup failure.

### Native PostgreSQL restore prerequisites and verified full-copy reuse (2026-10-10)

The Linux backup host includes the source cluster locale (`en_US.utf8`). A restored PGDATA root is created with mode 0700. Isolated validation uses TCP and disables Unix sockets (`unix_socket_directories=`), because the host root filesystem is read-only; database files remain in the dedicated writable restore mount.

A retry may reuse a complete staged full backup only when both source and staged `backup_manifest` files exist and their SHA-256 digests match. Native `pg_verifybackup` then verifies the staged files and WAL before PostgreSQL starts. Different manifests, incomplete staging, incremental chains and PITR staging require explicit reconciliation. This avoids copying a verified 29 GB dataset again after a boot-environment failure without accepting corrupt or unrelated data.

Verification: 12 PostgreSQL capability tests passed, including valid reuse, manifest mismatch and damaged staged data (`.artifacts/database-recovery/postgres-native-retry-current.trx`). The TCP socket configuration was parsed by the actual PostgreSQL binary. Native host image `sha256:1d5ac2d5ca1157e360fc8a6882360b1b81ee3c8b21a4c796076f9085a7da4cc9` was deployed with readiness Healthy. Live completion and data replacement are separate qualification checks and remain pending.

### Native Scylla Manager artifact manifests (2026-10-10)

Live host qualification exposed two adapter defects: native `sctool backup files` emits `URI | keyspace/table-id`, while the adapter assumed three delimited columns; and a 64 KB diagnostic capture limit silently truncated the machine-readable manifest. The adapter now derives native keyspace/table identity from the SSTable URI, retains bounded full manifest output up to 64 MiB, and rejects overflow instead of parsing partial evidence. Log previews remain separately bounded to 64 KB. Each fresh capture attempt uses a unique Manager task name; incomplete native staging is preserved and reconciled explicitly before retry.

Seven adapter tests passed, including a 6,500-artifact real-format manifest and overflow rejection (`.artifacts/database-recovery/scylla-native-manifest-current.trx`). Host image `sha256:4ed24bd84d66444e641e6801c165cdccd31d13f045d13870c45a433b3f9bb338` is deployed and healthy. The failed native staging remains at `E:/IFM/DatabaseBackup/native/scylla/e6f717a870b140f68397a34c36508a4d.inprogress.manifest-failed-20261010`; it is not accepted as a verified backup. Current host backup/publication qualification is being retried.

### Exact native capture task identity

Snapshot tags are now read from `sctool progress` for the exact completed task, never selected from a global backup listing. A stale tag can otherwise describe a different capture while the task reference describes the current retry. The seven adapter tests now include an intentionally stale global-list tag. Image `sha256:04ef55efd0c145ca0f1aaa920bd970dc77087a54a986a60650eebe4e22bcd046` contains this correction. Qualification operation `e6f717a870b140f68397a34c36508a4d` is preserved but excluded from replacement proof because of that metadata mismatch; corrected full capture operation `b7b0a3ec24c14dd5a00488808042f4a6` is queued through the real host.

### Retention waiting for backup or restore

An admitted retention request can remain queued while backup, restore or cutover work is active. The host resumes it on an idle journal scan. Follow the active recovery operations first. A terminal failed retention operation remains failed; qualify a fresh scheduling slot after correcting its cause rather than treating that failed run as a pass.


### Reused isolated Scylla target: schema preparation order

A live native restore exposed an existing-keyspace collision. On an explicitly disposable target, validate distinct source and target CQL addresses, discard non-system target keyspaces and verify their absence, then run Manager schema restoration followed by table restoration. Keep the protected source endpoints in the options copy when changing restore bucket. Preserve failed staging directories and use distinct Manager task names per attempt. Fourteen native adapter/capability tests pass; live replacement and application recovery subsequently passed, as recorded in the final acceptance below.

### Restored application admission (2026-10-11)

Both current PostgreSQL and Scylla backups have been restored from AWS and activated in the existing Development database containers. Read-only application-provider checks passed for 12 contexts and 242 tables; normal API admission resumed with readiness HTTP 200. Original data remains in operation-specific rollback volumes.

Physical PostgreSQL restoration can invalidate Redis event-stream identifiers. With application writers stopped, recovery removes only cache keys whose prefix is a registered IFM DataCacheName, before provider validation and normal application admission. Unrelated Redis keys are preserved. Reconciliation command identity includes the fencing token so a new attempt cannot reuse an ID with a different payload.

If admission has already resumed, automatic rollback is prohibited. Explicit operator reconciliation may resume from the application-validation stage after verifying the active database and preserved rollback volume. This admission-only path validates current data and resumes services without replacing data again. The prior failure and conditional external-journal requeue remain recorded. Regression verification: 17 tests passed. PostgreSQL automatic retention qualification completed successfully on both vaults; see the final acceptance below.
### Retention display recovery after PostgreSQL restore

The independent AWS journal survives a PostgreSQL restore, but a scheduled retention operation created after the database backup may be missing from the restored System Admin event log. A trusted terminal observation may reconstruct only that missing AWS retention display. It requires a producing host, positive service sequence, positive plan revision, and matching terminal phase/outcome. Existing immutable identities, policy revision, host, service sequence, and plan identity must match for an explicit repair. This emits a retention-result snapshot through the normal event projector; it does not emit an execution request or rerun deletion. A deterministic repair command ID separates it from an earlier rejected observation and makes duplicate repair requests stable.

Live verification recovered operation 78cbadec683da915fcab77434fe15afa using one latest independent observation. Duplicate requests succeeded; the independent execution journal did not change; the application query returned Completed. Twenty-four actor-state tests and one live integration test passed, including rejection of changed host, policy, plan, sequence and terminal outcome.

Retention execution verifies its stored signed plan and identity. An empty plan returns a zero-deletion result without a second catalog download; all checks for plans that actually delete objects remain unchanged. Eleven focused tests passed, including invalid signature and wrong revision rejection.
### Development qualification completed (2026-10-11)

Development AWS backup/restore qualification is complete. The authoritative result is `deploy/aws/database-backup/environments/development/qualification-status-20261010.json`, with `fullyQualified: true` and no remaining qualification items.

| Engine | Current full AWS backup | Restore and activation operation | Result |
| --- | --- | --- | --- |
| PostgreSQL | `6db8c6f638444ecf97b73a7d1278c03f` | `5ef071d14d424424b80d71c2d8c32ac5` | Native isolated restore verified; existing container data replaced; application admission healthy |
| ScyllaDB | `b7b0a3ec24c14dd5a00488808042f4a6` | `c9f77b5907af4883aec093b60130e2df` | Schema, table loading, repair and materialized views verified; existing container data replaced; application admission healthy |

Both full backups were published to the primary and recovery AWS vaults. Read-only application validation passed for 12 provider contexts and 242 tables. The repaired Scylla option-price fixture remained readable after restoration. Original database volumes remain available for rollback; the next isolated Scylla target has a distinct node identity and empty application schema.

Automatic retention completed for both engines in both vaults. PostgreSQL operations `00c6f4736296c754dcb49fb62be8d34d` and `12dfacd9860369429066d7917e5fc87c`, and Scylla operations `78cbadec683da915fcab77434fe15afa` and `e8b40991518343d5de5e3ba6761756b9`, all show Completed at 100% through the application query. These runs retained protected backups and deleted zero objects. Actual deletion was verified separately against the 16 owner-approved disposable versions; its original evidence is preserved. Normal scheduling remains 24 hours with the 35-day age boundary.

The final backup-host image is `sha256:041a3ce031813f9d180ff96f16be6c3d918c0b0a66ae7a483807a02469aad64a`. After deployment, the API, Linux backup host and Windows recovery controller returned HTTP 200; both active databases and the fresh isolated Scylla target were healthy. Evidence is under `.artifacts/database-recovery`, including `final-development-health.json`, the two PostgreSQL automatic-retention operation reports, and the successful cutover receipts. Historical failures and reconciliation receipts remain preserved.

This acceptance covers Development. Staging and Production requirements are unchanged. The preexisting Local Workstation missing-media jobs are outside this AWS qualification.
