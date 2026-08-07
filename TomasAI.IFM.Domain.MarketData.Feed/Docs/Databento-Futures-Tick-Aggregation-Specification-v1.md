# Databento futures tick aggregation and persistence specification

**Version:** 1.0  
**Status:** Codex-ready implementation specification; implementation requires an explicit user instruction to proceed  
**Date:** 2026-08-07  
**Initial asset scope:** Futures  
**Managed target:** .NET 10 (`net10.0`), x64  
**Storage target:** Existing MarketData ScyllaDB keyspace

**Codex execution rule:** Codex must read this document completely, execute the
repository audit in section 24, present a concrete implementation plan, and wait
for user approval before editing production code. After approval, Codex follows
the work packages in section 25 in order. It must not reinterpret a binding V1
decision without identifying the conflict and obtaining user approval.

## 1. Purpose

This specification defines the first production design for converting Databento
futures trade and quote records into ordered actor messages and persisting them
as immutable tick history.

The design has three principal components, described first in the required
processing order:

1. `TickAggregationService` owns the Databento ring consumer and per-ticker
   aggregation state. Every trade becomes one trade event. Quotes are buffered
   per ticker and become bounded quote-batch events.
2. `TickAggregationCommandActor` validates an insert command and durably saves
   the corresponding inserted event to `ActorEventSourceDb`.
3. `TickAggregationEventActor` converts changed events into insert commands,
   consumes the durably saved inserted events, and projects their data into
   ScyllaDB.

Only two new asset-neutral ScyllaDB tick tables are required: one for trades and
one for quotes. Futures is the first producer; later asset implementations use
the same tables. The existing futures and futures-option tick tables remain
unchanged and are classified as legacy.

This specification supersedes the futures price-change classification proposed
in `TomasAI.IFM.Framework.MarketData.DataBento/Docs/Tick_Price_Event_Pipeline_Specification_v0.1.md`.
The native Databento record and ring-buffer contracts remain unchanged.

## 2. Binding v1 decisions

1. V1 implements futures only. The identity and service layout permit later
   implementations for futures options, equities, and equity options.
2. The future topology is one input ring per asset type, with many tickers
   multiplexed through each ring. Aggregation state is always per ticker.
3. Every valid Databento trade record emits one
   `FuturesTickTradeDataChangedEvent`, even when its price equals the previous
   trade price.
4. Every valid Databento quote record is appended to that ticker's quote buffer.
5. Immediately before a trade event is emitted, the same ticker's non-empty
   quote buffer is emitted as a `FuturesTickQuoteDataChangedEvent` and cleared.
6. A full quote buffer is emitted and cleared even if no trade occurs.
7. Empty quote events are never emitted.
8. Partial quote buffers are emitted on graceful feed stop and ticker removal.
9. Quote and trade ordering is preserved by one output sequence shared by both
   categories for each stable ticker stream.
10. The default V1 quote-buffer capacity is 64 records per ticker. It remains a
    validated configuration value so paper-trading metrics can justify a later
    change.
11. Raw Databento signed 1e-9 fixed-point `long` prices are authoritative.
    Actor payloads also contain their exact `decimal` representation.
12. Actor mailbox identity is stable for a ticker and trading session:
    `ContractId + ValueDate + AssetTypeId`.
13. `TickDataId` identifies one emitted trade or quote batch:
    `ContractId + ValueDate + SequenceId + TimestampUtc`.
14. Databento `InstrumentId` is definition-date scoped and is never treated as
    the permanent domain identity.
15. Actor delivery and Scylla projection are at-least-once. Stable message IDs
    and idempotent Scylla primary keys make retries safe.
16. No tick is silently dropped. Exhausted buffers and full downstream channels
    apply bounded backpressure and ultimately fault visibly.
17. The two new Scylla tables have no default TTL. Tick history is retained
    until an explicit future retention or archival policy is approved.
18. Existing tick tables are not renamed, migrated, dual-written, or deleted by
    this implementation.
19. `AggregationTime` is UTC `TimeOnly`; `ValueDate` remains the exchange
    trading-session date.
20. A `ValueDate` change flushes the old date's non-empty quote buffer, closes
    that ticker/date state, and starts the new ticker/date sequence at 1 unless
    durable recovery finds an existing higher sequence.
21. One-sided quotes are preserved. An unavailable side retains Databento's raw
    undefined sentinel and has a null formatted decimal price.
22. Valid duplicate or out-of-order source records are preserved and counted in
    health metrics. Actor-message redelivery is deduplicated independently.
23. One quote-buffer actor event is stored as one Scylla row containing one
    bounded frozen quote collection. Array position preserves source order; no
    per-quote storage row or `quote_index` column is used.

## 3. TickAggregationService

### 3.1 Responsibility

`TickAggregationService` is the low-latency boundary between the managed
Databento feed and the actor system. It drains normalized records from the
futures input ring, resolves provider identity to domain identity, owns all
per-ticker mutable state, creates actor-event messages, and publishes them to a
bounded actor-event output channel.

ScyllaDB, Redis, NATS request/reply, and remote contract queries are forbidden
on the record-processing hot path. Contract mappings and sequence recovery must
be ready before a ticker is admitted to live processing.

### 3.2 Ownership and threading

- One service worker owns one asset ring. V1 owns the futures ring.
- The worker is the only writer to its ticker-state dictionary and quote
  buffers; no lock is required on the hot path.
- The state dictionary is keyed by Databento
  `InstrumentKey(PublisherId, InstrumentId)`. Each entry also contains the
  resolved stable `TickDataEntityId`.
- The dictionary is pre-sized from the subscribed ticker count and may grow
  only on the cold subscription path.
- Record classification is synchronous. No task is created per tick.
- Actor publication uses one bounded, ordered, single-reader output channel.
  If publication requires an asynchronous wait, the service awaits that wait at
  the batch/channel boundary and never uses fire-and-forget work.
- Metrics readers consume copied or atomic counters and never mutate ticker
  state.

### 3.3 Per-ticker state

```text
FuturesTickerAggregationState
    ProviderKey: InstrumentKey(PublisherId, InstrumentId)
    Dataset
    DefinitionDate
    ContractId
    ValueDate
    AssetTypeId = Futures
    NextSequenceId
    LastSourceSequence
    QuoteBufferOwner
    QuoteCount
    IsAccepting
```

The quote buffer is rented lazily, has fixed capacity, and stores owned copies
of the approved quote payload fields. It never retains a span, native pointer,
or `MarketDataBatch64` lease after the source batch is returned.

`NextSequenceId` is monotonic within the stable
`ContractId + ValueDate + AssetTypeId` stream and is shared by quote and trade
emissions. The service increments it only after the output channel accepts the
message. A publication retry reuses the same sequence, timestamp, event ID, and
command ID.

### 3.4 Identity readiness

Before processing the first tick for an instrument, the service must hold a
verified mapping containing:

```text
Dataset
DefinitionDate
PublisherId
InstrumentId
ContractId
AssetTypeId
```

The application-level mapping key is effectively
`Dataset + DefinitionDate + PublisherId + InstrumentId`. The reverse key is
`Dataset + DefinitionDate + ContractId`. `PublisherId` is retained because it
is part of Databento's runtime `InstrumentKey`.

The existing `IDatabentoContractMappingCache` in Blackboard is extended to
carry `PublisherId` and `AssetTypeId` in both directions. A verified immutable
local lookup is then built for the active feed. The mapping is also denormalized
into every new Scylla tick row, satisfying durable auditability without adding
a third table. The authoritative contract definition remains the existing
MarketData Securities/Databento definition workflow.

There is no fallback from an unknown instrument to a guessed contract or asset
type. A missing or conflicting mapping prevents that ticker from becoming
ready and produces a visible mapping fault; other valid tickers may continue.

`ValueDate` is the exchange trading-session date determined by the approved
calendar/session service. It is not automatically the UTC calendar date.
`DefinitionDate` remains the provider mapping date and is stored separately.

### 3.5 Sequence recovery

The service cannot restart at sequence zero for an existing entity/day. Before
admitting a ticker, it establishes the last committed sequence from the durable
actor event stream, reconciled with the latest projected sequence in both V1
Scylla tables, and sets `NextSequenceId = maximum + 1`.

The command/event pipeline must be caught up before live admission. One active
writer is permitted for each stable entity. A deployment must prevent two
services from concurrently owning the same entity/day; split-brain sequence
generation is a terminal readiness failure.

Sequence recovery is a cold-path operation. No durable maximum query is issued
per tick.

### 3.6 Quote processing

For a valid quote record:

1. Resolve its ticker state from `InstrumentKey`.
2. Validate record kind and source fields without rejecting an otherwise valid
   one-sided quote.
3. Convert each available raw price once using
   `raw / 1_000_000_000m`. Preserve an unavailable side's raw Databento
   sentinel and set that side's formatted price to null.
4. Copy the quote payload into the next owned buffer slot.
5. Update source-sequence and timestamp health counters.
6. If the buffer reaches capacity, create a quote changed event with emission
   reason `BufferFull`, publish it, transfer ownership, and clear the state.

