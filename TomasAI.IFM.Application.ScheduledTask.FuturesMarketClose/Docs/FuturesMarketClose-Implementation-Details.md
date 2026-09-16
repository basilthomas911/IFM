# Futures Market Close Scheduled Task — Implementation Details

## Purpose

`TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose` is a one-shot .NET 10 Worker scheduled for 17:01
`America/New_York`, Monday through Friday. It records the dated IFM close request, removes completed NATS
JetStream transport history, and submits durable DatabaseBackup commands for configured protection sets.

JetStream is transport storage. PostgreSQL event sourcing remains the durable business record. End-of-day maintenance
purges messages while retaining stream definitions, durable consumer definitions, and PostgreSQL events.

## Runtime workflow

1. Start one NATS actor-producer mailbox for the scheduled task.
2. Submit the dated application-shutdown command and stop if the command is rejected.
3. Capture the last sequence and message count for `EventStream` and each production projector PROCESS/REPLAY
   stream.
4. For each non-empty stream, wait up to the configured drain timeout for the relevant durable consumer to have zero
   pending and zero acknowledgement-pending messages.
5. Purge only messages below `captured last sequence + 1`. Messages published after the watermark remain available.
6. Submit one `RequestDatabaseBackupCommand` per configured protection set.
7. Report the task as failed if JetStream maintenance failed, after backup requests have still been submitted.
8. Stop the producer and host.

The application-shutdown command currently records the lifecycle request; final application-wide shutdown orchestration
remains deferred. JetStream safety therefore does not depend on a fixed delay or an assumed process stop. Consumer
drain state and a captured sequence watermark are the deletion boundary.

## Purge scope and safety

The checked-in production selectors are:

- exact stream `EventStream`;
- streams matching `^IFM_[A-Z][A-Za-z0-9]*Projector_(PROCESS|REPLAY)$`.

Lowercase integration, recovery-test, and other transient stream names are excluded. On `EventStream`, only the
production `EventConsumer` participates in the drain gate; abandoned integration-test consumers cannot block the
production close task. Every consumer on a selected projector stream participates in its drain gate.

A selected non-empty stream is not purged when:

- its required production consumer does not exist;
- any relevant consumer has pending messages;
- any relevant consumer has an unacknowledged delivery;
- the drain timeout expires; or
- NATS rejects inspection or purge.

The NATS purge request uses `Seq = captured LastSeq + 1`, whose server contract removes messages up to but excluding
that sequence. A concurrent message with a greater sequence is retained. The task never deletes stream or consumer
definitions.

## Message and authorization contract

Each backup request uses:

- `BackupSource.LocalWorkstation`;
- `DatabaseConsistencyMode.EngineConsistent`;
- `DatabaseRequestOrigin.ScheduledTask`;
- the configured `DatabaseBackup:Mode` (`Full`, `Automatic`, or `Incremental`);
- caller role `database-backup-operator`;
- the configured environment identity;
- the required configured logical destination; and
- a unique request/correlation identifier that also becomes the command identifier.

The scheduled task enters actor workflows through Command messages. It does not publish execution events or call
business storage implementations directly.

## Configuration

| Key | Required | Purpose |
| --- | --- | --- |
| `Nats:Url` | No | NATS endpoint; defaults to `nats://localhost:4222`. |
| `JetStreamEndOfDayPurge:DrainTimeoutSeconds` | Yes | Per-stream bounded drain wait. |
| `JetStreamEndOfDayPurge:DrainPollMilliseconds` | Yes | Interval between consumer-state reads. |
| `JetStreamEndOfDayPurge:EventStreamConsumerName` | Yes | Production shared-event durable consumer. |
| `JetStreamEndOfDayPurge:StreamNamePatterns` | Yes | Anchored production stream selectors. |
| `DatabaseBackup:EnvironmentIdentity` | Yes | Identity of the local environment being protected. |
| `DatabaseBackup:Destination` | No | Required logical destination; defaults to `online-vault`. |
| `DatabaseBackup:Mode` | No | Backup selection mode; checked-in configuration uses `Automatic`. |
| `DatabaseBackup:ProtectionSets` | Yes | Non-empty list of protection-set identifiers to submit. |
| `Serilog:*` | Yes for configured logging | Console/file logging policy. |

## Scheduler ownership

The Scheduler Host catalog declares manifest version 3 for this task. Schedule
`e296cd36-738e-47c9-b207-3fd7f8b6a0d0` is explicitly approved and uses Quartz cron
`0 1 17 ? * MON-FRI` in `America/New_York` with `DoNothing` misfire behavior.

An enabled deployment seed requires a non-empty `ActivationApprovalReference` and an available, hash-valid deployed
executable. Seed reconciliation updates an existing definition only while `updated_by` remains
`deployment-seed`. Once an operator changes the definition, later deployments preserve the operator-owned version.

## Operational behavior

- Full and incremental requests use the same scheduled task. In `Automatic` mode, the backup host chooses
  incremental only when its parent and policy checks pass.
- A purge failure is logged with the blocked stream and consumer counts. Database backup requests are still submitted,
  and the scheduled run exits as failed.
- A rejected lifecycle command or backup request produces a failed run.
- Cancellation propagates through producer startup, consumer drain waits, purge calls, and backup requests.
- Producer and host shutdown execute from `finally`.
- The task uses the shared one-shot runtime and cooperative Scheduler Host control-pipe cancellation.

Do not add database-name enumeration, per-database backup types, Core NATS completion listeners, unbounded drain
polling, or whole-stream deletion. The durable actor workflow owns recovery; this task owns bounded end-of-day
transport retention.
