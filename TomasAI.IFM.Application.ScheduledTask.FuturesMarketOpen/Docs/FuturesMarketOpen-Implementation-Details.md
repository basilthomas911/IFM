# Futures Market Open Scheduled Task — Implementation Details

## Purpose

`TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen` is a one-shot .NET worker executable intended to be launched when the futures market opens. It obtains the current IFM value date and, when that query succeeds, asks the application command service to start the IFM application services.

The executable does not contain a timer, cron expression, or recurring loop. Scheduling is an external deployment responsibility.

## Complete project folder map

This tree records every directory currently present beneath the project root, from root to leaf. `bin` and `obj` are generated build trees whose contents can change by build configuration and SDK.

```text
TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen/   Project root
├── Docs/                                                  Maintained project documentation
├── Properties/                                            Local launch and publish metadata
│   └── PublishProfiles/                                   Folder-publish profile
├── bin/                                                   Generated build output
│   ├── Debug/
│   │   └── net7.0/                                        Debug .NET 7 output leaf
│   └── Release/
│       └── net7.0/                                        Release .NET 7 output leaf
└── obj/                                                   Generated build intermediates
    ├── Debug/
    │   └── net7.0/
    │       ├── ref/                                       Debug reference assembly leaf
    │       └── refint/                                    Debug internal-reference leaf
    └── Release/
        └── net7.0/
            ├── ref/                                       Release reference assembly leaf
            └── refint/                                    Release internal-reference leaf
```

### Folder responsibilities

| Folder | Kind | Responsibility |
| --- | --- | --- |
| Project root | Source | Contains the worker entry point, one-shot worker, configuration files, project definition, and child folders. |
| `Docs/` | Documentation leaf | Contains this implementation and structure reference. |
| `Properties/` | Source metadata | Contains development launch configuration and publish profiles. |
| `Properties/PublishProfiles/` | Source metadata leaf | Contains the folder-based MSBuild publish profile. |
| `bin/` | Generated | Root of compiled application output. |
| `bin/Debug/` | Generated | Debug-configuration output. |
| `bin/Debug/net7.0/` | Generated leaf | Debug assemblies, dependencies, symbols, executable, and copied settings for .NET 7. |
| `bin/Release/` | Generated | Release-configuration output. |
| `bin/Release/net7.0/` | Generated leaf | Release assemblies, dependencies, symbols, executable, and copied settings for .NET 7. |
| `obj/` | Generated | Root of restore data and compiler/MSBuild intermediates. |
| `obj/Debug/` | Generated | Debug intermediates. |
| `obj/Debug/net7.0/` | Generated | Debug generated sources, caches, and intermediate assemblies. |
| `obj/Debug/net7.0/ref/` | Generated leaf | Debug reference assembly. |
| `obj/Debug/net7.0/refint/` | Generated leaf | Debug internal reference assembly. |
| `obj/Release/` | Generated | Release intermediates. |
| `obj/Release/net7.0/` | Generated | Release generated sources, caches, and intermediate assemblies. |
| `obj/Release/net7.0/ref/` | Generated leaf | Release reference assembly. |
| `obj/Release/net7.0/refint/` | Generated leaf | Release internal reference assembly. |

Do not manually edit or commit generated `bin` or `obj` content.

## Maintained file inventory

| File | Responsibility |
| --- | --- |
| `Program.cs` | Builds configuration and dependency injection, registers REST clients and the worker, runs the host, and terminates the process. |
| `Worker.cs` | Executes the market-open application-start workflow once and then stops the host. |
| `TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen.csproj` | Defines the .NET 7 worker project, packages, references, user-secrets identity, and settings copy behavior. |
| `appsettings.json` | Required base Serilog configuration. |
| `appsettings.Development.json` | Development command/query service endpoint configuration. |
| `appsettings.Production.json` | Production command/query service endpoint configuration. |
| `Properties/launchSettings.json` | Local WSL and executable launch profiles. |
| `Properties/PublishProfiles/FolderProfile.pubxml` | Folder-publish settings. |
| `Docs/FuturesMarketOpen-Implementation-Details.md` | This document. |

## Host startup and dependency injection

`Program.cs` starts with the default host configuration providers and then explicitly loads:

1. required `appsettings.json` from the current working directory;
2. optional `appsettings.{EnvironmentName}.json`.

Both JSON files use `reloadOnChange: true`.

The service container registers:

| Registration | Implementation/use |
| --- | --- |
| Logging | Serilog configured from the merged configuration. |
| Named non-generic logger | `IFM-ScheduledTask-FuturesMarketOpen`. |
| `IRestApiSerializer` | `NewtonSoftJsonSerializer`. |
| `ICommandServiceRestApiOptions` | Reads `AppSettings:CommandServerBaseUri`. |
| `ICommandService` | `CommandServiceRestApiClient`. |
| `IQueryServiceRestApiOptions` | Reads `AppSettings:QueryServerBaseUri`. |
| `IQueryService` | `QueryServiceRestClientApi`. |
| `IMarketDataQueryApi` | `MarketDataQueryApi`. The same registration appears twice; the later registration wins for single-service resolution. |
| `IApplicationCommandApi` | `ApplicationCommandApi`. |
| Hosted service | `Worker`. |

## Execution workflow

`Worker.ExecuteAsync` performs a single pass:

```text
Host starts Worker
  └─ Query current value date
       ├─ Failure/null
       │    └─ Log the query error
       └─ Success
            ├─ Log application startup
            ├─ Call StartApplicationAsync
            ├─ Wait 2 seconds
            └─ Log startup and value-date messages
  └─ Stop the host in finally
```

Detailed behavior:

1. Call `IMarketDataQueryApi.GetValueDateAsync()`.
2. Require both `Success == true` and a non-null value.
3. Call `IApplicationCommandApi.StartApplicationAsync()`.
4. Wait a fixed two seconds.
5. Log that services have started and that the value date was loaded.
6. Log that currently traded futures contracts are being loaded; no contract query follows this final log statement in the current implementation.
7. Always call `_host.StopAsync()` in the worker's `finally` block.

The worker catches and logs execution exceptions. After `RunAsync` exits, `Program.cs` always kills the current process from its outer `finally` block.

## Configuration surface

| Key | Required by code | Purpose |
| --- | --- | --- |
| `AppSettings:CommandServerBaseUri` | Yes | Base URI used by the command-service REST client. |
| `AppSettings:QueryServerBaseUri` | Yes | Base URI used by the query-service REST client. |
| `Serilog:*` | Required for configured logging behavior | Sink, minimum-level, enrichment, and application metadata. |
| `DOTNET_ENVIRONMENT` | Optional host input | Selects the environment-specific settings file. |

The base settings file contains Serilog configuration; endpoint values are held in environment-specific files. The application expects the settings files to be available relative to its current working directory.

## Project and deployment definition

- SDK: `Microsoft.NET.Sdk.Worker`
- Target framework: `net7.0`
- Nullable reference types: enabled
- Implicit usings: enabled
- Hosting package: `Microsoft.Extensions.Hosting` 3.1.22
- Logging packages: Serilog ASP.NET Core 3.2.0, Console 3.1.1, File 4.1.0
- Publish metadata: folder protocol, .NET 7 target, non-self-contained in the checked-in profile

The project has project references for command/query REST clients, REST messaging, messaging abstractions, and serialization. It also declares direct assembly references to command/query client Debug outputs.

## Operational characteristics and current limitations

- **External scheduling is required.** The worker has no internal schedule or recurrence.
- **The task is one-shot.** Success or failure leads to host shutdown.
- **Top-level failures are hidden.** The outer `catch` in `Program.cs` is empty, so host construction/runtime exceptions are not logged there.
- **Termination is forced.** `Process.GetCurrentProcess().Kill()` runs after the host exits rather than allowing normal process return.
- **Cancellation is not propagated.** `stoppingToken` is unused, and API calls plus the two-second delay do not receive it.
- **Command success is not inspected.** The return value from `StartApplicationAsync` is not checked before success is logged.
- **One date format is incorrect.** The first startup message uses `yyyy-mm-dd`; lowercase `mm` formats minutes, while the later message correctly uses `yyyy-MM-dd`.
- **The final contract-loading message has no matching operation.** No currently-traded-contract query occurs in this worker.
- **Configuration contains legacy naming.** The project `UserSecretsId` refers to Set Closing Price rather than Futures Market Open.
- **Project references are stale.** The checked-in command/query REST-client project-reference paths do not currently exist. Direct Debug assembly references partially reflect the same legacy client layout.
- **The project is outside the current solution.** It is not listed in `TomasAI.IFM.sln`, so solution builds do not validate it.

## Safe extension points

When extending this task:

1. Keep scheduling outside the worker unless ownership is intentionally moved into this process.
2. Propagate `stoppingToken` through cancellable API calls and delays.
3. Check and log the application-start service result.
4. Add any intended contract-loading work after its existing log statement, or remove the misleading statement.
5. Replace the empty top-level catch and forced kill with observable, graceful shutdown behavior.
6. Repair project references before using standalone builds or adding the project back to the solution.
