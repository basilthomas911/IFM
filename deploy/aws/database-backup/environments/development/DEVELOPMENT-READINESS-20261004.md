# Development AWS backup readiness - October 4, 2026

## Current decision

Full Development qualification remains pending Gate 14. This report does not authorize deletion or enable runtime AWS request admission. Gates 17 and 18 address Staging and Production; they are not required to claim Development qualification. Historical Development Gates 0-13 and 15-16 evidence is preserved, not replaced by the current checks.

## Checks completed now

- Full AWS unit suite: 90 passed, zero failed.
- Non-live AWS integration selection: 11 passed, zero failed. Live categories were excluded; these results do not claim additional live AWS coverage.
- Gates 11-16 qualification script: Passed. Infrastructure/IAM scan, tracked-source credential scan, warning-free Release build, 47 deterministic gate tests, and direct/transitive dependency vulnerability audit all passed.
- Release build: zero warnings, zero errors.
- Real non-evidence PostgreSQL backup pair: signed immutable publication and four fresh-target restores passed. Evidence: gate14-real-backup-qualification-20261004.json in this directory.
- Download verification and KMS signing checks passed. No IAM or key policies were changed by this continuation.

Detailed test results are retained under .artifacts/aws-development-qualification and .artifacts/aws-gate14-real-backup. The qualification script performed no AWS mutations.

## Prepared runtime configuration

appsettings.AwsDevelopment.prepared.json contains the deployed Development account, vault, journal, role and key references. It intentionally has Enabled, AcceptBackupRequests, LiveAwsTestsEnabled and DestructiveTestsEnabled false, and is not loaded by the host automatically. It contains no credentials.

Before activation, finish Gate 14, select and preflight a least-privilege host credential source, explicitly merge this profile into the host configuration, and qualify host startup/journal access and an actual Development backup request through the normal command path. Native PostgreSQL/Scylla source configuration, persistent workspace/journal settings and source credentials must be supplied by the existing host deployment. CLI qualification success does not establish that the running Docker host has credentials or AWS admission enabled. Do not mount root credentials or reuse the temporary broad qualification policy as a permanent runtime identity.

## Blocking exit criteria

1. Gate 14 candidate restore point 3291d2ff9099419aa6c9b3e08ecde95b remains under retention. Observed primary artifacts/manifests/publication deadline is November 8, 2026, 23:37:37.881 UTC (6:37 p.m. Eastern). Reobserve the complete signed catalog object set and both replicas; do not assume this is the latest deadline across all catalog documents.
2. Resolve legal-hold observations and require exact current version/length/hash/retention and replica evidence before execution.
3. Establish the reviewed retention policy revision, generate and sign the canonical exact-version plan, and obtain independent authorization bound to its revision and object tuples. The existing sole-owner Development bootstrap exception explicitly excludes retained-version deletion.
4. Execute only after lawful expiry through the constrained deletion workflow, retain reconciliation evidence, and prove the newer keep restore point and all prior qualification evidence remain readable.
5. Complete runtime identity/admission and host command-path qualification before representing the installed host as operational AWS backup.

No retention was shortened, no governance bypass or legal-hold change was performed, and no AWS backup was deleted. The current system cannot truthfully be labeled fully qualified today. Backup publication and restore checks are successful; the retention-deletion exit criterion requires future expiry and approval.

## Additional live qualification - October 4, 2026

The explicitly enabled SDK identity and DynamoDB journal live selection passed three tests, zero failed. It verified allowlisted account/Region identity, durable admission, exact duplicates, exclusive leases, fencing, terminal checkpoint/outbox acknowledgement, and restart recovery. Test operation IDs: f5a37e4e40b54d259072a58adc0e6c56 (contract) and 8e1db20df1e4408685c6b4472a1a065d (restart). Records are isolated qualification operations retained in the Development journal; no journal table or backup was deleted.

