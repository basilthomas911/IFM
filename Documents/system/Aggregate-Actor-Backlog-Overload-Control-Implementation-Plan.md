# Aggregate Actor Backlog and Overload Control Implementation Plan

**Work package:** SWO-02
**Status:** Approved; Tranches A through D implemented locally, production activation awaiting capacity evidence
**Priority:** P0
**Created:** 2026-08-09
**Last updated:** 2026-08-09
**Owner:** IFM engineering

## 1. Purpose

This document defines the implementation path for process-wide actor backlog limits and explicit overload behavior. Its purpose is to make queued memory calculable while preserving per-entity ordering, durable-event recovery, request/reply error handling, pooled-payload ownership, and graceful shutdown.

The plan is deliberately more specific than the summary in `System-Wide-Optimization-Plan.md`. Approval of this document authorizes the design direction, not arbitrary capacity values. Initial production limits must be derived from SWO-01 measurements and a stated memory budget.

## 2. Decisions proposed for approval

The implementation should proceed with these decisions unless review changes them:

1. Enforce both **message-count and serialized-payload-byte limits**. A message-count limit alone does not provide a meaningful memory bound.
2. Use one process-wide admission controller with global limits and actor-type limits. Keep the controller independent of NATS so in-process and future transports cannot bypass it.
3. Make enforced mailbox admission non-waiting. A full mailbox or exhausted process budget returns a structured rejection immediately instead of creating an unbounded population of suspended writers.
4. Keep the ready-mailbox channel physically unbounded but logically bounded. Once admission is bounded, its one-entry-per-scheduled-mailbox invariant limits ready entries without introducing scheduler deadlocks.
5. Release backlog capacity when a message is dequeued. Payloads still executing in handlers are bounded separately by the fixed worker count.
6. Preserve caller ownership until admission returns `Accepted`; after acceptance, the mailbox owns the message and must dispose it exactly once.
7. Return a retryable `ServiceResult<T>` failure for overloaded command and query requests. The proposed transport error code is `-429`, subject to approval.
8. Negatively acknowledge overloaded JetStream events with a configured delay. Do not acknowledge an event that was not admitted to every required destination.
9. Do not silently discard Core NATS traffic. Fire-and-forget Core traffic must be classified as either optional with an explicit measured drop policy or reliability-required and migrated to JetStream before enforcement.
10. Roll out through `Disabled`, `ObserveOnly`, and `Enforce` modes. Production starts in `ObserveOnly`; `Enforce` requires approved capacities and completed transport tests.

## 3. Current implementation evidence

### 3.1 Actor scheduler

The current V2 scheduler has valuable local bounds and ordering guarantees:

- every entity has an `ActorThreadQueueV2` with a semaphore-backed capacity of 8,192 messages;
- a scheduling bit allows only one ready-queue entry per entity mailbox;
- one worker processes a maximum batch of 64 messages before rescheduling;
- no two workers process the same entity concurrently;
- the pool uses `Environment.ProcessorCount * 2` workers;
- idle entity queues are opportunistically retired when an actor owns more than 1,024 retained queues; and
- accepted work is drained during graceful supervisor shutdown.

The aggregate weakness is that each distinct entity receives its own independently bounded queue. The shared `ActorReadyQueue` is unbounded, and the number of simultaneously non-empty entity queues has no process-wide admission limit. A high-cardinality burst can therefore multiply the per-entity capacity and queue-object cost.

### 3.2 NATS ingress

Core NATS and JetStream consumers already use bounded dispatch stripes:

- dispatcher count defaults to 4;
- each stripe has capacity 4,096;
- Core subscription channel capacity is `4,096 * dispatcher count`;
- JetStream `MaxAckPending` and requested message count are based on the same capacity; and
- pooled command, query, and event payloads have explicit ownership-transfer paths.

Dispatchers currently call actor mailbox `WriteAsync` with `CancellationToken.None`. When an entity queue is full, the write can wait indefinitely. One hot entity can therefore block its hash stripe and retain pooled payloads. The stripes bound that retention locally, but their configuration is hard-coded and is not coordinated with actor backlog capacity.

### 3.3 Delivery guarantees

