# IFM System-Wide Optimization Results

**Document type:** Living implementation and measurement record
**Status:** Active
**Created:** 2026-08-09
**Last updated:** 2026-08-09
**Baseline commit:** `32d025c8`
**Current work package:** SWO-01, Operational metrics export and stage timing

## 1. Executive result

SWO-01 now has a production-capable OpenTelemetry/OTLP export path, low-cardinality actor and NATS instruments, detailed actor processing-stage timing, focused correctness tests, and a BenchmarkDotNet overhead measurement. The complete domain integration gate passes with 193 of 193 tests across all ten domain integration projects.

The code tranche is implemented and verified, but SWO-01 remains in **Measuring** status until a production-like collector and paper-trading run provide the required p95/p99 attribution and exporter/load evidence. This document distinguishes implemented evidence from work that still requires a live topology.

## 2. Implemented scope

### 2.1 Host metrics export

`TomasAI.IFM.Framework.Telemetry` now provides `AddIfmMetrics`. The API server registers the pipeline during service configuration.

The pipeline exports through OpenTelemetry Protocol (OTLP) and subscribes to:

- `TomasAI.IFM.Shared.EventModelActor`;
- `TomasAI.IFM.Framework.Messaging.Nats`;
- `System.Runtime`;
- `Microsoft.AspNetCore.Hosting`;
- `Microsoft.AspNetCore.Server.Kestrel`;
- `System.Net.Http`; and
- `System.Net.NameResolution`.

Production configuration enables OTLP/gRPC to `http://localhost:4317`. Non-production configuration leaves metrics disabled by default. All values can be overridden through normal .NET configuration, including environment variables:

```text
Telemetry__Metrics__Enabled=true
Telemetry__Metrics__ServiceName=TomasAI.IFM.Application.Api.Server
Telemetry__Metrics__OtlpEndpoint=http://localhost:4317
Telemetry__Metrics__OtlpProtocol=grpc
```

`OtlpProtocol` accepts `grpc` or `http/protobuf`. The endpoint must be an absolute URI when metrics are enabled.

### 2.2 Actor runtime instruments

| Instrument | Type | Dimensions | Meaning |
| --- | --- | --- | --- |
| `ifm.actor.messages.accepted` | Counter | `actor.type` | Messages admitted to actor mailboxes. |
| `ifm.actor.messages.processed` | Counter | `actor.type` | Handlers that completed normally. |
| `ifm.actor.messages.failed` | Counter | `actor.type` | Exceptions escaping actor handlers. |
| `ifm.actor.messages.canceled` | Counter | `actor.type` | Handler cancellation by the actor-runtime token. |
| `ifm.actor.mailbox.depth` | Up/down counter | `actor.type` | Current queued message count. |
| `ifm.actor.mailbox.active` | Up/down counter | `actor.type` | Current active entity-mailbox count. |
| `ifm.actor.ready_queue.depth` | Up/down counter | `actor.type` | Scheduled mailboxes waiting for a worker. |
| `ifm.actor.mailbox.enqueue_wait.duration` | Histogram, ms | `actor.type` | Time waiting for mailbox capacity. |
| `ifm.actor.mailbox.queue_wait.duration` | Histogram, ms | `actor.type` | Accepted-message age when dequeued. |
| `ifm.actor.handler.duration` | Histogram, ms | `actor.type` | End-to-end actor handler execution. |
| `ifm.actor.stage.duration` | Histogram, ms | `actor.type`, `stage` | Processing-stage elapsed time. |
| `ifm.actor.stage.failures` | Counter | `actor.type`, `stage` | Failures handled within a domain actor stage. |

The bounded `stage` values are `validation`, `replay`, `execution`, `persistence`, `reply`, `publication`, and `denormalization`.

Mailbox timestamps are stored in a value-type queue envelope. The clock is read only when the corresponding histogram is enabled. The active-mailbox gauge is protected against unstarted-dispose and concurrent stop/retire paths, preventing negative or duplicate lifecycle measurements.

