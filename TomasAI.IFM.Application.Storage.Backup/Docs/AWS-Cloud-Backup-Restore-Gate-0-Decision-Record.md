# AWS Cloud Backup and Restore Gate 0 Decision Record

**Status:** Accepted for the implementation baseline

**Date:** 2026-08-21

**Decision authority:** Repository owner directive to complete Gate 0

**Production authorization:** Not granted by this record

## 1. Scope of acceptance

This record closes the architecture and policy choices required to begin local AWS adapter implementation. It does not
authorize creating, changing, or deleting an AWS resource. Development identity discovery is read-only. Staging and
production have no allowlisted AWS account and therefore fail closed until separately approved account IDs and roles
are supplied.

## 2. ADR-001: Infrastructure as code

**Decision:** Use native AWS CloudFormation YAML, deployed through reviewed change sets.

**Reasons:**

- the repository does not currently contain a Terraform, CDK, or Pulumi baseline;
- CloudFormation does not add a separate state backend or another application runtime;
- the installed AWS CLI can validate templates and create change sets;
- cross-account policies, KMS policies, Object Lock, replication, and DynamoDB are representable directly; and
- generated plans remain inspectable without running application code.

Reusable nested stacks are permitted. Direct `create-stack` or `update-stack` from a developer shell is prohibited for
staging and production. Gate 4 must add linting, policy-as-code, change-set review, drift detection, and rollback tests.

## 3. ADR-002: Accounts, Regions, identity, and authorization

**Decision:** Use `ca-central-1` as the primary Region and `ca-west-1` as the recovery Region. Both S3, DynamoDB, and KMS
publish regional endpoints in these Regions. This keeps the initial design within Canada while preserving a distinct
regional failure boundary.

| Environment | Workload account | Primary-vault account | Recovery-vault account | Authorized Regions | Mutation authority at Gate 0 |
| --- | --- | --- | --- | --- | --- |
| Development | `107651266250` | Consolidated with workload for development only | Consolidated with workload for development only | `ca-central-1`, `ca-west-1` | None |
| Staging | Deny all; not yet assigned | Deny all; must be distinct in the production-shaped topology | Deny all; must be distinct | `ca-central-1`, `ca-west-1` | None |
| Production | Deny all; not yet assigned | Deny all; must be a dedicated backup account | Deny all; must be a separate recovery account | `ca-central-1`, `ca-west-1` | None |

The read-only development identity discovered through STS is
`arn:aws:iam::107651266250:user/basil.thomas@live.ca`. It is acceptable only for Gate 0 discovery and controlled
development bootstrap. Its long-lived user credential shape is not accepted for staging or production. Production
uses temporary role sessions and three separate account trust domains.

The executable policy is `scripts/AwsBackup/gate0-identity-allowlist.json`. Empty staging/production account lists are
intentional deny-all values, not unfinished implicit defaults. Adding an account or setting mutation authorization
requires a reviewed change with the named environment approvers.

### Change approvers

| Change | Minimum approval before execution |
| --- | --- |
| Development read-only identity/Region policy | Repository owner |
| Development resource creation/change | Repository owner plus implementation-plan review |
| Staging account/role allowlist or infrastructure change set | Repository owner and security/operations reviewer |
| Production account/role allowlist or infrastructure change set | Repository owner, security, operations, and database owner |
| Production restore | Incident commander, database owner, and business owner |
| Production cutover | Separate application/business approval after `ReadyForCutover` |
| Legal-hold removal or retained-version deletion | Legal/security owner plus independent operations executor |

For a sole-owner company, Development resource creation may use a documented owner self-approval exception when an
independent reviewer does not exist. The exception must identify the exact account, Regions, stack prefix, templates,
budget, and approval reference, retain separate change-set review/execution steps, and preserve all technical controls.
It never extends to staging, production, legal-hold removal, retained-version deletion, production restore, or cutover.

## 4. ADR-003: Recovery objectives

These are engineering design targets, not demonstrated promises. Gates 10, 12, 13, 17, and 18 must replace target
values with measured results. A target that is not met causes policy review; results are never rounded down.

