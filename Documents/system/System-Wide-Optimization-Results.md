# IFM System-Wide Optimization Results

**Document type:** Living implementation and measurement record
**Status:** Active
**Created:** 2026-08-09
**Last updated:** 2026-08-13
**Baseline commit:** `32d025c8`
**Current work package:** SWO-06 activated with stream checkpoints; current split-brain implementation defects closed

> Current-state note: this is a chronological results log. Earlier tranche sections correctly record that reliability
> switches were disabled at that point in the rollout, but they no longer describe checked-in configuration. The four
> reliability switches are now enabled. The authoritative current controls, findings, unresolved risks, and release
> gates are in `Documents/system/Event-Sourcing-Projection-Split-Brain-Controls.md`.

## 1. Executive result

SWO-01 has a production-capable OpenTelemetry/OTLP export path, low-cardinality actor and NATS instruments, detailed actor processing-stage timing, focused correctness tests, and a BenchmarkDotNet overhead measurement. SWO-02 Tranches A through D add aggregate actor admission contracts, observe-only evidence, allocation-free runtime enforcement, explicit Core overload results, durable JetStream redelivery, required route migration, and local enforced saturation confirmation. SWO-03 now prevents Core NATS or JetStream consumer intake until every actor-owned startup dependency is ready and publishes that state through the API host readiness endpoint. Production admission configuration remains `ObserveOnly` until production-like capacity evidence is recorded and approved.

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
| `ifm.actor.worker.capacity` | Observable gauge | none | Configured logical actor-worker concurrency ceiling across active actor pools. |
| `ifm.actor.worker.busy` | Observable gauge | none | Workers currently owning a mailbox batch, including time awaiting an asynchronous actor handler. |
| `ifm.actor.worker.available` | Observable gauge | none | Capacity not currently occupied by a mailbox batch. |
| `ifm.actor.worker.utilization` | Observable gauge, percent | none | `busy / capacity × 100`; zero when no actor pool is active. |
| `ifm.actor.mailbox.enqueue_wait.duration` | Histogram, ms | `actor.type` | Time waiting for mailbox capacity. |
| `ifm.actor.mailbox.queue_wait.duration` | Histogram, ms | `actor.type` | Accepted-message age when dequeued. |
| `ifm.actor.handler.duration` | Histogram, ms | `actor.type` | End-to-end actor handler execution. |
| `ifm.actor.stage.duration` | Histogram, ms | `actor.type`, `stage` | Processing-stage elapsed time. |
| `ifm.actor.stage.failures` | Counter | `actor.type`, `stage` | Failures handled within a domain actor stage. |

The bounded `stage` values are `validation`, `replay`, `execution`, `persistence`, `reply`, `publication`, and `denormalization`.

Mailbox timestamps are stored in a value-type queue envelope. The clock is read only when the corresponding histogram is enabled. The active-mailbox gauge is protected against unstarted-dispose and concurrent stop/retire paths, preventing negative or duplicate lifecycle measurements.

Worker instruments measure logical actor concurrency slots, not operating-system or .NET ThreadPool threads. A worker is busy from the moment it dequeues a scheduled mailbox until it releases or reschedules that mailbox, including any time its `HandleMessageAsync` call is asynchronously suspended. Capacity is registered when `ActorThreadPoolV2` initializes and removed after its workers finish disposal. Available capacity and utilization are derived from the same process-wide capacity and busy counters, use no entity or worker identifiers, and therefore preserve the low-cardinality contract. Because these are observable gauges, very short saturation bursts between exporter collection intervals may be visible through queue-wait histograms without appearing as a 100% utilization sample.

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
2. mailbox depth, active mailbox count, ready-queue depth, worker capacity, busy/available workers, and worker utilization;
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

For worker saturation, graph `ifm.actor.worker.busy` and `ifm.actor.worker.available` against `ifm.actor.worker.capacity`, with `ifm.actor.worker.utilization` as the summary percentage. Sustained utilization near 100% is not by itself a fault: treat it as actor-pool saturation only when ready-queue depth or queue-wait percentiles also remain elevated. Compare that condition with `System.Runtime` ThreadPool, CPU, allocation, and GC metrics to distinguish an intentional logical concurrency ceiling from general process or ThreadPool starvation.

## 6. Remaining SWO-01 evidence and gaps

The following items require a live collector, provider-specific integration, or paper trading and are not claimed as complete:

- prove OTLP delivery and dashboard visibility in the production-like deployment;
- capture p95/p99 attribution during representative market traffic;
- validate exporter and aggregation cost under sustained load;
- add provider-specific storage retry, timeout, and connection-pool-pressure instruments where the PostgreSQL and Scylla drivers do not already expose adequate meters;
- add an explicit NATS receive-to-actor-dispatch latency measurement if queue-wait plus transport operation latency cannot localize that boundary;
- decide from real backlog evidence whether a current oldest-message gauge is worth its additional mailbox bookkeeping; the implemented dequeue-age histogram currently supplies the historical age distribution.

These gaps do not block the next optimization work package from being designed, but SWO-01 should remain **Measuring** until the operational acceptance criteria are met.

## 7. SWO-02 Tranche A result

### 7.1 Implemented baseline

Tranche A adds:

- validated `Disabled`, `ObserveOnly`, and future `Enforce` configuration contracts;
- structured admission result and bounded-cardinality rejection reasons;
- exact serialized payload-byte reporting for every current NATS actor-message implementation;
- process-wide and actor-type message/byte utilization, payload-size, and would-reject instruments;
- observe-only accounting integrated with enqueue, dequeue, failed write, and stop-drain paths;
- configurable per-mailbox and retained-idle-mailbox capacity;
- configurable Core NATS dispatcher/subscription capacity and JetStream dispatcher/outstanding/refill capacity; and
- a zero-retained-idle-queue lifecycle test and focused validation of options, accounting, payload ownership, and compatibility defaults.

`Enforce` mode is implemented at the actor-runtime boundary, and Tranches C/D complete the transport rejection contracts and required route migration. The checked-in host configuration remains `ObserveOnly` until production-like measurements support approved count, byte, payload, and mailbox limits.

### 7.2 Admission microbenchmark

`ActorAdmissionBenchmarks` executes 256 scheduled enqueue/dequeue operations with a 256-byte payload. On .NET 10.0.10 x64 under Windows 10.0.19045, using three warmups and eight measurement iterations:

| Mode | Mean | Increment | Allocated | Lock contentions |
| --- | ---: | ---: | ---: | ---: |
| Disabled | 84.710 ns/op | Baseline | 0 B reported | 0 reported |
| ObserveOnly | 150.620 ns/op | 65.910 ns/op | 0 B reported | 0 reported |

The incremental cost passes the proposed 75 ns gate. This isolated measurement does not include transport, serialization, handler, storage, telemetry export, or scheduler cost.

### 7.3 Capacity status

No production message or byte limits are claimed. `Documents/system/Actor-Backlog-Capacity-Worksheet.md` records compatibility geometry, memory formulas, required normal/open/reconnect/replay measurements, and the approval gate. Runtime enforcement can be validated with deterministic test limits, but production activation and transport enforcement remain blocked until that evidence and the Core traffic classification are reviewed.

### 7.4 Verification

The final Tranche A Release verification passed:

| Gate | Result |
| --- | ---: |
| Complete solution build | 0 warnings, 0 errors |
| `TomasAI.IFM.Shared.UnitTests` | 73/73 passed |
| `TomasAI.IFM.Framework.Messaging.Nats.UnitTests` | 37/37 passed |
| `TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests` | 40/40 passed |
| Ten-domain integration gate | 193/193 passed |

The domain TRX evidence is generated under `TestResults/SystemWideOptimizationTrancheA/` and is intentionally not source-controlled.

The first NATS integration run exposed two restart failures because an event listener disposed its internally owned process-level connection manager on `StopAsync`. The listener now disposes and recreates its owned manager between start cycles. A stale listener test also claimed that multiple mailbox subscriptions were invalid; the full domain gate proved MarketData Feed legitimately requires them, so the test now verifies multi-mailbox startup instead of blocking the supported production contract. One complete run also saw the existing SPSC concurrent test exceed its 10-second deadline; the test passed alone in 40 ms and the subsequent complete NATS run passed 40/40, so no SPSC implementation change was made without reproducible evidence.

### 7.5 Tranche B runtime enforcement

Tranche B adds:

- CAS-based global and actor-type message/byte reservations with reverse-order rollback;
- immediate oversized, global, actor-type, mailbox, stopping, and retired-queue outcomes;
- non-waiting entity-slot acquisition in `Enforce` mode while preserving existing waiting behavior in `Disabled` and `ObserveOnly`;
- a single reservation carried across retired-queue retry and stored in the accepted queue envelope;
- release on dequeue, failed publish, cancellation, exception, and stop drain;
- structured `TryAdmit`/`TryAdmitAsync` production paths used by actor workers and NATS dispatchers;
- explicit cold queue create/`TryAdd`/stop cleanup so losing factories cannot leak started queues; and
- a separate `ifm.actor.admission.rejected` metric with bounded actor-type and reason tags.

The deterministic suite covers exact count and byte boundaries, every controller rejection dimension, rollback, 2,000 concurrent reservation attempts, immediate hot-mailbox rejection, a 128-entity high-cardinality burst, payload ownership, stop drain, retired reservation transfer, distinct stopping behavior, and an eight-writer cold-key creation race.

### 7.6 Tranche B microbenchmarks

The final accepted-path run uses the same 256-operation scheduled enqueue/dequeue benchmark and environment as Tranche A:

| Mode | Mean | Increment over disabled | Allocated | Lock contentions |
| --- | ---: | ---: | ---: | ---: |
| Disabled | 89.37 ns/op | Baseline | 0 B reported | 0 reported |
| ObserveOnly | 178.11 ns/op | 88.74 ns/op | 0 B reported | 0 reported |
| Enforce | 146.75 ns/op | 57.38 ns/op | 0 B reported | 0 reported |

The enforced accepted path passes the 75 ns incremental gate. Observe-only is intentionally more expensive when hypothetical thresholds are configured because it accepts the message and also evaluates and records which limit would have rejected it.

Final enforced rejection results:

| Rejection path | Mean | Allocated | Lock contentions |
| --- | ---: | ---: | ---: |
| Global message limit | 4.424 ns | 0 B reported | 0 reported |
| Global byte limit | 18.950 ns | 0 B reported | 0 reported |
| Actor-type message limit | 32.921 ns | 0 B reported | 0 reported |
| Actor-type byte limit | 47.232 ns | 0 B reported | 0 reported |
| Payload too large | 11.570 ns | 0 B reported | 0 reported |
| Entity mailbox limit, including reserve/rollback | 91.943 ns | 0 B reported | 0 reported |

### 7.7 Tranche B verification

The final Tranche B Release gate passed in full:

| Gate | Result |
| --- | ---: |
| Complete solution Release build | 0 warnings, 0 errors |
| `TomasAI.IFM.Shared.UnitTests` | 82/82 passed |
| `TomasAI.IFM.Framework.Messaging.Nats.UnitTests` | 37/37 passed |
| `TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests` | 40/40 passed |
| Ten domain integration projects | 193/193 passed |

The exact final domain TRX evidence is under `TestResults/SystemWideOptimizationTrancheBFinal/` and is intentionally not source-controlled.

The full gate also verified two integration details that were corrected during the tranche: an event listener may legitimately start multiple mailbox-key subscriptions, and futures tick-date query parameters now preserve all seven .NET fractional-second digits. The latter has a deterministic BDD regression test and avoids silently truncating a `TimeOnly` value to milliseconds.

### 7.8 Tranche C transport behavior

Tranche C adds:

- a stable, non-sensitive retryable overload response with error code `-429`;
- a structurally compatible `ServiceResult<object>` failure whose null value deserializes as `ServiceResult<TResult>` without inspecting or deserializing the rejected request;
- exact once-only rejected-message disposal even when the reply attempt fails;
- explicit Core fire-and-forget classes: request/reply-only, durable live copy, optional, required non-durable, and unknown;
- startup rejection in `Enforce` mode for unknown or required non-durable traffic;
- one delayed JetStream NAK after any required fan-out branch rejects, with no ACK for that delivery;
- separate overload reply, NAK, optional-drop, and redelivery metrics; and
- enforced-mode validation requiring owned JetStream fan-out payloads and a positive NAK delay.

At Tranche C completion, the repository audit recorded Command and Supervisor fire-and-forget paths as required non-durable, Query as request/reply-only, Event as a durable live copy, and Notify/UI as optional. The configuration was therefore intentionally ineligible for `Enforce`; Tranche D resolves those required routes while retaining the startup guard for unknown or newly introduced required-non-durable traffic.

The Tranche B admission benchmark remains the applicable hot-path measurement because Tranche C does not change accepted mailbox admission. Transport rejection necessarily performs reply serialization/network I/O or a JetStream acknowledgement and is tested as an end-to-end contract rather than compared with the allocation-free admission threshold.