Core NATS is best-effort and at-most-once. JetStream provides persistent, acknowledged, at-least-once delivery. This distinction is binding for overload behavior: JetStream can NAK and redeliver, while a Core fire-and-forget message that has already been consumed cannot be recovered unless another durable copy exists.

### 3.4 Existing SWO-01 signals

The following instruments are available as the baseline:

- mailbox and ready-queue depth;
- active mailbox count;
- accepted, processed, failed, and canceled rates;
- enqueue wait, queue wait, handler, and stage duration;
- NATS operation duration and failures; and
- runtime CPU, allocation, GC, working-set, and thread-pool signals through OpenTelemetry.

SWO-02 will add admission-specific outcomes, byte utilization, overload replies, NAKs, and explicitly permitted optional drops.

## 4. Goals and non-goals

### 4.1 Goals

1. Make worst-case process backlog memory calculable from configuration and measured overhead.
2. Prevent a high-cardinality burst from creating unbounded queued messages or ready entries.
3. Prevent one hot entity or actor type from consuming all configured capacity.
4. Preserve FIFO ordering and single-threaded execution for each entity.
5. Produce explicit, retryable command and query failures under overload.
6. Preserve durable events through delayed NAK/redelivery.
7. Preserve exact pooled-buffer ownership on acceptance, rejection, cancellation, and shutdown.
8. Add enough metrics to select and safely change limits from evidence.
9. Add deterministic stress tests and repeatable benchmarks.

### 4.2 Non-goals

- Dropping immutable event history or using admission control as event retention.
- Introducing priorities within one entity mailbox; FIFO remains binding.
- Replacing NATS flow control or JetStream server retention limits.
- Dynamically adding worker threads in response to backlog.
- Treating overload as a domain-validation failure.
- Solving cross-process global capacity; this tranche bounds one actor host process.
- Optimizing optional UI latest-value channels, which have separate coalescing semantics.

## 5. Required invariants

1. An admission reservation is either transferred to exactly one mailbox entry or fully rolled back.
2. Every admitted reservation is released exactly once on dequeue or queue drain.
3. A rejected message remains owned by its caller until the caller replies, NAKs, retries, or disposes it.
4. The actor runtime never silently disposes a message and reports it as accepted.
5. A scheduled entity occurs at most once in the ready queue.
6. A ready entry implies a scheduled mailbox that had admitted work; therefore ready depth is logically bounded by admitted message count plus shutdown races.
7. Per-entity FIFO and maximum entity concurrency of one remain unchanged.
8. JetStream ACK occurs only after all required mailbox handoffs are accepted. Any rejected branch makes the fan-out delivery NAK.
9. Core request/reply overload completes before the client request timeout whenever the NATS connection remains available.
10. Graceful shutdown rejects new admission, drains previously accepted work, releases all reservations, and leaves every utilization counter at zero.
11. Entity IDs, subjects, stream IDs, symbols, and error text never become metric tags.

## 6. Proposed architecture

```text
NATS/Core request      JetStream event       In-process/future ingress
        |                     |                         |
        +---------- bounded transport buffers ----------+
                              |
                    ActorThreadQueues.Admit
                              |
                 +------------+-------------+
                 | ActorAdmissionController |
                 | global count + bytes     |
                 | actor-type count + bytes |
                 +------------+-------------+
                              |
                 per-entity mailbox capacity
                              |
            +-----------------+------------------+
            | accepted                           | rejected
            v                                    v
      scheduled mailbox           command/query retryable failure
            |                      JetStream delayed NAK
      shared ready queue           optional Core measured drop only
            |
       worker dequeue
            |
   release backlog reservation
            |
       execute handler
```

The admission controller bounds accepted backlog. Transport buffers remain separately bounded and are included in the total-memory worksheet. Handler payloads are limited by worker count because the backlog reservation is released at dequeue.

## 7. Admission model

### 7.1 Capacity dimensions

Admission must check all applicable dimensions:

- process-wide queued message count;
- process-wide queued serialized bytes;
- actor-type queued message count;
- actor-type queued serialized bytes;
- per-entity mailbox message count; and
- maximum individual payload size.

