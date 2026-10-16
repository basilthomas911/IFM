# API-server hot-path optimization measurements

Measured on 2026-10-16 against frozen reference implementations from commit `51075e3eb`. The production changes are in the API health checks, shared realtime snapshot calculations, trade-cache registry, and actor completion observer. These are ten measured opportunities in the inspected paths, not a claim that a whole-server profiler ranked every API operation.

## Results

Times are BenchmarkDotNet means. Allocation is managed bytes per operation. Concurrent cases report elapsed batch time divided by the number of completed operations, so their numbers represent aggregate throughput cost, **not individual request latency**. Improvements overlap and must not be added together.

| # | Production change and measured workload | Before | After | Speedup | Bytes/op before → after |
|---|---|---:|---:|---:|---:|
| 1 | Skip qualification until the next quote/risk deadline; fresh four-leg idle expiry | 1,061.900 ns | 14.874 ns | 71.4× | 2,320 → 0 |
| 2 | Qualify quotes, provenance and five signed Greeks in one pass; changed quote metrics | 989.564 ns | 129.402 ns | 7.65× | 2,320 → 368 |
| 3 | Calculate two spreads without LINQ arrays, sets, delegates or a temporary list | 941.9 ns | 260.5 ns | 3.62× | 1,568 → 312 |
| 4 | Direct indexed contract lookup in a four-leg immutable array | 26.148 ns | 7.899 ns | 3.31× | 40 → 0 |
| 5 | Publish contract-route arrays on membership changes; duplicate quote fanout to eight trades | 453.695 ns | 157.554 ns | 2.88× | 456 → 0 |
| 6 | ConcurrentDictionary trade lookup without the registry-wide lock; four readers | 116.788 ns | 2.967 ns | 39.4× | 0 → 0 |
| 7 | Reuse resident-cache membership arrays; idle expiry scan of 1,024 trades | 54,811.947 ns | 49,426.801 ns | 1.11× | 8,320 → 0 |
| 8 | Skip the completion-observer lock when observation is disabled; four readers | 105.499 ns | 1.727 ns | 61.1× | 0 → 0 |
| 9 | Reuse completed readiness health result while readiness/count are unchanged | 83.835 ns | 4.863 ns | 17.2× | 336 → 0 |
| 10 | Reuse completed lifecycle health result while lifecycle/handoff records are unchanged | 732.189 ns | 6.950 ns | 105.4× | 1,520 → 0 |

Items 1–5, 7, 9 and 10 reduce allocation pressure. Item 7 deliberately isolates registry overhead: both reference and optimized registries contain the **same current production cache**. Item 5 uses the same arrangement. Consequently their improvements do not double-count the deadline optimization.

The measured Gen0 collections per 1,000 operations fell from 0.0286 to 0 for idle expiry, 0.0286 to 0.0048 for qualification, 0.0191 to 0.0038 for spread calculations, 0.0057 to 0 for duplicate routing, 0.0610 to 0 for registry expiry, 0.0043 to 0 for readiness, and 0.0191 to 0 for lifecycle health. These measurements do not establish process-wide heap size, GC pause duration, endpoint p95/p99 latency, or HTTP requests/second.

## Implementation and correctness

- `TradeRealtimeSnapshotCalculations` contains the production calculation methods used directly by the benchmarks. Qualification preserves signed quantities, quote generation/skew checks, freshness, scenario provenance, and nullable unavailable values. It reuses immutable records only when their values are unchanged. Spread math preserves the original checked quantity sum and arithmetic short-circuiting.
- `TradeRealtimeHotCache` retains its per-trade writer lock and atomically published immutable snapshots. Every accepted input reschedules the earliest remaining quote/risk deadline. Expiry still triggers at the exact deadline without requiring another tick. Client mirrors retain server revisions and their own quality revisions.
- `TradeRealtimeCacheRegistry` retains serialized membership writes and per-cache retirement fences. Routing arrays change only when membership changes; readers never modify them. Find and quote routing no longer acquire the registry-wide lock. An invalidated resident array is rebuilt on the next expiry scan. These changes trade a bounded amount of persistent routing metadata for lower per-tick allocations; membership churn is not the benchmarked workload.
- `RealtimeActorCompletionEvidence.Record` reads the active target before locking, then rechecks target identity inside the lock before crediting an armed observation. Counts, timestamps, arm/disarm and capture remain serialized.
- Health checks still expose `Task<HealthCheckResult>`. They reuse completed tasks and read-only data, checking the current state on every call. There is no TTL that can hide a readiness or handoff failure. No production `Task.Run`, blocking wait, new thread, or sync-over-async was introduced.

Release build of the benchmark project and its API-server dependency graph passed with **zero warnings and zero errors**. The following selected regression suites passed: **92 tests, zero failures, zero skips**. This is targeted validation of the changed paths, not a claim that the entire repository test suite ran.

| Suite | Passed | Coverage |
|---|---:|---|
| Shared actor pool / completion observer | 34 | Exact parallel completion counts; disabled/armed/replaced targets; arm races; existing actor completion behavior |
| API lifecycle / readiness health | 4 | Immediate readiness, registry count, lifecycle and handoff invalidation; cached task reuse; read-only cached data |
| Trade monitoring / realtime pipeline | 32 | Concurrent registry membership churn and reads; retired-cache fencing; quote pricing completion; monitor lifecycle and scenario snapshots |
| UI bridge / whole-trade cache | 22 | Exact expiry/replacement deadlines; zero-allocation idle polling; checked quantity overflow; mirror revisions; trade plan display; real NATS round trip and 200-update catch-up |

