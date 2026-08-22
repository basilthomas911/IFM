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
3. Attach only `IFM-Gate4-CloudFormationExecution` to that role.
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