An actor-type limit may be smaller than the global limit to stop Event, Command, Query, Notify, or UI traffic from monopolizing the process. The first implementation will use hard caps, not a priority scheduler or borrowing reservations.

### 7.2 Payload charge

Add an admission-size property to `IActorMessage` and all production implementations:

- legacy `byte[]` messages charge their array length;
- owned command/query messages charge the owned memory length;
- owned event branches charge the shared payload length conservatively;
- synthetic/test messages use an explicit configured or test charge.

Charging the full shared event payload to every fan-out branch overestimates real memory but preserves safety. Shared-payload-aware charging may be considered later only if measurements show material lost capacity.

The queue envelope will carry the accepted byte charge so release does not inspect a message after ownership or payload release has changed.

### 7.3 Fast-path algorithm

The accepted path should use compare/exchange counters and no monitor lock:

1. reject an individually oversized payload;
2. reserve global message count;
3. reserve global bytes;
4. reserve actor-type message count;
5. reserve actor-type bytes;
6. resolve or safely create the entity queue;
7. attempt the entity slot without waiting;
8. publish the queue entry and schedule the entity; and
9. roll back every acquired dimension in reverse order if any later step fails.

Each numeric reservation uses a CAS loop that refuses a value above its configured maximum. Temporary reservations may reduce available capacity during the synchronous admission call, but accepted counters must never exceed a configured limit.

The entity-slot attempt must be non-blocking. This prevents writers for one full entity from holding global reservations while suspended.

### 7.4 Admission result

Replace ambiguous Boolean production decisions with a readonly result structure containing an outcome and bounded reason. Proposed reasons:

- `Accepted`;
- `GlobalMessageLimit`;
- `GlobalByteLimit`;
- `ActorTypeMessageLimit`;
- `ActorTypeByteLimit`;
- `MailboxLimit`;
- `PayloadTooLarge`;
- `Stopping`; and
- `MailboxRetired`, which is internal and retried by `ActorThreadQueues` rather than exposed as overload.

Add a new admission method to `IActorThreadQueues` while retaining compatibility wrappers during migration. All production NATS consumers and actor-thread write paths must migrate to the structured method in the same tranche. Compatibility wrappers must throw or return false consistently and must not conceal the rejection reason in new production code.

### 7.5 Reservation release boundary

Release message and byte reservations immediately after the queue entry is removed and before handler execution. This produces these calculable categories:

- queued memory: bounded by admission count and bytes;
- executing payload memory: bounded by actor worker count and maximum payload size;
- ready-queue references: bounded by non-empty admitted mailboxes; and
- idle queue objects: bounded by the per-actor retained-idle policy plus a transient high-cardinality admitted set.

Holding reservations through handler completion would bound more memory but would incorrectly turn slow I/O into admission starvation and reduce throughput. The fixed worker pool already bounds executing messages.

## 8. Entity queue lifecycle changes

### 8.1 Safe queue creation

`ConcurrentDictionary.GetOrAdd` may invoke its value factory more than once under a cold-key race. The current factory starts the queue inside that callback. SWO-02 counters and memory accounting require deterministic cleanup.

Replace this with either:

- a `Lazy<IActorThreadQueue>` dictionary whose losing values are never initialized; or
- an explicit `TryGetValue`/create/`TryAdd` loop that stops and disposes every losing queue.

The chosen implementation must prove that active-mailbox metrics, semaphores, channels, and admission state are balanced under concurrent first writes to one entity.

### 8.2 Idle retention

Move `MaxRetainedIdleQueues = 1024` into validated actor-runtime options. Retain opportunistic retirement rather than adding a timer per entity.

After a high-cardinality burst drains, every actor mailbox must converge to at most its configured retained-idle count. The maximum transient entity-queue count becomes calculable as retained idle queues plus admitted high-cardinality messages and a small bounded creation race.

### 8.3 Ready queue

Do not replace `ActorReadyQueue` with a bounded channel in this tranche. A second independently bounded scheduler queue creates a failure point after mailbox acceptance and complicates ownership. The scheduling bit plus bounded admission already limits its logical population.

Add a deterministic invariant test proving that ready depth never exceeds the number of non-empty scheduled mailboxes and returns to zero after drain and shutdown.

## 9. Transport overload contracts