The service does not collapse fully identical quotes. The CSV sample showed
that many quotes retain prices while sizes/counts change; every quote record is
therefore preserved.

A repeated or decreasing `SourceSequence` increments the appropriate duplicate
or out-of-order counter but does not suppress the valid source record. These
counters describe provider/source behavior and are separate from actor-message
idempotency.

### 3.7 Trade processing and cross-category order

For a valid trade record:

1. Resolve its ticker state.
2. If its quote buffer is non-empty, create and publish the quote changed event
   first with emission reason `TradeObserved`.
3. Clear/replace the accepted quote buffer.
4. Create and publish one trade changed event.

The shared output sequence makes the order explicit:

```text
quote batch: SequenceId = N
trade:       SequenceId = N + 1
```

This rule applies even when Databento publishes a trade and its associated quote
with the same source timestamp/sequence. The service never reverses the prior
quote history behind the trade.

### 3.8 Full buffer and graceful stop

When capacity is reached without a trade, the service publishes the full buffer
with `BufferFull`, then continues with an empty/replacement buffer. This bounds
memory for indefinitely quiet trade streams.

On graceful stop or ticker removal, the service stops accepting new input,
drains already accepted source records, publishes every non-empty partial quote
buffer with `FeedStopped` or `TickerRemoved`, waits until all output messages are
accepted, completes the output channel, and waits for the actor producer to
drain it. Empty buffers produce no event.

When an instrument's resolved `ValueDate` changes, the service first publishes
the old date's non-empty quote buffer with `ValueDateChanged`, then closes and
removes the old ticker/date state. It creates the new state before processing
the triggering record. A new stream starts at sequence 1 unless cold-path
durable recovery finds prior events for that same entity/date, in which case it
continues at the recovered maximum plus 1.

The solution-wide supervisor-to-storage cancellation contract is a known later
cross-cutting change. V1 must expose a graceful stop boundary compatible with
that future token propagation and must not introduce a conflicting private
cancellation model.

### 3.9 Buffer ownership

Before publication, the service owns the quote buffer. After the output channel
accepts the event transport, ownership belongs to the actor-event producer. The
producer returns the buffer only after MessagePack serialization has copied the
payload into transport-owned memory. A failed write retains ownership in the
service. Pool exhaustion backpressures; it never falls back to unbounded arrays.

## 4. TickAggregationCommandActor

### 4.1 Responsibility

`TickAggregationCommandActor` receives only persistence commands:

- `InsertFuturesTickTradeDataCommand`
- `InsertFuturesTickQuoteDataCommand`

It validates identity, schema version, mapping metadata, payload invariants,
sequence continuity, and idempotency. For a new valid command it applies the
corresponding inserted event to actor state and saves that event through the
existing event-source actor repository into `ActorEventSourceDb`.

This is the first durable save. It does not write ScyllaDB directly.

### 4.2 Actor identity and serialization

All commands use:

```text
ActorType = Command
Actor = TickAggregationCommand
EntityId = TickDataEntityId(ContractId, ValueDate, AssetTypeId)
RouteTo = FuturesTickDataBoundedContext
```

`Subject.EntityId`, actor thread identity, and event stream identity use the
stable `TickDataEntityId`; they must not use `TickDataId`, because doing so would
create a new actor lane for every emitted event.

Commands for one ticker/day therefore execute serially. Different tickers can
execute concurrently on different actor threads.

### 4.3 State and command result semantics

The minimal command state contains the last applied `SequenceId`, last applied
`CommandId`, last applied `TickDataId`, and snapshot metadata. Large quote or
trade payloads are not retained in live command state after the inserted event
is created.

An operation result and a state change remain different concepts:

- A new valid insert succeeds and changes state by appending an inserted event.
- An exact retry with the same stable `CommandId`/`TickDataId` succeeds without
  another state change or duplicate inserted event.
- A conflicting reuse of an ID with different content fails validation.

The actor returns success for the idempotent no-change case. It must not report
that a command failed merely because no state change was required.

State recovery must start from a snapshot and replay only the bounded tail
needed for sequence/idempotency validation. It must not reconstruct an
unbounded in-memory tick collection.

### 4.4 Validation rules

Both commands require:

- non-empty `CommandId`;
- `SchemaVersion == 1`;
- `EntityId` equal to the stable fields inside `TickDataId` and
  `AssetTypeId == Futures`;
- positive sequence and UTC `TimestampUtc`;
- non-empty contract ID and dataset;
- positive publisher/instrument IDs;
- a definition date compatible with the resolved mapping;
- exact available decimal/raw price equality and the approved undefined-quote
  sentinel/null pairing;
- source records kept in receive order even when their source-sequence values
  repeat or decrease;
- a payload count within configured limits.

The quote command additionally requires a non-empty array, count equal to array
length, unique zero-based quote positions, and count no greater than the
configured maximum of 64 in V1. The trade command contains exactly one trade.

### 4.5 Event creation

A successful new trade command appends
`FuturesTickTradeDataInsertedEvent`. A successful new quote command appends
`FuturesTickQuoteDataInsertedEvent`. All IDs originating before the command
save are copied unchanged. The event repository assigns its normal event-log
position; that `EventId` is not the tick `SequenceId`.

## 5. TickAggregationEventActor

### 5.1 Responsibility

`TickAggregationEventActor` has two deterministic stages distinguished by
event verb:

1. On a `...DataChangedEvent`, it creates and sends the matching insert command.
2. On a durably committed `...DataInsertedEvent`, it idempotently writes the
   payload to the matching ScyllaDB V1 table and publishes the matching complete
   or fail event.

This preserves the dependency direction:

```text
TickAggregationService
    -> FuturesTick*DataChangedEvent
    -> TickAggregationEventActor
    -> InsertFuturesTick*DataCommand
    -> TickAggregationCommandActor
    -> ActorEventSourceDb (FuturesTick*DataInsertedEvent)
    -> TickAggregationEventActor
    -> MarketData ScyllaDB
    -> FuturesTick*DataInsertedCompleteEvent
```

### 5.2 Changed-event handling

The actor validates the changed-event envelope without copying or transforming
payload values. It creates an insert command using the same stable
`CommandId`, entity identity, `TickDataId`, schema version, mapping metadata,
and payload. Per-entity mailbox ordering ensures a quote batch emitted before a
trade creates its command first.

Command publication is awaited. A failure is allowed to propagate to the actor
pipeline so the existing exception and retry behavior remains authoritative.
No fire-and-forget command publication is permitted.

### 5.3 Inserted-event projection

For a trade inserted event, the actor executes one prepared idempotent insert
into `tick_trade_data`.

For a quote inserted event, it writes one row to `tick_quote_data`. The ordered
quote array is bound as one bounded frozen collection in that row. The complete
collection is immutable and upserted through one prepared statement; no list
append/prepend operation and no multi-row batch is used. Retrying the event
safely replaces the same frozen cell at the same primary key.

Storage calls are awaited. The actor publishes an inserted-complete event only
after the required row has succeeded at the configured consistency level.
The complete event contains identity and counts only; it never echoes the large
tick payload.

If storage throws, normal actor exception handling produces/publishes the
inserted-fail event and permits durable replay/retry according to existing actor
policy. Failure messages include identity and diagnostic metadata, not a second
serialized copy of the quote buffer.

### 5.4 Terminal events

Inserted-complete and inserted-fail events are terminal for this persistence
flow. If routed back to `TickAggregationEventActor`, their registered handlers
perform telemetry/acknowledgement only and do not create another command or
storage write. An intentionally empty domain-event handler is valid by design.

## 6. End-to-end data flow and invariants

```text
Futures Databento SPSC ring (many InstrumentKeys)
    -> one TickAggregationService owner
       -> per-ticker quote buffer and shared quote/trade sequence
       -> bounded actor-event producer channel
    -> TickAggregationEventActor: Changed handlers
       -> TickAggregationCommandActor: Insert handlers
       -> ActorEventSourceDb: immutable Inserted events
    -> TickAggregationEventActor: Inserted handlers
       -> two MarketData ScyllaDB V1 tables
```

The following invariants are mandatory:

- Every accepted trade appears in exactly one trade changed/inserted payload.
- Every accepted quote appears in exactly one quote changed/inserted batch.
- Within one quote batch, payload order equals source order.
- Within one entity/day, `SequenceId` strictly increases across both event
  categories.
- A quote flush caused by a trade has a lower sequence than that trade.
- `TimestampUtc` is generated once by the service through `TimeProvider` and
  has `DateTimeKind.Utc`.
- A retry never regenerates `TickDataId`, `Id`, `CommandId`, timestamp, or
  sequence.
- Decimal price equals raw fixed-point price divided by one billion exactly.
- The stable entity ID, mapping metadata, payload type, and Scylla partition
  cannot disagree on asset type, contract, or value date.
- There is no silent drop, overwrite channel, or unbounded in-memory history.

## 7. Shared identity and enum schemas

