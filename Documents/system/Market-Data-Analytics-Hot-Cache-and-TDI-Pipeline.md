# Market Data Analytics, Market Outlook, and Traders Dynamic Index Pipeline

## Purpose

This document defines the system-wide conventions for generating intraday futures market-data analytics from the TickAggregation hot cache, generating the Traders Dynamic Index (TDI) from durable RSI events, and presenting selected analytics in Market Outlook. It applies to actor, API, storage, UI, console, and integration-test implementations.

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

### Authoritative application startup profile

After application startup has resolved the active ES contract and current value date, the API startup workflow starts RSI-13, ATR-14, ADX-14, and conventional MACD-9/12/26 actors for each of these timeframes: 15 seconds, 1 minute, 5 minutes, 15 minutes, 1 hour, and 4 hours. `FuturesIntradaySignalActivationProfile` is the single source of these identities and parameters. This produces exactly 24 actor start commands through `IMarketDataAnalyticsCommandApi`; TDI is not independently started because every valid RSI-13 window drives the matching TDI flow. The UI observes and queries the resulting state and does not own signal startup.

Startup records the result of every command. A partial failure is reported to the shell and status console, startup continues, and no automatic retry is attempted. Shutdown sends the matching 24 stop commands. Integration verification must observe a typed `Started` event for every configured identity, in addition to checking that every timeframe creates its signal timer registration.

### MACD configuration and identity

MACD uses the conventional default configuration of a 12-period fast EMA, a 26-period slow EMA, and a 9-period signal EMA. The public contract orders these values as `signalEmaPeriod`, `fastEmaPeriod`, and `slowEmaPeriod`.

All three periods are part of `FuturesMacdSignalEntityId`, `FuturesMacdDailySignalEntityId`, and `FuturesMacdSignalId`. They must also cross the REST and NATS query boundaries. Consequently, two MACD streams for the same contract, value date, and time frame remain different actor threads whenever any one of the three periods differs.

Each generated MACD model persists the current fast and slow EMA accumulators. The next market-price update applies the recursive EMA formulas to those accumulators, computes the MACD line as fast EMA minus slow EMA, and updates the signal EMA from the prior signal line. The command's current market price is therefore part of every calculation.

The durable projection is `futures_macd_signal_v2`, partitioned by `(contractId, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod)` and ordered by value date and timestamp. The original `futures_macd_signal` table remains unchanged as a legacy artifact; new writes and reads use only the v2 projection. Compatibility overloads that accept one period interpret it as the signal EMA period and supply the conventional 12/26 fast/slow defaults.

## Market Outlook realtime signal profile

Market Outlook is a quick operational view of the current market. Its indicator row displays MDI followed by ADX, ATR, and MACD. ADX, ATR, and MACD use one deliberately consistent five-minute profile so an operator can compare trend strength, volatility, and directional momentum at a glance.

The current profile is:

| Market Outlook field | Accepted signal | Displayed value | Purpose |
| --- | --- | --- | --- |
| ADX | Five-minute ADX-14 | `AdxValue` | Trend strength, with `PlusDI` and `MinusDI` providing direction |
| ATR | Five-minute ATR-14 with its 20-observation baseline | `AtrValue` | Current range; `AtrRatio` supplies the relative-volatility classification |
| MACD | Five-minute MACD-9/12/26 | `Histogram` | Directional momentum relative to its signal line |

Only a signal whose contract, value date, timeframe, and calculation periods match the active Market Outlook entity and this profile is eligible. ADX and MACD must be warm. ATR must be warm, positive, and have a calculated `AtrRatio`. An ineligible, stale, mismatched, or unwarmed signal does not replace the last accepted component.

### Event-to-view flow

```text
Five-minute completed bar
    -> ADX-14 / ATR-14 / MACD-9-12-26 generation command
        -> durable completed signal event
            -> signal event handler
                -> MarketOutlookComponentChangedRealtimeEvent
                    -> Market Outlook hot-cache update channel
                        -> composed MarketOutlookReadModel
                            -> API notification/query
                                -> Market Outlook view model and UI
```

The domain-completed signal is the calculation authority. Market Outlook does not recalculate the indicator, and the UI does not infer warm state from the displayed numeric value. Each component advances independently so a missing indicator does not suppress otherwise valid Market Outlook values.

### Startup historical seed

After the realtime analytics actors and their event attachments have started, application startup attempts to seed the Market Outlook five-minute ADX, ATR, and MACD streams with 35 historical bars. Thirty-five bars cover the largest current prerequisite, conventional MACD's 26-period slow EMA plus 9-period signal EMA; they also cover ADX-14 and ATR-14 with its 20-observation baseline.

