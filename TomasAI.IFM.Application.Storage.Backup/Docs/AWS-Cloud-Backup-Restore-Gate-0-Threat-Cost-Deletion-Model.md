# AWS Cloud Backup and Restore Gate 0 Threat, Cost, and Deletion Model

**Status:** Accepted implementation control baseline

**Date:** 2026-08-21

## 1. Data classification

| Data | Classification | Handling rule |
| --- | --- | --- |
| PostgreSQL/Scylla artifacts and restored targets | Restricted | Encrypted in transit/at rest; least-privilege read; never in logs, NATS, journal, or actor state |
| WAL and Scylla schema/topology artifacts | Restricted | Same controls and retention closure as the recovery point |
| Manifests, catalog, publication, and drill evidence | Confidential | Immutable, signed, bounded, independently readable; no secrets or raw native output |
| Journal operation metadata | Confidential | Encrypted DynamoDB, bounded item schema, no credentials/connection strings/large evidence |
| Account IDs, role/key/bucket ARNs, Regions, object version IDs | Internal | Allowed in safe configuration/evidence; not credentials |
| Access keys, secret keys, session tokens, private keys, database credentials | Secret | Process-memory credential providers only; never configuration, source, report, output, or telemetry |

## 2. Assets and trust boundaries

Protected assets are recovery artifacts, WAL continuity, manifests/signatures, exact S3 versions, catalog history,
Object Lock state, KMS keys, journal correctness, restore authorization, and recovery trust bundles.

Trust boundaries are the workstation/development host, workload account, primary backup account, recovery account,
database nodes, NATS/Core actors, human operators, and offline break-glass custody. The normal host may write new primary
objects but may not administer or delete vaults. The recovery role is independent of routine workload identity.

## 3. Threat register

| Threat | Primary control | Required validation gate | Residual handling |
| --- | --- | ---: | --- |
| Credential disclosure in config/log/test output | Default credential chain, redaction, secret scans, no credential option fields | 0, 3, 16 | Rotate affected credential and preserve only sanitized incident evidence |
| Wrong account or Region | STS preflight plus committed deny-by-default allowlist | 0, 3 | Reject before mutation; alert on configuration drift |
| Workload-account compromise | Separate vault/recovery accounts, constrained roles, Object Lock | 4, 13, 16 | Recovery-account-only drill |
| Backup-account compromise | Independent recovery account/key and one-way replication | 4, 13, 16 | Block primary trust and restore from exact recovery versions |
| Object deletion/ransomware | Versioning, Compliance Object Lock, legal hold, no normal delete permission | 4, 14, 16 | Retain independent exact versions and immutable audit |
| KMS key loss or malicious deletion | Independent keys, deletion controls, public signing trust bundle, audit | 4, 7, 13 | Key-loss runbook; recovery copy uses a different key |
| Artifact corruption/truncation/substitution | IFM SHA-256, S3 checksum, length, exact version ID, signed evidence | 6, 7, 10, 12 | Mark ineligible; never auto-fail over without validation |
| Manifest/catalog tampering | Canonical asymmetric signature and catalog reconstruction | 6, 7, 13 | Reject untrusted record and rebuild from signed versions |
| Journal duplicate/split brain | DynamoDB conditions/transactions, inbox idempotency, fencing lease | 5, 8 | Consistent read and reconciliation before retry |
| PostgreSQL chain or WAL gap | Direct-parent closure, WAL index/continuity, `pg_combinebackup` qualification | 9, 10 | Mark affected recovery interval ineligible |
| Partial Scylla protection set | Manager/node/topology completeness evidence | 11, 12 | Reject before publication/restore mutation |
| Unsafe restore/cutover | Fresh target, explicit replica, separate cutover approval | 10, 12, 18 | Dispose only approved isolated target |
| Retention deletes a dependency | Immutable revisioned exact-version plan and graph closure | 14 | Stop on drift; retain independent replica |
| Archive retrieval violates RTO | Class-specific storage policy and measured retrieval | 13, 16 | Keep operational class promptly retrievable |
| Denial of service or AWS outage | Bounded retries/backpressure, local WAL spool, health isolation | 8, 9, 16 | AWS degradation must not terminate Core/UI/local source |
| Insider misuse | Separation of duties, CloudTrail data/KMS events, protected workflows | 4, 14, 16 | Incident response and immutable audit review |
| Supply-chain compromise | Pinned SDK/IaC dependencies, vulnerability/signature scans | 1, 4, 16 | Block release and rotate compromised artifacts/credentials |

### Gate 0 residual risks

- The current development caller is a long-lived IAM user. It is restricted to development bootstrap and is rejected
  as a staging/production identity.
