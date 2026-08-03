# Set Closing Price Scheduled Task — Implementation Details

## Purpose

`TomasAI.IFM.Application.ScheduledTask.SetClosingPrice` is a one-shot .NET worker executable intended to run after the futures close. It obtains the IFM value date and currently traded futures contracts, uses the last tick at the assumed 4:00 p.m. close as each contract's closing price, persists that price, and stops trade placement for contracts whose IDs begin with `ES`.

The executable contains no internal timer, cron expression, or recurrence. An external scheduler must launch it at the intended time.

## Complete project folder map

This tree records every directory currently present beneath the project root, from root to leaf. `bin` and `obj` are generated build trees whose contents can change by build configuration and SDK.

```text
TomasAI.IFM.Application.ScheduledTask.SetClosingPrice/     Project root
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
| Project root | Source | Contains host startup, the closing-price worker, settings, project definition, and child folders. |
| `Docs/` | Documentation leaf | Contains this implementation and structure reference. |
| `Properties/` | Source metadata | Contains local launch configuration and publish profiles. |
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
| `Program.cs` | Builds configuration and DI, registers command/query REST clients and the worker, runs the host, and terminates the process. |
| `Worker.cs` | Executes the value-date, contract, closing-price, and trade-placement-stop workflow once. |
| `TomasAI.IFM.Application.ScheduledTask.SetClosingPrice.csproj` | Defines the .NET 7 worker, packages, project references, user-secrets identity, and settings copy behavior. |
| `appsettings.json` | Required base Serilog configuration. |
| `appsettings.Development.json` | Development command/query service endpoints. |
| `appsettings.Production.json` | Production command/query service endpoints. |
| `Properties/launchSettings.json` | Local .NET project launch profile. |
| `Properties/PublishProfiles/FolderProfile.pubxml` | Folder-publish settings. |
| `Docs/SetClosingPrice-Implementation-Details.md` | This document. |

## Host startup and dependency injection

`Program.cs` uses `Host.CreateDefaultBuilder(args)` and loads required base settings plus an optional environment-specific file from the current working directory. Both files use reload-on-change.

The service container registers:

| Registration | Implementation/use |
| --- | --- |
| Logging | Serilog configured from application settings. |
| Named non-generic logger | `IFM-ScheduledTask-SetClosingPrice`. |
| `IRestApiSerializer` | `NewtonSoftJsonSerializer`. |
| `ICommandServiceRestApiOptions` | Reads `AppSettings:CommandServerBaseUri`. |
| `ICommandService` | `CommandServiceRestApiClient`. |
| `IMarketDataFeedCommandApi` | `MarketDataFeedCommandApi`, used to insert closing prices. |
| `ITradePlacementCommandApi` | `TradePlacementCommandApi`, used to stop ES trade placement. |
| `IQueryServiceRestApiOptions` | Reads `AppSettings:QueryServerBaseUri`. |
| `IQueryService` | `QueryServiceRestClientApi`. |
| `IMarketDataFeedQueryApi` | `MarketDataFeedQueryApi`, used to obtain the last futures tick. |
| `IMarketDataQueryApi` | `MarketDataQueryApi`, used for value date and traded contracts. |
| Hosted service | `Worker`. |

## Execution workflow

```text
Host starts Worker
  └─ Query current value date
       ├─ Failure/null: log error
       └─ Success
            └─ Query currently traded futures contracts
                 ├─ Failure/empty: log error
                 └─ For each contract, sequentially
                      ├─ Query last tick at value-date 16:00
                      │    ├─ Success: insert closing price
                      │    └─ Failure: log error
                      └─ If ContractId starts with "ES"
                           └─ Stop trade placement
  └─ Stop host in finally
