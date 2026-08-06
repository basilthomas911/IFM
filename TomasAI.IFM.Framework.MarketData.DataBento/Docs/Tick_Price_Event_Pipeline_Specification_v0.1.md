# Databento tick-price event pipeline specification

**Version:** 0.1

**Status:** Codex-ready implementation specification; readiness-gate decisions remain intentionally unresolved

**Date:** 2026-08-06

**Managed target:** .NET 10 (`net10.0`), x64

**Native input:** Databento C++ SPSC ring containing canonical 64-byte market-data records

**Codex execution rule:** Codex must evaluate the readiness gate in section 25,
produce a repository-grounded implementation plan, and wait for approval before
editing production code. It must not invent unresolved actor or persistence
contracts.

## 1. Purpose

This document specifies the proposed managed pipeline that converts every
Databento market-data record into one of two actor-event categories:

1. `TickPriceChanged` when the comparable price for one ticker changes.
2. `TickPriceData` containing a bounded batch of ticks whose comparable price
   did not change.

The design preserves every source tick while reducing the number of individual
messages produced for repeated prices. It prevents unbounded in-memory history
by emitting a full `TickPriceData` buffer whenever its configured capacity is
reached, even when the price remains unchanged indefinitely.

The downstream side of the ticker channel will be an actor-event producer. That
producer will convert or serialize the channel items as actor messages and
publish them for processing by a tick manager actor and a tick aggregator actor.
The tick-data persistence target is ScyllaDB.

The exact actor message schemas, actor routing, actor behavior, and ScyllaDB
schema are deliberately reserved for later revisions of this document.

## 2. Scope

This version specifies:

- the boundary between the existing Databento feed and the new manager;
- per-ticker and per-price-kind state;
- price-change detection;
- bounded unchanged-price buffering;
- event ordering;
- channel ownership and backpressure;
- buffer ownership and lifetime;
- graceful stop and fault behavior;
- required metadata and conceptual message contracts;
- correctness invariants;
- metrics, tests, and CSV-driven capacity analysis;
- extension points for actors and ScyllaDB persistence.

This version does not specify:

- final actor class names, actor subjects, verbs, or entity IDs;
- MessagePack key assignments or message versioning attributes;
- NATS subjects, streams, consumers, or acknowledgement policies;
- final tick manager or tick aggregator actor state machines;
- ScyllaDB keyspace, table, partition, clustering, or retention definitions;
- the final comparable-price policy for quote, trade, and MBO records;
- the production buffer capacity;
- replay, snapshot, or recovery behavior beyond required source identities.

## 3. Proposed pipeline

```text
Databento live session
    -> native C++ producer thread
    -> native 64-byte SPSC ring
    -> managed drain thread
    -> pooled MarketDataBatch64
    -> TickPriceChangedManager
         -> TickPriceChanged channel item
         -> TickPriceData channel item
    -> bounded single-reader ticker channel
    -> actor-event producer
    -> actor messaging transport
         -> tick manager actor
         -> tick aggregator actor
    -> ScyllaDB tick-data persistence
```

The native producer, canonical record layout, ring, and managed drain remain
unchanged. Price-change detection belongs after the managed drain has obtained a
valid `MarketRecord64` and before its `MarketDataBatch64` lease is returned.

## 4. Terminology

### 4.1 Source tick

One immutable `MarketRecord64` received from the existing managed feed. A source
tick is a quote, trade, or MBO record and retains its Databento timestamps,
sequence, publisher ID, instrument ID, schema, flags, and payload.

### 4.2 Ticker identity

The canonical runtime identity is `InstrumentKey(PublisherId, InstrumentId)`.
Raw symbols are descriptive metadata and must not replace the provider identity
as the state dictionary key.

### 4.3 Comparable price

The raw signed 1e-9 fixed-point `long` selected from a source record for
price-change comparison. Comparisons must use the raw integer. Decimal or
floating-point conversion is forbidden on the hot path.

### 4.4 Price kind

The semantic meaning of a comparable price, for example:

- last trade;
- bid;
- ask;
- quote midpoint;
- MBO order price.

Prices with different meanings must not share one comparison state. The state
key is therefore conceptually `(InstrumentKey, PriceKind)`, called a price lane.
The final supported `PriceKind` values are deferred to the message-schema phase.

### 4.5 Changed tick

A source tick whose comparable price differs from the current price stored for
its price lane. The first valid comparable price is also a changed tick because
it establishes the initial price.

### 4.6 Unchanged tick

A source tick whose comparable price equals the current price stored for its
price lane. It is added to that lane's bounded tick-data buffer.

### 4.7 Boundary

One detected price change. A unique boundary identity correlates the
`TickPriceChanged` event with the partial `TickPriceData` event flushed because
of that change.

## 5. Binding decisions

1. Every valid source tick is represented exactly once: either by a
   `TickPriceChanged` event or inside one `TickPriceData` event.
2. A changed tick is not duplicated in the unchanged-price buffer.
3. A separate state machine exists for every active price lane.
4. Raw `long` prices are compared for exact equality.
5. Buffers have fixed, configured capacity and never grow.
6. A full buffer is published immediately when no price change has occurred.
7. When a price changes, `TickPriceChanged` is written to the ticker channel
   before any partial previous-price buffer is written as `TickPriceData`.
8. A partial buffer is not published on a price change when its count is zero.
9. A full-buffer publication does not produce a `TickPriceChanged` event and
   does not alter the lane's current price.
10. Remaining partial buffers are published during graceful stop and ticker
    removal.
