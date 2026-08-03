# Futures Market Close Scheduled Task — Implementation Details

## Purpose

`TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose` is a one-shot .NET worker executable intended to run at futures-market close. Its implemented business workflow shuts down the IFM application, discovers configured databases, submits backup commands, waits for completion/failure events, and exits.

Despite the project name, its core post-shutdown responsibility is database backup. It has no internal timer or recurring schedule; an external scheduler must launch it.

## Complete project folder map

This tree records every directory currently present beneath the project root, from root to leaf. `bin` is generated and can change after builds or publishing. Unlike the other two scheduled-task projects, no `obj` directory is currently present in this project tree.

```text
TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose/  Project root
├── Docs/                                                   Maintained project documentation
├── Properties/                                             Local launch and publish metadata
│   └── PublishProfiles/                                    Folder-publish profile leaf
└── bin/                                                    Generated build output
    ├── Debug/
    │   └── net7.0/                                         Debug .NET 7 output leaf
    └── Release/
        └── net7.0/                                         Release .NET 7 output leaf
```

### Folder responsibilities

| Folder | Kind | Responsibility |
| --- | --- | --- |
| Project root | Source | Contains host startup, the backup worker, settings, project definition, and child folders. |
| `Docs/` | Documentation leaf | Contains this implementation and structure reference. |
| `Properties/` | Source metadata | Contains local launch configuration and publish profiles. |
| `Properties/PublishProfiles/` | Source metadata leaf | Contains the folder-based MSBuild publish profile. |
| `bin/` | Generated | Root of compiled application output. |
| `bin/Debug/` | Generated | Debug-configuration output. |
| `bin/Debug/net7.0/` | Generated leaf | Debug assemblies, dependencies, symbols, executable, and copied settings for .NET 7. |
| `bin/Release/` | Generated | Release-configuration output. |
| `bin/Release/net7.0/` | Generated leaf | Release assemblies, dependencies, symbols, executable, and copied settings for .NET 7. |

Do not manually edit or commit generated `bin` content. A future restore/build may create an `obj` intermediate tree that should be treated the same way.

## Maintained file inventory

| File | Responsibility |
| --- | --- |
| `Program.cs` | Builds configuration and DI, registers REST/NATS/event-consumer services, runs the host, and terminates the process. |
| `Worker.cs` | Shuts down the application, submits database backups, observes progress events, waits for all database names to complete, and stops the host. |
| `TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose.csproj` | Defines the .NET 7 worker, packages, project references, settings copy rules, and user-secrets identity. |
| `appsettings.json` | Required base Serilog configuration. |
| `appsettings.Development.json` | Development command/query service endpoints. |
| `appsettings.Production.json` | Production command/query service endpoints. |
| `Properties/launchSettings.json` | Local launch profile, currently named for Database Backup. |
| `Properties/PublishProfiles/FolderProfile.pubxml` | Windows x64 folder-publish settings. |
| `Docs/FuturesMarketClose-Implementation-Details.md` | This document. |

## Host startup and dependency injection

`Program.cs` uses `Host.CreateDefaultBuilder(args)` and loads required base settings plus an optional environment-specific settings file from the current working directory. Both files use configuration reload-on-change.

The service container registers:

| Registration | Implementation/use |
| --- | --- |
| Logging | Serilog configured from application settings. |
| Named non-generic logger | `IFM-ScheduledTask-FuturesMarketClose`. |
| `INatsEventListenerOptions` | `NatsEventListenerOptions`. |
| `ISystemAdminUIEventConsumer` | `SystemAdminUIEventConsumer`, used to observe backup events. |
| `IRestApiSerializer` | `NewtonSoftJsonSerializer`. |
| `ICommandServiceRestApiOptions` | Reads `AppSettings:CommandServerBaseUri`. |
| `ICommandService` | `CommandServiceRestApiClient`. |
| `ISystemAdminCommandApi` | `SystemAdminCommandApi`. |
| `IQueryServiceRestApiOptions` | Reads `AppSettings:QueryServerBaseUri`. |
| `IQueryService` | `QueryServiceRestClientApi`. |
| `ISystemAdminQueryApi` | `SystemAdminQueryApi`. |
| `IApplicationCommandApi` | `ApplicationCommandApi`. |
| Hosted service | `Worker`. |

## Execution workflow

