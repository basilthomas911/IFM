# Development Gate 4 deployment preparation

This directory contains the non-secret, development-only deployment inputs and IAM policies approved under
`IFM-GATE4-20260822`. The sole owner accepted the development risk on 2026-08-22 because IFM is a one-person company
with no independent security/operations reviewer. This exception does not apply to staging, production, retained-
version deletion, legal-hold removal, or production recovery/cutover.

Development AWS mutation was enabled in `scripts/AwsBackup/gate0-identity-allowlist.json` after the role, policies,
identity, approved-Region access, and denied-Region boundary were verified on 2026-08-22. Staging and Production
remain disabled. Creating these IAM objects is a manual account-bootstrap action; the Gate 4 change-set scripts do
not grant their own permissions.

## Install the CloudFormation execution role

1. In IAM, create a customer-managed policy named `IFM-Gate4-CloudFormationExecution` from
   `cloudformation-execution-policy.json`.
2. Create a role using **Custom trust policy**, paste `cloudformation-execution-role-trust-policy.json`, and name the
   role `IFM-Gate4-CloudFormationExecutionRole`.
3. Attach `IFM-Gate4-CloudFormationExecution` to that role. Gate 15 adds one supplemental execution policy as
   described below because an IAM customer-managed policy is limited to 6,144 non-whitespace characters.
4. Confirm its ARN is
   `arn:aws:iam::107651266250:role/IFM-Gate4-CloudFormationExecutionRole`.

The role trust permits only `cloudformation.amazonaws.com`. Its permission policy is restricted to the two approved
Canadian Regions, deterministic development resources, tagged IFM KMS keys, named IFM roles, and the specific
development budget/audit resources. It cannot bypass Object Lock governance retention, disable/schedule deletion of
KMS keys, administer users, enable Regions, or delete CloudFormation stacks.

## Install the deployer policy

1. In IAM, create a customer-managed policy named `IFM-Gate4-Development-Deployer` from
   `gate4-deployer-policy.json`.
2. Attach it to IAM user `basil.thomas@live.ca`.
3. Keep the existing `IFM-CloudFormation-TemplateValidation` policy; it is harmlessly redundant for template
   validation and may be consolidated later.

The deployer can manage only stacks whose names begin with `ifm-database-backup-development-`, in `ca-central-1` or
`ca-west-1`, and can pass only the dedicated execution role to CloudFormation. Its direct qualification permissions are
limited to IAM simulation for two Development runtime roles, one fixed canary prefix in the two vaults, the exact
primary/audit KMS keys, and read-only immutable CloudTrail evidence. It has no delete, retention-bypass,
replication-change, DynamoDB, Config, Budgets, or key-administration permission.

## Reviewed deployment sequence

The safe values are recorded in `deployment-inputs.json`. After IAM installation is verified, a reviewed commit may
set only Development `awsMutationAuthorized` to `true`. Staging and Production remain deny-all.

Each stack begins as an explicit `CREATE` change set. A change set is reviewed before it is separately executed. The
order is:

1. Recovery vault in `ca-west-1`. The future replication role is authorized through the workload-account root only
   when `aws:PrincipalArn` exactly matches the deterministic replication-role ARN; this avoids a deployment-order
   cycle without broadening access.
2. Primary vault in `ca-central-1`, using the recovery bucket ARN and deployed recovery KMS key ARN.
3. Workload in `ca-central-1`, using the two vault ARNs and deployed primary KMS key ARN.
4. Audit in `ca-central-1`, using the final deterministic vault bucket names.

`New-AwsBackupChangeSet.ps1` requires the approval reference, explicit `CREATE`/`UPDATE` type, execution-role ARN,
allowlisted account/Region, and deterministic stack prefix. It creates a change set but never executes one.
`New-AwsBackupDevelopmentChangeSet.ps1` reads the reviewed inputs and builds the exact parameters for one stack at a
time; primary/workload creation additionally requires the preceding deployed KMS-key output.

Gate 4 completed on 2026-08-22. All four stacks are drift-clean, safe outputs are in `deployed-stack-outputs.json`, and
the live IAM/canary/audit results are in `gate4-live-qualification.json`. The retained canary must not be removed before
its recorded retain-until timestamp.

