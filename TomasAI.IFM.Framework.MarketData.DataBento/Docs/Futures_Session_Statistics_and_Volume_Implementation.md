# Futures session statistics and volume implementation

**Status:** Implemented and verified

**Date:** 2026-08-18

## Purpose

This document is the authoritative implementation note for rebuilding and
maintaining futures-session open, high, low, and volume from the Databento feed.
It applies to ES, VX, and every future registered through the same ticker-feed
path. The application and domain contracts remain provider neutral; Databento
details stop at `TomasAI.IFM.Framework.MarketData.DataBento`.

The resulting values update the existing `futures_eod_data` or
`vix_futures_eod_data` row for the contract and trading value date. They are not
a second EOD model and they do not require the UI to calculate or forward
volume.

## Startup and subscription flow

For every registered futures contract, `DatabentoMarketDataEpoch` requests:

- MBP-1 quote data;
- trade data;
- statistics data; and
- session-volume reconstruction.

Both statistics replay and trade replay begin at
`FuturesTradingValueDate.GetSessionStartUtc(ValueDate)`. This uses the trading
value-date convention, including the prior-evening session boundary, rather
than assuming that a session begins at midnight or on Monday.

The native C++ and Rust ABIs expose independent statistics- and trade-replay
start timestamps. ABI version 2 adds the trade-replay timestamp, the
`SessionVolume` data kind, a trade-replay-complete record, and the statistics
quantity field while preserving each normalized record at 64 bytes and the
feed configuration at 128 bytes.

Databento sends one replay-complete system record per replayed schema. The
native implementations classify the gateway messages (`Finished trades
replay` and `Finished statistics replay`) and close each replay independently;
a statistics completion therefore cannot prematurely expose trade volume, and
a trade completion cannot end statistics replay.

The CFE `XCBF.PITCH` statistics schema is primarily populated by its end-of-day
summary. A replay window containing no CFE statistics may complete trade replay
without emitting a statistics boundary during the smoke-test window. VX startup
therefore gates session volume on the trade boundary; it does not wait for a
statistics boundary. Any later CFE end-of-day summary is still applied as the
official open/high/low and cleared-volume replacement for its referenced date.

Only instruments explicitly mapped with `SessionVolume` receive the replay
flag. This is important when futures and options share a dataset: ordinary
option trades remain live domain inputs and cannot be mistaken for futures
volume bootstrap records.

## Provider-neutral accumulation

`FuturesSessionAccumulator` owns one in-memory state per contract and trading
value date:

- replayed trades add their sizes while volume quality is `Bootstrapping`;
- replayed trades never publish market-price, tick-storage, or downstream
  signal events;
- the trade-replay-complete marker changes quality to `ObservedComplete` and
  publishes the reconstructed cumulative volume; zero is a valid completed
  volume;
- subsequent live trades extend the observed cumulative volume before their
  normal real-time event is published;
- duplicate or out-of-order trade source sequences do not add volume twice;
- statistics types 1, 4, and 5 independently maintain session open, low, and
  high; and
- Databento cleared-volume statistic type 6 replaces the observed value and
  marks it `OfficialFinal`.

Cleared volume is routed by the UTC date of the statistic reference timestamp
(`ts_ref`), with the current trading value date as a defensive fallback. This
allows a provider correction or official final value for the prior session to
update the correct EOD row. Once official volume is final, later duplicate
statistics or trade records cannot inflate it.

The provider-neutral `FuturesSessionStatisticsSnapshot` contains open, high,
low, cumulative `long` volume, source sequence, event timestamp, value date,
and volume quality. Price statistics and volume have independent completeness
flags because either side can arrive first.

## Real-time domain and storage flow

The accumulator publishes `FuturesSessionStatisticsUpdatedRealtimeEvent` only
after it has usable price statistics or completed volume. The event is
real-time and has no replay backlog.

For the ordinary futures EOD path:

1. The handler reads the current EOD row.
2. Available provider open/high/low replaces locally inferred values.
3. Available cumulative session volume replaces the stored volume.
4. Daily percentage and direction are recalculated only when a consistent
   price-statistics snapshot is available.
5. Storage updates both `futures_eod_data` and its monthly projection without
   appending an artificial intraday row.

For VX:

1. Trade ticks remain the preferred close-price observations.
2. Quote midpoint updates remain available when VX trades are sparse.
3. Both paths attach the current session-statistics snapshot.
4. Storage sets cumulative volume rather than incrementing it, so repeated
   quote or trade observations cannot double-count volume.
5. A volume-only or price-only statistics event can update the existing
   `vix_futures_eod_data` row directly.

Legacy durable insert commands that do not carry a session snapshot retain
their existing additive-volume behavior. The live Databento path always uses
the cumulative snapshot semantics.

## Lifecycle and reset semantics

An epoch owns its feed and accumulator. Stop drains already committed native
records according to the existing bounded lifecycle rules. Disposal resets the
accumulator, and a later start builds a fresh epoch and replays the current
session from its trading-session boundary. No session totals leak across
stop/start or value-date transitions.

## Storage schema

All cumulative-volume fields are 64-bit:

- `futures_eod_data.volume`;
- `futures_eod_data_by_month.volume`;
- `futures_intra_day_data.volume`; and
- `vix_futures_eod_data.volume`.

Their authoritative Scylla types are `bigint`, and corresponding message,
read-model, API, and UI values use `long`. The legacy tick HLV query casts each
32-bit tick size to `bigint` before summing, preventing aggregate overflow and
return-type mismatch.

There are no production tables yet. Existing development databases created
from an older `int` definition must recreate the affected tables before using
this implementation. The integration fixture explicitly recreates only the
named EOD/volume tables and projection metadata in its test keyspace; ordinary
schema startup remains non-destructive.

## Verification requirements

The required automated coverage includes:

- C++ and Rust ABI layout and normalized-record parity;
- replay flags applied to trades only;
- replay completion, zero-volume completion, live continuation, duplicate
  sequence rejection, official-volume replacement, prior-date routing, and
  values above `Int32.MaxValue`;
- no domain tick or market-price publication for replay trades;
- ES and VX real-time actor routing;
- cumulative VX writes that are idempotent;
- canonical and monthly ES writes with 64-bit volume;
- real Scylla integration for all affected statements; and
- complete solution compilation after the public `long` contract propagation.

Live Databento qualification during an open session remains an operational
smoke test. Deterministic replay/native/unit/integration tests are the release
gate and do not depend on market hours.