### 9.1 Commands and queries

On admission rejection:

1. reply immediately with a retryable transport failure;
2. use the proposed error code `-429` and a stable non-sensitive message;
3. dispose the rejected request exactly once after the reply attempt; and
4. record reply success or failure separately from the admission rejection.

The reply must deserialize as `ServiceResult<TResult>` for every query result type without deserializing the incoming query merely to learn `TResult`. The first implementation candidate is a structurally compatible `ServiceResult<object>` failure with a null value because the MessagePack representation uses the same four fields.

Before transport code depends on that behavior, contract tests must prove compatibility with:

- `ServiceResult<GuidResult>`;
- a scalar read model;
- a collection read model;
- another representative reference-type result; and
- both owned and legacy NATS message implementations.

If structural compatibility is not guaranteed, the fallback design is a NATS status/error response that `NatsActorProducer` recognizes before typed body deserialization and converts locally into `ServiceResult<TResult>`.

The producer should expose overload as a normal unsuccessful service result, not as a domain-validation event and not as an opaque serialization exception.

### 9.2 JetStream events

On admission rejection:

1. release the rejected branch payload;
2. complete the fan-out handoff as failed;
3. issue a delayed NAK after all branches report their result;
4. do not ACK any delivery with a rejected required branch; and
5. count rejection, NAK, and redelivery separately.

The delay must be configurable and non-zero in enforcement mode to prevent an immediate redelivery loop while the host remains saturated. JetStream `MaxAckPending`, requested batch/message count, dispatch stripe capacity, NAK delay, and actor admission capacity must be tuned as one system.

At-least-once delivery means duplicates remain possible. Existing idempotency and duplicate-suppression rules continue to apply; overload control does not provide exactly-once actor effects.

### 9.3 Core fire-and-forget traffic

Core NATS cannot recover a message after local consumption. Before `Enforce` is enabled, inventory each Core fire-and-forget route and classify it:

| Classification | Overload behavior |
| --- | --- |
| Request/reply Command or Query | Return retryable overload result. |
| Durable business Event | Deliver through JetStream and use delayed NAK/redelivery. |
| Core copy of a durably published Event | Permit a measured optional live-copy drop only after the durable-copy invariant is tested. |
| Optional Notify/diagnostic traffic | Permit explicit measured drop according to configuration. |
| Required non-durable traffic | Block enforcement and migrate the route to a durable or request/reply contract. |

No category may default silently to drop. Unknown traffic classification is a startup configuration error in enforcement mode.

### 9.4 In-process callers

In-process callers receive the same structured admission result. Compatibility APIs may translate it into `ActorOverloadedException`, but new APIs should preserve the retryable reason. Rejected caller-owned messages must not be disposed by the actor runtime.

## 10. Configuration design

Add validated options under `ActorRuntime:Admission`. Names may be adjusted during implementation, but every concept must remain configurable.

```json
{
  "ActorRuntime": {
    "Admission": {
      "Mode": "ObserveOnly",
      "GlobalMessageLimit": 0,
      "GlobalByteLimit": 0,
      "MaximumPayloadBytes": 0,
      "DefaultActorTypeMessageLimit": 0,
      "DefaultActorTypeByteLimit": 0,
      "ActorTypes": {
        "Command": { "MessageLimit": 0, "ByteLimit": 0 },
        "Query": { "MessageLimit": 0, "ByteLimit": 0 },
        "Event": { "MessageLimit": 0, "ByteLimit": 0 },
        "Notify": { "MessageLimit": 0, "ByteLimit": 0 },
        "UI": { "MessageLimit": 0, "ByteLimit": 0 }
      },
      "DefaultMailboxMessageLimit": 8192,
      "RetainedIdleMailboxesPerActor": 1024,
      "JetStreamNakDelayMilliseconds": 250,
      "OverloadErrorCode": -429
    }
  }
}
```

Zero values above are placeholders indicating that capacity selection is unfinished; they are not proposed production values. `Enforce` must fail startup when a required limit is zero, negative, internally inconsistent, or larger than the applicable parent limit.

Move NATS dispatch stripe capacity from its hard-coded value into validated NATS options. Validate the combined NATS and actor settings at startup.