| Recovery class | Recovery point objective | Primary-vault recovery time target | Recovery-vault recovery time target |
| --- | ---: | ---: | ---: |
| PostgreSQL cluster and SystemAdmin projections | 5 minutes through continuous WAL | 4 hours | 8 hours |
| ScyllaDB declared protection set | 24 hours through daily complete Manager snapshot | 8 hours | 12 hours |
| DynamoDB execution journal | 5 minutes through PITR; immutable S3 evidence remains independent | 2 hours to a new table | Not an application recovery authority |
| S3 catalog/manifests/publication evidence | Zero after a catalog entry is committed | 2 hours to rebuild/verify | 4 hours to rebuild/verify |

A database restore always targets a fresh isolated target. Production cutover is outside the recovery-time measurement
and requires its own approval. Archive retrieval time is included in the recovery class that selects archived data.

## 5. ADR-004: Retention and Object Lock classes

| Class | Selection | Minimum retention | Storage intent | Lock mode |
| --- | --- | ---: | --- | --- |
| Operational | Every recovery-eligible PostgreSQL and Scylla restore point plus required dependencies/WAL | 35 days | Primary and recovery copies remain promptly retrievable | Governance in development/staging; Compliance in production after Gate 4 qualification |
| Monthly | Last recovery-eligible point for each UTC calendar month | 13 months | Lifecycle may use an archive class only if recovery target remains achievable | Same as environment |
| Legal hold | Exact object versions named by an approved hold | Until explicitly released | No lifecycle deletion while held | Hold is independent of time retention |
| Incomplete staging | Multipart parts and objects never published into an eligible catalog | 7-day reconciliation threshold, then approved cleanup | Staging only; never an eligible restore point | No bypass of a lock already applied |

Retention always closes over PostgreSQL base/direct-parent dependencies, WAL intervals, manifests, signatures,
publication/catalog evidence, and required replicas. Scylla snapshots are logically complete restore points and do not
receive an invented IFM SSTable dependency graph. Longer legal, tax, or brokerage retention can extend these minimums;
it cannot silently shorten them.

## 6. ADR-005: Encryption and signing

1. S3 and DynamoDB use customer-managed symmetric KMS keys owned in their respective trust domains.
2. Primary and recovery vaults use independent single-Region encryption keys.
3. Manifest and publication signatures use a dedicated asymmetric `ECC_NIST_P256` KMS `SIGN_VERIFY` key with
   `ECDSA_SHA_256` over the canonical SHA-256 digest.
4. The signature envelope records the full key ARN, algorithm, document digest, and schema version.
5. Only the public key and trusted metadata enter the offline recovery bundle.
6. Rollover overlaps old/new public trust so every retained object remains verifiable.
7. Encryption context binds environment, protection set, engine, restore point, and artifact identity where supported.

## 7. ADR-006: Native staging and transfer

AWS publication uses service-controlled, encrypted persistent staging outside the PostgreSQL and Scylla protected data
roots. The staging volume must provide at least `2 * largest dependency-complete backup + 20%` free capacity before a
capture begins. Initial concurrency is one capture or restore per engine until Gate 16 measures safe capacity.

PostgreSQL native tools write only to the allowlisted staging root. Scylla Manager coordinates the complete snapshot;
the Database Backup Service reads the Manager-approved backup location and uploads artifacts. PostgreSQL, Scylla nodes,
and Scylla Manager receive no AWS credentials under this design.

## 8. ADR-007: Cost-control decisions

1. Development uses one consolidated account but never qualifies production isolation.
2. DynamoDB starts in on-demand capacity mode with PITR and measured indexes only.
3. S3 Standard is the default during qualification; lifecycle transitions are disabled until restore-time evidence
   supports them.
4. Multipart size/concurrency, inventory frequency, and CloudTrail data events are bounded and measured.
5. AWS budgets and cost-allocation tags are mandatory in Gate 4.
6. Cost pressure cannot weaken encryption, immutability, dependency retention, replica count, or recovery objectives.

## 9. Supersession

Changing IaC technology, Regions, recovery targets, retention minimums, signing algorithm, staging boundary, or Scylla
transfer model requires a new ADR that identifies migration and compatibility impact. Assigning staging/production
account IDs extends ADR-002 but does not authorize resource mutation by itself.