### 7.9 Tranche C verification

The final Tranche C Release gate passed in full:

| Gate | Result |
| --- | ---: |
| Complete solution Release build | 0 warnings, 0 errors |
| `TomasAI.IFM.Shared.UnitTests` | 82/82 passed |
| `TomasAI.IFM.Framework.Messaging.Nats.UnitTests` | 52/52 passed |
| `TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests` | 45/45 passed |
| Ten domain integration projects | 193/193 passed |

The real-network NATS gate covers typed command and query overload replies through both owned and legacy payload paths, plus a rejected JetStream branch that receives a configured delayed NAK and succeeds on redelivery after simulated admission recovery. Unit coverage verifies four representative generic result shapes, reply-failure disposal, optional-drop disposal, fan-out ACK/NAK finalization, shared-payload reference counts, and enforcement startup validation.

The exact final domain TRX evidence is under `TestResults/SystemWideOptimizationTrancheCFinal/` and is intentionally not source-controlled.

### 7.10 Tranche D route migration and enforced stress

The production Command helper paths in `ActorService`, `UIActorService`, and `EventActorContext` now use Core request/reply instead of fire-and-forget publication. The helpers preserve transport failures such as retryable overload code `-429`; `EventActorContext` raises a `CommandException` when the command is not accepted. Commandless exception notifications are routed as durable Events. No production Supervisor actor exists, so the unused Supervisor Core consumer was removed from runtime startup. A manually started unknown or required-non-durable consumer still fails startup in `Enforce`.

Checked-in development and production configurations now classify Command and Query as request/reply-only, Event as a durable live copy, and Notify/UI as optional. Both configurations deliberately remain `ObserveOnly` with zero unapproved limits.

The Release local stress baseline produced:

| Scenario | Evidence |
| --- | --- |
| Eight-worker mixed traffic | 400,000 reserve/releases in 0.156 s; approximately 2.56 million operations/s |
| Process CPU during mixed traffic | 1.188 CPU-s over 0.156 wall-s; approximately 7.6 logical cores, or 23.8% of 32 visible processors |
| Process memory during mixed traffic | 15,256 allocated bytes and 929,792-byte working-set increase including the harness; isolated BenchmarkDotNet admission remains 0 B/op |
| Bounds and recovery | Peak eight concurrent reservations; configured limits never exceeded; message and byte accounting returned to zero |
| Hot mailbox | 100,000 attempts at approximately 2.01 million attempts/s; exactly 64 accepted and 99,936 immediately rejected |

These values demonstrate local mechanism capacity and accounting. They do not select production limits and are not a substitute for market-open, reconnect, replay, working-set, and GC measurements in the capacity worksheet.

### 7.11 Tranche D verification

The final Tranche D Release gate passed in full:

| Gate | Result |
| --- | ---: |
| Enforced admission stress tests | 2/2 passed |
| Real-network overload/recovery scenarios | 5/5 passed |
| `TomasAI.IFM.Shared.UnitTests` | 86/86 passed |
| `TomasAI.IFM.Framework.Messaging.Nats.UnitTests` | 56/56 passed |
| `TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests` | 45/45 passed |
| Complete solution Release build | 0 warnings, 0 errors |
| Ten domain integration projects | 193/193 passed |

The complete NATS run exposed a parallel-load-sensitive SPSC concurrency test: under competing integration work, its fixed ten-second cancellation expired before completing 10,000 operations, while isolated Release runs completed all operations in 22-23 ms. The test now uses dedicated producer/consumer threads and a non-parallel xUnit collection so unrelated integration activity cannot turn a ring-buffer correctness check into a host-scheduling test. Its timeout was not increased.

Real-network Enforce coverage uses an actual admission controller and an oversized payload, proves typed `-429` command/query results through owned and legacy payload paths, and proves delayed JetStream redelivery after recovery. Existing reconnect, replay, ownership, shutdown, and accounting tests remain part of the complete NATS and actor-runtime gates.

The exact final domain TRX evidence is under `TestResults/SystemWideOptimizationTrancheDFinal/` and is intentionally not source-controlled. SWO-02 remains in progress because production-like ObserveOnly/paper-trading measurements, capacity approval, and explicit production activation are still pending.

### 7.12 Experimental MPSC ring mailbox investigation

An independent `ActorThreadQueueMpscRing` now implements the same public mailbox and internal V2 scheduling contracts as the Channel-backed `ActorThreadQueueV2`. A validated `ActorRuntime:Admission:MailboxImplementation` option makes the implementations interchangeable at the DI boundary. `Channel` remains the checked-in development and production default, and the existing Channel implementation was not changed.

The ring uses sequence-stamped slots with one atomic producer ticket after capacity reservation, supports worker ownership handoff, carries the complete admission/timing envelope, and preserves asynchronous slot backpressure, immediate enforced rejection, scheduling, retirement, cancellation, and stop/drain ownership. The scheduler's single-consumer guarantee removes redundant consumer CAS, while the compatibility-reader semaphore is signaled only when an asynchronous reader is waiting. Eight focused tests cover the ring itself and its operation through the shared actor pool.

The final BenchmarkDotNet comparison uses isolated benchmark processes and persistent dedicated producer threads at 8,192 slots. This removes the `Parallel.For`/ThreadPool noise in the original comparison:

| Workload | Producers | Channel | MPSC ring | Ring result |
| --- | ---: | ---: | ---: | ---: |
| Scheduled round trip | 1 | 90.19 ns | 79.54 ns | 11.8% faster |
| Concurrent batch | 1 | 82.51 ns | 77.25 ns | 6.4% faster |
| Concurrent batch | 4 | 405.19 ns | 391.09 ns | 3.5% faster; confidence intervals overlap |
| Concurrent batch | 8 | 573.42 ns | 416.87 ns | 27.3% faster |

Neither implementation allocates per hot-path operation. At the current production-compatible capacity, empty mailbox construction allocated 2.13 KB for Channel versus 320.94 KB for the ring and took 224.6 ns versus 151.430 us. Ring allocation falls to 3.12 KB at 64 slots, 10.62 KB at 256, and 40.62 KB at 1,024. The optimized ring is now a credible throughput candidate, especially at eight producers, but remains default-off until high-cardinality retained-memory and end-to-end actor-pipeline evidence supports changing production configuration.

### 7.13 SPSC ring mailbox aligned to striped dispatch