11. The manager is owned by the existing single managed drain/consumer thread.
    Its mutable lane state requires no locks.
12. The ticker output channel is bounded, has one writer and one reader, and
    applies backpressure. It never silently drops or overwrites an event.
13. Once a pooled buffer is written to the channel, ownership transfers to the
    channel consumer. The manager must not reuse it.
14. The actor-event producer returns pooled transport memory only after the
    actor message has been completely serialized or copied.
15. Cancellation and graceful stop cannot abandon a partially filled buffer.
16. Source sequence and timestamp metadata are retained so downstream consumers
    can reconstruct source chronology even though boundary-first channel order
    is intentionally different from source-tick order.

## 6. Comparable-price policy

The manager requires a deterministic function:

```text
TrySelectPrice(MarketRecord64 record)
    -> zero or more (PriceKind, RawPrice) values
```

The final policy is deferred because quote, trade, and MBO prices have different
semantics. The implementation must obey these rules:

- A trade price must never be compared directly with a bid, ask, midpoint, or
  MBO order price in the same lane.
- Databento's undefined raw price value (`long.MaxValue`) is not a valid
  comparable price unless a future specification explicitly defines it.
- A record with no valid selected price follows a documented non-price policy;
  it must not silently corrupt the current price.
- If one source record produces multiple price kinds, each kind is processed in
  deterministic configured order.
- The selected policy and its version must be visible in health and event
  metadata.

The CSV-enabled soak test captures every raw quote, trade, and MBO field and is
the input for choosing this policy.

## 7. Per-lane state

Each active price lane owns the following logical state:

```text
PriceLaneState
    InstrumentKey
    RawSymbol
    PriceKind
    HasCurrentPrice
    CurrentRawPrice
    CurrentPriceEstablishedBySourceSequence
    CurrentPriceEstablishedAtEventTimestamp
    BufferOwner
    BufferedCount
    FirstBufferedSourceSequence
    LastBufferedSourceSequence
    FirstBufferedEventTimestamp
    LastBufferedEventTimestamp
    FirstBufferedReceiveTimestamp
    LastBufferedReceiveTimestamp
    NextChannelSequence
```

`BufferOwner` refers to fixed-capacity memory obtained from a dedicated pool.
The buffer stores owned copies of canonical tick data; it must never retain a
span, reference, or `MarketDataBatch64` lease after that batch is disposed.

Lane creation is lazy on the first valid comparable price. Lane removal occurs
only after any partial buffer has been published or an explicit terminal fault
has been recorded.

## 8. State machine

### 8.1 Initial price

When a valid comparable price is received and `HasCurrentPrice` is false:

1. Create a boundary identity.
2. Write a `TickPriceChanged` channel item with no previous price and with the
   changed source tick.
3. Set the lane's current price to the selected raw price.
4. Record the establishment source sequence and timestamps.
5. Leave the unchanged buffer empty.

### 8.2 Unchanged price with available capacity

When the selected price equals `CurrentRawPrice` and the buffer is not full:

1. Copy the source tick into the next buffer slot.
2. Initialize first-buffered metadata when this is the first item.
3. Update last-buffered metadata.
4. Increment the buffered count.
5. If the count is now equal to capacity, follow section 8.3 immediately.

### 8.3 Unchanged price fills the buffer

When an unchanged tick fills the buffer:

1. Create a `TickPriceData` channel item with emission reason `BufferFull`.
2. Include the current raw price, full buffer count, capacity, source range,
   timestamp range, and owned buffer.
3. Write the item to the ticker channel.
4. Transfer buffer ownership to the channel consumer.
5. Rent a replacement buffer or defer renting until the next unchanged tick.
6. Reset all buffered range metadata.
7. Keep `CurrentRawPrice` unchanged.

This rule bounds memory and permits an indefinitely unchanged market price
without accumulating an indefinitely large managed object.

### 8.4 Price change with a partial previous-price buffer

When the selected price differs from `CurrentRawPrice` and buffered count is
greater than zero:

1. Create one boundary identity.
2. Write `TickPriceChanged` for the new price and changed source tick.
3. Create `TickPriceData` for the buffered previous-price ticks with emission
   reason `PriceChanged` and the same boundary identity.
4. Write that `TickPriceData` item after `TickPriceChanged`.
5. Transfer previous buffer ownership to the channel consumer.
6. Update the lane's current price to the new raw price.
7. Record the new establishment source sequence and timestamps.
8. Reset or replace the unchanged buffer.

The deliberate channel order is boundary first, preceding unchanged data
second. Source ranges and the shared boundary identity preserve the fact that
the buffered ticks occurred before the changed tick.

### 8.5 Price change with an empty buffer

When the selected price differs and buffered count is zero:

1. Write only `TickPriceChanged`.
2. Update the lane's current price and establishment metadata.
3. Do not create an empty `TickPriceData` event.

### 8.6 Graceful stop or ticker removal

For every lane with buffered count greater than zero:

1. Create `TickPriceData` with emission reason `FeedStopped` or
   `TickerRemoved`.
2. Write it to the channel.
3. Transfer ownership and clear lane state.

After every partial buffer is accepted by the channel, the manager completes
the writer. The channel consumer must drain all accepted items before its actor
producer reports completion.

### 8.7 Feed fault

A feed fault must be visible. If the input records remain valid and channel
publication is possible, the manager may publish partial buffers with emission
reason `FeedFaulted`. If safe publication cannot be guaranteed, it records the
unpublished tick count and source ranges in terminal health. It must never emit
partially initialized or reused memory.