These types belong in the MarketData Feed shared-contract project and use
explicit integer MessagePack keys.

### 7.1 AssetTypeId

```csharp
public enum AssetTypeId : byte
{
    Unknown = 0,
    Futures = 1,
    FuturesOption = 2,
    Equity = 3,
    EquityOption = 4
}
```

`Unknown` is never valid for a persistence message.

### 7.2 TickDataEntityId

```csharp
[MessagePackObject]
public readonly record struct TickDataEntityId(
    [property: Key(0)] string ContractId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] AssetTypeId AssetTypeId) : IActorEntityId;
```

Canonical string/stream formatting must be culture invariant and delimiter
safe. One approved form is:

```text
{AssetTypeId}:{ValueDate:yyyyMMdd}:{escaped ContractId}
```

### 7.3 TickDataId

```csharp
[MessagePackObject]
public readonly record struct TickDataId(
    [property: Key(0)] string ContractId,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] long SequenceId,
    [property: Key(3)] DateTime TimestampUtc);
```

`TickDataId` is the business/idempotency identity for one emitted trade or
quote batch. It is not used as the actor mailbox entity ID.

### 7.4 QuoteEmissionReason

```csharp
public enum QuoteEmissionReason : byte
{
    BufferFull = 1,
    TradeObserved = 2,
    FeedStopped = 3,
    TickerRemoved = 4,
    FeedFaulted = 5,
    ValueDateChanged = 6
}
```

## 8. Tick payload schemas

Raw values remain authoritative. Decimal fields are materialized once by
`TickAggregationService`; formatting such as `0.000000000` is a presentation
concern and is not stored in the wire contract.

### 8.1 FuturesTickQuoteData

| Key | C# type | Field | Meaning |
|---:|---|---|---|
| 0 | `uint` | `SourceSequence` | Databento source sequence |
| 1 | `long` | `EventTimestampNanoseconds` | Provider event timestamp |
| 2 | `long` | `ReceiveTimestampNanoseconds` | Databento receive timestamp |
| 3 | `byte` | `HeaderFlags` | Normalized record header flags |
| 4 | `long` | `BidPriceRaw` | Authoritative signed 1e-9 bid |
| 5 | `decimal?` | `BidPrice` | Exact scaled bid, or null when the raw bid is undefined |
| 6 | `uint` | `BidSize` | Bid quantity |
| 7 | `uint` | `BidCount` | Bid order count |
| 8 | `long` | `AskPriceRaw` | Authoritative signed 1e-9 ask |
| 9 | `decimal?` | `AskPrice` | Exact scaled ask, or null when the raw ask is undefined |
| 10 | `uint` | `AskSize` | Ask quantity |
| 11 | `uint` | `AskCount` | Ask order count |

For each quote side, constructors/factories enforce exactly one of these states:

```text
raw price is available  -> decimal price = raw / 1_000_000_000m
raw price is undefined  -> raw sentinel is preserved and decimal price is null
```

An undefined side does not cause the quote record to be dropped. A non-null
decimal paired with the undefined sentinel, or a null decimal paired with an
available raw price, is invalid.

### 8.2 FuturesTickTradeData

| Key | C# type | Field | Meaning |
|---:|---|---|---|
| 0 | `uint` | `SourceSequence` | Databento source sequence |
| 1 | `long` | `EventTimestampNanoseconds` | Provider event timestamp |
| 2 | `long` | `ReceiveTimestampNanoseconds` | Databento receive timestamp |
| 3 | `byte` | `HeaderFlags` | Normalized record header flags |
| 4 | `long` | `PriceRaw` | Authoritative signed 1e-9 trade price |
| 5 | `decimal` | `Price` | Exact `PriceRaw / 1_000_000_000m` |
| 6 | `uint` | `Size` | Trade quantity |
| 7 | `byte` | `Action` | Databento normalized action |
| 8 | `byte` | `Side` | Databento normalized side |
| 9 | `byte` | `DbnFlags` | Databento payload flags |

V1 intentionally omits depth, channel ID, timestamp-in delta, timestamp-out,
and fields not required to reproduce approved futures trade/quote history.

## 9. Actor event message schemas

All actor events follow the repository's standard base event keys:

| Key | Field | Type |
|---:|---|---|
| 0 | `Subject` | `ActorSubject` |
| 1 | `Id` | `Guid` |
| 2 | `EntityId` | `TickDataEntityId` |
| 3 | `EventId` | `long` |
| 4 | `CommandId` | `Guid` |
| 5 | `AggregateId` | `string` |
| 6 | `EventSource` | `string` |
| 7 | `ReceivedOn` | `DateTime` |

The `EventId` field is the actor event-log position. It must never be used as a
replacement for `TickDataId.SequenceId`.

### 9.1 FuturesTickTradeDataChangedEvent

```text
Actor = TickAggregationEvent
Verb  = FuturesTickTradeDataChanged
EventType = DomainEvent
```

| Key | Field | Type |
|---:|---|---|
| 8 | `SchemaVersion` | `ushort` (`1`) |
| 9 | `TickDataId` | `TickDataId` |
| 10 | `AssetTypeId` | `AssetTypeId` (`Futures`) |
| 11 | `Dataset` | `string` |
| 12 | `DefinitionDate` | `DateOnly` |
| 13 | `PublisherId` | `ushort` |
| 14 | `InstrumentId` | `uint` |
| 15 | `TradeData` | `FuturesTickTradeData` |

### 9.2 FuturesTickQuoteDataChangedEvent

```text
Actor = TickAggregationEvent
Verb  = FuturesTickQuoteDataChanged
EventType = DomainEvent
```

| Key | Field | Type |
|---:|---|---|
| 8 | `SchemaVersion` | `ushort` (`1`) |
| 9 | `TickDataId` | `TickDataId` |
| 10 | `AssetTypeId` | `AssetTypeId` (`Futures`) |
| 11 | `Dataset` | `string` |
| 12 | `DefinitionDate` | `DateOnly` |
| 13 | `PublisherId` | `ushort` |
| 14 | `InstrumentId` | `uint` |
| 15 | `EmissionReason` | `QuoteEmissionReason` |
| 16 | `QuoteCount` | `ushort` |
| 17 | `QuoteData` | `FuturesTickQuoteData[]` |

The array length equals `QuoteCount`, is in source order, and is in the range
1..64 for V1.

The service generates a non-empty `CommandId` for each changed event. The event
actor reuses it for the resulting insert command, making the complete flow
correlatable and idempotent.

## 10. Actor command message schemas

All commands follow the repository's standard base command keys:

| Key | Field | Type |
|---:|---|---|
| 0 | `CommandId` | `Guid` |
| 1 | `Subject` | `ActorSubject` |
| 2 | `PostEvents` | `bool` |
| 3 | `EntityId` | `TickDataEntityId` |
| 4 | `ErrorCode` | `int` |
| 5 | `RouteTo` | `BoundedContextName` |

Both commands use actor `TickAggregationCommand`, route to
`FuturesTickDataBoundedContext`, and set `PostEvents = true`.

### 10.1 InsertFuturesTickTradeDataCommand

```text
Verb = InsertFuturesTickTradeData
```

| Key | Field | Type |
|---:|---|---|
| 6 | `SchemaVersion` | `ushort` (`1`) |
| 7 | `TickDataId` | `TickDataId` |
| 8 | `AssetTypeId` | `AssetTypeId` |
| 9 | `Dataset` | `string` |
| 10 | `DefinitionDate` | `DateOnly` |
| 11 | `PublisherId` | `ushort` |
| 12 | `InstrumentId` | `uint` |
| 13 | `TradeData` | `FuturesTickTradeData` |

### 10.2 InsertFuturesTickQuoteDataCommand

```text
Verb = InsertFuturesTickQuoteData
```

| Key | Field | Type |
|---:|---|---|
| 6 | `SchemaVersion` | `ushort` (`1`) |
| 7 | `TickDataId` | `TickDataId` |
| 8 | `AssetTypeId` | `AssetTypeId` |
| 9 | `Dataset` | `string` |
| 10 | `DefinitionDate` | `DateOnly` |
| 11 | `PublisherId` | `ushort` |
| 12 | `InstrumentId` | `uint` |
| 13 | `EmissionReason` | `QuoteEmissionReason` |
| 14 | `QuoteCount` | `ushort` |
| 15 | `QuoteData` | `FuturesTickQuoteData[]` |

## 11. Persisted, complete, and failed event schemas

The trade event family is defined together in one shared event file, matching
the current repository convention. The quote event family is defined in a
second shared event file.

### 11.1 FuturesTickTradeDataInsertedEvent

```text
Actor = TickAggregationEvent
Verb  = FuturesTickTradeDataInserted
EventType = DomainEvent
```

Keys 0..7 use the standard base event schema. Keys 8..15 exactly match the
trade changed-event payload defined in section 9.1. The command actor copies the
payload without recalculating decimal prices or identities.

### 11.2 FuturesTickQuoteDataInsertedEvent