The host startup-order test passed: journal initialization, native capability validation and AWS identity validation precede outbox, reconciliation, dispatcher and listener startup. Evidence: .artifacts/aws-development-qualification/live-identity-journal.trx and host-startup-order.trx. The existing Docker backup host is running and healthy, but that is its existing local profile, not proof of AWS request acceptance.

These results establish the workstation SDK identity and actual deployed journal contract. They do not establish a permanent least-privilege Docker runtime credential source, native command-to-AWS backup completion, or live retention deletion. Full qualification remains pending; the future retention deadline and independent approval requirement cannot be satisfied by further local tests. The Development bootstrap exception in README.md does not cover retained-version deletion, and the operations runbook requires an immutable plan ID, exact revision and independent approval.

## Development backup-and-restore activation preparation

The user authorized Development backup/restore operation with retention cleanup deferred and automatic deletion disabled. This is an operational Development scope, not full Gate 14 qualification. Prepared Docker override: Docker/DatabaseBackup/docker-compose.aws-development.yml. Compose validation passed without starting services. AWS protection-set mappings were aligned to the native container profile (core-postgresql and read-model-scylla); a persistent WAL spool mount and read-only role-session credential directory are explicit.

The runtime upload role preflight failed with AccessDenied for sts:AssumeRole from basil.thomas@live.ca. Live iam:GetRole is also denied, so the current trust policy could not be inspected. The repository role template trusts ECS tasks. Three prepared files provide a workstation trust statement (append without replacing existing trust), a caller assume-role policy, and a runtime supplement for primary version inventory/legal hold/readback plus recovery-role assumption. They grant no S3 object deletion, governance bypass or KMS/IAM administration. No IAM policy or role trust was changed automatically.

Required console setup: IAM role ifm-database-backup-upload-development Trust relationships receives workstation-upload-trust-statement.json; IAM user basil.thomas@live.ca receives workstation-assume-upload-policy.json; the upload role receives workstation-upload-runtime-supplement.json. These are Development-only additions; future CloudFormation updates must reconcile this change to prevent overwriting the trust/supplement. After installation, rerun upload-role preflight, then establish refreshed temporary role-session credentials, test native command-path backup/restore, and activate the override. Existing host remains unchanged until then. Never copy root credentials into the container.

## Runtime activation preflight - October 4, 2026

The scoped upload role now assumes successfully, can describe the Development journal and list primary versions. The host uses an expiration-aware credential_process profile with only temporary upload-role credentials in a user/SYSTEM-restricted local directory mounted read-only. Refresh-AwsDevelopmentBackupSession.ps1 refreshes that session every twenty minutes in a hidden background process. Long-term workstation/root keys are not mounted. The background helper is currently session-scoped; reboot persistence has not yet been installed.

The running Docker host accepted its upload-role identity. A domain RequestDatabaseBackupCommand was translated to the normal execution event and published through NATS; operation 57e0e1e29f2a4c779ba9d8538bcec52a reached native PostgreSQL capture, which failed before AWS publication. Read-only IDENTIFY_SYSTEM diagnosis found the exact cause: no pg_hba.conf entry for replication connection from host 172.25.0.1 as postgres. Password-authenticated normal SQL connectivity works. The configured database ifm_db does not exist, and the backup connection was corrected to postgres for native metadata checks.

Automatic approval review rejected both an unnecessary duplicate full diagnostic backup (storage/service impact) and the persistent PostgreSQL authentication change (not explicitly authorized as a security setting). No diagnostic backup or authentication modification was performed. Proposed narrowly scoped password-authenticated rule is in postgresql-backup-host-replication.conf. Explicit user approval is pending. AWS admission is disabled again until native backup/restore validation passes; role identity/profile remains configured and existing durable operation evidence is retained. No completed-host-backup claim is made.

## Approved replication access and resumed capture

The user explicitly approved the exact rule `host replication postgres 172.25.0.1/32 scram-sha-256`. It is installed in the running PostgreSQL container configuration; the original configuration remains at `/tmp/pgdata/pg_hba.conf.before-aws-development-20261004`. PostgreSQL reloaded successfully, pg_hba_file_rules reports the exact address and scram-sha-256 without a parsing error, and the backup host's read-only IDENTIFY_SYSTEM replication preflight succeeds.