## 9. Conceptual channel item contracts

The following fields describe required semantics, not final C# or actor schemas.

### 9.1 TickPriceChanged

Required conceptual fields:

- message/schema version;
- boundary identity;
- channel sequence;
- publisher ID and instrument ID;
- raw symbol;
- price kind;
- optional previous raw price;
- current raw price;
- source record kind;
- complete changed source tick or its lossless typed payload;
- source sequence;
- event timestamp nanoseconds;
- receive timestamp nanoseconds;
- local manager ingress timestamp for metrics only;
- price-policy version;
- feed/session identity;
- actor correlation and causation metadata, to be specified later.

### 9.2 TickPriceData

Required conceptual fields:

- message/schema version;
- optional related boundary identity;
- channel sequence;
- publisher ID and instrument ID;
- raw symbol;
- price kind;
- associated unchanged raw price;
- emission reason: `BufferFull`, `PriceChanged`, `FeedStopped`,
  `TickerRemoved`, or `FeedFaulted`;
- item count;
- configured buffer capacity;
- first and last source sequence;
- first and last event timestamp nanoseconds;
- first and last receive timestamp nanoseconds;
- immutable ordered tick payload containing exactly `item count` records;
- price-policy version;
- feed/session identity;
- actor correlation and causation metadata, to be specified later.

The serialized actor message must contain only owned immutable data. Pool owners,
spans, native pointers, and disposable transport leases are never serialized.

## 10. Ordering and identity

### 10.1 Source order

Records enter the manager in the order supplied by the existing managed drain.
Ticks stored inside one `TickPriceData` payload retain that order.

### 10.2 Channel order

Every price lane has a monotonic channel sequence. If one session writes all
lanes to one channel, a session-wide sequence is also required so total channel
order can be audited.

On a price boundary, channel order is:

```text
TickPriceChanged(new price, boundary B)
TickPriceData(previous price buffer, reason PriceChanged, boundary B)
```

On buffer exhaustion, channel order contains only:

```text
TickPriceData(current price full buffer, reason BufferFull)
```

### 10.3 Idempotency

Actor publication and ScyllaDB persistence must assume that delivery may be
retried. The final actor schemas require a stable idempotency identity derived
from feed/session identity, instrument identity, price kind, event category, and
source range or boundary identity. Random IDs alone are insufficient for replay
deduplication.

## 11. Buffer configuration and memory

Buffer capacity is a positive integer selected by deployment profile. Its final
default is deferred until CSV analysis is complete.

The principal retained payload bound is:

```text
active price lanes * buffer capacity * 64 bytes
```

Additional bounded memory consists of:

- per-lane state;
- the fixed buffer pool reserve;
- ticker-channel in-flight items;
- actor-producer serialization buffers.

The buffer implementation should use fixed arrays or pooled memory. It must not
use `List<T>` growth, per-tick objects, LINQ, boxing, closures, or per-record
tasks on the hot path.

Pool exhaustion follows backpressure; it does not allocate unbounded fallback
arrays. A production profile may preallocate enough buffers for all active
lanes plus the maximum number of channel items in flight.

## 12. Threading, locking, and channel semantics

- One managed feed drain thread calls the manager.
- The manager is not thread-safe by design and asserts single-thread ownership
  in diagnostic builds.
- Lane dictionaries and buffers are mutated only by that owner.
- The output ticker channel has one writer and one actor-producer reader.
- The channel uses bounded wait/backpressure behavior.
- Channel writes preserve order and never use fire-and-forget tasks.
- No lock is required in the manager hot path.
- Cold-path snapshots may copy counters using atomic reads without mutating
  lane state.

If actor publication is slower than feed consumption, pressure propagates from
the actor producer through the ticker channel to the manager and managed drain.
If pressure ultimately exhausts the native ring within its configured deadline,
the feed faults visibly rather than dropping ticks.

## 13. Actor integration placeholder

The ticker-channel reader will be an actor-event producer. A later revision must
specify at least:

1. The actor-message interfaces implemented by `TickPriceChanged` and
   `TickPriceData`.
2. Actor type, actor name, verb, subject, route, entity ID, and aggregate ID.
3. MessagePack keys and backward-compatible schema evolution.
4. Correlation, causation, command, event, and source IDs.
5. Which messages are consumed by the tick manager actor.
6. Which messages are consumed by the tick aggregator actor.
7. Whether one publication is routed to both actors or whether the producer
   emits actor-specific messages.
8. Actor mailbox capacity, ordering scope, retries, dead-letter behavior, and
   graceful shutdown.
9. How actor cancellation will integrate after solution-wide cancellation
   propagation is implemented.

The actor-event producer must preserve ticker-channel order for each price lane.
It disposes the channel item's pooled payload only after serialization or a safe
owned copy has completed.

## 14. Tick manager actor placeholder

A later revision should define whether the tick manager actor:

- owns the latest price per ticker and price kind;
- publishes higher-level price-transition notifications;
- validates source and channel sequences;
- detects gaps, duplicates, or out-of-order boundaries;
- maintains actor state and snapshots;
- routes tick-price changes to strategies, risk, UI, or analytics;
- participates in replay and recovery.

No behavior is binding until the actor message schema and actor responsibility
are added.

## 15. Tick aggregator actor placeholder

A later revision should define whether the tick aggregator actor:

- consumes `TickPriceData`, `TickPriceChanged`, or both;
- expands or retains the batched tick payload;
- validates count and source-range metadata;
- creates time, volume, trade, quote, or price-run aggregates;
- persists raw or aggregated ticks to ScyllaDB;
- controls write batching and concurrency;
- handles retries and idempotency;
- snapshots aggregation state;
- flushes partial aggregates during graceful shutdown.

