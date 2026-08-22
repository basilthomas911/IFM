# AWS Cloud Backup and Restore Gate 4 Development Approval

**Approval reference:** `IFM-GATE4-20260822`

**Date:** 2026-08-22

**Environment:** Development only

**AWS account:** `107651266250`

**Owner/approver:** Basil Thomas, sole repository and company owner

## Decision

The owner explicitly authorizes preparation and eventual creation of the Gate 4 development infrastructure defined
by the reviewed CloudFormation templates. IFM is a one-person company and therefore cannot provide an organizationally
independent security or operations reviewer for development. The owner accepts this separation-of-duties limitation
for Development under this recorded exception.

This exception is not approval to deploy arbitrary AWS resources. It is bounded to:

- stacks prefixed `ifm-database-backup-development-`;
- account `107651266250`;
- Regions `ca-central-1` and `ca-west-1`;
- the four Gate 4 workload, primary-vault, recovery-vault, and audit templates;
- the dedicated `IFM-Gate4-CloudFormationExecutionRole`; and
- a monthly development budget of USD 70 (a conservative operational substitute for the approved CAD 100 target,
  because AWS Budgets accepts USD only) with notification to the approved owner address.

## Compensating controls

1. Staging and Production remain empty-account, mutation-disabled environments.
2. The IAM user receives CloudFormation/change-set permissions and may pass only the dedicated execution role; it
   receives no direct data-service permissions from the deployer policy.
3. The execution role is action-limited and resource/prefix/Region constrained. It cannot bypass governance
   retention, disable or schedule deletion of KMS keys, administer IAM users, enable Regions, or delete stacks.
4. Change-set creation and execution remain separate. Every change set must be inspected before execution.
5. Object Lock, KMS, deletion protection, retained resources, CloudTrail, Config, alarms, and budget controls remain
   mandatory.
6. Live negative-access tests, drift detection, and disposable replication/retention/audit proof are required before
   Gate 4 can be marked complete.

## Explicit exclusions

This approval does not apply to staging, production, legal-hold removal, retained-version deletion, production restore,
or production cutover. Those operations retain their independent approval requirements. No AWS infrastructure was
created when this approval record was written.