```text
Actor = TickAggregationEvent
Verb  = FuturesTickQuoteDataInserted
EventType = DomainEvent
```

Keys 0..7 use the standard base event schema. Keys 8..17 exactly match the
quote changed-event payload defined in section 9.2.

### 11.3 Inserted-complete events

The event names and verbs are:

```text
FuturesTickTradeDataInsertedCompleteEvent
Verb = FuturesTickTradeDataInsertedComplete

FuturesTickQuoteDataInsertedCompleteEvent
Verb = FuturesTickQuoteDataInsertedComplete
```

Both implement `ICompleteEvent<TickDataEntityId>` and use the standard complete
base keys 0..7:

| Key | Field | Type |
|---:|---|---|
| 0 | `Subject` | `ActorSubject` |
| 1 | `EntityId` | `TickDataEntityId` |
| 2 | `Id` | `Guid` |
| 3 | `EventId` | `long` |
| 4 | `CommandId` | `Guid` |
| 5 | `AggregateId` | `string` |
| 6 | `EventSource` | `string` |
| 7 | `ReceivedOn` | `DateTime` |
| 8 | `SchemaVersion` | `ushort` |
| 9 | `TickDataId` | `TickDataId` |
| 10 | `AssetTypeId` | `AssetTypeId` |
| 11 | `PersistedRecordCount` | `ushort` |

Trade complete always has count 1. Quote complete reports the number of quote
items stored in the single frozen-collection row. Complete events deliberately
omit `TradeData` and `QuoteData` to avoid serializing the large payload twice.

### 11.4 Inserted-fail events

The event names and verbs are:

```text
FuturesTickTradeDataInsertedFailEvent
Verb = FuturesTickTradeDataInsertedFail

FuturesTickQuoteDataInsertedFailEvent
Verb = FuturesTickQuoteDataInsertedFail
```

Both implement `IErrorEvent<TickDataEntityId>` and follow the existing standard
error schema:

| Key | Field | Type |
|---:|---|---|
| 0 | `Subject` | `ActorSubject` |
| 1 | `EntityId` | `TickDataEntityId` |
| 2 | `Id` | `Guid` |
| 3 | `ErrorDate` | `DateTime` |
| 4 | `EventId` | `long` |
| 5 | `CommandId` | `Guid` |
| 6 | `EventSource` | `string` |
| 7 | `ErrorMessage` | `string` |
| 8 | `ErrorCode` | `int` |
| 9 | `ErrorType` | `ErrorType` |
| 10 | `ErrorData` | `string` |
| 11 | `ReceivedOn` | `DateTime` |
| 12 | `AggregateId` | `string` |
| 13 | `CommandName` | `string` |
| 14 | `CommandData` | `string` |
| 15 | `RouteTo` | `string` |
| 16 | `SchemaVersion` | `ushort` |
| 17 | `TickDataId` | `TickDataId` |
| 18 | `AssetTypeId` | `AssetTypeId` |
| 19 | `AttemptedRecordCount` | `ushort` |

`CommandData` is empty or a bounded redacted diagnostic summary. It must never
contain the serialized quote array. `ErrorData` follows the application's
existing production redaction policy and must not contain credentials.

`ToCompleteEvent` and `ToFailEvent` implementations construct the exact
concrete event family and validate `TickDataEntityId`. They must not use a
generic entity-type guard that conflicts with the actual returned event type.

## 12. ScyllaDB schema

### 12.1 Scope and legacy tables

Exactly two new asset-neutral tables are added to the existing MarketData
keyspace:

- `tick_trade_data`
- `tick_quote_data`

The same tables will store futures, futures-option, equity, and equity-option
records. `asset_type_id` separates those asset classes in the partition key.
V1 writes futures rows only.

These existing tables remain unchanged and are explicitly legacy:

- `futures_tick_data`
- `futures_tick_data_by_time`
- `futures_option_tick_data`
- `futures_option_tick_price_data`

No V1 code dual-writes the legacy tables. No migration or backfill is required
for initial deployment.

### 12.2 Query-first partition strategy

The mandatory primary query is all trade or quote ticks for one asset type and
contract over an inclusive `ValueDate` range. That query must execute as one CQL
statement using only primary-key restrictions and without `ALLOW FILTERING`.

Both tables therefore use:

```text
Partition key:      (asset_type_id, contract_id)
First clustering:  value_date
Second clustering: aggregation_time
Remaining order:   sequence_id
```

`value_date` is deliberately not part of the partition key. It is the first
clustering column, making any date range for one asset/contract a contiguous
slice of one partition. `aggregation_time` is a Scylla `time` column represented
as .NET `TimeOnly` in storage DTOs and query parameters, matching the existing
MarketData storage convention. There is no storage-bucket field in the schema,
actor messages, repository API, or query.

This layout intentionally prioritizes the application's dominant date-range
query over physical time bucketing. Each asset/contract partition grows for the
life of that contract. Partition size, row count, read page latency, and
compaction pressure must be measured during paper trading. If those measurements
later require physical bucketing, it must be introduced as a separately approved
query projection; a hidden bucket must not break the required single-statement
date-range contract.

### 12.3 Bounded quote collection type

The quote table contains one bounded typed array rather than one row per quote.
The array element is a keyspace-scoped UDT:

```sql
CREATE TYPE IF NOT EXISTS tick_quote_item (
    source_sequence bigint,
    source_event_timestamp_ns bigint,
    source_receive_timestamp_ns bigint,
    header_flags smallint,
    bid_price_raw bigint,
    bid_price decimal,
    bid_size bigint,
    bid_count bigint,
    ask_price_raw bigint,
    ask_price decimal,
    ask_size bigint,
    ask_count bigint
);
```

`bid_price` or `ask_price` is null when the corresponding raw field contains
Databento's undefined sentinel. The raw sentinel remains available for lossless
source reconstruction.

The table column type is:

```sql
quote_data frozen<list<frozen<tick_quote_item>>>
```

The inner UDT is frozen because it is nested. The outer frozen list stores the
entire immutable, ordered, capacity-limited array as one Scylla cell. V1 never
modifies a list element in place; retry replaces the complete value. The list is
limited to 64 items, so every read retrieves a small bounded collection. The UDT
is an auxiliary keyspace type, not a third table.

Scylla reads a frozen collection as a whole rather than paging its elements.
That behavior is acceptable only because the service and command validation
enforce the hard 64-item maximum; the collection can never grow with stream
history.

The storage session registers one explicit C# driver UDT mapping between
`tick_quote_item` and an internal `TickQuoteStorageItem`. The mapper converts the
actor array to one ordered `IReadOnlyList<TickQuoteStorageItem>` for binding and
back to the actor/read DTO on query. UDT field names and types are schema
contract; future additive fields use an explicit schema revision and never
reorder existing fields.

Schema creation order is `tick_quote_item`, `tick_trade_data`, then
`tick_quote_data`. Test-only teardown reverses that order. Production deployment
never drops the type or tables.

### 12.4 tick_trade_data

```sql
CREATE TABLE IF NOT EXISTS tick_trade_data (
    asset_type_id tinyint,
    contract_id text,
    value_date date,
    sequence_id bigint,
    aggregation_timestamp_utc timestamp,
    aggregation_timestamp_utc_ticks bigint,
    aggregation_time time,
    schema_version smallint,
    dataset text,
    definition_date date,
    publisher_id int,
    instrument_id bigint,
    actor_event_id uuid,
    actor_event_log_id bigint,
    command_id uuid,
    aggregate_id text,
    event_source text,
    received_on timestamp,
    source_sequence bigint,
    source_event_timestamp_ns bigint,
    source_receive_timestamp_ns bigint,
    header_flags smallint,
    price_raw bigint,
    price decimal,
    size bigint,
    action smallint,
    side smallint,
    dbn_flags smallint,
    PRIMARY KEY (
        (asset_type_id, contract_id),
        value_date,
        aggregation_time,
        sequence_id
    )
) WITH CLUSTERING ORDER BY (
    value_date ASC,
    aggregation_time ASC,
    sequence_id ASC
);
```

`aggregation_timestamp_utc_ticks` retains the exact .NET UTC tick value because
the Scylla `timestamp` presentation column has lower precision. The primary key
contains the `ValueDate`, queryable UTC `AggregationTime`, and domain sequence
from `TickDataId`. The ordinary UTC timestamp/ticks columns retain the complete
generated instant.

### 12.5 tick_quote_data

```sql
CREATE TABLE IF NOT EXISTS tick_quote_data (
    asset_type_id tinyint,
    contract_id text,
    value_date date,
    sequence_id bigint,
    aggregation_timestamp_utc timestamp,
    aggregation_timestamp_utc_ticks bigint,
    aggregation_time time,
    schema_version smallint,
    dataset text,
    definition_date date,
    publisher_id int,
    instrument_id bigint,
    actor_event_id uuid,
    actor_event_log_id bigint,
    command_id uuid,
    aggregate_id text,
    event_source text,
    received_on timestamp,
    emission_reason smallint,
    quote_count smallint,
    quote_data frozen<list<frozen<tick_quote_item>>>,
    PRIMARY KEY (
        (asset_type_id, contract_id),
        value_date,
        aggregation_time,
        sequence_id
    )
) WITH CLUSTERING ORDER BY (
    value_date ASC,
    aggregation_time ASC,
    sequence_id ASC
);
```