## 16. ScyllaDB persistence placeholder

The persistence specification must later define:

- keyspace and table names;
- partition keys and clustering order;
- partition-size limits;
- event-time versus receive-time ordering;
- raw tick and aggregate tables;
- actor-message idempotency keys;
- batch sizes and prepared statements;
- consistency levels;
- write retry and timeout policy;
- TTL, compaction, retention, and archival policy;
- late, duplicate, or out-of-order tick handling;
- replay range queries and snapshot interaction;
- schema migration and compatibility testing.

ScyllaDB I/O must remain outside the Databento drain and manager hot paths.

## 17. Graceful shutdown

The required shutdown sequence is:

1. Stop accepting new feed records.
2. Drain every record already accepted from the native ring.
3. Process all drained records through the manager.
4. Flush every partial price-lane buffer with `FeedStopped`.
5. Complete the ticker-channel writer.
6. Allow the actor-event producer to drain the channel.
7. Await actor-message publication acknowledgements according to the future
   actor transport specification.
8. Return all pooled buffers.
9. Dispose feed resources.

Cancellation requests graceful shutdown; they do not authorize dropping
accepted records. Exact cancellation-token propagation remains part of the
solution-wide cancellation work already deferred elsewhere.

## 18. Failure semantics

The following are terminal or explicitly degraded conditions:

- invalid or undefined comparable price under the selected policy;
- source sequence gap when continuity is required;
- buffer pool exhaustion;
- ticker-channel write timeout or completion;
- actor producer failure;
- serialization failure;
- actor transport rejection or acknowledgement timeout;
- incomplete graceful drain;
- pooled-buffer ownership violation;
- CSV or diagnostic capture failure when capture is enabled for a test.

Production behavior must never silently discard a tick, allocate unbounded
memory, reuse a published buffer, or continue after continuity is unknown.

## 19. Required metrics

At minimum, expose:

- source ticks processed by record kind;
- valid and invalid comparable prices;
- active instruments and price lanes;
- price changes;
- unchanged ticks buffered;
- full-buffer emissions;
- price-change partial-buffer emissions;
- shutdown, removal, and fault flushes;
- empty-buffer price changes;
- total `TickPriceChanged` messages;
- total `TickPriceData` messages;
- total ticks inside `TickPriceData` messages;
- current, average, and maximum buffer occupancy;
- buffer pool available, rented, and exhausted counts;
- ticker-channel depth, capacity, waits, and wait duration;
- actor publications, acknowledgements, retries, and failures;
- source-to-manager and source-to-publication latency percentiles;
- sequence gaps, duplicates, and ordering violations;
- unpublished tick count and ranges on terminal failure.

The conservation metric must always satisfy:

```text
processed source ticks
    = changed ticks represented by TickPriceChanged
    + unchanged ticks represented inside TickPriceData
    + explicitly reported terminally unpublished ticks
```

In a successful run, terminally unpublished ticks must be zero.

## 20. CSV analysis plan

The long-running Databento smoke tests support lossless CSV capture through
`IFM_DATABENTO_TICK_CSV_DIRECTORY`. Before choosing the production buffer size,
analyze representative market-open, high-volatility, low-volatility, rollover,
and option-chain periods.

Required analysis per instrument and proposed price kind:

- total tick count and rate;
- price-change count and rate;
- unchanged run-length distribution;
- p50, p90, p95, p99, p99.9, and maximum run length;
- time duration of unchanged runs;
- simulated full-buffer emission count for candidate capacities;
- simulated partial emission count at price changes;
- event reduction ratio;
- payload-size distribution;
- estimated channel bandwidth;
- estimated pool and retained-memory requirements;
- shutdown partial-buffer frequency;
- quote-versus-trade and instrument-specific differences.

Candidate buffer capacities should be powers of two for simple sizing, but the
selected capacity must be justified by measured distributions rather than by
micro-optimization preference.

## 21. Test specification

### 21.1 Deterministic unit tests

Cover at least:

- first tick emits one change and no data buffer;
- repeated price below capacity emits nothing yet;
- repeated price exactly fills capacity and emits one full data event;
- repeated price across multiple capacities emits multiple full events;
- price change with empty buffer emits only the change;
- price change with partial buffer emits change then data;
- changed tick is absent from the previous-price data buffer;
- full-buffer emission retains the current price;
- independent instruments do not share state;
- independent price kinds do not share state;
- undefined price follows configured policy;
- stop and removal flush partial buffers;
- no empty data event is produced;
- buffer ownership transfers exactly once;
- channel sequences and source ranges are correct;
- every permutation conserves all input ticks.

### 21.2 Property and stress tests

Generate random tick streams and assert:

- no loss or duplication;
- bounded retained memory;
- exact per-lane raw-price grouping;
- deterministic output for identical input;
- correct output ordering;
- all rented buffers are returned;
- backpressure never changes data.

### 21.3 Integration tests

Use small buffer capacities to force full and partial emissions, run the actor
producer, and verify actor-message order, correlation, serialization, retries,
and idempotency.

### 21.4 Live smoke tests

Extend the existing Databento soak to compare:

- source ticks;
- changed-event ticks;
- ticks contained in data events;
- channel and actor publication counts;
- persisted ScyllaDB ticks when that phase is implemented.

Every count must reconcile before the test passes.

### 21.5 Benchmarks

BenchmarkDotNet coverage should measure:

