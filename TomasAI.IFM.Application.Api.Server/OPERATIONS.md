# API server operations

## Process roles

`ApiHost:Role` defines the responsibilities of each process:

- `Gateway` exposes command and query HTTP endpoints and publishes directly to NATS. It does not start actors, feeds, projectors, runtime health endpoints, or domain hosted services.
- `Runtime` starts actors, feeds, projectors, and runtime health endpoints. It does not expose public command or query HTTP endpoints.
- `Combined` hosts both responsibilities for local development and compatibility.

Production defaults to `Gateway`. Deploy at least one separate runtime process with `ApiHost__Role=Runtime`. Development defaults to `Combined`.

## Schema initialization

Database schemas and catalogs are not migrated during normal server startup. Run the deployment step before starting gateway or runtime processes:

```powershell
dotnet TomasAI.IFM.Application.Api.Server.dll --initialize-schema-only
```

The command initializes every canonical schema, trade-strategy families, and the strategy catalog, then exits without binding HTTP or starting actors. Independent schemas run concurrently with a default maximum of four; trade-strategy families and the strategy catalog remain ordered after schema completion. Override the bound with `StartupOrchestration__MaximumSchemaInitializationConcurrency` (valid range 1-16).

## Actor startup orchestration

Actor initialization is bounded to eight concurrent actors by default so recovery and dependency startup cannot create an unbounded database or messaging burst. Configure the bound with `ActorRuntime__Startup__MaximumConcurrency` (valid range 1-64).

The host allows two minutes for the entire actor-startup phase by default. Configure the deadline with `StartupOrchestration__ActorStartupTimeout`. The deadline is linked to application shutdown. External consumer subscriptions do not open until every actor has initialized; independent subscriptions then open concurrently. Actor readiness remains false until both phases succeed, and a failure stops the supervisor and leaves the host unready.

## Logging

File logging is asynchronous and bounded. When its 4,096-event buffer is full, new log events are dropped instead of blocking request or actor threads. Console logging is enabled automatically in Development and can be explicitly enabled elsewhere with `Logging__Console__Enabled=true`.

Application lifecycle consumers wait on immutable status-change notifications rather than polling. Fixed schedules use one non-overlapping `PeriodicTimer` per hosted service; variable retry and market-boundary delays use `TimeProvider`-aware cancellable delays. Host shutdown cancels these waits and is treated as successful worker completion.

## Health and operational endpoints

- `/health/live` is a dependency-free process liveness probe.
- `/health/bootstrap` reports deployment, portfolio controls, and actor startup readiness where the process hosts actors.
- `/health/launch-ready` reports dependencies required to launch the application workflow.
- `/health/ready` reports full operational readiness.
- `/health/actors` reports actor-runtime readiness only.

Successful and unavailable health responses are cached in-process for one second. Operational snapshots under `/api/actor-health` and `/api/market-data/*health*` use the same one-second bound. This absorbs probe storms while limiting status staleness to one second. Client and intermediary caching remains disabled by the health-check response headers.

Deployment artifact hashes are validated at startup and by the one-minute enforcement loop. Request-time health checks read the last immutable validation result; they do not open or hash deployed binaries.

## Option expiry refresh

Option-root provider requests and published-cache read-back verification are bounded independently. Defaults are configured under `AppSettings:Databento:OptionExpiryCalendar`:

- `MaximumProviderConcurrency`: 2, valid range 1-16. This matches the default DataBento query-worker count.
- `MaximumVerificationConcurrency`: 4, valid range 1-16.
- `EnableDiagnosticWindowBenchmark`: false.

Refreshes for the same symbol remain serialized to prevent competing cache publication, while different symbols may refresh concurrently. The diagnostic window benchmark performs additional price, EOD, and cache queries and should only be enabled during targeted operational investigation.