The `quote_data` list order is the actor payload/source order. `quote_count`
must equal the frozen list length and remain in the range 1..64. One actor quote
event creates one Scylla row, so the primary key is identical in shape to the
trade table. Repeated row-level identity and mapping metadata is stored once per
buffer instead of once per quote.

This reduces row and cell overhead, repeated primary-key bytes, and repeated
actor/mapping metadata. It does not discard or compress away any quote payload.
SSTable compression remains independently managed by Scylla.

### 12.6 Type conversion rules

| C# source | Scylla column | Rule |
|---|---|---|
| `byte` enum/flags | `tinyint` or `smallint` | Validate 0..255 before cast |
| `ushort` publisher | `int` | Lossless widening |
| `uint` instrument/sequence/count/size | `bigint` | Lossless widening |
| `DateOnly` | `date` | No timezone conversion |
| `TimeOnly` | `time` | Queryable time-of-day using the existing driver mapping |
| UTC `DateTime` | `timestamp` plus `.Ticks bigint` | Store both display and exact identity |
| raw price `long` | `bigint` | Authoritative value |
| quote-side `decimal?` | nullable `decimal` UDT field | Exact scaled value, or null only for an undefined raw side |
| trade `decimal` | `decimal` | Exact scaled value; a persisted trade price is defined |
| `FuturesTickQuoteData[]` | `frozen<list<frozen<tick_quote_item>>>` | Preserve order; count 1..64; replace as one immutable value |
| `Guid` | `uuid` | Preserve original value on retry |

### 12.7 Writes, consistency, and idempotency

- All statements are prepared during storage startup, not per event.
- Trade projection is one insert/upsert.
- Quote projection is one insert/upsert containing the complete frozen quote
  list. It never uses list append/prepend or a CQL `BATCH`.
- V1 uses `LOCAL_QUORUM` consistency and the MarketData storage layer's
  explicitly configured timeout.
- No automatic retry may generate a new ID or change the payload.
- Scylla primary keys make repeated projection of the same inserted event an
  upsert of the same row.
- Complete is emitted only after the row succeeds.
- A quote retry replaces the same complete frozen cell at the same primary key.
- No `ALLOW FILTERING`, server-side collection scan, secondary index, or
  materialized view is required by V1 writes or ordered entity-range reads.
- V1 does not apply TTL and initially uses the existing MarketData schema
  context's default compaction behavior. Any compaction override, archival, or
  retention change requires paper-trading evidence and a separately reviewed
  schema revision.

### 12.8 Query shapes

Supported primary query shapes are:

```text
Trades for one asset/contract over an inclusive ValueDate range
Quote-buffer rows for one asset/contract over an inclusive ValueDate range
Trades or quotes for one ValueDate over an aggregation-time range
Trades or quotes between exact start/end date-time boundaries across ValueDates
All retained dates for one asset/contract, using paging
One trading date by setting equal start and end ValueDate
One TickDataId by asset, contract, ValueDate, aggregation time, and SequenceId
```

The required trade range query is:

```sql
SELECT *
FROM tick_trade_data
WHERE asset_type_id = ?
  AND contract_id = ?
  AND value_date >= ?
  AND value_date <= ?;
```

The required quote range query is:

```sql
SELECT *
FROM tick_quote_data
WHERE asset_type_id = ?
  AND contract_id = ?
  AND value_date >= ?
  AND value_date <= ?;
```

Both are legal contiguous clustering-range reads because the complete partition
key is supplied and `value_date` is the first clustering column. Neither query
uses an index, `IN`, token scan, or `ALLOW FILTERING`. Driver paging is mandatory
for large results; one CQL statement does not mean materializing the entire
range in memory. Each returned quote row contains at most 64 ordered items. A
flat quote-stream repository API expands one bounded collection at a time while
paging rows; it never loads every array before yielding results.

For an intraday range within one `ValueDate`, the trade query is:

```sql
SELECT *
FROM tick_trade_data
WHERE asset_type_id = ?
  AND contract_id = ?
  AND value_date = ?
  AND aggregation_time >= ?
  AND aggregation_time <= ?;
```

The quote query uses the same predicates against `tick_quote_data`. This is a
contiguous range because `value_date` has an equality restriction and
`aggregation_time` is the next clustering column. The two time bind values are
.NET `TimeOnly` values in the storage query DTO.

An exact date-time range spanning more than one `ValueDate` uses a clustering
tuple range:

```sql
SELECT *
FROM tick_quote_data
WHERE asset_type_id = ?
  AND contract_id = ?
  AND (value_date, aggregation_time) >= (?, ?)
  AND (value_date, aggregation_time) <= (?, ?);
```

The lower tuple is `(startValueDate, startTime)` and the upper tuple is
`(endValueDate, endTime)`, where both times are .NET `TimeOnly` values. The
equivalent trade query uses `tick_trade_data`. It remains one primary-key CQL
statement without filtering.

`AggregationTime` is derived once as
`TimeOnly.FromDateTime(TickDataId.TimestampUtc)` after validation that the
timestamp has `DateTimeKind.Utc`. It is therefore explicitly UTC time-of-day;
no machine-local or exchange-time conversion occurs in the storage mapper.
The full `aggregation_timestamp_utc` and exact
`aggregation_timestamp_utc_ticks` remain stored as ordinary columns for
identity, audit, and unambiguous reconstruction. The original Databento event
and receive timestamps also remain stored for source chronology, but they are
not the V1 range-query clock.

Cross-ticker, time-global, and analytics-specific queries require separate
derived tables/projectors and are outside V1. They must not compromise these
write-optimized base tables.

## 13. Contract mapping storage and Blackboard

The V1 hot-path mapping record is:

```csharp
public readonly record struct TickContractMapping(
    string Dataset,
    DateOnly DefinitionDate,
    ushort PublisherId,
    uint InstrumentId,
    string ContractId,
    AssetTypeId AssetTypeId);
```

The existing Blackboard mapping service is extended so contract-to-instrument
and instrument-to-contract lookups return the complete record. Cache keys remain
dataset and definition-date scoped; the instrument-direction key includes
publisher ID. Conflicting mappings are evicted and fault exactly as the current
cache does.

Scylla persistence is denormalized into both new tick tables through `dataset`,
`definition_date`, `publisher_id`, `instrument_id`, `contract_id`, and
`asset_type_id`. This records which mapping produced every stored tick while
honoring the requirement that this feature add only two Scylla tables.

If a separately queryable permanent mapping history is later required before a
tick exists, it belongs in the authoritative contract-definition domain and is
a separate specification; it is not a third tick-aggregation table.

## 14. Backpressure, failures, and shutdown

The bounded path is:

```text
native ring -> managed drain -> aggregation buffers -> actor-event channel
-> actor transport -> command actor -> event source -> event actor -> Scylla
```

Each boundary exposes depth, wait time, and failure counts. No layer silently
switches to an unbounded queue. Backpressure is allowed to reach the feed; if a
configured deadline is exceeded, the feed faults with the unaccepted identity
and source range available in diagnostics.

Storage and actor exceptions are not converted into an apparently successful
state reconstruction. They flow through the existing actor exception pipeline.
The implementation does not add defensive throws merely because historical
state is empty; a genuinely empty stream reconstructs empty state. Invalid live
commands fail normal validation.

Shutdown completion means all of the following are true:

1. input acceptance stopped;
2. the native/managed accepted input was drained;
3. all non-empty quote buffers were emitted;
4. the output channel was drained;
5. emitted insert commands were acknowledged/durable;
6. inserted-event projectors either completed Scylla writes or reported a
   durable visible failure/retry state;
7. every pooled buffer was returned exactly once.

## 15. Configuration

Candidate V1 configuration:

```csharp
public sealed record TickAggregationOptions
{
    public int FuturesQuoteBufferCapacity { get; init; } = 64;
    public int OutputChannelCapacity { get; init; }
    public int PreallocatedQuoteBufferCount { get; init; }
    public TimeSpan OutputBackpressureTimeout { get; init; }
    public TimeSpan GracefulStopTimeout { get; init; }
}
```

Validation rejects non-positive values, a quote capacity above the schema's V1
maximum, insufficient pool/channel relationships, and overflow-producing
settings. Schema version is a constant, not an option.

## 16. Metrics and operational evidence

Metrics are tagged by asset type and, where cardinality permits, contract:

- source quote and trade records;
- emitted quote batches and trade events;
- quote items per frozen-collection row and emission reason;
- active ticker states and rented buffers;
- buffer-full count and partial flush count;
- actor output channel depth and blocked duration;
- last source and output sequence per ticker;
- sequence gap, duplicate, mapping conflict, and out-of-order counts;
- changed-to-command and command-to-inserted latency;
- inserted-to-Scylla-complete latency;
- Scylla trade rows, quote-buffer rows/items, retries, timeouts, and failures;
- pooled buffers rented/returned/outstanding;
- graceful-stop duration and unflushed record count.

