# Realtime Actor JetStream Mailbox Design

**Version:** 0.1\
**Updated:** 2026-11-25\
**Status:** Comprehensive remaining-work audit completed: 22 core packages plus 2 adjacent account safety packages; 12 focused counterexamples verified. Production mailbox implementation/qualification remains incomplete
**Execution:** [v2.0 complete parallel plan](Realtime-Actor-JetStream-Mailbox-Implementation-Plan-v2.0.md)\
**Scope:** Actor runtime and transport infrastructure for `ActorType.Realtime`

The staged work and qualification gates are recorded in the
[implementation plan](Realtime-Actor-JetStream-Mailbox-Implementation-Plan-v1.0.md).

Realtime mailboxes will use JetStream as their authoritative pending-message queue. Each logical
`ActorThreadId` will have independent delivery and acknowledgement state. A busy destination must not
make an empty, unrelated mailbox reject messages or wait behind its dispatch stripe.

The initial development test profile defaults to JetStream memory storage. The same backend will
support file storage for a controlled comparison over two complete exchange trading sessions.
Memory storage provides recovery across an API restart while NATS remains running; it does not
provide recovery across loss of the single NATS server. It cannot, by itself, meet an unconditional
requirement that accepted messages are never lost.

## Scope and compatibility

- Apply the new mailbox backend only to `ActorType.Realtime`. Do not change that enum value into
  `ActorType.Event`, its business message contracts, or the domain-event replay implementation.
- Preserve Command, Query, Function and high-level domain Event delivery and admission behaviour.
- Preserve public actor handlers, strategy pipelines, financial rules, existing event sourcing and
  higher-level send APIs. Intercept transport and scheduling internally.
- Replace the large local Realtime message queue with broker pending messages and bounded in-flight
  buffers. Keep the ready-mailbox scheduler as execution coordination, not as payload storage.
- Remove the old shared Realtime queued-message rejection boundary for migrated traffic. Retain
  process telemetry and the existing admission rules for non-Realtime traffic. Bound Realtime RAM
  through explicit fetch, payload and execution limits.
- Make no new display/drop/coalescing classification in this design. The 25 registered Realtime
  destinations include mixed business/display handlers. Inventory existing explicit snapshot
  coalescing separately so it is neither accidentally widened nor silently changed between tests.

This is a contained runtime/transport change, but not a small queue-constructor substitution. It
requires publisher, routing, scheduler, acknowledgement and lifecycle integration. If an existing
handler lacks safe redelivery, record that as an implementation blocker rather than silently
changing its business rules or claiming lossless execution.

## Verified current implementation

- `ActorThreadId` consists of actor category, actor name and entity ID. The verb is not part of the
  mailbox identity; different verbs for the same destination share ordering.
- `NatsActorConsumer` receives `Realtime.>`, creates destination branches and awaits writes into
  shared dispatch stripes. A full stripe can stop the common intake loop.
- The current configuration has four normal dispatch stripes, additional protected stripes,
  a 2,048-message ordinary mailbox default and an 8,192-message Realtime admission budget with
  a 1,024-message protected reserve.
- In enforced mode, ordinary asynchronous mailbox admission returns rejection when full.
  Protected delivery uses a waiting admission helper. Neither is destination-isolated intake.
- `ActorThreadV2` reads `IScheduledActorThreadQueue.TryRead` synchronously. The backend needs an
  asynchronous broker delivery interface and scheduler integration.
- `IActorDeliveryCompletion` already provides a completion hook after handling. Reuse it where
  appropriate, while verifying failure, retirement and cancellation outcomes before acknowledging.
- On 2026-11-24 the running NATS server reported version 2.12.0, approximately 47 GiB configured
  maximum memory, 12 GiB maximum file storage and a 120-second file sync interval. Those server
  ceilings are not suitable substitutes for measured mailbox and stream budgets.

## Mailbox identity and storage

Keep `ActorThreadId` as the logical key, independent of process ID, worker number, actor generation
or the lifetime of an in-memory queue object. Derive broker resource names from a stable canonical
encoding; never use process-dependent `GetHashCode` for durable identity.

Use a destination-addressed subject with exactly one subject token representing the mailbox key.
The event verb and complete original identity remain in a versioned envelope. This makes a
per-subject message limit apply to the whole logical mailbox, rather than separately to each verb.
Store and validate the canonical identity with its encoded key.