```text
Host starts Worker
  ├─ Shut down IFM application services
  ├─ Wait 2 seconds
  ├─ Query database names
  │    └─ Failure: log and finish
  └─ Success
       ├─ Start asynchronous info-log queue
       ├─ Copy all database names into pending list
       ├─ Start system-admin backup event consumer
       ├─ Wait 1 second
       ├─ Submit one backup command per database
       ├─ Poll pending list every 2 seconds until empty
       ├─ Stop event consumer
       └─ Stop info-log queue
  └─ Stop host in finally
```

### Backup selection

The worker selects backup type and command timeout using the local day of week at the time each command is submitted:

| Day | Backup type | Command timeout |
| --- | --- | --- |
| Friday | Full | 60 minutes |
| Saturday | Full | 60 minutes |
| Sunday | Full | 60 minutes |
| Monday–Thursday | Differential | 15 minutes |

### Event-driven completion tracking

The worker initializes `completedNames` with every queried database name and registers four event-consumer callbacks:

| Event callback | Effect |
| --- | --- |
| Backup started | Enqueue an informational log message. |
| Backup information | Enqueue the database's information message. |
| Backup completed | Remove the database name from `completedNames`. |
| Backup failed | Remove the database name and enqueue a failure message. |

The worker does not exit its polling loop until every name has been removed by a completed or failed event.

## Configuration surface

| Key | Required by code | Purpose |
| --- | --- | --- |
| `AppSettings:CommandServerBaseUri` | Yes | Base URI used for application shutdown and backup commands. |
| `AppSettings:QueryServerBaseUri` | Yes | Base URI used to query database names. |
| `Serilog:*` | Required for configured logging behavior | Sink, minimum-level, enrichment, and application metadata. |
| `DOTNET_ENVIRONMENT` | Optional host input | Selects the environment-specific settings file. |

The event consumer additionally depends on NATS configuration understood by the referenced messaging/UI consumer assemblies. `Program.cs` does not bind a project-specific NATS configuration section directly.

## Project and deployment definition

- SDK: `Microsoft.NET.Sdk.Worker`
- Target framework: `net7.0`
- Hosting package: `Microsoft.Extensions.Hosting` 3.1.22
- REST package: RestSharp 106.10.1
- Logging packages: Serilog ASP.NET Core 3.2.0, Console 3.1.1, File 4.1.0
- Publish profile: folder protocol, Windows x64 runtime, self-contained, single-file, ReadyToRun
- Settings copy policy: Development, base, and Production settings are preserved in build and publish output

## Operational characteristics and current limitations

- **External scheduling is required.** No schedule exists in this executable.
- **The task is one-shot.** It stops the host after backup handling succeeds or fails.
- **Top-level failures are hidden.** `Program.cs` has an empty outer `catch`.
- **Termination is forced.** The current process is killed in `Program.cs` after host exit.
- **Cancellation is not propagated.** `stoppingToken` is unused by API calls, delays, event-consumer startup, or the completion polling loop.
- **Shutdown success is not inspected.** The result of `ShutdownApplicationAsync` is not checked.
- **The pending list is not synchronized.** Event callbacks can remove names while the worker reads `completedNames.Count`; `List<string>` is not thread-safe.
- **A rejected command can cause an infinite wait.** When `BackupDatabaseAsync` returns an unsuccessful result, the worker logs the error but does not remove that database from `completedNames`. If no failure event follows, the polling loop never ends.
- **The completion wait has no deadline.** A lost completion/failure event can keep the process alive indefinitely.
- **Cleanup is not uniformly guaranteed.** If an exception occurs after the event consumer or info queue starts, the catch logs it, but those components are not stopped from a dedicated `finally` block.
- **The worker uses fixed startup delays.** Two seconds follow application shutdown and one second follows event-consumer startup; neither establishes readiness.
- **Local time controls policy.** `DateTime.Now.DayOfWeek` selects backup type and timeout.
- **Naming is mixed.** XML comments, launch profile, and user-secrets identity refer to Database Backup while the project is named Futures Market Close.
- **Project references are stale.** The command/query REST-client project-reference paths do not currently exist.
- **The project is outside the current solution.** It is not listed in `TomasAI.IFM.sln`, so solution builds do not validate it.

## Safe extension points

1. Replace the mutable pending `List<string>` with a concurrency-safe tracker.
2. Remove a database from pending state when command submission definitively fails.
3. Add an overall completion timeout and cancellation-aware polling.
4. Put event-consumer and queue cleanup in `finally` blocks.
5. Inspect shutdown and backup command results explicitly.
6. Replace readiness delays with health/readiness checks where APIs support them.
7. Repair project references before standalone builds or solution inclusion.
