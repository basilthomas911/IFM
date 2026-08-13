# Futures Market Close Scheduled Task — Implementation Details

## Purpose

`TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose` is a one-shot .NET 10 Worker launched by an external
scheduler at futures-market close. It requests an orderly IFM application shutdown and then submits durable
DatabaseBackup commands for configured protection sets. It does not perform native backup work itself and does not
wait on transient UI notifications.

## Runtime workflow

1. Start one NATS actor producer mailbox for the scheduled task.
2. submit the dated application-shutdown command and stop if it is rejected;
3. wait two seconds for the existing shutdown workflow;
4. read the configured protection-set identifiers;
5. submit one `RequestDatabaseBackupCommand` per protection set through `IDatabaseBackupCommandApi`;
6. log each accepted durable recovery-operation identifier; and
7. stop the producer and host.

The DatabaseBackup Command/Event/Query actors and standalone capability host own durable acceptance, execution,
recovery, status, and completion. The scheduler therefore does not maintain a mutable list of database names or use a
Core NATS event listener to infer durable completion.

## Message and authorization contract

Each request uses:

- `BackupSource.LocalWorkstation`;
- `DatabaseConsistencyMode.EngineConsistent`;
- `DatabaseRequestOrigin.ScheduledTask`;
- caller role `database-backup-operator`;
- the configured environment identity;
- the required configured logical destination; and
- a unique request/correlation identifier that also becomes the command identifier.

This is an external non-actor participant entering the actor workflow through a durable Command message. It does not
publish execution events or call storage implementations directly.

## Configuration

| Key | Required | Purpose |
| --- | --- | --- |
| `Nats:Url` | No | NATS endpoint; defaults to `nats://localhost:4222`. |
| `DatabaseBackup:EnvironmentIdentity` | Yes | Identity of the local environment being protected. |
| `DatabaseBackup:Destination` | No | Required logical destination; defaults to `online-vault`. |
| `DatabaseBackup:ProtectionSets` | Yes | Non-empty list of protection-set identifiers to submit. |
| `Serilog:*` | Yes for configured logging | Console/file logging policy. |

The old command/query REST base URIs are not used. Database discovery is represented by versioned protection-set
configuration rather than the removed per-database SystemAdmin query.

## Hosting and dependencies

- SDK: `Microsoft.NET.Sdk.Worker`
- Target: .NET 10
- Messaging: shared `IActorProducer` implemented by `NatsActorProducer`
- APIs: `IApplicationCommandApi` and `IDatabaseBackupCommandApi`
- Logging: Serilog

`Program.cs` uses `Host.CreateApplicationBuilder`, registers the shared NATS producer and typed command APIs, and runs
`Worker`. No REST client or UI event-consumer dependency remains.

## Operational behavior

- An external scheduler determines when the process starts.
- Command acceptance is durable; later progress and completion are queried or observed through the DatabaseBackup
  actor contracts.
- A rejected shutdown or backup request is logged as an error and no false success is reported.
- Cancellation is propagated to producer startup, the readiness delay, and every backup request.
- Producer and host shutdown execute from `finally`.
- This project is built independently because it is not currently listed in `TomasAI.IFM.sln`.

Do not reintroduce database-name enumeration, per-database backup types, legacy SystemAdmin backup APIs, or Core NATS
completion listeners here. Scheduling policy should select protection sets; the durable actor workflow owns recovery.