### 10.1 Capacity selection worksheet

Record these inputs before selecting values:

- actor-host memory budget reserved for queued work;
- maximum and p50/p95/p99 serialized payload size by actor type;
- measured queue-envelope and empty-entity-queue footprint;
- number of actor types and registered actor mailboxes;
- fixed worker count;
- normal, market-open, reconnect, and replay arrival rates;
- p95/p99 handler time and drain rate by actor type;
- NATS subscription and stripe capacities; and
- JetStream `MaxAckPending`, batch request size, and redelivery delay.

The documented bound must include at least:

```text
actor queued payload bytes       <= GlobalByteLimit
actor queue/envelope overhead    <= GlobalMessageLimit * measured envelope cost
executing payload bytes          <= WorkerCount * MaximumPayloadBytes
ready queue reference overhead   <= GlobalMessageLimit * measured ready-entry cost
retained idle queue overhead      <= sum(actors * retained-idle limit * measured empty-queue cost)
Core ingress payload bytes        <= bounded subscription + stripe entries at maximum payload
JetStream ingress payload bytes   <= bounded outstanding + stripe entries at maximum payload
```

Because several structures may reference the same pooled event payload, the simple formula may conservatively overestimate memory. Capacity approval should prefer a safe upper bound over a fragile average.

## 11. Observability additions

Add low-cardinality instruments:

| Instrument | Type | Tags |
| --- | --- | --- |
| `ifm.actor.admission.rejected` | Counter | `actor.type`, `reason` |
| `ifm.actor.admission.in_use` | Up/down counter | `actor.type` |
| `ifm.actor.admission.bytes_in_use` | Up/down counter | `actor.type` |
| `ifm.actor.admission.payload.size` | Histogram | `actor.type` |
| `ifm.actor.mailbox.retired` | Counter | `actor.type` |
| `ifm.actor.mailbox.limit` | Counter | `actor.type` |
| `ifm.nats.overload.replies` | Counter | `actor.type`, `outcome` |
| `ifm.nats.overload.naks` | Counter | `actor.type`, `outcome` |
| `ifm.nats.overload.optional_drops` | Counter | `actor.type`, `traffic.class` |

The `reason`, `outcome`, and `traffic.class` values must be closed enums converted to stable strings. Do not tag entity IDs, subjects, verbs, payload type names, or error messages.

Dashboards should show:

- global and per-type message/byte utilization as a percentage of limits;
- rejection rate by bounded reason;
- hot mailbox-limit rate by actor type;
- command/query overload reply success and failure;
- JetStream NAK and redelivery rate;
- optional Core drops;
- backlog drain time after bursts; and
- allocation, GC, CPU, thread-pool, and working-set correlation.

Expose sustained saturation through a dedicated health check. Never fail process liveness solely because capacity is full. Readiness or a separate capacity status may become Degraded after a configured duration; deployment behavior must avoid creating a feedback loop that removes capacity during a burst.

## 12. Implementation tranches

### Tranche A: Contracts, options, and baseline

1. Add admission modes, options, validation, result, and reason types.
2. Add payload-size reporting to every `IActorMessage` implementation.
3. Move mailbox, retained-idle, dispatcher-stripe, and JetStream outstanding capacities into configuration.
4. Add observe-only metrics and payload-size evidence.
5. Create a capacity worksheet from normal and burst test traffic.

**Review gate:** approve actual count/byte limits and Core traffic classifications before enforcement.

**Implementation status (2026-08-09):** Complete in the working tree. The actor runtime now has validated admission contracts, exact serialized-payload size reporting, configurable actor and NATS capacity geometry, and observe-only message/byte accounting. The initial admission benchmark reported 65.910 ns incremental cost with no reported allocation or lock contention. Production count/byte limits and Core traffic classifications remain pending in `Actor-Backlog-Capacity-Worksheet.md`.

### Tranche B: Runtime enforcement

1. Implement the allocation-free uncontended admission controller.
2. Integrate atomic reserve/rollback into `ActorThreadQueues`.
3. Make per-entity admission non-waiting in the production path.
4. Carry the byte charge in the queue envelope.
5. Release permits on dequeue, stop drain, failed write, retired retry, and every exceptional path.
6. Make cold entity-queue creation race-safe.
7. Configure idle queue retention.