The existing operation 57e0e1e29f2a4c779ba9d8538bcec52a resumed after the host restart. pg_stat_progress_basebackup now reports streaming database files with backup_total=28964442624 bytes. This is an in-progress native backup, not yet a successful AWS publication or restore qualification. No duplicate backup was requested. New AWS request admission and destructive tests remain disabled while capture, publication and fresh-target restore verification are pending. The rule resides in the existing database volume; recreating PostgreSQL with a new volume requires reconciling this approved setting.

## Running native backup result and AWS publication blocker

Operation 57e0e1e29f2a4c779ba9d8538bcec52a finished its approximately 29 GB PostgreSQL capture. pg_verifybackup ran successfully, the native backup moved from its staging directory to its final operation directory, and the DynamoDB journal advanced to phase 9 (Verifying). This establishes native capture and verification, not completed AWS publication.

S3 contains 839 completed artifacts for this operation. The first multipart artifact has 29 stored parts totaling 236576768 bytes. The host reported AmazonS3Exception: the SHA-256 multipart completion request omitted each part's checksum. The host is degraded and schedules retries; its durable operation remains nonterminal. The running image was created 2026-08-22, whereas current repository multipart code explicitly sets PartETag.ChecksumSHA256. Deployment and serialized-request verification must be reconciled before resuming. Current repository publication also rejects already-existing immutable keys, so retries must safely reconcile the 839 existing exact versions rather than overwrite or delete them. No backup artifacts, multipart uploads or retention settings were deleted/changed.

During monitoring, the credential refresher's Windows PowerShell ACL and null-argument compatibility issues were corrected. Temporary role refresh succeeded and then refreshed again in the hidden continuous process. Folder access remains limited to the current user and SYSTEM. Native backup and existing AWS evidence are preserved; new AWS admission remains disabled. Exact observation summary: .artifacts/aws-development-qualification/running-host/publication-blocker.json.

## Running-host primary AWS backup publication completed

The existing operation and restore point 57e0e1e29f2a4c779ba9d8538bcec52a completed through the rebuilt running host. DynamoDB reports phase 14 (Completed), terminal=true. The primary vault contains 6,836 native artifacts totaling 28,979,076,300 bytes, plus engine manifest/signature and publication/signature documents; the final catalog entry exists. Exact publication version 8.zy4wgA.HbvqCGB144BuB7lDOy8YdmT matches the catalog-bound SHA-256 digest. Both stored publication and engine-manifest signatures verified successfully against the allowlisted KMS signing key. The engine manifest digest matches the signed publication record. The host's publication path completed all exact-version object read-back checks before writing the catalog.

The repair deployed current per-part SHA-256 completion serialization, added verified exact-version artifact reuse on retry, and recovered the original publication retention clock from its first immutable artifact. Retried artifacts retain their original deadlines; no S3 version, multipart upload, legal hold or retention setting was deleted, overwritten, shortened or otherwise changed. Development signed-document size is bounded at 16 MiB for the large file set. The full AWS backup unit suite passed 94 tests; the strengthened same-size changed-content check also passed in the 12-test publication selection. Current running image is sha256:ed527ff3e89c6e193043855eb9d228df8800180e9a7dfe7a0d38b5c165799dfb.

Evidence: .artifacts/aws-development-qualification/running-host/publication-verified.json, catalog-entry.json, publication.json, engine-manifest.json and downloaded signature envelopes. This establishes successful native capture, verification and primary AWS publication for the actual running-host backup. An isolated restore of this 29 GB restore point is the next gate; the earlier four isolated fixture restores do not substitute for it. Recovery-vault publication/readback for this point has not been claimed. New AWS backup request admission and automatic deletion remain disabled pending the subsequent Development activation checks. Full Gate 14 deletion qualification remains deferred.