The broader trade tests exposed a pre-existing stale assertion in `IronCondorMonitoringTriggerTests`: it forbade `PublishRealtimeAsync` even though the baseline production handler already calls it. The assertion now requires exactly that publication and still rejects unrelated calls, including order dispatch. No production monitoring behavior was changed to accommodate the test.

The equivalence runner passed 1,000 seeded randomized snapshot comparisons **and** 1,000 sequential accepted quote updates through complete baseline/current caches. It compares serialized whole snapshots after every update, normalizing only independent monitor IDs.

Reproduce the regression checks from the repository root:

```powershell
dotnet test TomasAI.IFM.Shared.UnitTests -c Release --filter 'FullyQualifiedName~RealtimeCompletionOptimizationTests|FullyQualifiedName~ActorThreadPoolV2'
dotnet test TomasAI.IFM.Domain.Application.UnitTests/TomasAI.IFM.Domain.Application.Actor.UnitTests.csproj -c Release --filter 'FullyQualifiedName~HealthCheck|FullyQualifiedName~ActorReadinessHealthCacheTests'
dotnet test TomasAI.IFM.Domain.Trade.UnitTests -c Release --filter 'FullyQualifiedName~TradeRealtimePipelineTests|FullyQualifiedName~IronCondorMonitoring|FullyQualifiedName~FuturesOptionRealtimeActorTests'
$env:IFM_TRADE_CACHE_NATS_TEST = '1'
dotnet test TomasAI.IFM.UI.Rust.SystemTests -c Release --filter 'FullyQualifiedName~TradeRealtime|FullyQualifiedName~TradeMonitorUpdatesTests|FullyQualifiedName~TradePlanDisplayTests'
```

The NATS test uses the configured local transport with a unique test actor subject. It does not submit trades or alter broker state. The other selected tests use isolated fixtures.

## Measurement method and reproducibility

BenchmarkDotNet 0.15.8; Release; .NET SDK 10.0.302; .NET runtime 10.0.10; Windows 10 22H2; AMD Ryzen Threadripper 1950X, 16 physical / 32 logical cores. The job explicitly enables server GC, uses one out-of-process launch, three warmups and eight measured iterations, with MemoryDiagnoser and ThreadingDiagnoser. See the generated reports for confidence intervals and outlier handling. Existing application processes were left running; build/tests were not run concurrently with measurement. This is a local development machine, not an isolated performance laboratory.

The four-reader cases use four persistent dedicated threads, two barriers per batch, and 4,096 operations per worker. `OperationsPerInvoke=16,384` normalizes the batch. Barrier costs are included on both sides. No per-operation Task.Run/thread-pool scheduling is hidden in the comparison. ThreadingDiagnoser observed contention on the old shared-lock paths and no contention at its reported precision on the corresponding new paths. Its zero thread-pool work count is expected for dedicated workers. Allocation counters and contention diagnostics are not a substitute for process-wide tracing of all threads.

Reference code in `Baselines/` is benchmark-only. Namespace/class visibility changes allow it to coexist with production code. The qualification and spread cases call matching calculation bodies; registry cases isolate registry changes using identical current cache implementations. Leg lookup uses the same pre-change LINQ search shape, with a cached benchmark-instance predicate rather than the production call's extra captured locals; that allocation result is conservative. Health setup exercises the actual readiness flag without constructing actor intake services. Setup checks JSON equality of full before/after snapshots after initial inputs, normalizing only their independently generated monitor IDs. `--verify-api-optimizations` additionally checks qualification and spread outputs for 1,000 seeded randomized reachable snapshot states.

```powershell
dotnet build TomasAI.IFM.Application.Api.Server.Benchmarks -c Release
dotnet run --project TomasAI.IFM.Application.Api.Server.Benchmarks -c Release --no-build -- --verify-api-optimizations
dotnet run --project TomasAI.IFM.Application.Api.Server.Benchmarks -c Release --no-build -- --filter '*ApiHotPathOptimizationBenchmarks*' --artifacts BenchmarkDotNet.Artifacts/api-server-hot-path --exporters json --buildTimeout 600
dotnet run --project TomasAI.IFM.Application.Api.Server.Benchmarks -c Release --no-build -- --filter '*ApiHotPathOptimizationBenchmarks.SpreadMath*' --artifacts BenchmarkDotNet.Artifacts/api-server-hot-path-spread-checked --exporters json --buildTimeout 600
```

The first attempt hit BenchmarkDotNet's default generated-build timeout before measuring any cases. The successful retry used a 600-second build timeout and completed all 20 cases. [Main generated report](Results/2026-10-16/main.md), [CSV](Results/2026-10-16/main.csv), and [raw measurements/environment JSON](Results/2026-10-16/main.json) preserve that run. Item 3 in the summary uses the subsequent two-case run after the checked-arithmetic correction: [final spread report](Results/2026-10-16/spread-checked.md), [CSV](Results/2026-10-16/spread-checked.csv), [raw JSON](Results/2026-10-16/spread-checked.json). The original spread measurements in the main raw run are superseded; the other nine pairs are unchanged. Both successful runs executed every selected benchmark.

These results support the changes for the stated workloads. Their effect on production depends on quote rates, trade count, duplicate rates, reader concurrency, health polling, and membership churn. The applications have not been restarted or deployed by this work. Sunday live-market verification remains a separate check of feed-to-screen behavior.