### 2.3 NATS instruments

Existing publish, receive, dispatch-failure, duplicate-suppression, and listener-only counters remain registered. This tranche adds:

| Instrument | Type | Dimensions | Meaning |
| --- | --- | --- | --- |
| `ifm.nats.operation.duration` | Histogram, ms | `operation` | Core publish, Core request/reply, or acknowledged JetStream publish latency. |
| `ifm.nats.operation.failures` | Counter | `operation` | Failed publish or request operations. |

The bounded `operation` values are `core_publish`, `core_request`, and `jetstream_publish`.

### 2.4 Cardinality contract

Metric tags may identify a bounded actor type, processing stage, or transport operation. Entity IDs, command IDs, event IDs, stream IDs, subjects, contract IDs, symbols, and exception text are deliberately excluded. Those values belong in trace or structured-log context, not metric dimensions.

## 3. Benchmark evidence

### 3.1 Method

`ActorMetricsBenchmarks` performs 256 scheduled mailbox enqueue/dequeue operations per invocation against the same in-process actor queue. It measures the instrumented implementation with no `MeterListener` and with a listener subscribed to all actor instruments.

Reproduce it with:

```powershell
dotnet run --project TomasAI.IFM.Framework.Messaging.Nats.Benchmarks -c Release -- --filter "*ActorMetricsBenchmarks*"
```

BenchmarkDotNet configuration:

- BenchmarkDotNet 0.15.8;
- .NET SDK 10.0.302;
- .NET runtime 10.0.10, x64 RyuJIT;
- Windows 10.0.19045;
- in-process toolchain;
- 3 warmups and 8 measurement iterations;
- memory and threading diagnosers.

The environment denied the processor-identification query, so the report records the processor as unknown. This result should therefore be compared only with later runs on the same host and configuration unless processor details are captured independently.

### 3.2 Result

| Metrics listener | Mean per mailbox operation | Error | Standard deviation | Allocated | Lock contentions |
| --- | ---: | ---: | ---: | ---: | ---: |
| Disabled | 80.172 ns | 0.395 ns | 0.206 ns | 0 B reported | 0 reported |
| Enabled | 204.843 ns | 0.860 ns | 0.450 ns | 0 B reported | 0 reported |

An active listener adds approximately **124.671 ns per measured mailbox operation**, or 2.555 times the dormant-path time in this deliberately small microbenchmark. The absolute incremental cost extrapolates to about 12.47 milliseconds of one CPU core per second at 100,000 mailbox operations per second. This extrapolation excludes collector aggregation, export, domain handler work, NATS, storage, and operating-system scheduling, so it is a capacity estimate rather than an end-to-end claim.

The dormant path reported no per-operation allocation and reads no instrumentation clock when its histogram is disabled. A previously captured actor-queue report showed 76.524 ns for its scheduled hot-path benchmark, but its operation shape differs from `ActorMetricsBenchmarks`; it is retained as context and is not treated as a valid before/after ratio.

Benchmark artifacts are generated under `BenchmarkDotNet.Artifacts/results/` and are intentionally not source-controlled.

## 4. Validation evidence

### 4.1 Focused tests

The complete `TomasAI.IFM.sln` Release build succeeds with 0 warnings and 0 errors.

| Project | Result |
| --- | ---: |
| `TomasAI.IFM.Shared.UnitTests` | 67/67 passed |
| `TomasAI.IFM.Framework.Messaging.Nats.UnitTests` | 34/34 passed |
| `TomasAI.IFM.Domain.MarketData.UnitTests` | 47/47 passed |

The focused coverage includes instrument publication, mailbox flow, lifecycle gauge balancing, existing actor runtime behavior, NATS behavior, and deterministic MarketData value-date cases.

### 4.2 Full domain integration gate

The final Release run built and executed every domain integration project sequentially with no restore.

