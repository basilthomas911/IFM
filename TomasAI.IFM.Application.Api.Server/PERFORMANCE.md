# API server performance verification

Measured on 2026-09-26 with .NET 10.0.10 on a 16-core/32-thread Ryzen Threadripper 1950X. Both whole-process runs used Release builds, 32 concurrent clients, an 18-second load window, and `GET /api/actor-health`.

## Request latency

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Requests/second | 1,817.50 | 1,925.10 | +5.9% |
| Mean | 17.57 ms | 16.60 ms | -5.5% |
| p50 | 16.19 ms | 15.70 ms | -3.0% |
| p90 | 25.94 ms | 23.17 ms | -10.7% |
| p95 | 30.04 ms | 26.31 ms | -12.4% |
| p99 | 42.17 ms | 34.05 ms | -19.3% |
| Maximum | 108.46 ms | 79.57 ms | -26.6% |

The endpoint is a live runtime snapshot rather than a fixed payload. The sampled response grew from approximately 113 KB before to 291 KB after because runtime state differed, so whole-process allocation and contention rates are directional diagnostics rather than an apples-to-apples serializer comparison.

## Runtime counters and allocation profile

| Metric | Before | After |
| --- | ---: | ---: |
| Average working set | 434.1 MB | 378.9 MB |
| Live managed heap | 65.2 MB | 52.4 MB |
| Live managed objects | 582,955 | 501,108 |
| Allocation rate | 197.1 MB/s | 256.8 MB/s |
| Gen 0 / 1 / 2 collections | 54 / 21 / 2 | 57 / 28 / 1 |
| Total GC pause time | 253 ms | 482 ms |
| Maximum thread-pool queue | 68 | 11 |
| Lock-contention events/second | 827.7 | 1,154.5 |

The larger live response explains much of the higher allocation, GC-pause, and lock-event rate during the after run. The GC dump nevertheless retained 19.7% fewer managed bytes and 14.0% fewer objects after the load.

## BenchmarkDotNet serializer isolation

This benchmark serializes the same representative supervisor snapshot in both cases, isolating JSON metadata from changing runtime state.

| Serializer metadata | Mean | Allocated/operation | Relative time | Relative allocation |
| --- | ---: | ---: | ---: | ---: |
| Reflection | 618.9 us | 358.75 KB | 1.00 | 1.00 |
| Source generated | 444.4 us | 335.82 KB | 0.72 | 0.94 |

Source-generated metadata reduced mean serialization time by 28.2% and allocation by 6.4%.

## Gateway process split

A Production `Gateway` process reached healthy readiness with no actor/runtime endpoints, runtime hosted services, or SimpleInjector actor discovery. Its post-start working set was 131.1 MB with 71.1 MB private memory and 59 operating-system threads. The runtime-only registration trim reduced gateway working set from an intermediate 172.3 MB measurement to 131.1 MB.

## Health and operational paths

All HTTP comparisons used the same Release build host, 32 concurrent clients, and an 18-second request window. Bootstrap results below use the runs collected concurrently with `dotnet-counters` and `dotnet-trace`.

| Path and metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Bootstrap requests/second | 1,092.6 | 98,723.9 | 90.4x |
| Bootstrap mean | 29.238 ms | 0.323 ms | -98.9% |
| Bootstrap p95 | 34.941 ms | 0.634 ms | -98.2% |
| Bootstrap p99 | 37.191 ms | 1.362 ms | -96.3% |
| Operations requests/second | 67,947.8 | 110,912.0 | +63.2% |
| Operations mean | 0.470 ms | 0.288 ms | -38.8% |
| Operations p95 | 0.777 ms | 0.398 ms | -48.7% |
| Actor-health requests/second | 2,492.5 | 2,924.0 | +17.3% |
| Actor-health mean | 12.819 ms | 10.927 ms | -14.8% |
| Actor-health p95 | 26.487 ms | 17.203 ms | -35.0% |
| Actor-health p99 | 47.357 ms | 22.839 ms | -51.8% |

The bootstrap trace previously attributed 43.6% inclusive CPU to `DeploymentIdentityMonitor.CaptureArtifactHashes`, including 38.0% exclusive CPU reading deployed binaries. Neither file reading nor deployment hashing appears in the after trace.

The after run processed approximately 90 times as many requests, so absolute allocation and GC counts increased. Normalized allocation fell from approximately 28.4 KB to 4.75 KB per bootstrap request (-83.3%). Lock-contention events per request fell approximately 5.7%, average working set fell from 392.7 MB to 307.0 MB, and the live managed heap remained effectively flat at 47.4 MB versus 47.2 MB.

BenchmarkDotNet isolated the eliminated work:

| Operation | Before | After | Allocation before | Allocation after |
| --- | ---: | ---: | ---: | ---: |
| Deployment identity request read | 3.056 ms | 0.781 ns | 13,174 B | 0 B |
| Representative operational response | 351.282 us | 0.849 ns | 171,965 B | 0 B |