Related mailbox subjects can share a stream, preferably separated by destination actor to reduce
storage interference. Create an independent durable filtered pull consumer for each active logical
mailbox. WorkQueue retention is appropriate only with non-overlapping destination filters; a source
message intended for several destinations must first have a distinct destination envelope for each.
One consumer acknowledgement must not complete another destination's work.

Persist or reconstruct the destination registry independently of active in-memory mailbox objects.
Do not delete a consumer with outstanding work when its API queue object becomes idle or is evicted.
Consumer discovery, creation and retirement must be bounded and asynchronous, not part of every
message's execution path.

## Publication and processing

1. Resolve the required destinations using the existing routing semantics. Preserve the route
   revision or destination manifest so recovery does not silently change which recipients were owed.
2. Encode the source message and original origin, timestamp, dataset, generation and causation
   metadata with the destination identity and stable source/destination delivery ID.
3. Publish each destination envelope with JetStream publication acknowledgement. Record a source
   handoff as complete only when every required destination publication is confirmed or a recoverable
   publication outbox owns the remaining work.
4. Fetch a bounded number of messages and bounded payload bytes when execution capacity is available.
   Pending work stays on the broker. Do not fetch an unlimited backlog into client buffers.
5. Execute the existing handler with one processing owner for that `ActorThreadId`. Other mailboxes
   can progress concurrently under the existing worker budget. Broker waits must not occupy all
   actor execution workers or introduce a shared stripe that blocks unrelated destinations.
6. Acknowledge after the handler's successful terminal processing or a confirmed durable handoff.
   An in-memory enqueue is not a successful durable handoff.
7. Preserve the failing message for recovery and do not execute later messages from that mailbox
   ahead of it. Other mailboxes continue. Transport recovery is separate from retries inside a
   strategy decision; do not add decision-loop retries.

JetStream redelivery is at least once. Stable delivery IDs, existing commit identities and verified
idempotency must prevent duplicate workflows, fills, reservations and other business effects.
Publication deduplication windows alone do not establish permanent business idempotency.

A restart or generation change must not acknowledge an unfinished old handler or restamp an old
message as fresh. Fence retired execution and late acknowledgements. Preserve current signal-age
and domain eligibility semantics in the unchanged handlers.

The ingress assurance gate must include Rust publishers. Moving only messages already received by
the optional Core NATS reader into JetStream leaves an earlier loss window. C# publisher interception
and any required Rust transport acknowledgement/source-recovery integration belong to this runtime
work package; strategy/indicator business code remains unchanged. Existing raw-history journaling
must not be assumed to reconstruct arbitrary destination workflows without verified routing evidence.

Test handlers that publish to their own mailbox or form delivery cycles while a destination is full.
Do not wait indefinitely inside a handler for space that only that same handler can release.
The transport may require a recoverable outgoing-publication outbox. Its ownership, bounds and cost
must be explicit rather than hidden in unlimited suspended tasks.

## Memory and file storage

| Backend | Pending messages live in | API restart with NATS running | Single NATS server restart |
| --- | --- | --- | --- |
| JetStream Memory | NATS server RAM | Can resume retained pending work | Pending messages are lost without another recoverable source |
| JetStream File | NATS storage volume | Can resume pending work | Can recover stored work; OS/power-loss guarantees depend on sync/replication policy |

Both choices use JetStream queues and consumers. The memory option is not a return to the C#
in-process mailbox channel. A durable consumer name does not make RAM-backed messages persistent.

Memory is the experimental development default requested for this comparison. It is not qualified
as restart-safe storage for required financial or market-history deliveries. Existing authentic raw
storage journals and domain-event durability remain enabled and unchanged.

Compare actual file performance instead of assuming that a disk write and flush occur for every
message. Record batching, storage volume, replication and sync policy. The running server's current
120-second sync setting must not be presented as a per-message power-loss guarantee.

Storage type is a stream-level creation choice. Use distinct test streams and explicit drain/cutover
between backends; do not assume an existing memory stream can be switched to file in place.

## Limits and full queues

Both backends require finite limits. Memory queues can exhaust broker RAM; file queues can fill the
storage volume. Independent consumption does not remove those physical limits.

