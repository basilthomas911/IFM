# Market Data Analytics Hot-Cache and Traders Dynamic Index Pipeline

## Purpose

This document defines the system-wide conventions for generating intraday futures market-data analytics from the TickAggregation hot cache and for generating the Traders Dynamic Index (TDI) from durable RSI events. It applies to actor, API, storage, UI, console, and integration-test implementations.

## Design boundaries

- TickAggregation owns the current live trade and quote snapshots for each contract.
- `IMarketDataApi.IsTickDataStreamActive(contractId)` reports whether at least one active stream lease exists. Signal consumers should check it before using a hot-cache price when freshness matters.
- `IMarketDataApi.TryGetLastTickPrice(contractId, out snapshot)` reads the latest hot-cache snapshot without enforcing a lease. This deliberately keeps cache access separate from stream ownership.
- Timed indicators sample the hot cache. They do not query the futures end-of-day projection for a live price and do not write each tick to Redis or another blackboard cache.
- A sampled snapshot must match the requested contract, value date, and futures asset type before it can generate a command.
- Source sequences are accepted once per active signal registration. Repeated or older source sequences do not generate duplicate commands.
- The feed exchange timestamp, not the local timer clock, is used as the signal identifier timestamp.

## Signal flow

```text
DataBento trade
    -> TickAggregation hot-cache snapshot
        -> timed RSI sampler
            -> GenerateFuturesRsiSignal command (durable)
                -> FuturesRsiSignalsGeneratedEvent (durable RSI window)
                    -> TDI event handler
                        -> GenerateFuturesTdiSignal command (durable)
                            -> TDI event log and v2 Scylla projection

TickAggregation hot-cache snapshot
    -> timed ATR / MACD / ADX samplers
        -> corresponding durable Generate command
```

TDI does not have an independent price timer. It is downstream of the durable RSI window so RSI and TDI cannot silently use different price samples for the same calculation path.

## Traders Dynamic Index definition

The implementation is the standard RSI-based Traders Dynamic Index, not the former custom trend-direction indicator.

The initial, versioned configuration is `TDI-13-2-7-34-34-1.6185-SMA-v1`:

| Input | Value |
| --- | ---: |
| RSI period | 13 |
| Price line | 2-period SMA of RSI |
| Signal line | 7-period SMA of RSI |
| Market base line | 34-period SMA of RSI |
| Volatility lookback | 34 RSI values |
| Volatility bands | Base line +/- 1.6185 population standard deviations |
| Oversold / midline / overbought | 32 / 50 / 68 |

The minimum input is 34 ordered RSI samples produced with RSI period 13. The calculator also records the price/signal divergence, cross direction, market state, trend direction, strength, source sequence, and source event timestamp.

Supported TDI periods follow the authoritative UI intraday profile: 15 seconds, 1 minute, 5 minutes, 15 minutes, 1 hour, and 4 hours. Daily, weekly, and monthly TDI requests are rejected.

## Durable actor handoff

`FuturesRsiSignalsGeneratedEvent` is the durable handoff from RSI to TDI. The TDI event actor registers for this routed event and issues a `GenerateFuturesTdiSignalCommand` only when:

- the event period is supported for intraday TDI;
- the RSI period is 13;
- at least 34 valid RSI observations are present;
- all observations identify the same contract, value date, and time period.

The source event identifier is reused as the downstream command identifier. Event-source command deduplication and projection checkpoints therefore remain the authority during replay or redelivery.

## Message and storage contract versioning

New MessagePack members are appended; existing numeric keys are not reordered or reused.

- `FuturesRsiSignalReadModel` appends source sequence and source event timestamp.
- TDI command and identifiers append the calculation configuration identifier and time period.
- `FuturesTdiSignalReadModel` preserves legacy keys 0-7 and appends all version-2 calculation, classification, and provenance fields.

Version-2 projections are stored in `futures_traders_dynamic_index_signal`, partitioned by `(contractId, timePeriod, configurationId)` and ordered by value date and timestamp. The old `futures_tdi_signal` table remains a legacy artifact and is not populated by the v2 projector. It must not be silently interpreted as standard TDI data.

