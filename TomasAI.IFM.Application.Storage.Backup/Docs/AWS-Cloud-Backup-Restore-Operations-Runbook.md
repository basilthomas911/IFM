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