The health endpoint must make stalled stages distinguishable. A single generic
"tick pipeline unhealthy" flag is insufficient.

### 16.1 CSV sizing evidence

The 2026-08-06 ES futures capture contained 195,425 records over 600.036
seconds:

| Record type | Count | Share | Average rate |
|---|---:|---:|---:|
| Quote | 179,610 | 91.9074% | 299.332/s |
| Trade | 15,815 | 8.0926% | 26.357/s |

Quote runs between trades had p50 10, p95 49, p99 82, and maximum 267 records.
With capacity 64, the capture produces approximately 11,210 quote events and
15,815 trade events, or 27,025 actor events total: an 86.171% message-count
reduction relative to one actor event per source record. Capacity 128 improves
that reduction only to approximately 86.298%, so 64 is the V1 default pending
paper-trading evidence.

With one frozen quote collection per row, the same capture would write about
11,210 quote rows and 15,815 trade rows instead of 179,610 quote rows and 15,815
trade rows. Quote and trade row counts are therefore of the same order but are
not guaranteed to be equal: a trade may arrive without buffered quotes, and a
long quote run may emit one or more `BufferFull` rows before the next trade.

## 17. Required correctness tests

### 17.1 Service tests

- first quote buffers without emitting;
- trade with no quotes emits one trade only;
- trade flushes only its own ticker's quote buffer before the trade;
- full quote buffer emits and clears without a trade;
- quote buffers for interleaved tickers remain isolated;
- all four future asset-ring instances can use the same service contract, while
  V1 enables futures only;
- graceful stop and ticker removal flush non-empty partial buffers;
- `ValueDate` rollover flushes the old non-empty buffer with
  `ValueDateChanged`, closes the old state, and starts/recovers the new stream;
- no operation emits an empty quote event;
- every accepted source record is conserved exactly once;
- duplicate and out-of-order valid source records are preserved while metrics
  identify them;
- sequence is shared and strictly increasing across quote/trade emissions;
- failed channel write does not advance sequence or lose buffer ownership;
- available raw/decimal fields are exactly equivalent and undefined quote sides
  retain the raw sentinel with a null decimal;
- mapping miss/conflict prevents only the affected ticker from readiness;
- pooled buffers are returned exactly once after serialization.

### 17.2 Actor tests

- every changed-event verb parses to the correct type;
- changed quote/trade events create the matching command without payload drift;
- all MessagePack schemas round-trip with exact key compatibility;
- same entity serializes quote-before-trade processing;
- duplicate command succeeds without creating another inserted event;
- conflicting duplicate fails validation;
- command success remains distinct from state update;
- inserted event is durable before Scylla projection begins;
- complete events omit large payloads;
- terminal complete/fail handlers do not recurse;
- exceptions are handled by the actor pipeline.

### 17.3 Scylla integration tests

- schema creation adds the `tick_quote_item` UDT and exactly the two V1 tables
  while preserving legacy tables;
- one trade inserted event creates one expected row;
- one quote inserted event creates one row whose frozen list preserves every
  item in source order;
- replaying either event is idempotent;
- retry after a failed quote-row upsert replaces the same complete frozen list;
- trade rows and every quote UDT item preserve raw/nullable-decimal invariants
  and unsigned ranges;
- exact-day, multi-day, multi-week, and multi-month `ValueDate` range queries
  execute as one CQL statement without `ALLOW FILTERING`;
- paged range reads preserve `ValueDate`, aggregation-time, sequence, and
  in-list quote order without duplicates;
- `LOCAL_QUORUM`/configured timeout behavior is observable;
- no TTL is applied.

## 18. BenchmarkDotNet requirements

Implementation is not complete until BenchmarkDotNet compares a baseline of
one actor payload per source record with the V1 service and batching path.
MemoryDiagnoser is mandatory.

Required inputs:

- all quotes, no trades;
- alternating quote/trade;
- quote runs of 10, 49, 82, and 267;
- interleaved 1, 16, 64, and 256 tickers;
- capacity boundary at 63/64/65;
- CSV-derived ES distribution;
- raw-to-decimal materialization;
- MessagePack trade and quote-batch serialization;
- command actor parse/dispatch;
- event actor trade-row and 1/64-item frozen quote-row insert preparation;
- row-per-quote baseline versus one frozen-list row for prepared binding,
  allocation, logical row count, and encoded payload size where the driver
  exposes it without a live network dependency.

Required reported metrics:

- mean, median, p95/p99 where the harness supports them;
- operations/second and source ticks/second;
- bytes allocated per source tick;
- Gen0/Gen1/Gen2 collections;
- emitted actor messages per source tick;
- Scylla rows per source quote and repeated-envelope bytes avoided;
- quote buffers rented/returned;
- end-to-end service-to-storage latency under controlled backpressure.

Before/after results must be recorded in the MarketData Feed optimization
details document and summarized in this specification. Synthetic benchmarks do
not determine production settings without paper-trading evidence.

## 19. Implementation layout

Candidate locations, subject to a repository audit immediately before coding:

```text
TomasAI.IFM.Domain.MarketData.Feed.Shared/
  Commands/
    InsertFuturesTickTradeDataCommand.cs
    InsertFuturesTickQuoteDataCommand.cs
  Events/
    FuturesTickTradeDataChangedEvent.cs
    FuturesTickQuoteDataChangedEvent.cs
    FuturesTickTradeDataInsertedEvent.cs
    FuturesTickQuoteDataInsertedEvent.cs
  ViewModels/
    TickDataEntityId.cs
    TickDataId.cs
    TickDataPayloads.cs
    TickContractMapping.cs

TomasAI.IFM.Domain.MarketData.Feed/
  TickAggregation/
    Service/
    Command/Actor/TickAggregationCommandActor.cs
    Command/State/
    Command/Validation/
    Event/Actor/TickAggregationEventActor.cs
    Event/Extensions/

TomasAI.IFM.Application.Storage/MarketDataDb/
  TickAggregation/
  Schema/MarketDataSchemaCql.cs
  Schema/MarketDataSchemaDb.cs
```

The Databento framework project remains actor-transport-neutral. A producer at
an integration layer that can reference both the framework transport and domain
contracts maps the owned aggregation transport into these actor messages; the
framework must not gain a reverse reference to the domain project.

## 20. Implementation sequence and review gates

1. Implement the approved shared identities, enum values, payload fields,
   actor names/verbs, and MessagePack keys in this specification.
2. Implement pure per-ticker aggregation and buffer ownership with deterministic
   tests.
3. Implement mapping extension, readiness, session value-date resolution, and
   durable sequence recovery.
4. Implement changed events and the ordered actor-event producer.
5. Implement insert commands, command actor, minimal state, snapshots, and
   inserted event families.
6. Add the two Scylla schemas and prepared storage paths.
7. Implement inserted-event projection, complete/fail handling, retry, and
   graceful stop.
8. Add unit, serialization, actor, and Scylla integration tests.
9. Add/run BenchmarkDotNet before/after suites and record results.
10. Run the approved Fund integration suite for final core-actor validation,
    then perform a live Databento reconciliation soak before production.

Each numbered implementation stage ends with a reviewable diff. This document
does not authorize implementation; coding begins only after explicit approval.

## 21. V1 acceptance criteria

V1 is complete when:

- futures quote and trade payload schemas are approved and versioned;
- the service preserves all accepted futures trades and quotes exactly once at
  the logical event level;
- per-ticker quote buffering remains bounded and isolated;
- quote-before-trade order is proven through both actor stages and storage;
- actor event sourcing and Scylla projection are replay-safe and idempotent;
- only the approved `tick_quote_item` UDT and two V1 Scylla tables are added;
- all legacy tick tables are untouched and receive no V1 writes;
- mapping identity is present in Blackboard, local runtime state, actor
  envelopes, and stored rows;
- available raw and decimal prices agree exactly, while an undefined quote side
  retains its raw sentinel with a null decimal;
- unit, MessagePack, actor, storage, shutdown, and recovery tests pass;
- BenchmarkDotNet before/after results and paper-trading metrics are documented;
- no unbounded queue, collection, history replay, or per-tick asynchronous task
  exists in the hot path.

## 22. Deferred work

The following are intentionally outside V1:

- futures-option, equity, and equity-option event/message/table
  implementations;
- replacement or deletion of legacy IBKR tick tables;
- analytics/materialized query tables;
- market-wide cross-ticker query projections;
- retention, TTL, cold archival, and deletion policy;
- an independent permanent Databento mapping-history table;
- solution-wide cancellation-token propagation from supervisor through actor,
  repository, and storage;
- micro-optimizations that are not supported by paper-trading metrics.

## 23. Codex implementation authority

This specification defines the intended implementation but does not itself
authorize source changes. A user instruction such as `proceed with WP1 through
WP6` authorizes only the work packages named in that instruction.

