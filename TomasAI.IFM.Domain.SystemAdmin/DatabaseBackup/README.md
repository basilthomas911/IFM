# DatabaseBackup concerns

DatabaseBackup is a grouping of concern-owned actors. There are no root `Command`, `Query`, or `Event` dispatch folders.

| Concern | Command owner | Other responsibilities |
| --- | --- | --- |
| `Backup` | `DatabaseBackupCommandActor` | Backup requests, cancellation, capture boundaries, artifact observations and backup queries |
| `Recovery` | `DatabaseRecoveryCommandActor` | Restore, drills, approval, validation, cutover and recovery queries |
| `Policy` | `DatabaseBackupPolicyCommandActor` | Policy updates, enforcement observations and setup queries |
| `Retention` | `DatabaseBackupRetentionCommandActor` | Retention evaluation, execution, results and forecasts |
| `BackupSet` | `DatabaseBackupSetCommandActor` | Legal holds and backup-set queries |
| `Service` | `DatabaseBackupServiceCommandActor` | Independent service capability observations and service health queries |
| `Lifecycle` | No command actor | The operation-log query actor |

Each concern places its actors, concrete message handlers, authoritative state, repository and projector beneath its own `Command`, `Query` and `Event` folders, where applicable. Each command actor loads one distinct state type. State contains common operation metadata and only the owning concern's additional facts; Recovery does not load Policy or Retention command state.

Concrete command extension classes keep `Execute`, pure `Compute` and source-event creation together. Shared receipt validation mechanics live in the domain's shared implementation folder. Every mapped handler is beside its owning actor, including lifecycle receipts reused across operation kinds.

## Message ownership and ordering

Operator command and query APIs choose the concern directly through contract route metadata. Host observations choose a concern through their verb and immutable operation kind. Generic statistics and reconciliation observations use the same event mailbox as the operation's other observations. Artifact observations remain in the backup operation's sequence. There is no central forwarding command, query or event actor.

Progress, completion, cancellation, verification and reconciliation commands go to the owner of the operation. Restore and cutover remain in Recovery; backup and verification remain in Backup; retention remains in Retention. Policy receipts go to Policy. This preserves each operation's service sequence checks, duplicate-observation fingerprints and ordered source-event batches.

## Persistence compatibility

Existing persisted event CLR types, event names, numeric serialization keys and execution work-order schemas are unchanged. Backup retains its historical command route. Other concerns have distinct command routes and streams. Their repositories replay the historical `Command.DatabaseBackupCommand.<operation-id>` stream before replaying their own concern stream, without copying or rewriting stored events. The append version belongs only to the concern's stream; the business revision includes all replayed history.

Shared PostgreSQL transaction mechanics still commit source events, read-model facts and execution intents together. Projectors have concern-owned workers while the read-model receipt identity remains compatible with the existing projection and rebuild path.

The combined `Shared/DatabaseBackup/Legacy/DatabaseBackupReplayState` is a compatibility replay/testing view. No concern actor or repository owns that combined state.

This source refactor does not deploy or restart applications. Producers and consumers using the new concern routes must be deployed together. Already queued messages addressed to retired root routes require an explicit cutover plan before deployment; there is no implicit legacy forwarding actor.

## Shared locations

Reusable implementation mechanics are in [`../Shared/DatabaseBackup`](../Shared/DatabaseBackup/README.md), within the domain project. Wire contracts and route metadata remain in `TomasAI.IFM.Application.Contracts.SystemAdmin/DatabaseBackup` and the existing lower-level shared host contracts. The contracts assemblies do not reference domain implementation.