| Domain integration project | Passed | Failed |
| --- | ---: | ---: |
| Domain.Application.Actor | 1 | 0 |
| Domain.Fund | 26 | 0 |
| Domain.MarketData.Analytics | 25 | 0 |
| Domain.MarketData.Feed | 44 | 0 |
| Domain.MarketData | 4 | 0 |
| Domain.MarketData.Securities | 14 | 0 |
| Domain.OptionPricer | 8 | 0 |
| Domain.Reference | 31 | 0 |
| Domain.SystemAdmin | 1 | 0 |
| Domain.Trade | 39 | 0 |
| **Total** | **193** | **0** |

TRX evidence for the exact-final-binary run is generated under `TestResults/SystemWideOptimizationFinal/` and is intentionally not source-controlled.

### 4.3 Defect found by the full gate

The first full run exposed a deterministic Sunday-before-market-open failure in `GetValueDate`. The implementation treated all Sunday times as the Monday value date while the integration contract expects no value date until the futures session opens at 18:00.

The calculation is now isolated in a deterministic internal method and covers Saturday, Sunday before and after 18:00, Monday before and after 18:00, and Friday after 18:00. The repaired MarketData integration suite passes 4/4 in the final full run.

## 5. Dashboard and paper-trading procedure

The initial dashboard should show these panels by bounded actor type and operation:

1. accepted, processed, failed, and canceled message rates;
2. mailbox depth, active mailbox count, and ready-queue depth;
3. p50/p95/p99 enqueue wait, queue wait, handler duration, and stage duration;
4. stage-failure rate;
5. NATS publish/request latency and operation failures;
6. NATS receive, dispatch-failure, duplicate-suppression, and listener-only rates;
7. process CPU, working set, allocation rate, GC collections/pauses, and thread-pool queue length;
8. ASP.NET Core/Kestrel request rate, active requests/connections, and latency where emitted by the selected runtime meters.

For a paper-trading capture:

1. start the OTLP collector and its time-series backend;
2. enable the API server metrics configuration before process startup;
3. record the commit, host, runtime, collector configuration, histogram aggregation, and test interval;
4. capture normal flow, market-open burst, reconnect/replay, and graceful shutdown periods separately;
5. correlate p95/p99 end-to-end latency with actor queue, handler stage, NATS, runtime, and storage signals;
6. record maximum backlog and whether it drains after each burst;
7. attach dashboard exports or stable queries to this document before changing SWO-01 to Complete.

Alert thresholds should be derived from the first representative paper-trading distribution. Until then, alert on sustained non-zero failure rates, monotonically growing mailbox/ready-queue depth, failure to drain after a burst, and runtime resource saturation rather than inventing latency thresholds without evidence.

## 6. Remaining SWO-01 evidence and gaps

The following items require a live collector, provider-specific integration, or paper trading and are not claimed as complete:

- prove OTLP delivery and dashboard visibility in the production-like deployment;
- capture p95/p99 attribution during representative market traffic;
- validate exporter and aggregation cost under sustained load;
- add provider-specific storage retry, timeout, and connection-pool-pressure instruments where the PostgreSQL and Scylla drivers do not already expose adequate meters;
- add an explicit NATS receive-to-actor-dispatch latency measurement if queue-wait plus transport operation latency cannot localize that boundary;
- decide from real backlog evidence whether a current oldest-message gauge is worth its additional mailbox bookkeeping; the implemented dequeue-age histogram currently supplies the historical age distribution.

These gaps do not block the next optimization work package from being designed, but SWO-01 should remain **Measuring** until the operational acceptance criteria are met.

## 7. References

- [OpenTelemetry .NET metrics documentation](https://opentelemetry.io/docs/languages/dotnet/metrics/)
- [OpenTelemetry.Extensions.Hosting package](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/)
- [OpenTelemetry OTLP exporter package](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol/)
- [Built-in .NET runtime metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime)
- `Documents/system/System-Wide-Optimization-Plan.md`

## 8. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.1 | 2026-08-09 | Recorded SWO-01 implementation, benchmark, focused tests, complete domain integration gate, defect correction, and remaining production-like measurements. |