Within an approved work package, Codex may:

- add and edit source files in the projects listed in sections 19 and 25;
- add the `tick_quote_item` UDT and additive schema definitions for
  `tick_trade_data` and `tick_quote_data` to the existing MarketData schema
  registration;
- add unit, serialization, actor, storage-integration, and benchmark coverage;
- update this specification and the MarketData Feed optimization-results
  document with implementation evidence;
- make small repository-convention refactors required to integrate the new
  code, provided they do not alter unrelated domain behavior.

Codex is not authorized by this specification to:

- commit, push, create a pull request, or modify remote systems;
- drop, rename, migrate, backfill, or write to legacy tick tables;
- execute destructive schema operations against a real environment;
- change the native Databento ABI or C++ ring-record layout;
- optimize or expand the legacy IBKR feed implementation;
- implement futures-option, equity, or equity-option actor messages;
- perform the deferred solution-wide cancellation-token change;
- expose credentials or run credentialed live tests without explicit approval.

## 24. Mandatory Codex repository audit and readiness gate

Codex performs WP0 before production edits, even if the repository has changed
since this specification was written.

### 24.1 Required read-only audit

Codex must:

1. Read this document completely.
2. Run `git status --short` and preserve all existing user changes.
3. Search for any applicable `AGENTS.md` instructions.
4. Inspect the current project references among the Databento framework,
   MarketData Feed shared/domain, Blackboard, Application Storage, test, and
   benchmark projects.
5. Inspect the current actor-message patterns, including explicit MessagePack
   base keys, serialization constructors, concrete complete/fail conversions,
   actor API factories, registration, and supervisor wiring.
6. Inspect `IEventSourceActorDbContext`, actor state repositories, snapshots,
   and the available bounded-tail replay APIs. Only `ActorEventSourceDb` may be
   used; the removed `EventSourceDb` must not be reintroduced.
7. Inspect the current normalized Databento quote/trade record definitions,
   managed batch ownership, synchronous readers, and contract mapping cache.
8. Inspect `MarketDataSchemaCql`, `MarketDataSchemaDb`, `MarketDataDbContext`,
   prepared CQL/parameter patterns, `DateOnly`/`TimeOnly` driver mappings, and
   integration-test fixture behavior.
9. Inspect existing error-code ranges, bounded-context routing, actor names, and
   DI registration so new values do not collide.
10. Inspect the existing benchmark harness and optimization-results format.
11. Identify any specification statement that conflicts with compilable current
    repository contracts.
12. Present a concrete file-by-file implementation plan, validation plan,
    working-tree overlap, assumptions, and blockers, then wait for approval.

WP0 is read-only except for an explicitly requested update to this
specification. It does not create placeholder production files.

### 24.2 Resolved decisions Codex must not reopen

The following decisions are already approved:

- futures only in V1;
- one asset ring containing many independently buffered tickers;
- every trade emits one trade changed event;
- quotes are buffered per ticker and flushed before that ticker's trade;
- a capacity-64 quote buffer flushes when full;
- raw `long` and exact nullable `decimal` quote prices are both carried;
- undefined quote sides preserve the raw sentinel and use a null decimal;
- stable actor identity is `ContractId + ValueDate + AssetTypeId`;
- `TickDataId` is `ContractId + ValueDate + SequenceId + TimestampUtc`;
- sequence and timestamp originate in `TickAggregationService` and are reused
  unchanged on retry;
- aggregation `TimeOnly` is UTC while `ValueDate` is the trading-session date;
- `ValueDate` rollover flushes the old quote buffer and starts/recovers the new
  entity/date sequence;
- valid duplicate and out-of-order source records are preserved and measured;
- command success and actor-state mutation are separate concepts;
- an exact duplicate command may succeed without another state change;
- empty domain-event handlers are permitted when intentionally terminal;
- changed events flow to commands, inserted events are durable in
  `ActorEventSourceDb`, and inserted events project to Scylla;
- storage has exactly two new asset-neutral tables: `tick_trade_data` and
  `tick_quote_data`;
- the complete storage partition key is `(asset_type_id, contract_id)`;
- `value_date` and `aggregation_time` are the first two clustering columns;
- storage query DTOs use `DateOnly` and `TimeOnly`;
- date/date-time ranges execute through primary-key CQL without
  `ALLOW FILTERING`;
- each quote buffer is stored as one row containing one bounded frozen list;
  list position preserves source order without a `quote_index` column;
- `tick_quote_item` is the one auxiliary frozen UDT; it is not a third table;
- complete and failure events do not repeat large quote payloads;
- existing tick tables remain legacy and receive no new writes;
- history has no default TTL;
- repository `LoadStateAsync` and `SaveStateAsync` overrides retain ordinary
  awaited base/repository calls; this feature does not introduce insignificant
  async-elision micro-optimizations;
- solution-wide cancellation propagation remains deferred.

### 24.3 Conditions requiring user direction

Codex stops and requests direction only when repository evidence reveals a
material conflict that changes a binding decision, public schema, dependency
direction, data-loss behavior, or external environment. Naming or file-layout
differences that preserve semantics are handled using existing conventions and
reported in the plan.

Missing credentials, Scylla availability, or native live-feed availability do
not block deterministic implementation. Codex completes all safe local work and
reports the unavailable gated validation separately.

## 25. Codex work packages

The packages are dependency ordered. Initial user approval may authorize one
package or the entire sequence. When the entire sequence is authorized, Codex
continues through it without repeatedly requesting approval unless section 24.3
is triggered.

### WP0 - Repository audit and implementation plan

Deliverables:

- completed section 24 audit;
- current dependency and actor-flow map;
- exact files to add or modify;
- mapping of every binding specification requirement to an implementation and
  test location;
- validation commands and environment-gated checks;
- list of existing working-tree changes and overlap risk;
- reviewable plan presented before production edits.

### WP1 - Shared contracts and serialization

Deliverables:

- `AssetTypeId`, `TickDataEntityId`, `TickDataId`, `QuoteEmissionReason`, and
  complete mapping/payload contracts;
- the two changed-event contracts;
- the two insert-command contracts;
- inserted, complete, and fail event families, grouped according to current
  repository conventions;
- explicit MessagePack keys exactly matching sections 7 through 11;
- construction/validation helpers for exact raw-to-decimal conversion;
- serialization round-trip and compatibility tests for every message family;
- tests proving stable subject/entity identity and non-empty correlation IDs.

WP1 must not add Scylla or live-feed behavior.

### WP2 - TickAggregationService, mapping, and bounded ownership

Deliverables:

- validated aggregation options with a default quote capacity of 64;
- single-owner futures aggregation service and per-ticker state;
- fixed-capacity pooled quote buffers with explicit ownership transfer;
- futures quote/trade mapping from normalized Databento records;
- extension of Blackboard mapping to include publisher and asset type while
  preserving both lookup directions and conflict behavior;
- cold-path mapping readiness and durable sequence initialization;
- bounded ordered actor-event producer integration without introducing a
  framework-to-domain reverse dependency;
- graceful stop, ticker removal, backpressure, and visible fault behavior;
- deterministic unit tests for every transition and conservation invariant in
  section 17.1.

WP2 must not perform Scylla, Redis, provider-query, or NATS request/reply work on
the per-record hot path. It must not create a task, lock, LINQ iterator, boxed
value, or growable collection per tick.

### WP3 - TickAggregationCommandActor and event sourcing

Deliverables:

- command actor parsing, dispatch, validation, registration, and exception
  handling following current actor conventions;
- minimal command state and snapshot/bounded-tail recovery;
- insert execution that appends the correct inserted event to
  `ActorEventSourceDb`;
- exact retry success without duplicate state mutation/event append;
- conflicting retry rejection;
- state repository and DI registration;
- command actor unit, BDD where appropriate, and integration tests;
- proof that a successful command may report no state update.

Payload arrays are not retained in long-lived command state. Repository async
overrides use clear `async`/`await` code consistent with the rest of the
solution.

### WP4 - TickAggregationEventActor and Scylla projection

Deliverables:

- changed-event handlers that await publication of the matching insert command;
- inserted-event handlers that await idempotent storage projection;
- terminal complete/fail handlers that cannot recurse;
- additive `tick_quote_item`, `tick_trade_data`, and `tick_quote_data` schema
  registration exactly matching section 12;
- storage DTOs using `DateOnly ValueDate` and `TimeOnly AggregationTime`;
- prepared trade insert, one frozen-collection quote-row write, exact-day,
  date-range, intraday-time, and date-time tuple-range queries;
- driver paging without materializing an unbounded result;
- complete events emitted only after the row succeeds;
- retry-safe replacement of the complete frozen quote collection;
- storage unit and Scylla integration tests in section 17.3.

No WP4 query may use `ALLOW FILTERING`, a token scan, secondary index,
materialized view, or a caller-visible storage bucket. Only the two approved new
tables are added.

### WP5 - End-to-end registration, lifecycle, and integration