Production mailbox writes were traced to the Core and JetStream dispatch loops. Each `ActorThreadId` hashes to one stripe within its single registered actor-type consumer, so an entity mailbox has one logical stripe producer. `ActorThreadId` includes actor type, actor name, and entity ID but excludes the verb, which keeps every verb for the entity in the same ordered mailbox. The actor scheduler guarantees one concurrent consumer. `ActorThreadQueueSpscRing` makes this invariant explicit as a third DI-selectable implementation. It is now the recommended production implementation, while checked-in production configuration deliberately remains on Channel with capacity 8,192 until a controlled paper-trading or initial-production review.

The production `ActorThreadPoolV2` differs from the legacy leased-thread design: it owns a fixed `2 * ProcessorCount` worker set and one shared ready-mailbox queue. A worker processes at most 64 messages before releasing or rescheduling the mailbox, and the atomic mailbox scheduling state prevents another worker from overlapping it. The same logical entity can therefore migrate between OS worker threads while remaining strictly sequential. There is no two-minute task retirement in V2. Drained entity mailboxes remain retained up to `RetainedIdleMailboxesPerActor` (1,024 by default); beyond that bound, newly-idle mailboxes retire immediately and their workers simply continue reading the shared ready queue.

The SPSC hot path uses cache-isolated producer/consumer indices, direct power-of-two array masking, no producer CAS, no per-message capacity semaphore, and no atomic mailbox count. The queue owns one immutable ring for its lifetime, drain completion reads only the remotely-owned producer index, and already-scheduled burst writes avoid a redundant locked compare/exchange. Full-ring backpressure and empty compatibility-reader waits use transition-only signals. Async backpressure, cancellation, enforced immediate rejection, accounting, FIFO scheduling, retirement, and stop/drain ownership remain covered.

Isolated-process BenchmarkDotNet results:

| Workload | Capacity | Channel | MPSC ring | SPSC ring | SPSC result |
| --- | ---: | ---: | ---: | ---: | ---: |
| Scheduled actor round trip | 8,192 | 89.37 ns | 78.88 ns | 24.68 ns | 72.4% faster than Channel |
| 4,096-message enqueue/schedule/drain burst | 8,192 | 86.50 ns | 78.80 ns | 24.43 ns | 71.8% faster than Channel |
| Dedicated single-producer batch | 8,192 | 82.07 ns | n/a | 27.50 ns | 66.5% faster than Channel |
| Dedicated single-producer batch | 65,536 | 82.80 ns | n/a | 27.77 ns | 66.5% faster than Channel |

Every hot-path case reports 0 B/op. The final immutable-ring/drain and scheduling pass improved the SPSC scheduled round trip from 25.19 ns to 24.68 ns (2.0%); the read-before-CAS scheduling fast path improved the scheduled burst from 24.83 ns to 24.43 ns per message (1.6%). Increasing SPSC capacity from 8,192 to 65,536 did not produce a meaningful throughput penalty in this 4,096-operation working set. Empty 8,192-slot mailbox creation allocates 256.98 KB for SPSC versus 2.13 KB for Channel and 321.05 KB for MPSC. SPSC is recommended for production based on the verified topology and measured throughput, subject to confirming the producer invariant, full-pipeline latency, retained memory, and high-cardinality behavior during paper trading or an initial controlled production run. Until that review, Channel remains the active production selection and 8,192 remains the default capacity; 65,536 remains experimental.

### 7.14 SWO-03 actor startup readiness gate

The runtime now uses an actor-first startup contract:

1. register all actors, producers, and Core/JetStream consumers;
2. keep runtime readiness false and consumer connections closed;
3. await every actor startup hook, including actor-owned projector recovery and durable queue startup;
4. start all Core and JetStream consumers; and
5. atomically publish readiness true only after consumer startup completes.

This removes the previous interval in which a consumer could accept traffic before an actor's mailbox, producer, repository, projector, or recovery operation was ready. Actor startup does not issue requests through these external consumers, so the ordering does not introduce a startup dependency cycle. JetStream durable messages remain on the server while its consumer is unopened; Core callers receive normal no-responder/timeout behavior rather than having work accepted by a partially initialized process.

Cancellation or failure at registration, actor startup, or consumer startup holds readiness false and invokes the existing non-cancelable supervisor shutdown path. Graceful shutdown also clears readiness before stopping consumer intake. The API server registers an `actor_runtime` readiness health check and maps it to `/health/ready`; it returns healthy only while the supervisor reports intake-safe readiness.

Verification on 2026-08-09:

| Gate | Result |
| --- | ---: |
| Focused `ActorCancellationTests` lifecycle coverage | 13/13 passed |
| Complete `TomasAI.IFM.Shared.UnitTests` | 105/105 passed |
| API server Debug and Release builds | 0 warnings, 0 errors |
| Ten domain integration projects, sequential Release run | 193/193 passed |

The deterministic startup test holds actor initialization incomplete and proves consumer startup and readiness publication have not occurred. A separate failure test proves actor-start failure does not open consumers, does not publish ready, and invokes rollback. Existing cancellation coverage proves partial registration is also rolled back. The complete domain gate confirms actor-first startup does not introduce a dependency cycle in Fund projector recovery, analytics event routing, or the other production actor startup hooks.

### 7.15 SWO-05 ScyllaDB ITI query projections

The remaining active futures ITI filtered reads now use three additive, bounded projections: contract/day for mode and
event reads, contract/month for ranges, and contract/trend/mode/month for latest-trend discovery. Canonical
`futures_iti_signal` remains the source of truth and fallback. Inserts and exact time-period deletes maintain all three
tables under the existing scoped V3 mutation/guard protocol. The idempotent MarketData backfill truncates only query
projections, streams canonical ITI rows in bounded batches, publishes the month inventory, independently fingerprints
all three targets, and publishes readiness only when every identity matches. Application-storage CQL now contains zero
`ALLOW FILTERING` statements.

Two dormant predicted-delta aggregate methods were removed from the storage interface. Their queries referenced
`PredictedDelta`, `FuturesRSI`, and `FuturesMDI`, which do not exist in the canonical ITI schema or write contract, and
there were no application callers. No replacement field mapping was inferred.

The final benchmark uses the real configured Scylla test service, one driver/session and consistency configuration for
both paths, the same logical latest-row result, five warmups, 100 measured single-query samples, and no outlier removal.
The after path uses the production bounded trend/mode/month primary key; the earlier unbounded prototype is superseded.