## Gates 5-10 live qualification policy

The Gates 5-10 implementation uses the workload roles during normal operation. The interactive Development
qualification tests require a temporary, bounded user policy because the deployer policy intentionally contains no
DynamoDB or general vault permissions.

1. In IAM, create a customer-managed policy named `IFM-Gates5-10-LiveQualification` from
   `gate5-10-live-qualification-policy.json`.
2. Attach it to IAM user `basil.thomas@live.ca` only for the Development qualification window.
3. Run the live journal, publication, signing, and recovery tests and capture their immutable evidence identifiers in
   `AWS-Cloud-Backup-Restore-Gates-5-10-Validation-Report.md`.
4. Detach the temporary policy after qualification. The service continues to use the least-privilege workload roles.

The policy cannot delete S3 object versions, bypass or shorten retention, administer KMS keys, mutate recovery-vault
objects, or access staging/production. The live journal test currently stops safely at `dynamodb:DescribeTable` until
this policy is attached; no journal mutation occurs in that denied run.

## Install the Development Gates 11-16 policy updates

The updated qualification file deliberately reuses the existing customer-managed policy object
`IFM-Gates5-10-LiveQualification`; this avoids accumulating overlapping temporary user policies. Its new default
version adds the exact KMS checksum-read permissions, Development CloudWatch namespace/read access, Cost Explorer
read access, and permission to request the separately MFA-gated retention execution role. It still grants no direct
delete, retention bypass, legal-hold mutation, IAM administration, or staging/production access.

Sign in to the AWS console as the account root only for these IAM bootstrap actions:

1. Open **IAM > Policies > IFM-Gates5-10-LiveQualification > Policy versions > Create version**.
2. Paste the complete contents of `gate5-10-live-qualification-policy.json`, create the version, and select
   **Set this version as default**. Do not edit an old version in place.
3. On the policy's **Entities attached** tab, attach it to `basil.thomas@live.ca` for the qualification window if it is
   not already attached.
4. Open **IFM-Gate4-CloudFormationExecution > Policy versions > Create version**.
5. Paste the complete contents of `cloudformation-execution-policy.json`, create it, and set it as default. This base
   policy is 5,960 non-whitespace characters, below IAM's 6,144-character customer-managed-policy limit.
6. Create a customer-managed policy named `IFM-Gate15-CloudFormationExecution` from
   `gate15-cloudformation-execution-policy.json`. Its bounded contents contain only the deterministic
   Development dashboard and alarm-topic permissions.
7. Attach `IFM-Gate15-CloudFormationExecution` to `IFM-Gate4-CloudFormationExecutionRole`. Keep both execution
   policies attached only to that role; never attach either one to the IAM user.
8. If AWS reports the five-version limit while promoting an existing policy, delete only the oldest **non-default** policy version after comparing it with
   the repository history. Never delete the current default version.
9. Sign out of the root session. Continue all qualification commands with the Development IAM user's environment
   credentials.

The base execution-policy promotion widens the deterministic Development alarm name boundary for Gate 15. The
supplemental policy permits CloudFormation to deploy only the deterministic Development dashboard and SNS topic.
Splitting those permissions keeps both documents below IAM's per-policy limit without granting the IAM user those
infrastructure permissions directly.

If this policy was created from an earlier Gate 15 file that omitted `sns:ListTagsForResource`, create and promote a
new default version from the current file. This read-only action lets CloudFormation compare the deterministic topic
tags during drift detection; it does not grant topic access to the IAM user.

After both updated versions are default and the supplemental policy is attached, validate the workload template and
prepare (but do not execute) its reviewed update:

```powershell
aws cloudformation validate-template --region ca-central-1 `
  --template-body file://deploy/aws/database-backup/workload/template.yaml

.\scripts\AwsBackup\New-AwsBackupDevelopmentChangeSet.ps1 `
  -Stack Workload -ChangeSetType UPDATE `
  -PrimaryEncryptionKeyArn 'arn:aws:kms:ca-central-1:107651266250:key/4772d4b1-82d9-49fc-acca-b97e73fe93df' `
  -Confirm
```

