# Databento tick aggregation implementation details

**Status:** Phase 1 realtime cutover complete; Databento session-statistics EOD
integration completed on 2026-08-18.

This document describes the active implementation. The original durable design remains in
[Databento-Futures-Tick-Aggregation-Specification-v1.md](Databento-Futures-Tick-Aggregation-Specification-v1.md)
as historical context only; its TickAggregation CommandActor/EventActor pipeline is superseded.
System-wide semantics are authoritative in
[Actor Implementation Conventions](../../../Documents/system/Actor-Implementation-Conventions.md)
and
[Actor Message Types and Delivery Conventions](../../../Documents/system/Actor-Message-Types-and-Delivery-Conventions.md).

## Active pipeline

```text
Databento native ring
  -> managed per-InstrumentKey bounded channels
  -> exclusive multiplexed zero-copy batch reader
  -> TickAggregationService
       -> per-ticker capacity-64 pooled quote buffer
       -> quote-before-trade sequence ordering
       -> normalized decimal hot cache
       -> ES session open/high/low statistics replay plus live hot cache
       -> accepted VX quote publishes a realtime quote-price snapshot immediately
  -> bounded single-reader TickAggregationEventPublisher
  -> Core NATS Realtime.TickAggregationRealtime changed event
  -> TickAggregationRealtimeActor
  -> TickAggregationRealtimeProjector
       -> realtime inserted source
       -> tick_trade_data / tick_quote_data Scylla write
       -> realtime inserted complete or fail
```

The live path has no TickAggregation command actor, event-source stream, JetStream process/replay
queue, outbox, checkpoint, retry, or recovery worker. Each storage projection is attempted once.
Failure is published and logged; the next Databento observation is the next opportunity to update
current state.

The Core producer is owned and started by the primary `TickAggregationRealtimeActor`. The external
publisher resolves that producer after actor registration and neither starts nor stops it. This
avoids a synthetic backend actor and preserves a single lifecycle owner.

## Realtime downstream branches

The inserted trade source and the lightweight market-price source are routed to independent bounded realtime mailboxes:

- `FuturesEodDataRealtimeActor` computes rolling futures/VX EOD data and uses
  `FuturesEodDataRealtimeProjector` for one-attempt storage plus source/complete/fail publication.
  ES remains transaction-price driven. VX accepts either a trade or, when the quote is newer than
  the last trade, the exact midpoint of a positive non-crossed quote. Quote-derived VX observations
  have zero volume. The realtime VX quote path does not wait for the pooled quote batch to flush to
  `tick_quote_data`.
- `FuturesOptionTickDataRealtimeActor` filters futures-option trades, combines the exact trade with
  the latest lease-independent hot quote/Greeks snapshot, and publishes the established UI Notify
  contract.

`FuturesTickDataEventActor` and `FuturesOptionTickDataEventActor` remain durable only for explicit
stream start/stop command lifecycles. They do not subscribe to realtime ticks. Production live-feed
code resides under `Realtime` folders; `Event` folders retain only durable behavior.

## Databento session statistics and rolling futures EOD

The ES ticker feed subscribes to Databento `statistics` in addition to live
quotes and trades. At epoch startup, only the statistics subscription is replayed
from the trading session start (`ValueDate - 1` at 18:00 America/New_York,
converted to UTC with DST rules). VX remains quote/trade only because it has a
separate rolling EOD path.

The native C++ and alternate Rust adapters normalize `StatMsg` into the fixed
64-byte ABI. The managed aggregator retains only these provider statistics:

- opening price (`StatType = 1`);
- trading-session low (`StatType = 4`); and
- trading-session high (`StatType = 5`).

Replay records are accumulated without publishing partial state. The native
replay-complete marker causes one complete provider-neutral
`FuturesSessionStatisticsSnapshot` to be published and retained in the hot
cache. Later live high/low/open changes publish another transient realtime
observation. Invalid, deleted, undefined, stale, incomplete, or internally
inconsistent values are ignored.

`FuturesEodDataRealtimeActor` is the sole domain owner of this flow. If the
session row does not exist yet, the snapshot remains in `IMarketDataApi` hot
state and the next ES trade creates the row with official open/high/low values.
If the row exists, a `FuturesEodSessionStatisticsUpdatedEvent` is projected once
through `FuturesEodDataRealtimeProjector`. It updates the canonical
`futures_eod_data` row and its monthly projection without appending an
intraday-history observation. The normal realtime source/complete/fail family
is still published; no replay, retry, or durable queue is introduced.

Only `DailyPercentChange` and `PriceDirection` are recomputed because they
depend on session open. Session high and low are output/storage fields. Mean,
standard deviation, Bollinger bands, market direction, and market-direction
indicator depend on closing-price history and are intentionally unchanged by a
statistics-only correction.

The previous storage-layer override that treated yesterday's close as today's
open and its `FuturesOpenPrice` Blackboard cache have been removed. Storage now
persists the domain model exactly as supplied.

## Ordering, ownership, and storage

Every accepted trade emits one trade observation. Quotes are isolated by ticker and flush before
that ticker's trade, at 64 records, at value-date rollover, and during graceful stop. Raw 64-bit
prices and exact decimal prices are retained. `SequenceId` is shared across trade and quote events
for one contract/trading date, and aggregation timestamps are UTC.

The MarketData keyspace owns UDT `tick_quote_item` and tables `tick_trade_data` and
`tick_quote_data`. Both inserts are prepared, idempotent primary-key upserts. Persisting these rows
does not make the realtime actor messages replay durable.

Pooled quote-buffer ownership ends only after the bounded publisher has serialized the accepted
observation. Downstream UI contracts must copy explicitly approved values and must never expose the
pooled buffer.

## Phase 1 verification

- MarketData Feed unit suite covers realtime actor routing, contract identity, projector
  descriptors, futures/VX EOD, option hot-quote combination, and absence of durable tick routes.
- Analytics unit suite covers realtime Daily/Weekly/Monthly ITI and the temporary Futures Trade
  Signal compatibility projection.
- Focused workflow integration tests cover exact-decimal futures trades, VX dependency gating,
  quote-only VX midpoint projection, option hot-quote combination, Core producer ownership, and hosted Core-NATS-to-Scylla VX EOD
  source/complete flow.
- Session-statistics tests cover native ABI/layout and live SDK normalization in
  C++ and Rust, replay coalescing and live updates, application epoch
  subscription/replay configuration, realtime actor routing and metric
  recalculation, raw-record-to-domain integration, canonical/monthly Scylla
  mutation, and the absence of an intraday append.
- Host teardown treats an already-stopped market-data epoch as an idempotent stream release while
  preserving all other shutdown failures.

## Deferred to Phase 2 or later

- converting RSI, ATR, ADX, MACD, and TDI event/projector chains to realtime;
- replacing the temporary Futures Trade Signal/Market Outlook compatibility branch during UI
  optimization;
- realtime UI quote/limit-order-book contracts and throttling;
- credentialed live Databento soak and production telemetry thresholds; and
- removal of historical compatibility contracts after event-store migration policy is decided.