The seed process:

1. Creates the previous 35 market-session-aware five-minute windows ending at the startup cutoff.
2. Reads the last retained futures trade tick at or before each window end.
3. Accepts that tick only when its market timestamp belongs to the requested window and its price is positive.
4. Builds a completed historical five-minute bar using the retained close price for open, high, low, and close.
5. Submits the bars in chronological order through the normal ADX, ATR, and MACD command actors with historical-seed provenance.

The range must be contiguous. A missing tick, a tick outside its window, an invalid price, a rejected command, or an unavailable startup dependency stops the seed attempt for that contract. These are degradations rather than application-startup failures: the actors remain active, start with empty or partially initialized historical state, and continue warming from live five-minute bars. Market Outlook displays `N/A` with neutral styling until each signal becomes warm.

Historical seeding may carry an observation value date earlier than the current actor stream value date because market-session windows can cross value-date boundaries. The historical-seed flag permits prior observation dates while still rejecting future observations. Normal live commands retain the standard value-date validation.

### Temporary presentation classifications

The current red, yellow, and green classifications are hardcoded presentation rules. They provide a fast operational summary and are not trade-entry rules, feed-health rules, or proof that the overall market is healthy.

| Indicator | Green | Yellow | Red |
| --- | --- | --- | --- |
| ADX | `AdxValue >= 25` and `PlusDI > MinusDI` | `AdxValue < 25`, or equal directional indicators | `AdxValue >= 25` and `MinusDI > PlusDI` |
| ATR ratio | `0.80 <= AtrRatio <= 1.25` | `0.60 <= AtrRatio < 0.80`, or `1.25 < AtrRatio <= 1.50` | `AtrRatio < 0.60`, or `AtrRatio > 1.50` |
| MACD histogram | `Histogram >= 2.0` | `-2.0 < Histogram < 2.0` | `Histogram <= -2.0` |

The ATR cell displays `AtrValue`, even though its color comes from `AtrRatio`. The ADX cell displays `AdxValue`, while its color combines strength with directional indicators. The MACD cell displays and classifies the histogram. Unavailable or warming values display `N/A` with the neutral Market Outlook colors.

These thresholds are the first working defaults. A later reference-data parameter set will own the Market Outlook red/yellow/green ranges so they can be documented, versioned, assigned, and optimized in backtesting without changing code. Until that parameter set is implemented, code and tests must change together whenever a threshold changes.

### Compatibility and observability

Market Outlook message contracts add new members only at unused MessagePack keys. Existing keys remain in place so previously stored or transmitted models continue to deserialize. ADX and MACD read models expose an explicit `IsWarm` member; historical generation commands expose explicit historical-seed provenance.

Operational logs distinguish a completed seed from an incomplete or failed optional seed. Snapshot diagnostics report `ADX warming`, `ATR warming`, or `MACD warming` while the corresponding component is unavailable. Expected warm-up and missing-history paths do not throw exceptions and do not prevent live recovery.

## UI and external consumers

UI and console consumers should consume the durable completed signal events or query the v2 projection. They should not independently calculate TDI from raw ticks, because that would create a second calculation and sampling authority. A later UI optimization may use asynchronous streams and throttling, but it must preserve this durable domain boundary.

## Testing requirements

- Formula tests use deterministic RSI series and assert exact price, signal, base, band, cross, state, trend, and strength outputs.
- Warm-up tests verify that fewer than 34 RSI values cannot produce TDI.
- Hot-cache tests verify active-stream gating, identity checks, feed timestamp use, and source-sequence deduplication.
- Actor tests verify MessagePack parsing and durable RSI-to-TDI routing.
- Storage integration tests verify the v2 table and partition isolation by time period and configuration.
- Replay tests verify that the event projector writes only the v2 projection and respects normal event-source projection checkpoints.
- Market Outlook eligibility tests reject wrong contracts, value dates, timeframes, periods, and unwarmed signals while accepting the configured warm five-minute profile.
- Historical-seed tests cover complete and incomplete contiguous ranges, prior-value-date acceptance, future-value-date rejection, live warm-up fallback, and command rejection.
- Presentation tests cover every ADX, ATR-ratio, and MACD threshold boundary plus neutral `N/A` behavior.
- UI binding tests verify that component updates refresh ADX, ATR, and MACD without allowing an unrelated trade-signal refresh to overwrite them.

## Deferred work

- Market-session-aware daily, weekly, and monthly scheduling is separate from the intraday TDI pipeline.
- Contract-roll scheduling remains responsible for selecting the active contract; signal actors only validate the contract they are given.
- Market Outlook color ranges remain hardcoded until the dedicated versioned reference-data parameter set is designed and assigned.
