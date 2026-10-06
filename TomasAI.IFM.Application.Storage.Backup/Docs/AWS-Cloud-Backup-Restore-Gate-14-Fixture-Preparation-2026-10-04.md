# Gate 14 disposable fixture preparation — 2026-10-04

## Completed

Prepared two retention-only synthetic JSON objects in a dedicated Development prefix:
`v1/environment/development/gate14/disposable/014a9d09a13d4621affbf2695173093c/`.
The candidate is 165 bytes; the newer keep object is 160 bytes. Neither contains application data or constitutes a database backup/verified restore point. S3 accepted both with KMS encryption and exact version IDs. Primary HEAD observations report `COMPLETED` replication and Governance retention through November 8, 2026, at 19:50:25.661 UTC (candidate) and 19:50:28.204 UTC (keep), approximately 2:50 p.m. Eastern after daylight-saving time ends.

The existing 35-day bucket retention policy, legal holds, IAM permissions, old qualification evidence, and recovery catalog were not modified. These new objects intentionally cannot be deleted during their retention period.

## Prepared review materials

- `deploy/aws/database-backup/environments/development/gate14-disposable-fixture-20261004.json`: exact primary keys, versions, local payload hashes, lengths, deadlines, and verification status.
- `gate14-fixture-readback-policy-20261004.json` in that directory: proposed read-only S3 access to the four exact fixture keys and S3-constrained decrypt access to the two vault keys. It includes no publication, deletion, retention-bypass, or IAM administration actions. Encryption context may be the bucket ARN when S3 Bucket Keys are active; S3 read scope remains limited to the fixture keys. Review before temporary attachment to the approved qualification identity/role; remove after qualification.
- `gate14-deletion-plan-draft-20261004.json`: explicitly non-executable draft with no signature, approval, or invented policy revision. The keep object and all earlier evidence remain protected.
- `scripts/AwsBackup/New-AwsGate14DisposableFixture.ps1`: creates a new local fixture bundle by default; `-Publish` uploads two new uniquely named objects without overwriting or weakening retention. Running it again creates a different fixture and begins another 35-day retention window.
- `scripts/AwsBackup/Test-AwsGate14DisposableFixture.ps1`: exact-version readback in both vaults, hash/length validation, and observation capture. It performs no deletion or authorization.

## Verification completed and remaining prerequisites

Exact-version readback succeeded October 4, 2026, at 22:40:01 UTC (6:40 p.m. Eastern). Both objects downloaded from both vaults; all four lengths and SHA-256 hashes matched. Evidence: `.artifacts/aws-gate14-fixture/014a9d09a13d4621affbf2695173093c/verified-observations.json`. Observations are also copied into the tracked fixture descriptor. The user attached IAM readback permissions and edited the recovery KMS policy; the verification script changed no AWS permissions.

Subsequent exact-version GetObjectLegalHold checks returned NoSuchObjectLockConfiguration for all four versions. This is not recorded as explicit OFF; legalHoldVerified remains false. No legal-hold state was changed.

AwsRetentionPolicy.Create requires a non-empty verified database restore-point catalog and protects the newest point per engine plus dependencies. These synthetic fixtures are not database backups and cannot be fabricated into that catalog. Canonical planning remains blocked pending an approved fixture qualification path or a designated eligible non-evidence database restore point. Signing and independent approval remain outstanding.

Candidate retention ends November 8, 2026, at 19:50:25.661 UTC (2:50:25.661 p.m. Eastern). No retention bypass or deletion is authorized. Gate 14 remains incomplete.

The draft is not accepted by the constrained executor. After the actual retain-until deadline, reobserve all exact identities, checksum/length, legal hold, retention and replica state. Reconcile the fixture with the existing verified catalog and newest/dependency policy; do not label a synthetic object as an independently recoverable database backup. Only then produce the canonical signed plan through the existing `AwsRetentionPlanAuthorizationService`, obtain independent approval of its exact revision, and execute through the MFA-gated `AwsExactVersionDeletionExecutor`. Keep the newer object and all retained qualification evidence.

The fixture alone prepares the retention-deletion drill; it does not replace database recovery qualification or complete Gate 14. No deletion was attempted.

## Real-backup qualification preflight - October 4, 2026

The supported continuation is a dedicated non-evidence database backup and a newer independently recoverable backup, published and restore-tested through the existing signed catalog path. Do not repurpose the retained Gate 11 snapshot as a deletion candidate or represent the synthetic JSON objects as database restore points. Record the new operations as explicitly non-evidence retention-drill candidates and preserve their restore-test evidence separately.

The focused Gate 14 unit suite passed: 13 passed, zero failed, zero skipped. The local Docker inventory has no Gate 11 isolated source/restore containers, so the historical multi-node live test cannot simply be rerun as a ready disposable environment.