**Review gate:** deterministic concurrency tests and microbenchmarks pass before transport rejection is enabled.

**Implementation status (2026-08-09):** Complete. The runtime uses atomic process and actor-type count/byte reservation, a structured admission result, non-waiting enforced mailbox slots, one reservation across retired-queue retry, and release on dequeue, failed publish, cancellation, exception, and stop drain. Explicit create/`TryAdd`/stop makes cold mailbox creation race-safe. Enforced accepted admission adds 57.38 ns over disabled operation with no reported allocation or lock contention; rejection paths range from 4.42 ns for a full global count budget to 91.94 ns for a full entity mailbox. Focused concurrency, high-cardinality, ownership, retirement, shutdown, and real-network NATS tests pass.

### Tranche C: Transport behavior

1. Migrate Core NATS dispatchers to structured admission results.
2. Implement and verify generic-compatible command/query overload replies.
3. Migrate JetStream primary and fan-out dispatch to delayed NAK on rejection.
4. Implement the approved optional Core policy and block unknown required traffic.
5. Verify exact pooled-buffer disposal and fan-out reference counts.
6. Add transport overload metrics.

**Review gate:** real-network NATS integration tests prove reply, NAK, redelivery, and ownership behavior.

**Implementation status (2026-08-09):** Complete in the working tree. Core command/query rejection returns the stable retryable `ServiceResult<object>` wire shape with error code `-429`; owned and legacy clients deserialize it as representative typed `ServiceResult<TResult>` values. Rejected messages are disposed exactly once even when reply publication fails. Required JetStream fan-out branches coordinate one final ACK or a configured delayed NAK, and redelivery is counted independently. Transport metrics cover overload reply outcomes, NAK outcomes, redelivery, and explicit optional drops.

At Tranche C completion, the checked-in Core inventory classified Query as request/reply-only, Command and Supervisor fire-and-forget traffic as required non-durable, Event as a durable live copy, and Notify/UI as optional. This deliberately blocked `Enforce` until the required routes were migrated. Tranche D resolves that code-level blocker; production remains `ObserveOnly` until capacity values are approved from production-like evidence.

### Tranche D: Rollout and confirmation

1. Deploy `ObserveOnly` with selected capacities.
2. Compare would-reject counts with expected opening and replay bursts.
3. Enable `Enforce` in integration and paper-trading environments.
4. run sustained hot-entity, high-cardinality, mixed-traffic, reconnect, and replay tests;
5. verify memory stays below the calculated bound and the backlog drains;
6. run the full domain integration gate; and
7. record results in `System-Wide-Optimization-Results.md` before marking SWO-02 Complete.

**Implementation status (2026-08-09):** Local confirmation is complete. Production command sends now use request/reply and preserve typed transport failures, commandless exception notifications use the durable Event route, and the unused Supervisor consumer is no longer started. Enforced consumers reject unknown or required-non-durable classifications at startup. Release stress tests prove bounded mixed-traffic accounting and immediate hot-mailbox rejection, and real-network tests prove typed Core rejection plus delayed JetStream recovery. The complete solution and domain gates pass. Production remains `ObserveOnly`; deployment, paper-trading observation, measured memory inputs, selected capacities, and explicit activation approval remain external rollout work.

## 13. Test plan

### 13.1 Admission-controller unit tests

- admits exactly the configured message and byte capacity;
- rejects the next reservation with the correct reason;
- enforces actor-type limits below the process limit;
- rejects an oversized single payload;
- rolls back earlier dimensions when a later dimension rejects;
- never exceeds a limit under many concurrent producers;
- returns every counter to zero after release;
- Disabled and ObserveOnly modes preserve their documented behavior; and
- invalid or incomplete Enforce configuration fails startup.

### 13.2 Queue and scheduler tests