| Limit | Purpose |
| --- | --- |
| `MaxMsgsPerSubject` | Bound retained work for one encoded mailbox identity |
| `MaxMsgSize` | Bound one payload and help calculate worst-case fetch memory |
| Stream `MaxBytes` and optional `MaxMsgs` | Bound the destination stream's aggregate storage |
| Consumer `MaxAckPending` | Bound fetched but unacknowledged work independently for each mailbox |
| Fetch message/byte bounds | Bound API client buffers and in-flight ownership |
| NATS account/server storage limits | Protect total broker resources |

For work that must be preserved, use WorkQueue retention, explicit acknowledgements, `DiscardNew`
and `DiscardNewPerSubject` with the per-mailbox message limit. Do not use `DiscardOld` or automatic
age/TTL eviction for unprocessed required work. When capacity is exhausted, reject the new publish
explicitly and retain it at its recoverable source/outbox until delivery can resume. The rejected
publication must not be reported as successfully accepted and then disposed.

NATS 2.12 supports the listed per-subject discard control. It does not give every consumer its own
physical storage reservation. One mailbox can still fill a shared actor stream; record this limit
and test it. Cross-actor isolation must be tested with separately budgeted actor streams. Any finer
physical-stream isolation needs evidence from those tests rather than a claim of unlimited isolation.

Choose numeric budgets from payload p50/p95/p99/maximum, measured burst rate, drain rate and desired
outage coverage. A starting calculation is retained bytes approximately equal to excess arrival
bytes per second times burst duration, plus measured overhead and headroom. Record the allocation
across streams and leave broker/OS headroom. Do not adopt the current approximately 47 GiB server
memory ceiling as the default queue allowance or reuse 2,048 without measuring it.

## Qualification over two full trading days

Compare the current local-mailbox baseline, JetStream Memory and JetStream File. The two broker
backends must each cover the same two complete, calendar-qualified exchange trading sessions,
including overnight activity, cash open/close, EOD and session rollover. Shortened holidays are
reported separately and do not silently substitute for full-session evidence.

Capture identical source envelopes and destination manifests and preserve arrival timing for matched
replay. Run both backends against isolated test namespaces and databases with the same handlers and
configuration. Never let the second copy submit live orders, alter production balances or publish
into authoritative workflow/feed subjects. If simultaneous shadows compete for machine resources,
also run sequential matched replays on the same hardware and report contention separately.

| Evidence | Required comparison |
| --- | --- |
| Latency | Publication acknowledgement, queue wait, handler and end-to-end p50/p95/p99/max by destination |
| Throughput | Arrival/processing rates, maximum backlog age and burst drain time |
| Resources | API and NATS CPU, allocation, LOH/Gen 2/pause, working set, broker RAM, file IO and storage |
| Integrity | Accepted source/destination identities accounted for, order preserved, no duplicate business effects |
| Isolation | Saturate one mailbox and one actor stream; quantify unrelated destination delay and failures |
| Recovery | API restart, actor replacement, connection interruption, broker restart, full queues and failed publication |
| Lifecycle | Shutdown, consumer discovery, idle eviction, pending-work retirement and backend cutover |

Run BenchmarkDotNet before and after for publish/ack and handler dispatch under representative
payloads, bursts and high mailbox cardinality. Qualify small bounded prefetch as well as one-message
correctness cases; do not assume one broker round trip per message is sufficient for the tick rate.
Benchmark file and memory with identical retention, acknowledgement, prefetch and replica settings.

Memory's broker-restart loss boundary must be demonstrated on disposable infrastructure and clearly
reported; it cannot pass the same restart durability criterion as file storage without a separately
verified persistent source. File testing must distinguish broker-process restart from OS/power loss.
Do not destroy the active broker to prove this boundary.

Only one backend is authoritative at a time. Before changing it, stop new admission for the affected
routes, durably account for pending source publications, drain or explicitly migrate outstanding
work, then switch routing and verify identities. Rollback must not delete a stream with pending work.

## Implementation sequence and completion

1. Measure destination payloads and arrival bursts; finalise stream budgets and publication ownership.
2. Implement the Realtime-only transport envelope, stable mailbox registry and confirmed fan-out.
3. Add broker-backed scheduling and completion integration while preserving non-Realtime behaviour.
4. Verify ordering, deduplication, full-queue ownership, self-send safety and all recovery boundaries on
   disposable NATS/PostgreSQL, including consumer cleanup and source-generation handling.