- constant price below and across buffer capacity;
- price change every tick;
- realistic CSV-derived price-run distributions;
- one instrument versus large option chains;
- allocation rate;
- manager throughput and latency;
- channel publication with available capacity and backpressure;
- serialization of full and partial actor messages.

## 22. Acceptance criteria

The manager phase is complete only when:

1. All state-machine rules are implemented and unit tested.
2. Every input tick is conserved with zero duplication.
3. Memory is bounded by configured lanes, buffers, pool, and channel capacity.
4. Hot-path processing performs no per-tick managed allocation after warm-up.
5. Normal operation uses no manager lock.
6. Channel and buffer ownership are proven by tests.
7. Graceful stop flushes all accepted ticks.
8. Live soak counts reconcile from native production through channel output.
9. Benchmark results meet targets established after CSV analysis.
10. The actor message and persistence revisions of this document are approved.

## 23. Deferred decisions for the next revision

1. Final name: `TickPriceChangedManager`, `TickPriceChangeManager`, or another
   domain name.
2. Supported price kinds and quote/trade/MBO selection policy.
3. Production buffer capacity and per-profile overrides.
4. Whether a single source record may create multiple price-lane outputs.
5. Final actor message types and shared contracts project.
6. Actor subjects, verbs, entity IDs, aggregate IDs, and routing.
7. Tick manager actor responsibilities.
8. Tick aggregator actor responsibilities.
9. ScyllaDB raw-tick and aggregate schemas.
10. Actor transport acknowledgement, retry, and dead-letter semantics.
11. Replay, snapshot, and recovery semantics.
12. Retention and archival policies.

## 24. Codex implementation directive

This section converts the domain design into an executable repository task.
When Codex is asked to implement this specification, it must:

1. Read this document completely.
2. Read `Databento_Market_Data_Specification_v1.1.md` and the applicable phase
   implementation documents.
3. Inspect the current repository rather than assuming the file map below is
   unchanged.
4. Preserve unrelated working-tree changes.
5. Evaluate every mandatory item in the readiness gate.
6. Create a phased implementation plan tied to concrete files and tests.
7. Present the plan for review before making production-code changes unless the
   user explicitly says to proceed immediately.
8. Implement only approved work packages.
9. Verify each package before proceeding to its dependents.
10. Update this document when implementation decisions replace placeholders.

The words **must**, **must not**, **required**, and **forbidden** are binding.
The words **proposed** and **candidate** identify names or locations Codex must
confirm against the repository before use.

## 25. Codex readiness gate

Codex must report this gate as part of its plan. A mandatory unresolved item
blocks only the dependent work packages, not independent analysis or test-harness
work.

### 25.1 Already binding

- [x] Input records are canonical `MarketRecord64` values.
- [x] Runtime instrument identity is `InstrumentKey`.
- [x] Raw `long` fixed-point prices are used for equality.
- [x] State is isolated by instrument and price kind.
- [x] The first valid price emits `TickPriceChanged`.
- [x] Changed ticks are not duplicated in `TickPriceData`.
- [x] Unchanged ticks enter a fixed-capacity buffer.
- [x] A full unchanged buffer is emitted immediately as `TickPriceData`.
- [x] A price boundary writes `TickPriceChanged` before the previous partial
  `TickPriceData` buffer.
- [x] Empty `TickPriceData` events are forbidden.
- [x] Stop and ticker removal flush partial buffers.
- [x] One manager owner thread mutates state without locks.
- [x] Output backpressure is bounded and lossless.
- [x] Actor and ScyllaDB I/O remain outside the native ring/drain hot path.

### 25.2 Mandatory before core feed integration

- [ ] Select the initial comparable price kinds: trade, bid, ask, midpoint, MBO,
  or an approved combination.
- [ ] Decide whether one source quote produces two lanes (`Bid` and `Ask`) or
  one composite lane.
- [ ] Define the handling of records with no valid comparable price.
- [ ] Select integration mode:
  - preserve the existing raw `MarketDataBatch64` readers and add the event
    pipeline as an optional sidecar; or
  - replace a selected production reader path with the event pipeline while
    retaining raw readers for diagnostics and compatibility.
- [ ] Select whether manager scope is one feed session, one ticker, or one
  option chain. The recommended scope is one manager per feed session with one
  lane dictionary.
- [ ] Select the configuration surface and deployment-profile ownership.
- [ ] Select an initial required buffer capacity for functional tests. This is
  not necessarily the eventual production default.
- [ ] Select output channel item capacity and full-wait timeout semantics.

### 25.3 Mandatory before actor-message implementation

- [ ] Choose the shared project that owns `TickPriceChanged` and
  `TickPriceData` actor contracts.
- [ ] Define actor message interfaces and generic entity ID types.
- [ ] Define Actor, Verb, Subject, RouteTo, AggregateId, EventId, CommandId,
  correlation, and causation fields.
- [ ] Define MessagePack keys and schema-version strategy.
- [ ] Decide whether channel items are actor messages directly or internal
  transport items mapped by the actor-event producer. The recommended
  dependency direction is internal framework transport items mapped to domain
  actor messages by a project that references both assemblies; the framework
  must not acquire a reverse dependency on a domain project.
- [ ] Define event routing to the tick manager actor and tick aggregator actor.
- [ ] Define publication acknowledgement, retry, and terminal-failure behavior.

### 25.4 Mandatory before actor and ScyllaDB implementation

