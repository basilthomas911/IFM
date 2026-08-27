# Futures VWAP Signal Implementation

Implementation Record v1.0

| Item | Value |
| --- | --- |
| Status | VWAP-0 through VWAP-9 implemented |
| Date | 2026-08-26 |
| Scope | Exact ES futures session VWAP |
| Calculation owner | `FuturesVwapSignalCommandActor` |

## Architecture

VWAP is a pure trade-derived realtime signal with no timeframe. Its entity ID
is `(ContractId, ValueDate, ConfigurationId)`, where `ValueDate` identifies the
CME trade session and reset boundary.

TickAggregation remains the normalized trade-lineage authority and supplies
one executed price, executed size, action, condition flags, stream epoch, and
trade ordinal per trade-originated market-price event. It performs no VWAP
calculation.

The `FuturesVwapSignalRealtimeActor` is stateless. It owns the current ES
stream lease, filters non-trade/non-current-contract input, translates feed
contracts into provider-neutral VWAP observations, and sends
`UpdateFuturesVwapSignalCommand`.

The event-sourced `FuturesVwapSignalCommandActor` is the sole calculation and
state owner. Its exact accumulator applies:

```text
CumulativePriceVolume += TradePrice * TradeSize
CumulativeVolume += TradeSize
VWAP = CumulativePriceVolume / CumulativeVolume
```

It commits `FuturesVwapSignalUpdatedEvent` into the PostgreSQL ACID event log.
The Command-folder projector writes `futures_vwap_signal` in ScyllaDB and
publishes complete/fail lifecycle events. The Event actor is stateless. The
Query actor provides partition-safe latest and bounded-history reads.

## Continuity and recovery

Duplicate or older ordinals are idempotent. A forward ordinal gap, unexpected
stream epoch, correction/cancel/clear action without deterministic source
correlation, or replay-shaped live input invalidates exactness instead of
guessing a value.

Historical load and repair use bounded `RecoverFuturesVwapSignalCommand`
batches. Recovery has a generation ID and ordered batch ordinal, uses the same
accumulator as live processing, and remains invalid until the final batch.
Historical trades are never republished as live market-price events.

## Storage

ScyllaDB table `futures_vwap_signal` partitions by contract, value date, and
configuration. Clustering by `asOfUtc` and `lastTradeOrdinal` supports latest
and bounded session history without `ALLOW FILTERING`. Stored lineage includes
the exact numerator, volume, eligible trade count, epoch, ordinal, validity,
invalid reason, and calculation method.

## Verification

- Unit tests cover weighted calculation, duplicate and older ordinals, gaps,
  epoch changes, unsupported corrections, replay parity, incomplete recovery,
  no-timeframe identity, and stateless Realtime structure.
- BDD scenarios cover a complete trade session and invalid-gap-to-valid-replay
  behavior.
- Integration coverage sends normalized realtime trades through Core NATS,
  the Realtime and Command actors, PostgreSQL event sourcing, the Command
  projector, ScyllaDB, and the Query actor.

## Deferred work

The unified latest-signal cache, automatic acquisition after a detected live
gap, and explicit session-close orchestration remain MDSI-15/later work. They
must not move calculation state into the Realtime actor or weaken exactness.