| Canonical rows | Before mean | After mean | Mean change | Before p95 | After p95 | Before p99 | After p99 | Before queries/s | After queries/s | Allocation change |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 4,096 | 11.315 ms | 1.285 ms | -88.64% | 11.989 ms | 1.595 ms | 12.310 ms | 1.622 ms | 88.4 | 778.3 | +30.50% |
| 32,768 | 78.658 ms | 1.358 ms | -98.27% | 80.970 ms | 1.636 ms | 83.387 ms | 1.799 ms | 12.7 | 736.3 | +30.57% |

The measured positive lookup is one Scylla request on both paths. Driver-side Scylla tracing shows the 4,096-row
canonical request returning one 4,096-row page and the 32,768-row request paging six 5,000-row pages plus 2,768 rows.
The projected request reports one partition and one live clustering row at both sizes. Coordinator trace duration was
10,102-10,467 microseconds before versus 442-813 microseconds after at 4,096 rows, and 78,961-80,997 microseconds before
versus 532-631 microseconds after at 32,768 rows. This independently confirms that the latency change comes from
bounded routing rather than a client-only shortcut.

The projection allocates about 4.1-4.3 KiB more per request (17.35-18.32 KiB total), a 30.5% directional increase in
this in-process driver measurement. That is an explicit tradeoff for the richer partition key and request setup; the
absolute cost stays bounded as canonical history grows, while the old query's database work and latency scale with the
partition. Monitor allocation rate if this lookup becomes a very high-QPS client path, but do not trade the bounded
Scylla access pattern back for fewer client allocations. Latest negative lookup fan-out is bounded by the finite ITI
month inventory and stops at the first matching month. Range reads issue one request per contract/month partition with
at most eight concurrent requests.

The final integration run also exposed a pre-existing Market Data Feed test race: parallel classes shared fixed
Scylla rows and one NATS/host environment, allowing one EOD test to overwrite another's arrange data. The project now
serializes its integration tests; the independently passing failed test, complete 44-test Feed rerun, and final
ten-domain rerun confirm the correction. A separate RSI storage test cleanup was corrected to delete by the complete
partition key required by Scylla.

Final verification:

| Gate | Result |
| --- | ---: |
| Serial complete solution Release build | 0 warnings, 0 errors |
| CQL/binding/projection policy tests | 16/16 passed |
| Real-Scylla complete MarketData storage class | 58/58 passed |
| Market Data Analytics integration tests | 25/25 passed |
| Market Data Feed integration tests | 44/44 passed |
| Ten domain integration projects, sequential Release run | 193/193 passed |

### 7.16 SWO-06 Event-projector reliability Tranche A

Tranche A establishes the durable contracts and storage boundary without activating a new production projector path.
Stable effect identities now derive from projector, immutable source event ID, and effect kind. Projection descriptors
must declare their target idempotency strategy, execution contexts bind that strategy and effect identity to one fenced
execution token, and reliability options bound recovery pages, stream concurrency, replay attempts/delay, claim leases,
and outbox batches.

The additive PostgreSQL schema extends `event_projector_state` with source stream/name, revision, execution token,
lease, retry scheduling, failure, completed-stage, and UTC update metadata. It also stages a transactional publication
outbox keyed by `(ProjectorName, EventId, EffectKind)` with a unique deterministic message ID. New storage APIs use
compare-and-set token/revision/stage predicates for claim, renewal, transition, and terminalization. Recovery is exposed
as a joined, bounded event-ID keyset page, eliminating the need for a second state lookup when the later runtime path is
activated. Existing projector runtime behavior is unchanged in this tranche.

Real PostgreSQL tests prove:

- eight simultaneous initial inserts create exactly one state row;
- sixteen simultaneous claim attempts create exactly one active owner;
- an expired lease can be taken over and the previous token is fenced;
- repeated stale transitions and renewals return no state mutation;
- terminalization clears token/lease and prevents further ownership; and
- two bounded keyset pages return three joined event/state rows in order without duplicates.

The baseline benchmark retains the current full-set materialization, per-event JSON deserialization, state N+1 call
shape, state write call, and queue call. Fake storage and queue operations complete synchronously, so these numbers are
a CPU/allocation lower bound and deliberately exclude PostgreSQL/NATS latency. BenchmarkDotNet 0.15.8 ran on .NET
10.0.10 with Concurrent Workstation GC on the AMD Ryzen Threadripper 1950X host.

| Pending events | Current mean | Allocated per recovery | Approx. allocation/event |
| ---: | ---: | ---: | ---: |
| 1,000 | 52.44 ms | 6.49 MB | 6.64 KB |
| 10,000 | 109.56 ms | 64.85 MB | 6.64 KB |
| 100,000 | 1,002.74 ms | 648.53 MB | 6.64 KB |

The 1,000-event result has a short-iteration warning and wide confidence interval; the 100,000-event result is stable
at 1.003 seconds with 7.0 ms standard deviation. The important baseline finding is linear cumulative allocation: the
current synthetic lower bound allocates roughly 649 MB at 100,000 pending events before any real database or NATS
cost. MemoryDiagnoser did not measure peak retained inventory. Tranche B/C comparison must demonstrate inventory
bounded by page size and configured lanes, no state N+1 query, and no same-stream overlap.

Focused verification:

| Gate | Result |
| --- | ---: |
| Reliability identity/options/descriptor/context tests | 5/5 passed |
| PostgreSQL timestamp-with-time-zone parameter tests | 3/3 passed |
| Projector state/schema policy tests | 3/3 passed |
| Complete real-PostgreSQL EventSource snapshot/reliability class | 13/13 passed |
| Complete Fund unit tests | 191/191 passed |
| Complete Framework.Storage unit tests | 391/391 passed |
| Complete solution Release build | 0 warnings, 0 errors |
| Recovery baseline sizes | 3/3 completed |

### 7.17 SWO-06 Event-projector reliability Tranche B

Tranche B separates durable queue preparation, handler registration, recovery publication, and worker startup. Queue
preparation creates or updates JetStream resources without opening consumers; handler registration performs no NATS
I/O; and enqueueing before the first explicit start durably publishes while workers remain stopped. After a successful
start, enqueueing can restart workers retired by the two-minute idle timeout. `StopAsync` disables that restart until a
new explicit start. Partial startup faults cancel and dispose both workers.

`BaseEventProjector` now keeps readiness false through preparation, handler registration, and recovery handoff. It
publishes a ready snapshot only after both workers start. Cancellation or failure performs non-cancelable queue
rollback, clears the actor context, retains readiness false, records the failure reason, and rethrows. Both production
and integration composition roots bind `EventProjectorReliability` explicitly; `BoundedRecoveryEnabled` is checked in
as `false`.