- [ ] Approve tick manager actor responsibilities.
- [ ] Approve tick aggregator actor responsibilities.
- [ ] Define actor state, snapshots, replay, and recovery.
- [ ] Define ScyllaDB partition and clustering keys.
- [ ] Define persistence idempotency, consistency, batching, retry, and TTL.
- [ ] Define raw versus aggregated tick retention.

## 26. Repository scope and dependency rules

Codex must begin with these existing locations and adjust only after inspecting
the current repository:

| Concern | Existing location |
|---|---|
| Public feed contracts | `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/FeedContracts.cs` |
| Feed configuration | `TomasAI.IFM.Framework.MarketData.DataBento/Configuration/DatabentoFeedOptions.cs` |
| Canonical native structures | `TomasAI.IFM.Framework.MarketData.DataBento/Interop/NativeTypes.cs` |
| Ticker managed drain and routing | `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/SyntheticTickerFeed.cs` |
| Option-chain feed | `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/SyntheticOptionChainFeed.cs` |
| Existing SPSC batch transport | `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/BoundedBatchChannel.cs` |
| Existing pooled batch | `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/MarketDataBatch64.cs` |
| Managed unit tests | `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests` |
| Credentialed integration tests | `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests` |
| Live and soak tests | `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests` |
| CSV soak capture | `TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/DatabentoTickCsvCapture.cs` |

Dependency rules:

1. The native C++ ABI and 64-byte structures do not change for the manager.
2. The framework project must not reference `Domain.MarketData.Feed` or another
   higher-level domain assembly.
3. Actor contracts belong in a shared domain/contracts assembly selected in
   section 25.3.
4. The actor-event producer belongs at an integration boundary that can
   reference both framework transport contracts and actor contracts.
5. ScyllaDB code belongs in the storage/application layer, not the Databento
   framework.
6. Existing raw-reader public behavior remains compatible unless replacement
   mode is explicitly approved.

## 27. Candidate implementation file map

Codex must confirm naming conventions before creating files. The following map
is the intended separation of responsibilities:

### 27.1 Framework core

```text
TomasAI.IFM.Framework.MarketData.DataBento/
  Configuration/
    TickPricePipelineOptions.cs
  Runtime/TickPricePipeline/
    TickPriceKind.cs
    TickPriceLaneKey.cs
    SelectedTickPrice.cs
    ITickPriceSelector.cs
    TickPriceSelector.cs
    TickPriceDataEmissionReason.cs
    TickPriceChangedTransport.cs
    TickPriceDataTransport.cs
    TickPriceChannelItem.cs
    TickRecordBufferLease.cs
    TickRecordBufferPool.cs
    BoundedTickPriceChannel.cs
    TickPriceChangedManager.cs
    TickPricePipelineHealth.cs
```

These are candidate file names, not permission to create unnecessary one-type
files. Codex should combine tightly related small value types when that reduces
complexity without harming discoverability.

### 27.2 Tests and benchmarks

```text
TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/
  TickPricePipeline/
    TickPriceSelectorTests.cs
    TickRecordBufferPoolTests.cs
    BoundedTickPriceChannelTests.cs
    TickPriceChangedManagerTests.cs
    TickPricePipelineConservationTests.cs

TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/
  TickPriceChangedManagerBenchmarks.cs
  Program.cs
  README.md
  RESULTS.md
```

The benchmark project does not currently exist and is created only in the
benchmark work package.

### 27.3 Actor integration, deferred until schemas are approved

Candidate responsibilities, with final projects determined later:

```text
<selected shared contracts project>/
  Events/TickPriceChangedEvent.cs
  Events/TickPriceDataEvent.cs
  TickPriceEntityId.cs

<selected producer project>/
  DatabentoTickActorEventProducer.cs

<selected actor project>/
  TickManager/...
  TickAggregator/...

<selected storage project>/
  ScyllaDb/TickData/...
```

Codex must not create these actor/storage files while section 25.3 or 25.4 is
unresolved.

## 28. Proposed framework contracts

These signatures are implementation guidance. Codex may adjust accessibility,
names, and grouping to match repository conventions, but it must preserve the
semantics and explain material deviations in the plan.

```csharp
public enum TickPriceKind : byte
{
    Trade = 1,
    Bid = 2,
    Ask = 3,
    Midpoint = 4,
    Mbo = 5
}

public enum TickPriceDataEmissionReason : byte
{
    BufferFull = 1,
    PriceChanged = 2,
    FeedStopped = 3,
    TickerRemoved = 4,
    FeedFaulted = 5
}

public readonly record struct TickPriceLaneKey(
    InstrumentKey Instrument,
    TickPriceKind PriceKind);

public readonly record struct SelectedTickPrice(
    TickPriceKind PriceKind,
    long RawPrice);

public interface ITickPriceSelector
{
    int Select(
        in MarketRecord64 record,
        Span<SelectedTickPrice> destination);
}
```

The selector returns a count rather than allocating an enumerable. Destination
capacity must be a small compile-time maximum covering all approved price kinds.

```csharp
public sealed record TickPricePipelineOptions
{
    public required int BufferRecordCapacity { get; init; }
    public required int ChannelItemCapacity { get; init; }
    public required int PoolBufferCount { get; init; }
    public required TimeSpan ChannelFullTimeout { get; init; }
    public required IReadOnlySet<TickPriceKind> PriceKinds { get; init; }
}
```

Options validation must reject zero, negative, unsupported, internally
inconsistent, or overflow-producing values before any buffer is allocated.

Conceptual manager surface:

```csharp
internal sealed class TickPriceChangedManager
{
    internal void Process(in MarketRecord64 record);

    internal void RemoveInstrument(
        InstrumentKey instrument,
        TickPriceDataEmissionReason reason);

    internal void Complete(TickPriceDataEmissionReason reason);

    internal TickPricePipelineHealth GetHealth();
}
```

`Process` is synchronous. It must not return before the input tick has been
classified and either represented by a changed item or copied into owned buffer
memory. `Complete` is idempotent and rejects later processing.

The channel transport types are framework-owned and actor-transport-neutral
until section 25.3 is approved. They must carry every field required by section
9 without implementing domain actor interfaces.

## 29. Normative processing pseudocode

Codex must implement behavior equivalent to the following pseudocode:

```text
PROCESS(record):
    assert manager is accepting input
    selectedCount = selector.Select(record, stackSelectedPrices)

    if selectedCount == 0:
        apply approved no-price policy
        return

    for selected in deterministic selected-price order:
        key = (record instrument, selected price kind)
        lane = get-or-create lane(key)

        if lane has no current price:
            boundary = next boundary identity
            WRITE_CHANGED(lane, record, previous=null,
                          current=selected.rawPrice, boundary)
            lane establish current price from record
            increment represented-changed counter
            continue

        if selected.rawPrice == lane.currentRawPrice:
            if lane has no buffer:
                lane.buffer = pool.RentOrBackpressure()

            copy record into lane.buffer[lane.count]
            update lane buffered range metadata
            lane.count++
            increment buffered-tick counter

            if lane.count == configured capacity:
                WRITE_DATA(lane, reason=BufferFull, boundary=null)
                transfer buffer ownership
                clear lane buffer metadata
            continue

        boundary = next boundary identity
        WRITE_CHANGED(lane, record, lane.currentRawPrice,
                      selected.rawPrice, boundary)
        increment represented-changed counter

        if lane.count > 0:
            WRITE_DATA(lane, reason=PriceChanged, boundary)
            transfer buffer ownership
            clear lane buffer metadata

        lane establish new current price from record
```

```text
COMPLETE(reason):
    if already completed: return
    stop accepting input

    for lane in deterministic lane order:
        if lane.count > 0:
            WRITE_DATA(lane, reason, boundary=null)
            transfer buffer ownership
            clear lane buffer metadata

    complete channel writer
    mark manager complete
```

`WRITE_CHANGED` and `WRITE_DATA` use the same synchronous bounded-writer policy.
A channel-full wait is measured. Timeout is terminal and must preserve ownership
of any item not accepted by the channel so it can be reported and safely
returned.

## 30. Codex work packages

Codex must plan and implement in this dependency order. Each package ends with a
reviewable diff and verification evidence.

### WP0 - Repository audit and binding decisions

Deliverables:

- confirm current file and dependency structure;
- list relevant uncommitted changes without altering them;
- map every readiness-gate item to resolved, assumed, or blocked;
- analyze CSV data if the user requests a capacity recommendation;
- present the implementation plan and wait for approval.

No production code is changed in WP0.

### WP1 - Pure selector and manager contracts

Prerequisites: comparable price policy and initial functional buffer capacity.

Deliverables:

- validated pipeline options;
- price-kind and lane value types;
- allocation-free selector contract and implementation;
- transport-neutral changed/data contracts;
- deterministic selector unit tests.

No feed integration or actor dependency is added.

### WP2 - Fixed pool and bounded SPSC event channel

Deliverables:

- preallocated record-buffer pool;
- ownership-enforcing buffer lease;
- bounded synchronous single-writer/single-reader event channel;
- completion, stop, timeout, and unread-drain behavior;
- pool and channel unit tests, including ownership misuse.

Codex should reuse proven semantics from `BoundedBatchChannel` and `BatchPool`
where possible, without forcing incompatible types into those classes. A generic
refactor requires separate justification and must not increase the existing raw
feed's hot-path cost.

### WP3 - TickPriceChangedManager state machine

Deliverables:

- per-lane state and manager;
- all section 8 transitions;
- conservation counters and health snapshot;
- deterministic and property-style tests;
- zero-loss, zero-duplication, ordering, and shutdown verification.

WP3 must be testable with an in-memory writer before feed integration.

### WP4 - Databento feed integration

Prerequisite: approved integration mode.

Deliverables:

- ticker and option-chain integration at the approved managed boundary;
- public/internal reader exposure consistent with dependency rules;
- lifecycle wiring and partial flush on stop;
- health aggregation;
- synthetic feed tests;
- live soak reconciliation from source records to both output categories.

The native ABI remains unchanged.

### WP5 - BenchmarkDotNet coverage

Deliverables:

- new benchmark project added to the solution;
- constant, every-tick-change, capacity-boundary, multi-instrument, and
  CSV-derived distributions;
- MemoryDiagnoser output;
- before/after or bypass/manager comparisons where meaningful;
- summarized results in `RESULTS.md` and this specification.

Benchmarks do not replace conservation and integration tests.

### WP6 - Actor messages and actor-event producer

Prerequisite: all section 25.3 decisions.

Deliverables:

- versioned actor contracts in the approved shared project;
- mapping from framework transport to actor messages;
- ordered producer with acknowledgement and ownership handling;
- serialization round-trip tests;
- actor routing integration tests;
- failure and retry tests.

### WP7 - Tick manager and tick aggregator actors

Prerequisite: approved actor responsibilities, replay, and snapshot semantics.

Deliverables:

- actors, state, command/event handling, and supervisors;
- actor unit, BDD, and integration tests;
- graceful stop and recovery tests;
- updated actor sections in this document.