- accepted messages preserve FIFO order and entity concurrency of one;
- a full entity mailbox rejects without leaving a suspended writer;
- one hot entity cannot consume reservations beyond its mailbox limit;
- high-cardinality admission never exceeds global count or byte limits;
- cold-key races create one retained queue and dispose every losing queue;
- retired-queue retry transfers one reservation without double charge;
- dequeue and stop-drain release exactly once;
- ready depth remains logically bounded and returns to zero;
- accepted work drains during shutdown while new work receives `Stopping`; and
- every accepted or rejected message is disposed exactly once by its owner.

### 13.3 NATS unit tests

- overload failure payload deserializes into representative `ServiceResult<TResult>` types;
- owned command/query rejection replies and disposes once;
- reply failure still disposes once and records a failure;
- owned JetStream event rejection releases its branch once;
- one rejected fan-out branch produces one final NAK and no ACK;
- all accepted branches produce one ACK;
- optional Core drop requires explicit classification and increments its metric; and
- unknown Core classification fails enforcement startup.

### 13.4 Real-network integration tests

- Core command overload returns the retryable service failure before request timeout;
- Core query overload returns the same typed contract;
- JetStream overload NAKs and redelivers after capacity is released;
- `MaxAckPending` halts further server delivery at the configured bound;
- repeated NAK/redelivery does not leak pooled payloads or duplicate acknowledgements;
- reconnect and shutdown preserve admission accounting; and
- accepted messages maintain per-entity ordering across saturation and recovery.

### 13.5 Full regression gate

Run, in Release configuration:

- `TomasAI.IFM.Shared.UnitTests`;
- `TomasAI.IFM.Framework.Messaging.Nats.UnitTests`;
- `TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests` when its NATS prerequisites are available;
- all ten domain integration projects, currently 193 tests;
- the complete `TomasAI.IFM.sln` build; and
- `git diff --check`.

## 14. Benchmark and stress plan

### 14.1 BenchmarkDotNet

Add `ActorAdmissionBenchmarks` alongside the current actor queue and metrics benchmarks. Measure:

- disabled admission baseline;
- enforced accepted path below capacity;
- global count rejection;
- global byte rejection;
- actor-type rejection;
- mailbox rejection;
- one producer and 2/4/8 concurrent producers;
- single entity versus high-cardinality entities; and
- metrics disabled versus enabled.

Record mean, error, standard deviation, allocated bytes, Gen0/1/2, completed work items, and lock contentions.

Proposed accepted-path threshold for review:

- zero heap allocation per admitted or rejected queue operation when no transport reply is required;
- zero monitor-lock contention in the admission controller;
- no more than 75 ns incremental uncontended admission cost on the current benchmark host; and
- no material representative-pipeline throughput regression beyond measurement variance without a documented safety tradeoff.

The absolute threshold is more important than a percentage against an approximately 80 ns mailbox microbenchmark.

### 14.2 Stress scenarios

1. **Hot entity:** sustained writes to one entity beyond its mailbox limit.
2. **High cardinality:** one message each for more unique entities than the global count limit.
3. **Mixed actor types:** Command, Query, and Event traffic competing for the global budget.
4. **Large payload:** payloads near the configured maximum byte charge.
5. **Fan-out:** one JetStream event routed to several actor mailboxes.
6. **Slow storage:** handler latency increased while ingress remains high.
7. **Reconnect/replay:** JetStream backlog delivered at replay speed.
8. **Shutdown under saturation:** stop intake, drain accepted work, and verify zero accounting residue.

Every scenario records maximum message/byte utilization, rejection/NAK/reply counts, p50/p95/p99 queue and handler latency, drain time, CPU, allocation, GC, working set, and ownership failures.

## 15. Acceptance criteria

SWO-02 can move to Complete only when:

1. configured message and byte limits are never exceeded in enforcement tests;
2. the documented memory formula covers actor backlog, transport buffers, executing handlers, ready entries, and retained queues;
3. no accepted message, reservation, pooled buffer, or queue resource leaks on any tested path;
4. per-entity FIFO and single concurrency remain deterministic;
5. commands and queries receive explicit retryable overload results;
6. overloaded durable events are NAKed and later redelivered without silent loss;
7. any optional Core drop is explicitly configured and observable;
8. graceful shutdown drains accepted work and rejects new work without hanging;
9. accepted and rejected hot paths satisfy the approved allocation and contention thresholds;
10. all focused, NATS integration, and 193 domain integration tests pass;
11. a production-like saturation run demonstrates bounded memory and backlog recovery; and
12. results, environment, configuration, and rejected alternatives are recorded in `System-Wide-Optimization-Results.md`.