- Development is consolidated in one account and cannot qualify production isolation.
- Current database sizes, daily change, WAL rate, object count, and restore-compute requirements are not yet measured;
  the cost model is therefore parametric.
- The existing local Database Backup Host currently crash-loops when recovering journal work because its online-vault
  enrollment file is absent. AWS remains disabled. Gate 1 must make source startup/resilience independent and must not
  execute those stale operations merely to clear the finding.

None of these residual risks grants AWS mutation authority.

## 4. Cost model

### 4.1 Monthly formula

```text
S3 storage
  = sum(average retained GB per vault and storage class * regional GB-month rate)

S3 operations
  = PUT/COPY/LIST/GET/lifecycle/inventory requests * regional request rates

Replication
  = replicated GB * cross-Region transfer rate + destination PUT/KMS requests

KMS
  = customer-managed keys * monthly key rate + encrypt/decrypt/sign/verify requests

DynamoDB
  = read/write request units + indexed/table GB + PITR protected GB

Audit and operations
  = CloudTrail data events + log storage + metrics/alarms + restore compute + archive retrieval + egress
```

AWS lists S3 storage, request, retrieval, transfer, management, and replication as separate cost components. DynamoDB
PITR is billed from protected table/index size, and reducing its recovery window does not reduce that PITR charge.
AWS KMS currently lists customer-managed key storage at USD 1 per key-month before rotation additions. The planned
minimum of four dedicated keys (workload journal, primary encryption, recovery encryption, signing) therefore creates
an approximate USD 4/month key-storage floor before API calls. This is not the total system price.

### 4.2 Required measured inputs

Gate 4 cannot deploy a non-canary environment until these values are recorded for PostgreSQL and Scylla separately:

- compressed full-backup GB and daily changed GB;
- average/peak WAL GB per hour and maximum outage spool;
- snapshot object count and average object size;
- backup and restore frequency by retention class;
- multipart part size, concurrency, and requests per run;
- journal reads/writes/items/index sizes per operation;
- primary-to-recovery replicated GB;
- inventory, CloudTrail data event, metric, and log volume; and
- restore drill compute hours, archive retrieval GB, and egress.

The Gate 4 change set must include an AWS Pricing Calculator export or equivalent dated regional calculation for low,
expected, and 2x-growth scenarios. A 20% budget warning and a 50% anomaly alarm are the initial controls; measured
normal variance may revise them through review.

### 4.3 Cost safety

Cost optimization may change storage class, multipart parameters, inventory cadence, or DynamoDB capacity mode only
after recovery and load evidence. It may not reduce required replicas, checksums, signing, encryption, immutability,
legal holds, dependency closure, or measured recovery objectives.

## 5. Resource deletion policy

### 5.1 Never automatically deleted

- CloudFormation stacks or buckets containing protected versions;
- a KMS key with retained decryptable artifacts;
- a legal hold or unexpired Object Lock retention period;
- a PostgreSQL base/parent/WAL object required by an eligible point;
- a Scylla artifact belonging to an eligible complete snapshot;
- manifests, signatures, publication/catalog records, or deletion evidence required to prove history; or
- the only policy-required primary or recovery replica.

### 5.2 Allowed deletion sequence

1. The retention planner reads a consistent catalog/inventory view and creates a signed immutable plan.
2. The plan contains environment, policy revision, plan revision, exact bucket/key/version IDs, dependency proof,
   retain-until time, legal-hold state, replica proof, reason, and expected reclaimed bytes.
3. A separate approver accepts that exact revision.
4. The executor assumes the constrained deletion role and re-reads every object immediately before deletion.
5. Any drift, unknown version, new hold, unexpired retention, missing independent replica, or dependency stops the plan.
6. Only listed exact versions are deleted; prefixes, wildcards, current-version aliases, and bucket sweeps are invalid.
7. Execution and reconciliation evidence is written immutably.

### 5.3 Development teardown

Disposable Gate 4 canary stacks must use unique run identifiers and an explicit inventory. Teardown is allowed only
after the inventory is reviewed and any Object Lock period expires. A stack delete failure caused by retained objects
is expected and must not be bypassed. Temporary multipart uploads may be aborted after the seven-day reconciliation
threshold when they have no published catalog identity.

### 5.4 KMS deletion

KMS keys are disabled only after proving that no retained artifact depends on them and the recovery copy uses an
independently usable key. Key deletion uses the maximum approved waiting period and requires security plus operations
approval. Cancellation and recovery are tested before production.

## 6. Authoritative pricing references

- Amazon S3 pricing: https://aws.amazon.com/s3/pricing/
- Amazon DynamoDB pricing: https://aws.amazon.com/dynamodb/pricing/
- AWS KMS pricing: https://aws.amazon.com/kms/pricing/