```

Detailed behavior:

1. Call `IMarketDataQueryApi.GetValueDateAsync()` and require a successful, non-null value.
2. Call `GetCurrentlyTradedFuturesContractsAsync()` and require a successful, non-empty array.
3. Construct one `tickDate` as `valueDate.Date.AddHours(16)`, representing an assumed normal-session close at 4:00 p.m.
4. For each contract, call `GetLastFuturesTickDataAsync(contractId, tickDate)`.
5. On a successful, non-null tick, use its `Price` and call `InsertFuturesClosingPriceAsync` with an ID containing the contract and `tickDate.Date`.
6. Log insertion success or service error.
7. Independently of tick/insert success, when the contract ID begins with the case-sensitive prefix `ES`, create a `TradePlacementId` from the contract and value date and call `StopTradePlacementAsync`.
8. Continue sequentially through all contracts.
9. Always stop the host from the worker's `finally` block.

The worker catches and logs execution exceptions. Once the host exits, `Program.cs` kills the current process in its outer `finally` block.

## Configuration surface

| Key | Required by code | Purpose |
| --- | --- | --- |
| `AppSettings:CommandServerBaseUri` | Yes | Base URI for closing-price insert and trade-placement-stop commands. |
| `AppSettings:QueryServerBaseUri` | Yes | Base URI for value-date, contract, and last-tick queries. |
| `Serilog:*` | Required for configured logging behavior | Sink, minimum-level, enrichment, and application metadata. |
| `DOTNET_ENVIRONMENT` | Optional host input | Selects the environment-specific settings file. |

## Project and deployment definition

- SDK: `Microsoft.NET.Sdk.Worker`
- Target framework: `net7.0`
- Nullable reference types: enabled
- Implicit usings: enabled
- Hosting package: `Microsoft.Extensions.Hosting` 3.1.22
- REST package: RestSharp 106.10.1
- Logging packages: Serilog ASP.NET Core 3.2.0, Console 3.1.1, File 4.1.0
- Publish profile: folder protocol, .NET 7 target, non-self-contained
- Settings copy policy: Development settings are explicitly preserved in output and publish directories; other settings rely on SDK default content handling

## Operational characteristics and current limitations

- **External scheduling is required.** The executable has no internal clock or recurrence.
- **The task is one-shot and sequential.** Contract operations are awaited one at a time; task duration grows with contract count and service latency.
- **Top-level failures are hidden.** The outer `catch` in `Program.cs` is empty.
- **Termination is forced.** The process is killed after host exit.
- **Cancellation is not propagated.** `stoppingToken` is unused by queries, commands, or the contract loop.
- **The close time is fixed.** Every contract uses 16:00 on the value date, with no exchange, product, holiday, early-close, or timezone adjustment.
- **Date/time kind is implicit.** `valueDate.Date.AddHours(16)` does not establish a timezone or UTC conversion in this worker.
- **Closing-price persistence is not retried.** Query or insert failures are logged and processing moves to the next contract.
- **ES trade placement can stop without a closing price.** The prefix check runs even when the tick query or price insertion fails.
- **The ES selection rule is string-based and case-sensitive.** It uses `ContractId.StartsWith("ES")` without an explicit comparison mode.
- **A single unexpected exception stops the remaining batch.** The outer try/catch surrounds the entire loop rather than each contract.
- **Project references are stale.** The command/query REST-client project-reference paths do not currently exist.
- **The project is outside the current solution.** It is not listed in `TomasAI.IFM.sln`, so solution builds do not validate it.

## Safe extension points

1. Resolve close time from exchange/session calendars and an explicit timezone.
2. Pass `stoppingToken` through supported client calls and check it between contracts.
3. Decide whether trade placement should stop only after closing-price persistence succeeds, and encode that policy explicitly.
4. Add bounded retries or a recoverable failure queue for transient query/command failures.
5. Isolate per-contract exceptions so one malformed or unavailable contract does not abort the entire batch.
6. Replace the empty top-level catch and forced kill with observable graceful shutdown.
7. Repair project references before standalone builds or solution inclusion.