## 16. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Limits are selected from averages and fail during market open. | Use maximum/p99 payloads, burst arrival rates, and a documented memory budget; roll out ObserveOnly first. |
| Immediate rejection turns a short burst into client retries. | Size buffers from measured drain capacity, return a retryable failure, and use delayed JetStream NAK. |
| Hot entities monopolize process capacity. | Apply a non-waiting per-mailbox cap before a message can remain admitted. |
| Actor-type caps waste capacity. | Start with measured hard caps; consider borrowing only after evidence, not in the first correctness tranche. |
| Core fire-and-forget traffic is lost. | Inventory and classify every route; migrate required traffic to JetStream or request/reply before Enforce. |
| Fan-out byte charging is too conservative. | Accept the safe overestimate initially; optimize shared charging only with measured need and ownership tests. |
| Generic overload reply is serialization-incompatible. | Make structural compatibility a contract-test gate and retain the NATS status-response fallback. |
| Admission counters leak during races or shutdown. | Centralized reverse-order rollback, queue-envelope charge, lifecycle tests, and zero-after-drain assertions. |
| A bounded ready queue deadlocks after mailbox acceptance. | Leave the ready channel unbounded and rely on the scheduling invariant plus bounded admission. |
| Health-based orchestration removes capacity during a burst. | Keep liveness healthy; expose saturation separately and review readiness behavior with deployment topology. |

## 17. Rollback strategy

1. Change `Mode` from `Enforce` to `ObserveOnly` or `Disabled` and restart the host.
2. Preserve the structured result and metrics code; disabling enforcement must bypass rejection while retaining existing queue behavior appropriate to that mode.
3. Do not roll back JetStream data or acknowledge messages solely to clear overload.
4. If overload replies are incompatible, disable enforcement before reverting the reply protocol.
5. Retain all overload metrics and test evidence so capacity failures are not rediscovered without context.

## 18. Review checklist

The reviewer should explicitly approve or change:

- non-waiting admission versus bounded asynchronous waiting;
- count plus byte budgeting;
- reservation release at dequeue;
- global and actor-type hard caps without priority borrowing;
- proposed overload error code `-429`;
- delayed JetStream NAK policy;
- Core traffic classification requirement;
- ObserveOnly-to-Enforce rollout;
- proposed 75 ns accepted-path overhead threshold; and
- the rule that actual production capacities are selected only after the Tranche A worksheet.

## 19. References

- `Documents/system/System-Wide-Optimization-Plan.md`
- `Documents/system/System-Wide-Optimization-Results.md`
- `Documents/system/Actor-Backlog-Capacity-Worksheet.md`
- `TomasAI.IFM.Framework.Messaging.Nats/PERFORMANCE.md`
- `TomasAI.IFM.Framework.Messaging.Nats.Benchmarks/RESULTS.md`
- [NATS Core delivery semantics](https://docs.nats.io/nats-concepts/core-nats)
- [JetStream consumer acknowledgements, MaxAckPending, and delayed NAK](https://docs.nats.io/nats-concepts/jetstream/consumers)

## 20. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.1 | 2026-08-09 | Created the review-ready SWO-02 implementation, test, benchmark, rollout, and rollback plan from the current actor and NATS runtime. |
| 0.2 | 2026-08-09 | Recorded approved Tranche A implementation, benchmark outcome, capacity worksheet, and the still-blocked enforcement gate. |
| 0.3 | 2026-08-09 | Recorded Tranche B atomic runtime enforcement, deterministic tests, microbenchmark gates, and the remaining Tranche C transport-policy boundary. |
| 0.4 | 2026-08-09 | Recorded Tranche C typed overload replies, Core traffic classification, delayed JetStream NAK/redelivery, ownership tests, transport metrics, and the remaining Tranche D rollout gate. |
| 0.5 | 2026-08-09 | Recorded Tranche D command-route migration, Supervisor removal, enforced stress and real-network confirmation, complete regression gates, and the remaining production capacity/activation evidence. |
