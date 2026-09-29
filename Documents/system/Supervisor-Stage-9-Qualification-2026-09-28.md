# Supervisor Stage 9 Qualification Record

**Date:** 2026-09-28  
**Scope:** Supervisor lifecycle, health collection, operational presentation, persistence, fault containment, and rollout safeguards  
**Automatic lifecycle mutation:** Disabled by default; explicit configuration is required

## Qualification outcome

The locally executable Stage 9 implementation and qualification gates pass. The production rollout gates remain deliberately separate: a wall-clock full trading-session observation and an observe-only deployment must be completed in the target environment before automatic lifecycle mutation is approved.

## Build and regression evidence

- `dotnet build TomasAI.IFM.sln -c Release --no-restore`: succeeded with zero warnings and zero errors.
- Full sequential solution matrix (`dotnet test TomasAI.IFM.sln -c Release --no-build --no-restore -m:1`): succeeded; every discovered runnable test passed. Explicitly skipped cases remain opt-in live/manual qualifications.
- Supervisor unit/fault-injection suite: 41/41 passed, including actor-source, state, logger, fallback logger, advisory sink, and action-coordinator failures without loss of polling liveness.
- 30-minute synthetic soak: passed with 128 actors, 50 ms accelerated polling, repeated healthy/degraded/critical/recovery phases, and an injected actor-collection exception every seventeenth observation.
- Regime Discovery verification: 71/71 passed through the production Kestrel/NATS actor host.
- MarkerProjector persistence/recovery family: 34/34 passed against its isolated PostgreSQL/NATS fixture.
- UI architecture baseline: 23/23 focused rules passed; volatility history remains off the UI caller thread without `Task.Run`.
- Supervisor shutdown/restart focus: 4/4 passed after event actors were changed to stop the exact producer instance they started.
- The final full solution matrix was run with `-m:1` so latency qualification tests and isolated container fixtures were not distorted by unrelated project-level contention. Notable infrastructure-backed results include Storage 574/574, Securities 16/16, MarketData 43/43, Feed 59/59, Analytics 46/46, OptionPricer 16/16, Reference 17/17, Trade 152/152, Portfolio 197/197, and Supervisor 41/41.

## Runtime profile

The 30-minute soak was sampled with `dotnet-counters` and `dotnet-trace`. The raw evidence is retained beside this record:

- `Supervisor-Stage9-after-counters.json`
- `Supervisor-Stage9-after.nettrace`

Observed two-minute counter window:

| Signal | Result |
| --- | ---: |
| Allocation rate | 394,046 bytes/second average; 418,080 maximum |
| Gen 0 collections | 0.084/second average; 1 maximum sample |
| Gen 1 collections | 0.008/second average; 1 maximum sample |
| Gen 2 collections | 0 |
| GC pause | 0 seconds average; 0.002 seconds maximum |
| Lock contentions | 0 |
| Working set | 69.5 MB average; 73.3 MB maximum |
| Thread-pool queue length | 0 |

The workload uses its dedicated polling thread, so no thread-pool queue growth was expected or observed.

## BenchmarkDotNet evidence

Artifacts are retained under `Supervisor-Stage9-BenchmarkDotNet`. Actor-health JSON source generation was compared with reflection metadata under the same .NET 10 job:

| Method | Mean | Allocated | Relative latency | Relative allocation |
| --- | ---: | ---: | ---: | ---: |
| Reflection metadata | 766.5 microseconds | 472.79 KB | 1.00 | 1.00 |
| Source-generated metadata | 602.8 microseconds | 449.87 KB | 0.79 | 0.95 |

Source-generated serialization reduced mean latency by approximately 21% and allocation by approximately 5% for the qualified aggregate payload.

## Faults found and corrected during qualification

- Scheduled-but-not-yet-observed mailboxes are now treated as active during retirement, preventing accepted work from being retired before processing begins.
- Automatic restart remains observe-only unless `Supervisor:AutomaticMutationEnabled` is explicitly true; both disabled and generation-fenced enabled paths are tested.
- The Regime Discovery verification host now disables the Development startup parameter snapshot so its database-published parameter sets and seeded signal cache cannot diverge.
- EventSource integration fixtures share one authoritative isolated infrastructure and initialize the schema before projector tests use it.
- Event, query, event-source command, and event-source function actors retain and stop the exact producer instance acquired at startup, avoiding registry-removal races during shutdown.
- The Databento instrument-identity lifecycle test now preserves the originating exception, performs deterministic cleanup, and allows a bounded ten-second lifecycle deadline under full-matrix load; the complete Databento unit suite passed 149/149 runnable tests both focused and in the full matrix.
- Processor readiness is established synchronously with `StartAsync`, removing a host-start race.
- Weekly/monthly ITI defaults and every corresponding test contract use the authoritative 10/30 trading-day values.

## Rollout gates not represented as local completion

The following are operational observations, not code-test substitutes:

1. Run the build in observe-only mode for one complete production trading session.
2. Retain the Supervisor history journal, heartbeat logs, incidents, GC/runtime counters, and trader-visible Actor Health evidence from that session.
3. Review the evidence and explicitly approve automatic lifecycle mutation in production.

Until those actions are complete, the system remains safely observe-only by default. The 30-minute accelerated soak executed about 36,000 collection cycles—more than twenty-six 23-hour sessions at the production 60-second cadence—but it is recorded as synthetic evidence, not falsely described as wall-clock production observation.