The optional bounded coordinator reads joined event/state rows in event-ID keyset pages, eliminating the recovery
state N+1 read. It holds only the current page, processes independent streams with configured concurrency, and
preserves ascending order within each stream across page boundaries. Tranche B originally claimed before publication;
Tranche C moved normal claims to the unified queue worker and relies on stable JetStream message IDs to de-duplicate
concurrent recovery publication. Unknown event types are terminalized as blocked failures. The
legacy SQL upsert now supplies the additive stream identity required by the Tranche A schema, preserving rollback and
default-path compatibility.

BenchmarkDotNet 0.15.8 ran on .NET 10.0.10 with Concurrent Workstation GC on the AMD Ryzen Threadripper 1950X host.
Both paths use synchronous fake storage and queue operations, so PostgreSQL and NATS latency are excluded. The bounded
path uses 256-row pages and eight stream lanes.

| Pending events | Current full-set/N+1 | Bounded joined keyset | Mean ratio | Current allocation | Bounded allocation |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 | 54.39 ms | 12.29 ms | 0.23 | 6.49 MB | 6.65 MB |
| 10,000 | 109.94 ms | 143.12 ms | 1.31 | 64.85 MB | 66.59 MB |
| 100,000 | 1,020.38 ms | 317.05 ms | 0.31 | 648.53 MB | 666.96 MB |

At 100,000 events the synthetic bounded path is approximately 68.9% faster. The 10,000-event bounded measurement has
high variance (32.37 ms standard deviation and a wide confidence interval), so it is insufficient evidence of a
stable regression. MemoryDiagnoser reports cumulative allocation, not peak retained inventory: both paths deserialize
every event and cumulative allocation remains linear, with about 2.8% overhead for page grouping and lane scheduling
at 100,000. The implementation's recovery inventory is nevertheless bounded to one 256-row page plus its active
stream groups instead of retaining the complete backlog. Real PostgreSQL/NATS measurements remain a rollout gate.

Final verification:

| Gate | Result |
| --- | ---: |
| Bounded recovery/readiness focused Fund tests | 15/15 passed |
| Complete Fund unit tests | 199/199 passed |
| Durable queue lifecycle unit tests | 16/16 passed |
| Real-NATS durable queue lifecycle tests | 5/5 passed |
| Real-PostgreSQL claim/keyset/legacy-compatibility tests | 4/4 passed |
| Complete Fund integration tests | 26/26 passed |
| Current/bounded benchmark cases | 6/6 completed |
| Complete solution Release build | 0 warnings, 0 errors |

The complete domain integration confirmation remains reserved for every final SWO-06 tranche/activation gate. Tranche
B is complete but not production-active.

### 7.18 SWO-06 Event-projector reliability Tranche C

Tranche C replaces the mutable per-call `EventProjectorBuilder` with one frozen descriptor table per projector. Startup
validates that the descriptor types exactly match `ProjectedEventTypes`, registers maximum-attempt handling once, and
uses a cached type-to-descriptor dispatch map. `FundEventProjector` declares all eight operations as
`NaturalKeyMutation` and returns the explicit `EventProjectionApplyResult` contract.

`EventProjectorExecutionEngine` now owns process and replay execution. It conditionally claims a PostgreSQL state row
with a unique execution token and lease, applies one external stage, and advances only through token/revision/stage
compare-and-set transitions. A failure conditionally releases the same fence, clears its token/lease, records bounded
retry metadata, and rethrows. This avoids exhausting 30-second queue redeliveries behind a two-minute abandoned lease.
Unregistered types terminalize with `unregistered-source-event`; unknown recovered payloads retain
`unknown-source-event` manual-resolution state.

Live fenced initialization now inserts projector state from a join to `event_log` and `event_name_id`. The persisted
event ID supplies stream ID and source name atomically, so execution does not assume the serialized event contains an
`AggregateId` and adds no stream lookup. Bounded recovery excludes active unexpired leases and publishes normal work
without taking ownership; the single queue worker path owns execution claims. Concurrent recovery publications use the
queue's stable NATS message ID for JetStream de-duplication.

The Fund target audit found no ID generation, increment, append-under-new-key, or external call. The three upserts,
two deletes, two deterministic updates, and one no-op are repeat safe by natural key. A real-ScyllaDB integration test
applies every descriptor twice and verifies identical final state, including repeated delete absence. No target receipt
table is required for the current Fund scope.

The crash-after-write unit fault applies the Fund target write, deliberately rejects its next checkpoint, verifies
claim release, reapplies the same target write, and reaches one terminal completion. Real PostgreSQL storage coverage
proves a released stage is immediately claimable and stale owner revisions remain fenced. A live integration flow runs
with bounded recovery and fenced execution enabled through real PostgreSQL, NATS JetStream, and ScyllaDB.

Compatibility remains explicit. Immutable descriptors also drive the default legacy checkpoint path, while
`BoundedRecoveryEnabled` and `FencedExecutionEnabled` both remain `false` in checked-in application settings. Unused
parallel projector state, timing, retry-action, empty-result, and mutable-builder types were removed after a complete
reference audit.

Final verification:

| Gate | Result |
| --- | ---: |
| Complete Fund projector/unit suite | 202/202 passed |
| Projector-focused real PostgreSQL storage tests | 8/8 passed |
| Fenced/legacy projector real PostgreSQL + NATS + ScyllaDB tests | 3/3 passed |
| Complete Fund integration suite | 27/27 passed |
| Sequential Release domain integration tests | 193/193 passed |
| Application.Actor integration project | No discoverable tests (composition-only project) |
| Complete solution Release build | 0 warnings, 0 errors |

Tranche C is complete but not production-active. Tranche D transactional publication outbox and typed terminal failure
delivery are next.

### 7.19 SWO-06 Event-projector reliability Tranche D

Tranche D activates the schema prepared in Tranche A without enabling it in production configuration. Processing,
completion, and failure stages can now use a single PostgreSQL data-modifying CTE to conditionally advance or
terminalize the fenced state row and insert the concrete MessagePack publication into `event_projector_outbox`. The
durable key is `(ProjectorName, EventId, EffectKind)` and the SHA-256-derived message identity remains stable across
every retry.

`EventProjectorOutboxDispatcher` claims at most the configured batch size using `FOR UPDATE SKIP LOCKED`, a dispatch
token, and a bounded lease. Successful sends conditionally mark `Published`; failures become `Retrying` with capped
backoff, and exhausted delivery becomes `Failed`. A crash or ambiguous acknowledgement is reclaimed after lease expiry
and republishes the identical payload with the same deterministic `IEvent.Id`. This remains durable at-least-once
delivery, not exactly once.