TDI queries must identify the time period and configuration. Compatibility overloads default to one minute and the standard v1 configuration, but new callers should be explicit.

## ATR, MACD, ADX, and RSI sampling convention

The signal start event owns the timer registration. On every timer callback the handler:

1. checks whether the requested contract has an active tick stream;
2. tries to read its last TickAggregation trade snapshot;
3. validates contract, value date, and asset type;
4. rejects a source sequence already processed by that registration;
5. constructs the domain signal ID with the feed event timestamp; and
6. emits the appropriate durable generation command.

Stopping the signal removes its timer registration and its sequence-deduplication state. These actors sample immutable snapshots; they do not retain or mutate TickAggregation's live price state.

### Authoritative UI startup profile

After the UI has resolved the active ES contract and current value date, it starts RSI-13, ATR-14, ADX-14, and conventional MACD-9/12/26 actors for each of these timeframes: 15 seconds, 1 minute, 5 minutes, 15 minutes, 1 hour, and 4 hours. `FuturesIntradaySignalActivationProfile` is the single source of these identities and parameters. This produces exactly 24 actor start commands through `IMarketDataAnalyticsCommandApi`; TDI is not independently started because every valid RSI-13 window drives the matching TDI flow.

Startup records the result of every command. A partial failure is reported to the shell and status console, startup continues, and no automatic retry is attempted. Shutdown sends the matching 24 stop commands. Integration verification must observe a typed `Started` event for every configured identity, in addition to checking that every timeframe creates its signal timer registration.

### MACD configuration and identity

MACD uses the conventional default configuration of a 12-period fast EMA, a 26-period slow EMA, and a 9-period signal EMA. The public contract orders these values as `signalEmaPeriod`, `fastEmaPeriod`, and `slowEmaPeriod`.

All three periods are part of `FuturesMacdSignalEntityId`, `FuturesMacdDailySignalEntityId`, and `FuturesMacdSignalId`. They must also cross the REST and NATS query boundaries. Consequently, two MACD streams for the same contract, value date, and time frame remain different actor threads whenever any one of the three periods differs.

Each generated MACD model persists the current fast and slow EMA accumulators. The next market-price update applies the recursive EMA formulas to those accumulators, computes the MACD line as fast EMA minus slow EMA, and updates the signal EMA from the prior signal line. The command's current market price is therefore part of every calculation.

The durable projection is `futures_macd_signal_v2`, partitioned by `(contractId, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod)` and ordered by value date and timestamp. The original `futures_macd_signal` table remains unchanged as a legacy artifact; new writes and reads use only the v2 projection. Compatibility overloads that accept one period interpret it as the signal EMA period and supply the conventional 12/26 fast/slow defaults.

## UI and external consumers

UI and console consumers should consume the durable completed signal events or query the v2 projection. They should not independently calculate TDI from raw ticks, because that would create a second calculation and sampling authority. A later UI optimization may use asynchronous streams and throttling, but it must preserve this durable domain boundary.

## Testing requirements

- Formula tests use deterministic RSI series and assert exact price, signal, base, band, cross, state, trend, and strength outputs.
- Warm-up tests verify that fewer than 34 RSI values cannot produce TDI.
- Hot-cache tests verify active-stream gating, identity checks, feed timestamp use, and source-sequence deduplication.
- Actor tests verify MessagePack parsing and durable RSI-to-TDI routing.
- Storage integration tests verify the v2 table and partition isolation by time period and configuration.
- Replay tests verify that the event projector writes only the v2 projection and respects normal event-source projection checkpoints.

## Deferred work

- Market-session-aware daily, weekly, and monthly scheduling is separate from the intraday TDI pipeline.
- Contract-roll scheduling remains responsible for selecting the active contract; signal actors only validate the contract they are given.
- UI-specific realtime notification contracts and throttling policies will be designed when the WinForms UI reaches its stable end state.
