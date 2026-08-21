# AWS Cloud Backup and Restore Gate 0 Baseline Inventory

**Status:** Frozen Gate 0 baseline

**Date:** 2026-08-21

**Source commit:** `6d516cc342098f3d6da05d4e9bf7ae13e15deccf`

## 1. Governing documents

| Document | Version |
| --- | ---: |
| `Database-Backup-Architecture-Overview.md` | 0.9 |
| `AWS-Cloud-Backup-Restore-Architecture.md` | 0.6 |
| `Local-Backup-Restore-Architecture.md` | 0.3 |
| `Local-Workstation-Backup-Restore-Code-Implementation-Specification.md` | 1.6 |
| `AWS-Cloud-Backup-Restore-Code-Implementation-Specification.md` | 0.3 after Gate 0 closure |

## 2. Code and schema baseline

| Area | Implemented baseline |
| --- | --- |
| Public source | `BackupSource.AwsCloud` already exists beside `LocalWorkstation`; no AWS event family is needed |
| Contracts | Source-neutral commands, queries, service/domain events, read models, IDs, and operation states |
| MessagePack | Requested mode and lineage use existing append-only key 31 |
| Manifest | `DatabaseBackupManifest` schema v2; v1 remains readable as a legacy full backup |
| Lineage | `None`, `Automatic`, `Full`, `Incremental`; PostgreSQL direct parent/base/depth/native kind; Scylla logically complete dedup lineage |
| Application ports | Processor/registry, journal, native validation, PostgreSQL/Scylla capabilities, chain planner, publication, restore source, manifest, catalog, checksum, signature, retention, evidence, statistics |
| SystemAdmin projection | Existing source-neutral operation/restore-point/replica/policy/health tables and `backup_lineage_json` |
| Local journal | SQLite operation, inbox, checkpoint, artifact replica, outbox, run statistics, and reconciliation tables |
| Local publication | Immutable signed manifest/catalog/media identity and dependency-aware restore selection |
| Host | Standalone `Api.DatabaseBackup.Host`, durable JetStream listener/outbox, dispatcher, reconciliation, readiness/liveness |

AWS SDK project/package references do not exist in the solution at Gate 0. AWS CLI and AWS PowerShell are workstation
operator prerequisites, not application dependencies.

## 3. Native baseline

| Capability | Baseline evidence |
| --- | --- |
| PostgreSQL | Local adapter accepts major versions 15 through 18; development qualification reports PostgreSQL 17.2 and container tools 17.11; full and PostgreSQL 17 incremental/combine behavior is covered by integration tests |
| Scylla | Local adapter accepts Manager major versions 3 through 4; current containers use Scylla 6.2.2 and Manager 3.4.2; complete snapshot/restore behavior is covered by integration tests |
| Workstation commands | `pg_basebackup`, `pg_combinebackup`, `pg_verifybackup`, and `sctool` are not on the Windows host PATH; native validation runs through controlled containers/adapters |
| NATS | `ifm-nats-server`, image `nats:2.12.0-alpine`, remained healthy throughout Gate 0 testing |

The local native qualification history is retained in the Phase 10 and incremental validation reports. Gate 0 did not
create a database backup, restore a database, change a database, or stop any application container.

## 4. AWS workstation tools

| Tool | Installed version | Gate 0 use |
| --- | ---: | --- |
| AWS CLI v2 | 2.36.29 | Independent operator verification and future CloudFormation workflows |
| AWS Tools for PowerShell | 5.0.282 | Modular STS, S3, DynamoDB, and KMS operator cmdlets |
| AWS SDK for .NET core used by PowerShell | 4.0.102.0 | PowerShell module runtime only; not an IFM PackageReference |

The PowerShell modules are `AWS.Tools.SecurityToken`, `AWS.Tools.S3`, `AWS.Tools.DynamoDBv2`, and
`AWS.Tools.KeyManagementService`. They are installed for the current user. `PSModulePath` includes the redirected
OneDrive WindowsPowerShell modules folder.

## 5. Credential and identity baseline

Only environment-variable names were inspected. The observed names were `aws_access_key_id` and
`aws_secret_access_key`; values were never read into reports or printed. Windows treats environment-variable names as
case-insensitive. Linux deployment must use the standard uppercase AWS SDK names and a session token whenever the
credential is temporary.

Read-only STS discovery returned:

| Field | Safe result |
| --- | --- |
| Partition | `aws` |
| Development account | `107651266250` |
| Principal | `arn:aws:iam::107651266250:user/basil.thomas@live.ca` |
| Tested endpoint Region | `ca-central-1` |
| Mutation authorization | `false` |

No S3, DynamoDB, KMS, IAM, CloudFormation, or other mutable AWS API was called.

## 6. Runtime inventory finding

All existing data/infrastructure containers remained running, and NATS remained healthy. The existing development
Database Backup Host container was already in a restart loop with exit code 139. Its journal reports 23 recoverable
operations, while `/var/lib/ifm/database-backup/online-vault/vault/enrollment/media.json` is absent. The resulting
`DirectoryNotFoundException` escapes `DatabaseBackupExecutionDispatcher`, stops the host, and produces cancellation
noise while Kestrel/NATS shut down.

This is not caused by AWS work and does not invalidate the passing shared/native test baseline. It is recorded as
`G0-F1`, severity 3 for AWS sequencing because AwsCloud is disabled, with mandatory Gate 1 closure before an AWS
processor can be enabled. The fix must fail/reconcile the affected operation safely and keep the host alive; it must
not create enrollment evidence or execute 23 stale operations merely to silence the restart loop.

## 7. Baseline test boundary

The Gate 0 regression boundary is:

1. SystemAdmin unit tests;
2. SystemAdmin behavior tests;
3. SystemAdmin integration tests including actor/host/journal/projection flow;
4. Framework Storage integration tests including journal, chain planner, publication, deterministic native
   PostgreSQL/Scylla capabilities, and disposable Docker native restore cases; and
5. the live read-only AWS identity preflight acceptance test.

Exact results are in `AWS-Cloud-Backup-Restore-Gate-0-Validation-Report.md`.