The one projector-wide maximum-attempt callback now resolves the immutable source descriptor, converts its typed
failure event, and atomically terminalizes state plus failure outbox. Null or throwing terminal conversion fails closed
to manual resolution. `BlockedStage` preserves the exact failed stage. Bounded operator APIs page pending, failed, or
blocked rows; retry-exact reopens that stage and requeues the immutable source event; skip requires and durably records
an operator reason.

The Fund consumer audit found no registered Fund actor handlers that apply business effects from the projector's
completion/failure event types. The outbox nevertheless assigns the stable effect-derived `IEvent.Id`; future consumers
must use that ID as a durable receipt or prove natural idempotency.

Fault injection covers publish failure before acknowledgement and publish success before the PostgreSQL delivery
marker. Both paths retry with an identical typed payload/event identity. Real PostgreSQL coverage proves state/outbox
atomicity, mutually exclusive leased claims, published-row exclusion, operational classification, exact-stage retry,
and skip-with-reason. A live flow proves completion delivery through PostgreSQL, the NATS JetStream projector queue,
and the ScyllaDB Fund target.

Final verification:

| Gate | Result |
| --- | ---: |
| Complete Fund projector/unit suite | 207/207 passed |
| Tranche D atomic-outbox/operator PostgreSQL cases | 2/2 passed |
| Tranche D focused outbox/typed-terminal/operator unit cases | 5/5 passed |
| Live transactional-outbox Fund flow | 1/1 passed |
| Complete Fund integration suite | 28/28 passed |
| Sequential Release domain integration tests | 195/195 passed |
| Complete solution Release build | 0 warnings, 0 errors |

The broad `Application.Storage.IntegrationTests` project was also attempted. Its projector-focused cases passed, but the
run was not counted as a pass: an unrelated Trade DB empty-range test found a pre-existing
`TradePlanForwardLossRatioReadModel { ForwardLossRatio = 0.25 }`, and the broad project then reached the five-minute
runner cap. This is recorded as shared-database fixture contamination, not Tranche D evidence.

`BoundedRecoveryEnabled`, `FencedExecutionEnabled`, and `TransactionalOutboxEnabled` remain `false` in both checked-in
application configurations. Tranche E ordering, observability, performance benchmarks, dashboards, and staged rollout
are next.

### 7.20 SWO-06 Event-projector reliability Tranche E

The PostgreSQL claim now enforces durable projector/stream order: an event cannot be claimed while an earlier event in
the same stream has an outcome other than completed, already-completed, or superseded. This condition applies to both
process and replay deliveries and is backed by the existing projector/stream/event partial index. Fund retains
`NeverSupersede`, so a failed, cancelled, blocked, or otherwise unresolved predecessor intentionally stops that Fund
stream until retry or an explicit operator skip resolves it.

Ordering and transient claim deferrals are represented by typed delivery-deferred exceptions. Process and replay
workers negatively acknowledge them with bounded delay rather than treating them as application failures. The replay
consumer permits transport redelivery while the application continues to enforce the configured maximum only for
genuine processing failures. A concurrent duplicate holding a valid lease is acknowledged because its owner already
owns durable progress; an expired or otherwise unclaimable row remains eligible for redelivery.

The meter `TomasAI.IFM.Application.EventProjector` is registered with the shared OTLP pipeline. Low-cardinality
instruments cover event outcomes, stage/recovery/startup/outbox durations, recovery batch size, readiness, logical
worker occupancy, and durable PostgreSQL gauges for pending/blocked/terminal-failed/expired-lease and outbox
pending/retrying/oldest-age state. A separately controlled sampler obtains the complete operational snapshot in one
query. No metric contains event, stream, command, aggregate, entity, or exception identifiers.

BenchmarkDotNet measurements used .NET 10 Release on the same Windows host. The telemetry benchmark records a batch of
256 stage completions:

| Path | Mean per 256 stages | Approximate mean per stage | Allocated |
| --- | ---: | ---: | ---: |
| Meter dormant | 226.410 ns | 0.884 ns | 0 B |
| Meter observed | 14.9913 us | 58.56 ns | 0 B |
| Incremental observed cost | 14.7649 us | 57.68 ns | 0 B |

The CPU-only outbox serialization/deserialization benchmark measured 1.4163 ms per 256 messages, or approximately
5.532 us/message (about 180,750 messages/second), allocating 546,744 bytes per batch (2,136 bytes/message). Enabling the
metrics listener measured 1.4408 ms with the same allocation. These numbers intentionally exclude PostgreSQL, NATS,
network, acknowledgement, and target-consumer latency and are not an end-to-end throughput claim.

The synthetic recovery comparison was rerun with the current and joined-keyset implementations:

| Pending events | Current path | Bounded path | Bounded latency change |
| ---: | ---: | ---: | ---: |
| 1,000 | 50.33 ms | 11.90 ms | 76.4% lower |
| 10,000 | 117.22 ms | 141.54 ms | 20.7% higher; inconclusive due to 42.6 ms standard deviation |
| 100,000 | 1,040.23 ms | 291.27 ms | 72.0% lower |

The 100,000-event bounded result is 8.1% faster than the Tranche B measurement of 317.05 ms; the baseline is 1.9%
slower than its prior 1,020.38 ms, consistent with normal run-to-run variance. The fixture constructs/deserializes the
entire synthetic source set and therefore reports total allocations of approximately 6.5/65/652 MB for the baseline
and 6.7/67/670 MB for the bounded comparison. Those fixture allocations do not model the live coordinator's bounded
one-page inventory. PostgreSQL and NATS latency are excluded, so real readiness, round-trip, percentile, and peak-live-
memory objectives remain part of the production-like canary.

Final verification:

| Gate | Result |
| --- | ---: |
| Complete NATS messaging unit suite | 58/58 passed |
| Complete Fund projector/unit suite | 208/208 passed |
| Focused real-PostgreSQL storage coverage | 7/7 passed |
| Complete Fund integration suite | 29/29 passed |
| Live same-stream process/replay ordering case | 1/1 passed |
| Sequential Release domain integration tests | 196/196 passed |
| Complete solution Release build | 0 warnings, 0 errors |