Review that the change set adds only the bounded dashboard, alarms, SNS route/subscription, runtime metric permission,
and outputs. Execute it separately only after that review. Confirm the subscription email before running:

```powershell
.\scripts\AwsBackup\Invoke-AwsBackupGate15AlertDrill.ps1
.\scripts\AwsBackup\Invoke-AwsBackupGate15AlertDrill.ps1 -Execute -Confirm
```

For the Gate 5 PITR exercise, first preview and then explicitly execute the retained restore-to-new-table runbook:

```powershell
./scripts/AwsBackup/Invoke-AwsJournalPitrQualification.ps1 `
  -TargetTableName 'ifm-database-backup-journal-development-pitr-YYYYMMDDTHHMMSSZ'

./scripts/AwsBackup/Invoke-AwsJournalPitrQualification.ps1 `
  -TargetTableName 'ifm-database-backup-journal-development-pitr-YYYYMMDDTHHMMSSZ' `
  -Execute -Confirm
```

The restored table is intentionally retained for evidence. The script validates its key schema, `WorkQueueIndex`,
tags, active state, and PITR. Alarm attachment remains a reviewed infrastructure change and must be recorded before
Gate 5 is closed.

## Current Development admission

Development backup/restore admission was accepted separately from retention/deletion on 2026-10-10.
See [Development backup/restore acceptance](DEVELOPMENT-BACKUP-RESTORE-ACCEPTANCE-20261010.md).
A separate, explicitly approved exception now covers only the 16 disposable Gate 14 candidate versions. The scoped Development Gate 14 live deletion qualification passed; Production acceptance remains incomplete;
they do not block on-demand Development backup/restore requests.

## Owner console permissions for the Development deletion test

The owner approved protection changes and permanent deletion for the 16 exact versions in
[the exception approval inventory](gate14-development-exception-approval-20261010.json).
The newer restore-verified backup and application backups are excluded. This is a one-time Development qualification exception.

Use existing AWS console access to grant the named IAM user temporary permissions. No MFA role or root access keys are required for this direct-user path.

1. Sign in to account **107651266250** using existing owner console access.
2. Open **IAM > Policies > Create policy > JSON**. Create four customer-managed policies using the entire corresponding files below. Resolve any console validation errors before saving.
3. Open **IAM > Users > basil.thomas@live.ca > Permissions > Add permissions > Attach policies directly**. Attach those four policies. If the user's managed-policy quota is reached, stop rather than replacing existing policies or granting AdministratorAccess.
4. Return to this task and report that the policies are installed. The qualification runner will first verify permissions, the preserved backup, and the exact candidate inventory. Installation alone does not qualify deletion.
5. After the test, detach and delete only these four temporary policies. They also stop granting permission at **October 11, 2026, 11:35:32 AM Eastern** (15:35:32 UTC).

| Customer-managed policy name | JSON file |
| --- | --- |
| IFM-Gate14-Owner-Primary-Deletion-20261010 | [Primary deletion](gate14-owner-primary-deletion-policy-20261010.json) |
| IFM-Gate14-Owner-Primary-Protection-20261010 | [Primary protection](gate14-owner-primary-protection-policy-20261010.json) |
| IFM-Gate14-Owner-Recovery-Deletion-20261010 | [Recovery deletion](gate14-owner-recovery-deletion-policy-20261010.json) |
| IFM-Gate14-Owner-Recovery-Protection-20261010 | [Recovery protection](gate14-owner-recovery-protection-policy-20261010.json) |

Deletion permissions are restricted by exact bucket, key and version ID. Protection permissions are restricted by the 16 exact keys: AWS does not list version-ID conditions for PutObjectRetention or PutObjectLegalHold. Consequently, those protection permissions can affect another version at the same key while the permission is active; actual changes remain authorized only for the 16 approved version IDs. Legal hold can be set only to OFF by these policies. Both kinds of permission are restricted to this IAM user and the fixed expiration time.

See the [AWS S3 action and condition reference](https://docs.aws.amazon.com/service-authorization/latest/reference/list_s3.html) and [governance retention rules](https://docs.aws.amazon.com/AmazonS3/latest/userguide/object-lock-managing.html).

The existing workload deletion role remains unchanged. Its MFA condition and retention-bypass denial do not apply to these direct-user grants unless a separate explicit deny also applies to the user. If AWS still denies an operation, stop and record the denial; do not weaken bucket protections or change other backups.

The generator `scripts/AwsBackup/New-AwsGate14OwnerExceptionPolicies.ps1` checks the approved inventory, duplicates, the preserved backup exclusion, expiration, and policy size. It generates local files only. It does not attach permissions, remove holds, shorten retention, delete objects, or extend approval automatically. The owner reported the policies installed, and successful live AWS operations verified the required deployed permissions. The live deletion test passed. The running backup host's retention command wiring remains outstanding.
## Development retention/deletion qualification completed — October 10, 2026

The owner-approved live test passed at 16:12 UTC. The normal signed-plan deletion executor deleted exactly **16 disposable object versions**, eight in each vault (81,192,266 bytes total). Every deleted version was checked absent. The newer backup `b4c472227f044f419ac975f29a2463af` was resolved through its signed catalog and its objects reverified in both vaults. Application backups and development databases were unchanged.

Before deletion, the approved candidate versions had legal hold explicitly set OFF and Governance retention shortened. The executor ran only after that retention expired, with fresh eligibility observations, and did not use a retention-bypass deletion header. This one-time owner exception does not change Production approval rules.

Both live integration tests passed: deletion/preservation and immutable audit publication. The canonical execution plans were KMS signed. Signed audit records were uploaded with 35-day Governance retention and read back successfully. See [execution result and exact AWS evidence references](gate14-execution-result-20261010.json).

**Cleanup:** detach and delete the four temporary customer-managed policies listed above. Their permissions expire October 11 at 11:35:32 AM Eastern, but removing them completes cleanup.

This qualifies the standalone signed-plan executor in Development. Automatic retention and retention requests through the running backup host are still unwired; this result does not enable those operations or establish full Production qualification.
## Automatic retention host wiring — October 10, 2026

The owner reported the four temporary qualification policies removed. The permanent workload IAM
update is deployed (`ifm-backup-development-20261010123359`, `UPDATE_COMPLETE`). The upload role
can assume the retention-plan and retention-execution roles; both role chains were checked live.

The Development backup host now requests retention through the System Admin command actor,
then processes its persisted execution event using the AWS journal. The compose profile enables
one daily check for each configured PostgreSQL/Scylla protection set in each vault, with a 35-day
age boundary. The newest backup and required incremental dependencies are retained. Exact-version
Object Lock and legal holds still apply; there is no automatic protection bypass or shortening.

Signed plans, deletion/failure evidence, operation checkpoints and service results support inspection.
Failed/interrupted deletion is terminal and requires reconciliation rather than blind retry.
The System Admin API actor must be running to admit new scheduled operations. An unavailable API
causes a logged five-minute admission retry, while backup/restore processing remains available.

See the [operations runbook](../../../../../TomasAI.IFM.Application.Storage.Backup/Docs/AWS-Cloud-Backup-Restore-Operations-Runbook.md#automatic-development-retention-through-the-backup-host).
## Development option tick price repair ? October 10, 2026

The current `market_data_test_db.futures_option_tick_data` schema still declared
`optionprice`, `bidprice`, `askprice`, and `underlyingprice` as `decimal`, although
commit `fd9ce01d0` (September 27) changed the application CREATE TABLE definitions
to `double`. CREATE TABLE IF NOT EXISTS did not migrate the existing table.
This was an existing source-database issue, not a backup/restore schema change.

The single affected integration fixture (`ES20180511P2525`, 2025-02-20, tick
123456) contained IEEE double bytes under decimal columns. With the API stopped,
`scripts/DatabaseRecovery/repair-development-option-tick-prices.py --apply`
archived all original row fields (prices as raw blobs), verified the archive,
and rebuilt only this table using double price columns. The four values now
read as 100.5, 100.0, 101.0, and 150.0 respectively.

Preserved recovery evidence:

- Raw original row: `market_data_test_db.futures_option_tick_data_legacy_20261010`.
- Original native snapshot tag: `ifm_option_tick_decimal_repair_20261010`.
- Local execution evidence: `.artifacts/database-recovery/option-tick-price-repair.log`.
- Original row SHA-256: `d92c39900bfd0511add4bae50d7edc7ba7c2318a49110e5cbdea7c49aa204f61`.
- Repaired row SHA-256: `cc2e4051514f070b741f5fd6cedbc11b5144dd8b763441f5226b9fa3cf235f42`.

Verification: four decoder tests passed; repeat inspection reports already
repaired. The application's read-only provider validation passed across all
12 schema contexts (242 table probes). This is a readability check, not proof
of completeness of every database row.

A post-repair native snapshot (`ifm_option_tick_repaired_schema_20261010`)
was also taken; its saved `schema.cql` declares all four prices as double.
New native backups capture the corrected schema and data. The Scylla restore
implementation restores schema before restoring table data. Older backups
remain unchanged and can reintroduce the old decimal schema if restored; run
the application-provider health validation before returning a restore to use.
The corrected table has not yet completed a new AWS backup/isolated-restore
round trip; this repair does not claim that outstanding qualification is done.


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
source/target CQL addresses before any target mutation. It drops all non-system
keyspaces on that approved isolated target and verifies their absence before Manager
restores schema and then table data. Existing keyspaces otherwise block Manager schema
restoration. System keyspaces and the active source database remain untouched.

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

### Current repaired Scylla AWS vault round trip � passed (2026-10-10)

The live integration test completed successfully for restore point `c013a4d1d681447aaf539c6ece6b95bd`. It verified the primary vault and then restored from the recovery vault alone to a new isolated named volume. Native schema, table, repair and view restoration completed, and the repaired option tick returned `OptionPrice=100.5`, `BidPrice=100`, `AskPrice=101`, and `UnderlyingPrice=150` with double columns. Evidence: `.artifacts/aws-qualification-20261010/current-scylla-aws-completion.trx` and `.artifacts/database-recovery/scylla-recovery-repaired-row.txt`.

This completes the standalone current-data AWS round trip. The backup-host native layout and actual Development replacement remain separate qualification steps; this result does not declare them complete.

### Native restore manifest relocation correction

The normal host restore exposed a manifest comparison issue when portable Scylla objects were imported from `ifm-development` into `ifm-development-restore`: unchanged object keys acquired a different bucket prefix. Native verification failed before any target table preparation. The adapter now rebases only that configured bucket prefix for comparison with the captured manifest; all object keys, metadata, schema entries, local manifest hashes and topology identities remain checked. Ten adapter tests pass, including bucket relocation, changed-key rejection and unexpected-bucket rejection. The subsequent live corrective restore and container replacement passed; historical failure evidence is retained.

The corrective live attempt passed manifest verification and then exposed a second adapter omission: the restore-location options copy did not carry the source CQL contact points. The safety check rejected it before schema or data modification. Source addresses are now retained; 14 native adapter/capability tests pass, including overlap rejection. The empty failed staging directory was preserved under `.source-endpoint-failed-20261010` before corrective retry.

### Current database activation qualification (October 10–11)

The corrected native Scylla restore completed all table loading, post-restore repair and materialized-view recreation. Its option fixture has prices 100.5 / 100 / 101 / 150 and the four price columns are double. The target-only marker is absent. Evidence: `.artifacts/database-recovery/scylla-current-native-restore-ready.json` and the adjacent schema/price/marker reports.

The first Windows cutover attempt failed before data replacement because it opened protected Windows process handles before filtering IFM process names. The original Scylla container was never stopped and its rollback volume was empty. The failure and reconciliation receipts are preserved. The corrected controller and explicit same-definition reapproval are tested; active-data ambiguity still blocks automatic replacement. The corrected retry completed successfully. Do not infer full qualification until `qualification-status-20261010.json` records successful activation and application validation for both engines and the remaining automatic-retention proof.
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