The cached-response microbenchmark measures reuse of an already serialized body; the end-to-end HTTP results above include output-cache lookup, response copying, Kestrel, and socket costs.

## Trace observations

The focused after trace retained runtime GC, threading, contention, and sampled-profiler events. Exclusive samples moved as follows:

- `LowLevelLifoSemaphore.WaitForSignal`: 36.29% to 31.82%.
- `Monitor.Enter_Slowpath`: 9.66% to 11.30%.
- `ActorMailboxId` JSON converter: 2.71% to 2.07%.
- `SupervisorRuntimeContext.Capture`: 0.98% to 0.89%.
- `ActorMetricsStore.CaptureSnapshot`: 0.87% to 0.46%.

The remaining monitor contention is predominantly in runtime/thread-pool scheduling under the concurrent response-write workload; it is not treated as a demonstrated improvement in this change set.

## Polling, timers, and threading

Measured on the same .NET 10 host with BenchmarkDotNet. Each lifecycle benchmark publishes the terminal state from a one-shot timer after 1 ms. The before implementation can only observe it on its next 100 ms poll; the after implementation resumes from the store's change notification. Timer-delay benchmarks compare the former custom `TaskCompletionSource`/`ITimer` graph with the runtime `Task.Delay` TimeProvider path now used by variable-delay workers.

| Operation | Before | After | Change | Allocation before | Allocation after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Lifecycle observation | 108.755 ms | 14.674 ms | -86.5% | 832 B | 800 B |
| Zero-delay timer completion | 38.562 us | 1.243 us | -96.8% | 392 B | 0 B |

The notification path removes ten lifecycle-store reads and ten timer wake-ups per second while a handoff is pending. Option-calendar startup similarly changes from four reads/timers per second to one change notification. Fixed-interval FMP import and rollover preparation now retain one `PeriodicTimer` for the worker lifetime instead of building a fresh one-shot delay graph for each interval. Deployment enforcement's existing `PeriodicTimer` is now driven by the injected `TimeProvider` as well. All workers remain single-consumer and awaited, so timer callbacks cannot overlap their work. The BenchmarkDotNet threading diagnostics recorded two completed thread-pool work items for the former custom zero-delay timer and none for the runtime zero-delay path.

The lifecycle figures include Windows timer granularity for the synthetic 1 ms publication. They demonstrate the removed polling floor; they are not presented as request latency or database latency.

## Startup and actor orchestration

Measured on the same .NET 10 host with BenchmarkDotNet's in-process toolchain. The benchmark models independent startup operations with a fixed asynchronous delay so it isolates orchestration rather than database or NATS variability.

| Operation | Before | After | Change | Allocation before | Allocation after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Eight consumer subscriptions | 122.69 ms | 13.66 ms | -88.9% | 1.48 KB | 3.78 KB |
| Thirteen schema initializers | 201.09 ms | 60.24 ms | -70.0% | 2.30 KB | 4.40 KB |

Consumer subscriptions now open concurrently after every actor has initialized. Independent schema initializers run with a configurable maximum concurrency of four; the dependent strategy-family and strategy-catalog steps remain ordered after schemas complete. The additional short-lived allocation is the coordination state for fan-out and is confined to process or deployment startup.

Actor initialization itself changed from unbounded fan-out to a configurable maximum of eight. That is a resource-safety change, not a latency optimization: it caps simultaneous actor recovery, database, and messaging work. A deterministic concurrency test with twelve actors and a configured maximum of three observed exactly three concurrent initializations. Readiness remains false until all actors and all consumers have started, and any failure retains the existing rollback behavior.

## Option refresh and request hot paths

Measured on the same .NET 10 host with BenchmarkDotNet's in-process toolchain. The option fan-out case models the 31 ES option roots with equal asynchronous provider latency. The mapping case resolves 10,000 expiries against a 24-contract ordered futures curve.

| Operation | Before | After | Change | Allocation before | Allocation after |
| --- | ---: | ---: | ---: | ---: | ---: |
| 31 option-root loads | 480.6 ms | 248.1 ms | -48.4% | 5.26 KB | 9.26 KB |
| 10,000 expiry mappings | 551.2 us | 391.0 us | -29.1% | 880,000 B | 0 B |
| Unused HTTP-context tracking per request | 39.06 ns | 0.79 ns | -98.0% | 96 B | 0 B |

Provider loads and published-cache verification now use separately configurable concurrency bounds of two and four respectively. The provider bound matches the two native DataBento query workers. Different symbols have independent refresh gates, so one slow symbol no longer blocks another. Each refresh captures one timestamp, maps definitions to the ordered futures curve with binary search, and omits the former live-price/EOD/window diagnostic queries unless diagnostics are explicitly enabled. The extra fan-out allocation is short-lived coordination state scoped to refresh; concurrency remains bounded.

A source-generated JSON candidate for the instrument-definition request was measured and rejected: on the representative PascalCase payload it increased mean parse time from 992.7 ns to 1,436.9 ns and allocation from 248 B to 472 B. The endpoint retains its existing reflection path.