### WP8 - ScyllaDB persistence

Prerequisite: complete section 25.4 persistence decisions.

Deliverables:

- migrations/schema definitions;
- prepared and idempotent write paths;
- bounded batching outside the feed hot path;
- integration tests against the approved ScyllaDB test environment;
- retention, replay, and operational documentation.

## 31. Required test names and invariants

Codex may adapt names to repository style, but coverage must remain traceable:

| Required behavior | Candidate test name |
|---|---|
| Initial price | `FirstPriceEmitsChangedOnly` |
| Same price below capacity | `UnchangedPriceBuffersWithoutPublishing` |
| Exact capacity | `UnchangedPriceAtCapacityEmitsFullData` |
| Multiple full buffers | `LongUnchangedRunEmitsEachFullBuffer` |
| Empty-boundary buffer | `PriceChangeWithEmptyBufferEmitsChangedOnly` |
| Partial boundary buffer | `PriceChangeEmitsChangedBeforePartialData` |
| Changed tick uniqueness | `ChangedTickIsNotIncludedInPreviousPriceData` |
| Per-instrument isolation | `InstrumentsMaintainIndependentState` |
| Per-kind isolation | `PriceKindsMaintainIndependentState` |
| Stop flush | `CompleteFlushesEveryPartialBuffer` |
| No empty data | `ManagerNeverPublishesEmptyData` |
| Pool ownership | `PublishedBufferReturnsOnlyAfterConsumerDisposal` |
| Conservation | `RandomStreamsConserveEverySourceTick` |
| Bounded memory | `LongUnchangedRunKeepsRetainedMemoryBounded` |
| Backpressure | `FullChannelWaitsWithoutDroppingOrReordering` |
| Completion | `CompleteIsIdempotentAndRejectsNewInput` |

Every state-machine test must assert both output content and ownership cleanup.
Count-only assertions are insufficient.

## 32. Benchmark matrix and reporting

Minimum benchmark parameters:

- buffer capacities: `32`, `128`, `512`, `2048`;
- instruments: `1`, `32`, and one representative option-chain size;
- patterns:
  - all ticks change price;
  - all ticks retain one price;
  - price changes exactly before capacity;
  - price changes exactly after capacity;
  - CSV-derived run-length distribution;
- output modes:
  - in-memory writer with capacity available;
  - bounded writer under controlled pressure;
- metrics:
  - mean, p95 where supported, operations/second;
  - allocated bytes/op and Gen0/Gen1/Gen2;
  - messages per source tick;
  - pool rents/returns;
  - full and partial buffer emissions.

Benchmark artifacts must state runtime, GC mode, CPU, build configuration,
buffer capacity, instrument count, and input distribution. Codex must not claim
a production buffer size from synthetic benchmarks alone.

## 33. Verification commands

Codex must use the current project names discovered in the repository. Expected
commands at the time of this revision are:

```powershell
dotnet build .\TomasAI.IFM.Framework.MarketData.DataBento\TomasAI.IFM.Framework.MarketData.DataBento.csproj -c Release --no-restore --nologo

dotnet test .\TomasAI.IFM.Framework.MarketData.DataBento.UnitTests\TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj -c Release --no-restore --nologo

dotnet build .\TomasAI.IFM.sln -c Release --no-restore --nologo

git diff --check
```

After feed integration, run the gated synthetic/live tests appropriate to the
approved phase. Credentialed tests must not expose `DATABENTO_API_KEY` in output.
CSV analysis runs set `IFM_DATABENTO_TICK_CSV_DIRECTORY` and must reconcile CSV
rows with consumed ticks.

After WP5 exists:

```powershell
dotnet run --project .\TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks -c Release -- --filter "*TickPriceChangedManager*"
```

Actor and ScyllaDB commands are added only after their projects and environments
are approved.

## 34. Codex constraints and handoff format

Codex must follow these implementation constraints:

1. Do not change the native ABI for this feature.
2. Do not introduce framework-to-domain project references.
3. Do not invent actor schemas, ScyllaDB keys, or persistence semantics.
4. Do not replace existing raw readers without explicit approval.
5. Do not use unbounded channels, collections, or fallback allocations.
6. Do not use per-tick tasks, LINQ, boxing, reflection, or async state machines
   in the manager hot path.
7. Do not retain `MarketDataBatch64.Records` after disposing the batch.
8. Do not publish pooled memory without an explicit ownership transfer.
9. Do not swallow sequence, pool, channel, serialization, or drain failures.
10. Do not treat BenchmarkDotNet improvement as proof of correctness.
11. Preserve unrelated working-tree changes and call out overlap before editing.
12. Use `apply_patch` for source and document edits.

At each approved work-package handoff, Codex reports:

- implemented behavior;
- files created or changed;
- binding decisions applied;
- deviations from this specification and rationale;
- tests and benchmarks run with results;
- remaining readiness-gate blockers;
- whether any generated artifacts remain outside source control;
- whether changes were committed or pushed.

A useful future invocation is:

```text
Read Docs/Tick_Price_Event_Pipeline_Specification_v0.1.md completely.
Audit the current repository and evaluate section 25's readiness gate.
Create a concrete plan for WP0 through WP3 and let me review it before making
production-code changes. Do not invent any unresolved actor-message or ScyllaDB
contracts.
```

## 35. Revision history

| Version | Date | Summary |
|---|---|---|
| 0.1 | 2026-08-06 | Initial fixed-buffer price-change pipeline plus Codex readiness gate, repository file map, normative pseudocode, work packages, verification commands, and actor/persistence stop points. |