Deliverables:

- actor API/factory, supervisor, DI, configuration, health, and metrics wiring;
- end-to-end futures flow from synthetic normalized records through event
  source and Scylla projection;
- quote-before-trade ordering proof across both actor stages;
- multi-ticker isolation and concurrency tests;
- restart, durable sequence recovery, idempotent replay, failed-row retry,
  backpressure, ticker removal, and graceful-stop coverage;
- all MarketData Feed unit/integration tests passing;
- the full Fund integration suite run as the final core-actor regression suite.

The credentialed Databento live smoke/soak run remains separately gated by user
approval and available credentials. Test output must never print the API key.

### WP6 - BenchmarkDotNet and documentation evidence

Deliverables:

- `TickAggregationServiceBenchmarks` and any narrowly required serialization or
  storage-preparation benchmarks in the existing MarketData Feed benchmark
  project;
- a genuine before baseline representing one actor message per source record;
- after measurements for capacity-64 per-ticker quote aggregation;
- all distributions and metrics required by section 18;
- raw BenchmarkDotNet artifacts left ignored/untracked unless repository policy
  says otherwise;
- summarized reproducible results added to
  `TomasAI.IFM.Domain.MarketData.Feed.Benchmarks/RESULTS.md`;
- implementation outcomes and deviations recorded in this specification and
  `Docs/Domain-Actor-Optimization-Details.md`.

Codex must never invent, extrapolate, or relabel benchmark results. If the
benchmark environment cannot run, it records the blocker and leaves the results
explicitly pending.

## 26. Non-negotiable Codex implementation constraints

1. Preserve all unrelated user changes in a dirty working tree.
2. Follow existing namespaces, nullable rules, warning-as-error settings,
   constructors, actor base classes, validation, API factories, schema contexts,
   and test conventions discovered during WP0.
3. Use explicit integer MessagePack keys. Never reorder or reuse a published
   key. Additive future fields append new keys.
4. Use the stable `TickDataEntityId` for `ActorSubject.EntityId`, actor thread,
   stream, and aggregate routing. Never use `TickDataId` as the mailbox identity.
5. Generate IDs once, propagate them unchanged, and make every retry
   idempotent.
6. Keep raw prices authoritative and validate exact decimal equality using
   `raw / 1_000_000_000m`.
7. Keep quote buffers bounded and per ticker. Never use an unbounded channel,
   history list, or fallback allocation path.
8. Preserve pooled-buffer ownership until channel acceptance and return it only
   after serialization has completed.
9. Do not use fire-and-forget actor publication or storage writes.
10. Do not conceal exceptions, mapping conflicts, sequence conflicts,
    backpressure timeouts, partial writes, or pool ownership failures.
11. Do not add code-thrown reconstruction failures merely because a stream or
    requested event type is empty; return the best valid empty state unless an
    underlying query actually fails.
12. Keep intentionally empty terminal event implementations when required by
    domain routing.
13. Do not alter existing command semantics: successful operation and state
    mutation remain independent.
14. Do not replace clear awaited repository operations with `new`, task
    wrapping, or async-elision micro-optimizations.
15. Do not add cancellation parameters across unrelated layers in this feature;
    remain compatible with the deferred solution-wide cancellation design.
16. Do not change native layouts, legacy IBKR feeds, or legacy tick-table
    behavior.
17. Do not add a third Scylla table, index, materialized view, storage bucket,
    `ALLOW FILTERING`, or full-table/token scan. The single `tick_quote_item`
    UDT defined in section 12.3 is approved.
18. Use prepared/bound statements and the established storage abstraction; do
    not construct per-record CQL strings.
19. Page date-range results and stream/map pages incrementally. Never call
    `ToList` over an unbounded tick history.
20. Treat live credentials and captured market data as sensitive. Do not print,
    serialize into errors, or commit secrets.
21. Do not commit or push unless the user explicitly requests it after reviewing
    the completed changes.

## 27. Required verification commands

Codex adjusts only test filters that differ because of final repository naming;
it reports every command, result, duration where practical, and skipped gated
check. Commands run from the repository root.

### 27.1 Fast build and unit loop

```powershell
dotnet build .\TomasAI.IFM.Domain.MarketData.Feed.Shared\TomasAI.IFM.Domain.MarketData.Feed.Shared.csproj -c Release --no-restore --nologo

dotnet build .\TomasAI.IFM.Domain.MarketData.Feed\TomasAI.IFM.Domain.MarketData.Feed.csproj -c Release --no-restore --nologo

dotnet test .\TomasAI.IFM.Domain.MarketData.Feed.UnitTests\TomasAI.IFM.Domain.MarketData.Feed.UnitTests.csproj -c Release --no-restore --nologo

dotnet test .\TomasAI.IFM.Framework.MarketData.DataBento.UnitTests\TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj -c Release --no-restore --nologo
```

### 27.2 Storage and domain integration

```powershell
dotnet test .\TomasAI.IFM.Application.Storage.IntegrationTests\TomasAI.IFM.Application.Storage.IntegrationTests.csproj -c Release --no-restore --nologo --filter "FullyQualifiedName~TickAggregation"

dotnet test .\TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests\TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.csproj -c Release --no-restore --nologo
```

Scylla-backed tests require the repository's configured integration environment.
If unavailable, Codex still builds the integration project and reports the
runtime test as environment-blocked rather than claiming it passed.

### 27.3 BenchmarkDotNet

```powershell
dotnet run --project .\TomasAI.IFM.Domain.MarketData.Feed.Benchmarks\TomasAI.IFM.Domain.MarketData.Feed.Benchmarks.csproj -c Release --no-restore -- --filter "*TickAggregation*"
```

Benchmarks run outside a debugger with a quiet machine. The results record the
runtime, GC mode, CPU, build, quote capacity, ticker count, and distribution.

### 27.4 Final core-actor validation

```powershell
dotnet test .\TomasAI.IFM.Domain.Fund.IntegrationTests\TomasAI.IFM.Domain.Fund.IntegrationTests.csproj -c Release --no-restore --nologo

dotnet build .\TomasAI.IFM.sln -c Release --no-restore --nologo

git diff --check

git status --short
```

The Fund integration suite is the required system-level regression suite for
these core actor changes. Other incomplete historical integration suites are
not substituted unless the user requests them.

## 28. Required Codex test and benchmark evidence

Codex may not declare a work package complete from compilation alone. The
handoff maps each changed behavior to at least one deterministic test. Count-only
tests are insufficient for buffered data: they also verify contents, order,
identity, ownership, and cleanup.

Before/after benchmark comparison uses identical logical input and reports:

```text
Baseline: one actor message per normalized quote/trade record
After:    one event per trade plus per-ticker bounded quote batches
```

The benchmark excludes real network and database variance from the pure service
comparison. Separate benchmarks may measure serialization and prepared-write
construction. Live soak metrics are reported separately and never mixed into
BenchmarkDotNet tables.

## 29. Codex handoff format

At every approved work-package boundary, Codex reports:

- behavior implemented;
- files added or modified;
- specification decisions applied;
- any repository-grounded deviation and its rationale;
- tests/builds/benchmarks run and exact results;
- environment-gated checks not run;
- remaining work and blockers;
- current `git status --short` summary;
- whether generated artifacts remain outside source control;
- confirmation that no commit or push occurred unless separately requested.

The final handoff also updates the acceptance checklist in section 21 and the
revision history below. A failing required test is reported plainly; Codex does
not weaken, delete, skip, or rewrite a valid test merely to obtain a green run.

## 30. Reusable Codex prompts

Use this prompt to begin safely with a plan:

```text
Read TomasAI.IFM.Domain.MarketData.Feed/Docs/Databento-Futures-Tick-Aggregation-Specification-v1.md completely.
Execute WP0 only. Inspect the current repository, working tree, actor patterns,
Databento contracts, Blackboard mapping, MarketData Scylla storage, tests, and
benchmarks. Present a concrete file-by-file implementation and validation plan
mapped to WP1-WP6, identify conflicts or blockers, and wait for my review. Do not
edit production code, commit, or push.
```

After approving the plan, use:

```text
Proceed with WP1-WP6 from the approved plan and the V1 specification. Preserve
all unrelated changes, validate each work package, run BenchmarkDotNet, run the
full Fund integration suite as final core-actor validation, update the
specification and optimization results with actual evidence, and report the
complete handoff. Do not commit or push.
```

For a smaller review boundary, replace `WP1-WP6` with the specific approved
package range.

## 31. Revision history

| Version | Date | Summary |
|---|---|---|
| 1.0 | 2026-08-07 | Codex-ready implementation specification for the futures TickAggregationService, command/event actors, versioned actor messages, per-ticker quote buffering, nullable quote-side decimals with raw sentinels, UTC `TimeOnly`, ValueDate rollover, preserved duplicate/out-of-order source records, Blackboard mapping, a bounded frozen quote-list UDT, and exactly two asset-neutral MarketData Scylla tables with single-statement date/time range queries while preserving legacy tick tables. |