AWS signing preflight failed: kms:DescribeKey was denied for CLI user basil.thomas@live.ca on signing key arn:aws:kms:ca-central-1:107651266250:key/2edd60e5-be19-483d-b4df-88df45aa2fb2 because no identity-based policy permits it. This confirms missing metadata access, not a tested denial of kms:Sign. The existing Gates 11-16 report records that IFM-Gates5-10-LiveQualification was detached after the earlier drills. Its reviewed source is deploy/aws/database-backup/environments/development/gate5-10-live-qualification-policy.json (supplemental permissions remain separately scoped).

Before publication, restore the bounded qualification access required by the selected live workflow and rerun signing/source preflight. No policy was attached automatically. No new database backup, catalog entry, signature, approval, retention modification, or deletion was produced during this preflight. A newly published backup will inherit the current retention window; its exact expiry must be observed rather than borrowed from the synthetic fixture.

## Signing access restored - October 4, 2026

After the user reattached IFM-Gates5-10-LiveQualification, the explicitly enabled live signing test passed. Evidence operation b5c765cdaf0840d9b53b76b7c91cdb36 used signing key 2edd60e5-be19-483d-b4df-88df45aa2fb2 and exported public-key SHA-256 705F75DDB510986F565B97B9329996EF6B9786E27CBE21E0FE796301908107CB. KMS signing, online verification, public-key export, offline verification, and tampered-document rejection passed.

Correction to the earlier preflight: DescribeKey remains denied on the signing key, but it is not required by this signing implementation. Do not request additional DescribeKey permissions merely to satisfy that diagnostic check. Signing access is no longer a blocker. This test signs a qualification document, not the deletion plan; the draft remains unsigned and unapproved. Real non-evidence database backup creation and restore qualification remain outstanding, as do retention expiry and exact-version deletion approval.

## Continuation review - October 4, 2026

A fresh read-only GetObjectLegalHold check against all four exact fixture versions again returned NoSuchObjectLockConfiguration. The verification remains explicitly unknown rather than OFF; no legal hold or retention was modified.

The next actionable prerequisite is a supported real-backup qualification environment: create a dedicated disposable database source, publish a non-evidence backup and a newer backup through the signed catalog workflow, and independently restore-test the retained backup. The synthetic candidate expiry on November 8 does not make it eligible for the existing database restore-point planner. A new real backup inherits its own observed retention deadline. Alternatively, an explicitly reviewed fixture-only qualification path would require a separate implementation and approval; no policy guard is bypassed here.

Readback and signing access have passed. Canonical deletion-plan creation is blocked by the missing qualified non-evidence database restore-point pair and unresolved legal-hold observations. The plan remains unsigned and unapproved, and no deletion was attempted. Earlier statements that waiting until November 8 alone permits completion are superseded by this prerequisite review.

## Real non-evidence backup pair completed - October 4, 2026

Live qualification run `051f030cd7ab4e3c88a7913ad6928127` created an isolated PostgreSQL 17.2 source with known data, captured two genuine native full backups using pg_basebackup tar format with streamed WAL, and published them through S3DatabaseBackupPublicationCapability and the existing KMS-signed catalog workflow. No application database was modified.

- Candidate restore point: `3291d2ff9099419aa6c9b3e08ecde95b`.
- Newer keep restore point: `b4c472227f044f419ac975f29a2463af`.
- Protection set: `postgresql-gate14-disposable-051f030cd7ab4e3c88a7913ad6928127`.
- Each backup contains 40,586,585 native artifact bytes. The source is test data, not a backup of IFM production/application records.
- Both restore points were staged from both AWS vaults through exact-version, hash-checked restore preparation and restored into four fresh PostgreSQL targets. Recovery staging used the configured recovery-read role.
- Candidate validation: 100 rows, sum 6312.50; keep validation: 101 rows, sum 6438.75. All four checks passed.
- Live test: one passed, zero failed, approximately 90 seconds. The test-created containers and anonymous volumes were removed; AWS backups and local evidence were retained.
- Primary artifacts, manifests and publication documents (seven versions per point) have observed Governance retention until November 8, 2026, 23:37:37.881 UTC (candidate; 6:37:37.881 p.m. Eastern) and 23:38:22.601 UTC (keep). Complete catalog-object and replica eligibility must still be reobserved before execution.

Tracked evidence: `deploy/aws/database-backup/environments/development/gate14-real-backup-qualification-20261004.json`. Detailed local evidence: `.artifacts/aws-gate14-real-backup/051f030cd7ab4e3c88a7913ad6928127/`; test report: `.artifacts/aws-gate14-real-backup/test-results/gate14-real-backup.trx`. Re-running the live test produces another pair with a new retention deadline; it is not a read-only verification command.

This supersedes the missing-real-backup-pair blocker above. The older synthetic-fixture draft remains non-executable and must not be relabeled as this real backup plan. Legal-hold and complete catalog eligibility observations, retention expiry, canonical plan signing, independent approval and constrained deletion remain outstanding. Gate 14 remains incomplete; no deletion was attempted or authorized.