`BoundedRecoveryEnabled`, `FencedExecutionEnabled`, `TransactionalOutboxEnabled`, and
`BacklogMetricsPollingEnabled` remain `false` in both checked-in application configurations. Tranche E completes the
implementation gate, not the operational activation gate. Observe-only OTLP and backlog sampling should be enabled
first; fenced execution plus bounded recovery should then be canaried before the publication outbox is enabled. The
full procedure and Grafana contract are in the projector implementation document.

### 7.21 SWO-08 OptionPricer Black-76 migration

The QLNet-backed `OptionCalculator` and result class were removed from Domain OptionPricer Shared. Their replacement
is an immutable value-type compatibility API in Framework OptionPricer that uses the existing managed Black-76 implied-
volatility and Greeks implementation. All current production consumers now reference the framework implementation.
The QLNet package references were removed from Domain OptionPricer Shared and Shared, and unused QLNet imports were
removed solution-wide. A restored solution asset scan found zero QLNet package entries and zero product dependency-
manifest entries.

BenchmarkDotNet ShortRun results on .NET 10.0.10 with Concurrent Workstation GC were:

| Case | Mean per option | Managed allocation | Completed work items | Lock contention |
| --- | ---: | ---: | ---: | ---: |
| Implied volatility plus Greeks, one call | 375.7 ns | 0 B | 0 | 0 |
| Implied volatility plus Greeks, four legs | 406.4 ns | 0 B | 0 | 0 |

Verification passed 16/16 Domain OptionPricer unit tests, 8/8 Domain OptionPricer integration tests, 479/479
MarketData Feed unit tests, and 126/126 UI presentation unit tests. Direct Release builds of Framework OptionPricer, Domain OptionPricer, MarketData Feed, UI
ViewModels, UI Views, and the benchmark project passed with zero warnings and zero errors. The full solution build
continued to fail on eight pre-existing BDD fixture constructor mismatches for newly required actor logger parameters;
none are in the option-pricing migration or its dependency paths.

These results prove removal of the former in-process allocation/locking mechanism, but are not an end-to-end option-
feed capacity claim. Independent numerical reference matrices, solver-edge coverage, exercise-style enforcement, and
paper-trading verification remain open under the dedicated migration specification.

## 8. References

- `Documents/system/System-Wide-Telemetry-and-Distributed-Tracing-Design.md`
- [OpenTelemetry .NET metrics documentation](https://opentelemetry.io/docs/languages/dotnet/metrics/)
- [OpenTelemetry.Extensions.Hosting package](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/)
- [OpenTelemetry OTLP exporter package](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol/)
- [Built-in .NET runtime metrics](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/built-in-metrics-runtime)
- `Documents/system/System-Wide-Optimization-Plan.md`
- `Documents/system/Aggregate-Actor-Backlog-Overload-Control-Implementation-Plan.md`
- `Documents/system/Actor-Backlog-Capacity-Worksheet.md`

## 9. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.1 | 2026-08-09 | Recorded SWO-01 implementation, benchmark, focused tests, complete domain integration gate, defect correction, and remaining production-like measurements. |
| 0.2 | 2026-08-09 | Recorded SWO-02 Tranche A contracts, observe-only instrumentation, configurable capacity geometry, microbenchmark result, and pending production-like capacity evidence. |
| 0.3 | 2026-08-09 | Recorded SWO-02 Tranche B atomic runtime enforcement, concurrency and lifecycle coverage, accepted/rejected benchmarks, complete Release verification, and pending transport activation. |
| 0.4 | 2026-08-09 | Recorded SWO-02 Tranche C Core classification, typed overload replies, delayed JetStream redelivery, transport metrics, complete Release verification, and the remaining rollout gate. |
| 0.5 | 2026-08-09 | Recorded SWO-02 Tranche D route migration, local enforced stress and CPU/allocation evidence, complete regression verification, and the remaining production capacity/activation gate. |
| 0.6 | 2026-08-09 | Recorded the optional MPSC ring mailbox, correctness coverage, Channel comparison benchmarks, and the decision to retain Channel as the production default. |
| 0.7 | 2026-08-09 | Recorded the optimized atomic-ticket/single-consumer MPSC ring, isolated persistent-producer benchmarks, and its improved throughput while retaining the production rollout gate. |
| 0.8 | 2026-08-09 | Recorded the striped-topology SPSC mailbox, transition-only backpressure, 8,192/65,536 capacity benchmarks, and the unchanged Channel/8,192 production defaults. |
| 0.9 | 2026-08-09 | Marked SPSC as the recommended production implementation while retaining Channel as the checked-in selection until paper-trading or initial-production validation. |
| 1.0 | 2026-08-09 | Added process-wide actor-worker capacity, busy, available, and utilization gauges with logical-slot semantics, lifecycle accounting, focused tests, and Grafana correlation guidance. |
| 1.1 | 2026-08-09 | Recorded SWO-03 actor-first startup ordering, readiness health publication, rollback/shutdown semantics, and deterministic verification. |
| 1.2 | 2026-08-09 | Recorded the in-progress SWO-05 bounded ITI projections, real-Scylla before/after benchmark, and focused reconciliation/read/write validation. |
| 1.3 | 2026-08-09 | Completed SWO-05 with Scylla trace evidence, final benchmark percentiles and allocation tradeoff, deterministic integration-test isolation, and the complete validation gate. |
| 1.4 | 2026-08-09 | Recorded SWO-06 Tranche A reliability contracts, additive state/outbox schema, fenced storage concurrency evidence, bounded recovery API, and 1k/10k/100k current-path baseline. |
| 1.5 | 2026-08-10 | Recorded SWO-06 Tranche B lifecycle separation, default-off bounded recovery, readiness rollback, compatibility correction, benchmark comparison, and focused/full Fund verification. |
| 1.6 | 2026-08-10 | Recorded SWO-06 Tranche C immutable descriptors, unified fenced execution, claim release, all-eight Fund repeat-apply proof, fail-closed unknown handling, cleanup, and complete regression gate. |
| 1.7 | 2026-08-10 | Recorded SWO-06 Tranche D atomic publication outbox, leased bounded dispatch, deterministic consumer IDs, typed terminal failure, operator controls, fault injection, and complete domain regression gate. |
| 1.8 | 2026-08-10 | Recorded SWO-06 Tranche E durable same-stream ordering, OTLP metrics and operational sampling, benchmark evidence and limitations, staged rollout, and the 196-test domain regression gate. |
| 1.9 | 2026-08-13 | Recorded SWO-08 QLNet removal, framework Black-76 compatibility migration, zero-allocation/zero-contention benchmarks, focused regression gates, and remaining numerical and paper-trading validation. |
