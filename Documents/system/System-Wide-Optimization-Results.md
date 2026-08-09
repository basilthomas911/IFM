# IFM System-Wide Optimization Results

**Document type:** Living implementation and measurement record
**Status:** Active
**Created:** 2026-08-09
**Last updated:** 2026-08-09
**Baseline commit:** `32d025c8`
**Current work package:** SWO-02 Tranche D, rollout and confirmation

## 1. Executive result

SWO-01 has a production-capable OpenTelemetry/OTLP export path, low-cardinality actor and NATS instruments, detailed actor processing-stage timing, focused correctness tests, and a BenchmarkDotNet overhead measurement. SWO-02 Tranches A through D add aggregate actor admission contracts, observe-only evidence, allocation-free runtime enforcement, explicit Core overload results, durable JetStream redelivery, required route migration, and local enforced saturation confirmation. Production configuration remains `ObserveOnly` until production-like capacity evidence is recorded and approved.

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

## 8. References

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