5. Run BenchmarkDotNet and matched two-session qualification for memory and file backends.
6. Record results and choose deployment settings based on latency, throughput and the required
   failure guarantees. Do not mark the design implemented or the two-day trial complete beforehand.

No application code, live queue configuration or running profile is changed by this document update.

## References

- [Existing aggregate admission plan](Aggregate-Actor-Backlog-Overload-Control-Implementation-Plan.md)
- [Actor thread identity](../../TomasAI.IFM.Shared.Contracts/EventModelActor/ActorThreadId.cs)
- [Current shared realtime ingress](../../TomasAI.IFM.Framework.Messaging.Nats/NatsActorConsumer.cs)
- [Current actor scheduler](../../TomasAI.IFM.Application.EventModelActor/ActorThreadV2.cs)
- [Existing delivery completion hook](../../TomasAI.IFM.Shared.Contracts/EventModelActor/Contracts/IActorDeliveryCompletion.cs)
- [NATS stream storage, limits and discard configuration](https://github.com/nats-io/nats.docs/blob/master/nats-concepts/jetstream/streams.md)
- [NATS storage and failure guarantees](https://docs.nats.io/learn/jetstream/surviving-node-loss)
- [NATS bounded pull consumption](https://docs.nats.io/learn/jetstream/pull-consumers)

### Iron Condor request replay update - 2026-11-24

Original requests now survive clock/cache/policy drift through a checksummed persistent binary journal.
Both durable captures precede Function dispatch; completed captures remain for source redelivery.
The executed production-context counterexample still suppresses a failed notification on unchanged-cache
replay. Retain/recover pending publication independently of cache Apply before qualifying this
destination. Current results include 2,060 unit checks, 28 scoped abrupt-process cases, benchmark costs
and precise exclusions. No transport cutover or full-session qualification is implied.

### Iron Condor publication recovery update - 2026-11-24

The actual publication context now retains immutable pending notifications independently of cache
Apply, retries the same ID/payload/time/origin and drains pending work at actor startup. Confirmed
producer completion and durable archival precede publication completion. A sent marker does not
prove Core Notify subscriber processing. Full results include 2,074 Trade checks, 20 scoped abrupt
process cases, file-journal overhead and retention limits. Stage 1 stops at the executed Vertical
Spread required-request exception/successful-settlement counterexample. No cutover is qualified.

### Vertical Spread settlement update - 2026-11-24

Required plan/handoff errors now fail source settlement without optional metrics. All 2,090 Trade
checks and 12 scoped Memory/File abrupt-process cases passed with unchanged parameters and
PostgreSQL surrogate receipt/acceptance ledgers. Existing fingerprints tolerate later RequestedAtUtc;
original completed plan and exit command identities/bytes are preserved. Full financial execution,
parameter changes and real generation recovery remain unqualified. Stage 1 stops at the executed
Futures position required-request exception/successful-settlement counterexample. No cutover enabled.

### Futures position/clock settlement update - 2026-11-24

Required plan/handoff errors now fail settlement independently of metrics on both dispatch paths.
All 2,119 Trade checks and 24 scoped legacy-envelope Memory/File abrupt-process cases passed with
PostgreSQL surrogate receipts. A real Daily flatten same-source retry preserves its handoff. Newer
Daily exit-required plans, however, change authority under the same per-generation handoff ID;
the actual restored owner rejects them. Retain/claim and recover the original Daily exit intent
before qualifying this destination. Current results record scope limits and BenchmarkDotNet means.
No production transport cutover or full financial replay qualification is implied.

### Daily exit-intent ownership update - 2026-11-24

A first-authority binary journal now precedes Daily exit dispatch, owner success must echo the
original command ID, and later observations reuse the confirmed intent. Startup checks current
position identity before replaying pending commands. All 2,132 Trade checks and 20 scoped Memory/File
abrupt-process cases passed with actual owner domain logic and PostgreSQL surrogate persistence.
Current results record file/lookup costs, legacy/multi-host exclusions and financial qualification
limits. Stage 1 stops at the executed Futures route failed-ServiceResult/successful-ACK/consumed-cursor
counterexample. Per-route delivery receipts and cursor ownership need recovery before cutover.
