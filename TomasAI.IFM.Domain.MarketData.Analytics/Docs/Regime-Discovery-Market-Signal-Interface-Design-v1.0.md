# Regime Discovery Market Signal Interface Design

Design Specification v1.0

| Item | Value |
| --- | --- |
| Status | Initial design specification |
| Date | 2026-08-25 |
| Owner | Market Data Analytics bounded context |
| Primary consumer | Intrinsic Time Strategy Workflow - Regime Discovery pipeline |
| Companion design | `Regime-Discovery-Specification-v1.0.md` |
| Companion implementation plan | `Regime-Discovery-Implementation-v1.0.md` |
| Market Signal implementation plan | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |
| Target | Deterministic V1 / .NET 10 actor application |

## 1. Purpose

This document defines the Market Data Analytics changes required to supply
Regime Discovery with one coherent, immutable, point-in-time market-signal
snapshot. It translates the consumer requirements in the Regime Discovery
documents into upstream observation, indicator, provenance, caching, warm-up,
health, persistence, compatibility, and interface responsibilities.

Market Data Analytics remains the only authority that calculates and validates
market signals. Regime Discovery consumes completed signal values and performs
regime classification; it does not calculate indicators from ticks, bars, or
database rows.

## 2. Scope

### 2.1 Included

- One provider-neutral interface from Market Data Analytics to Regime
  Discovery.
- A coherent observation/bar source shared by all required indicators.
- RSI(14), ADX(14), ATR(14), conventional MACD(12,26,9),
  EMA10/20/50/200, Bollinger(10,2) and Bollinger(20,2), rolling
  range/high-low, VIX level, front/second VX term structure, session VWAP,
  and optional realized volatility.
- Existing target-horizon ITI signals and optional TDI evidence.
- Six intraday observation timeframes plus Daily.
- Complete source provenance, calculation identity, validity, warm-up, and
  freshness metadata.
- A Databento historical data load of at least one complete calendar year of
  daily observations, with enough valid trading sessions to warm EMA200, and
  the volume-bearing intraday history required for historical VWAP when that
  output is requested.
- Separation of raw Futures EOD observations from EMA, Bollinger, volatility,
  and other derived analytics.
- A bounded latest-value cache and atomic snapshot capture.
- Startup warming, contract rollover, health, observability, persistence, and
  testing requirements.
- Compatibility with the current realtime RSI-13 -> TDI path.

### 2.2 Excluded

- Trend, Volatility, Market Structure, or Fusion scoring and classification.
- Strategy Workflow configuration persistence.
- Regime Discovery actor implementation.
- ML.NET change-point detection, HMMs, adaptive weighting, and LLM analysis.
- Broker, order execution, portfolio, or risk-management behavior.
- Automatic business retries.
- Weekly or Monthly indicator calculation. Weekly and Monthly are strategy
  horizons, not market observation timeframes.

## 3. Architectural boundaries

1. TickAggregation remains the authority for latest live trade/quote state.
   It normalizes source trade facts but does not calculate VWAP.
2. A shared analytics observation/bar coordinator converts accepted market
   data into one ordered observation stream per contract and timeframe.
3. Bar-derived indicator actors calculate only from that shared observation
   stream. They must not independently sample TickAggregation for the same
   interval once the new path is enabled. Session VWAP is the explicit
   trade-derived exception: its actor consumes normalized trade-originated
   market-price events and owns the cumulative calculation.
4. Market Data Analytics owns all indicator formulas, warm-up rules,
   calculation versions, signal validation, latest-value cache entries, and
   signal persistence. This includes the complete VWAP accumulator and
   calculation lifecycle.
5. Regime Discovery supplies a provider-neutral list of required signal keys
   and freshness limits. It never supplies a Trade-domain type to Market Data
   Analytics.
6. ScyllaDB remains durable query/history storage and the source for startup
   warming. It is not queried during a live Regime Discovery snapshot capture.
7. A missing, stale, invalid, not-warm, future-dated, or incompatible required
   signal is returned explicitly. It is never replaced with zero or another
   default.
8. Cache state is operational and rebuildable. It is not authoritative
   business history.
9. Regime Discovery receives its target-horizon ITI trigger as part of the
   capture request. Market Data Analytics validates its identity and includes
   it in the frozen snapshot without recalculating it.
10. No Market Data Analytics project references Domain.Trade.Shared.

## 4. Existing system inventory

### 4.1 Reusable capabilities

| Capability | Existing implementation | Design use |
| --- | --- | --- |
| Latest live ES/VX price | `IMarketDataApi.TryGetLastTickPrice` and `FuturesMarketPriceSnapshot` | Source for live observation and VIX-futures legs |
| Active stream status | `IMarketDataApi.IsTickDataStreamActive` | Live observation health evidence |
| Intraday lifecycle profile | `FuturesIntradaySignalActivationProfile` | Existing six-timeframe identity convention |
| Realtime RSI | `FuturesRsiSignalRealtimeActor` | Preserve RSI-13/TDI and extend shared observations to RSI-14 |
| Realtime ATR/ADX/MACD | Existing realtime actors/projectors | Extend provenance and consume shared observation IDs |
| TDI | RSI-13 window -> TDI realtime actor | Optional intraday confirmation; unchanged formula |
| ITI | Daily/Weekly/Monthly realtime generation | Target workflow trigger and ITI evidence |
| Futures EOD | `FuturesEodDataModel`, its command actor, and Scylla projections | Existing raw-plus-derived implementation to split into raw EOD observation and Analytics outputs |
| EOD indicators | Daily RSI/ATR/ADX/MACD signal variants | Daily warm-up/support input after lifecycle completion |
| Market Outlook coordinator | `MarketOutlookSnapshotRealtimeActor` | Useful coordination precedent, not the new signal cache |
| Redis RSI cache | `FuturesRsiSignalCacheModel` and Daily counterpart | Compatibility cache; not the atomic regime snapshot |
| Scylla indicator tables | Latest/history queries for RSI/TDI/MACD/ADX/ATR/ITI | Startup warmer and diagnostics |
| Latest-value channels/cache primitives | Shared caching/channel infrastructure | Reusable implementation primitives |

### 4.2 Gaps

- Current intraday RSI, ATR, ADX, and MACD timers may sample different latest
  ticks for the nominally same interval; there is no shared observation ID.
- RSI-13 is activated for TDI, but Regime Discovery requires RSI-14.
- MACD, ADX, and ATR read models lack complete source sequence/event timestamp,
  schema, and calculation-configuration provenance.
- EMA20/EMA50/EMA200 and their slopes are not produced as a coherent signal.
- EMA10 is not produced with the other trend horizons.
- ATR baseline and ATR ratio are not produced.
- The current FuturesBarData model carries one BarValue rather than complete
  OHLCV and therefore cannot authoritatively support true range, rolling
  highs/lows, or breakout distance.
- Intraday Bollinger width/position and its width baseline do not exist.
- The current `FuturesEodDataModel` calculates Bollinger/statistical fields in
  the Market Data Feed bounded context, while `BollingerBands` also interprets
  VX data. Raw EOD acquisition, indicator calculation, and volatility
  interpretation are therefore coupled.
- There is no Bollinger(10,2) signal and no independently cached Bollinger
  family from which EOD views can be composed.
- There is no session-aware futures VWAP signal.
- Current VIX level is not distinguished from a VX futures price.
- Front/second VX contract identity and term-structure ratio are not exposed as
  one current signal.
- There is no unified typed latest-signal cache, atomic capture revision, or
  snapshot health report.
- Intraday strategy-required signal activation currently depends on UI
  startup. A headless strategy host must not depend on the UI being open.
- Daily scheduled indicator lifecycle is incomplete for the new seven-
  timeframe contract.
- The current managed Databento runtime has live-feed and reference-data
  capabilities, but this bounded context does not yet expose the historical
  OHLCV/trade acquisition and idempotent backfill workflow required to warm a
  new Daily EMA200 series.

## 5. Shared market observation stream

### 5.1 Observation authority

Add a server-owned `FuturesTradeSessionBarSignal`. Its stateless Realtime
actor routes live trades through a concrete actor-centric accumulation Model.
For each active contract the Model owns the six intraday schedules:

- 15 seconds
- 1 minute
- 5 minutes
- 15 minutes
- 1 hour
- 4 hours

The Model observes accepted trades continuously, builds ephemeral OHLCV
buckets, and closes one immutable bar at each configured exchange-session
boundary. The Realtime actor sends the completed bar to the event-sourced
Publisher Command actor. After the ACID event-log commit and ScyllaDB
projection, the stateless Publisher Event actor publishes one
`FuturesTradeSessionBarClosedRealtimeEvent`.
Every required bar-derived indicator for that contract/timeframe consumes the
same event. The VWAP actor instead consumes every eligible normalized trade so
that a bar close cannot discard within-bar price/volume information.

The observation contains:

```text
ObservationId
ContractId / instrument identity
ValueDate
TimeFrame
IntervalStartUtc / IntervalEndUtc
Open / High / Low / Close / Volume
TradeCount / PriceVolumeSum
FirstSourceSequence / LastSourceSequence
FirstMarketEventUtc / LastMarketEventUtc
CalculatedAtUtc
SchemaVersion / CalculationVersion
IsComplete / IsValid
```

`ObservationId` is deterministic from contract, timeframe, interval end, and
last accepted source sequence. Duplicate or older observations are rejected.
All timestamps are UTC in contracts; exchange-session alignment uses the
configured market calendar and time zone.

`PriceVolumeSum` is the sum of each accepted trade price multiplied by its
eligible trade volume within that closed observation. It provides bar-level
audit/replay evidence when the source schema supplies trades; the live session
VWAP authority remains `FuturesVwapSignalCommandActor`, not the observation
coordinator or Realtime router. `PriceVolumeSum` is not inferred from OHLC alone. Historical
inputs that contain only interval bars must state their VWAP calculation
method and cannot be labeled tick-exact.

### 5.2 Session alignment

- ValueDate follows the existing futures trading-day definition.
- Intraday buckets align from the futures session start rather than process
  startup time.
- The normal maintenance break does not create fabricated zero-volume bars.
- Four-hour bars align to the session boundary, not midnight UTC.
- A Daily observation is the latest completed trading session and is emitted
  only after its authoritative EOD/session-close barrier.
- VWAP resets at the configured futures session boundary, not midnight UTC,
  and excludes the maintenance break according to the same market calendar.
- Holidays, early closes, and contract-roll boundaries come from the market
  calendar/contract service. They are not embedded as indicator constants.

### 5.3 Migration from independent timers

The existing RSI/ATR/ADX/MACD Start and Stop commands remain compatibility
surfaces during migration. Once an entity is attached to the shared
coordinator, its legacy per-indicator timer must not sample independently.
Both paths cannot generate the same logical observation.

The authoritative activation profile becomes server-owned. UI controls may
start/stop market feeds, but the UI is not the owner of strategy-required
indicator calculation. Contract rollover stops the old contract coordinator,
starts the new contract coordinator, and preserves separate cache keys and
histories.

## 6. Signal contract model

### 6.1 Common metadata

Every latest-value entry implements the semantics of
`MarketAnalyticsSignalMetadata`:

```text
SignalKind
ContractId / instrument identity
FuturesSeriesId / continuation identity when applicable
TimeFrame
ObservationId
MarketDataAsOfUtc
CalculatedAtUtc
SourceSequence
SchemaVersion
CalculationConfigurationId
CalculationVersion
IsWarm
IsValid
ValidationIssueCodes[]
```

`MarketDataAsOfUtc` is the last exchange event used by the calculation, never
the local timer or persistence timestamp. `CalculatedAtUtc` records local
calculation completion. Configuration identity distinguishes formula inputs
such as RSI period or MACD periods. Cache insertion rejects a lower source
sequence or an older observation for the same key.

### 6.2 Signal key

`MarketAnalyticsSignalKey` consists of:

```text
MarketSeriesIdentity (specific ContractId or roll-aware FuturesSeriesId)
SignalKind
TimeFrame
CalculationConfigurationId
```

The key prevents RSI-13 from being mistaken for RSI-14 and prevents alternate
MACD, Bollinger, EMA, or baseline configurations from overwriting one another.
It also prevents a Daily root-continuation value from masquerading as a
specific-contract intraday value.

### 6.3 Existing contract evolution

Append MessagePack members to MACD, ADX, and ATR generated/read-model
contracts for:

- SourceSequence
- SourceEventTimestamp / MarketDataAsOfUtc
- ObservationId
- SchemaVersion
- CalculationConfigurationId
- CalculationVersion

RSI already carries source sequence and timestamp; append ObservationId,
schema, and calculation identity without reordering existing keys. TDI keeps
its existing schema/configuration/source fields and appends ObservationId.
Daily and intraday variants use the same provenance semantics.

Warm/valid status belongs to the cache envelope, not to immutable historical
read models whose calculation already completed.

### 6.4 Futures market-price event evolution for VWAP

Use the existing `FuturesMarketPriceUpdatedRealtimeEvent` as the live input to
VWAP. Its current `FuturesMarketTradeSnapshot.LastSize` is copied from the
normalized Databento trade record `Size`; it is the size of that individual
trade observation, not cumulative session volume. `BidSize` and `AskSize` are
quote liquidity and never contribute to VWAP.

Append, without reordering or reusing existing MessagePack keys, the
provider-neutral trade semantics needed by an actor-owned accumulator:

```text
NormalizedTradeAction
NormalizedTradeSide
NormalizedTradeConditionFlags
StreamEpochId
TradeOrdinal
```

The Databento adapter maps its action, side, header flags, and DBN condition
flags into these provider-neutral values. It may retain raw provider flags as
provenance, but Market Data Analytics does not implement its eligibility rules
against unexplained provider-specific numeric constants.

`TradeOrdinal` is a monotonically increasing per-contract/value-date ordinal
for accepted non-replay trade inputs. `StreamEpochId` changes when the source
stream is reconstructed. These are delivery-lineage values, not indicator
calculations. They allow the VWAP actor to distinguish a duplicate from a
missed trade even when quote records are interleaved in the provider source
sequence.

The event can contain the cached latest trade when `UpdateSource` is `Quote`.
Consumers must therefore treat the trade as a new VWAP input only when:

```text
UpdateSource == FuturesMarketPriceUpdateSource.Trade
```

TickAggregation remains responsible only for normalized event production and
lineage. It must not add cumulative price-volume, calculate VWAP, or decide
which trade conditions the strategy accepts.

## 7. Required signal designs

### 7.1 Current price

The snapshot includes the latest valid trade price and its TickAggregation
source sequence/timestamp. A live price is valid only when contract, asset
type, and value date match the request. Stream-active status is recorded
separately so a Daily support value is not confused with a live price.

### 7.2 RSI

- Preserve RSI-13 exclusively as the standard TDI source.
- Add RSI-14 as the Regime Discovery trend input.
- Both consume the same shared observation close.
- RSI-14 exposes RSI value and slope.
- Slope is current RSI minus prior RSI for the same key/observation sequence.
- RSI is warm after the configured period plus the prior value required for
  slope; RSI-14 therefore requires at least 15 valid closes.
- A missing prior slope value means not warm; it is not set to zero.

### 7.3 ADX

ADX remains period 14 and provides ADX, PlusDI, MinusDI, direction, and
strength. It consumes OHLC bars from the shared observation stream. Warm-up
follows the existing Wilder calculation requirements and is reported
explicitly. The read model receives full common provenance.

### 7.4 MACD

MACD remains conventional fast 12, slow 26, signal 9 and provides fast EMA,
slow EMA, MACD line, signal line, histogram, direction, and strength. It is not
a substitute for EMA10/20/50/200. The read model receives the common provenance
and uses the shared observation close.

### 7.5 EMA signal

Add a coherent logical `FuturesEMASignal` family. Following the repository's
C# acronym convention, the concrete public type should be named
`FuturesEmaSignalReadModel`. One signal contains all configured periods so a
consumer cannot accidentally combine values calculated from different
observations:

```text
Price
Ema10 / Ema20 / Ema50 / Ema200
PreviousEma10 / PreviousEma20 / PreviousEma50 / PreviousEma200
Ema10Slope / Ema20Slope / Ema50Slope / Ema200Slope
Common metadata
```

Each EMA uses the conventional multiplier `2 / (period + 1)` and the same
completed-observation close. Slope is current EMA minus the previous EMA of
the same period. ATR normalization occurs when the snapshot composer joins
this signal with the same-timeframe ATR signal; it is not calculated by
introducing an EMA -> ATR actor dependency.

Periods count completed observations. They are 10/20/50/200 days on the Daily
timeframe and 10/20/50/200 bars on an intraday timeframe. The family is fully
warm only after EMA200 and one prior EMA200 value exist. Individual period
values may be exposed earlier with per-period warm flags, but Regime Discovery
must not treat the whole family as warm until every configured required period
is warm.

### 7.6 ATR volatility signal

ATR remains period 14. Extend the Market Data Analytics output with a
`FuturesAtrVolatilitySignalReadModel` containing current ATR, prior ATR,
baseline ATR, ATR ratio, true range, and common metadata.

The V1 default baseline is the simple mean of the prior 20 completed ATR
values, excluding the current ATR. BaselinePeriod is part of calculation
configuration. ATR ratio is current ATR divided by positive baseline ATR.
The signal is not warm until ATR(14), a complete 20-ATR baseline, and a prior
ATR are present.

### 7.7 Bollinger Band signal

Add a logical `FuturesBBSignal` family, represented in C# as
`FuturesBbSignalReadModel`, calculated from the same completed closes as the
EMA signal. It contains two independently configured bands:

```text
Price
Ema10 / StandardDeviation10 / UpperBand10 / LowerBand10 / Width10 / Position10
Ema20 / StandardDeviation20 / UpperBand20 / LowerBand20 / Width20 / Position20
Width20Baseline / Width20Ratio
Common metadata
```

V1 explicitly uses EMA10 and EMA20 from the same-observation
`FuturesEmaSignalReadModel` as centerlines and two population standard
deviations of the corresponding 10 or 20 completed closes:

```text
UpperBandN = EmaN + (2 * PopulationStandardDeviationN)
LowerBandN = EmaN - (2 * PopulationStandardDeviationN)
WidthN = UpperBandN - LowerBandN
PositionN = (Close - LowerBandN) / WidthN
```

This is an EMA-centered Bollinger definition, rather than the conventional
SMA-centered definition, and therefore receives its own calculation version.
The BB actor may consume the matching EMA signal after both are produced from
the same observation, or a coordinator may supply the EMA values directly;
it must reject a different ObservationId. EMA output supplies only the
centerline. The BB calculation still owns its rolling close window and
standard deviation.

The width baseline is the simple mean of the prior 20 completed Width20
values, excluding the current width. A zero or negative width is invalid.
On Daily, the periods mean 10 and 20 completed trading days; on intraday they
mean 10 and 20 completed bars.

### 7.8 Market Structure signal

Add `FuturesMarketStructureSignalReadModel`, calculated from shared OHLCV
observations and the compatible BB/ATR outputs, containing:

```text
Price / Open / High / Low / Close
CurrentRange / AtrNormalizedRange
Prior20High / Prior20Low
BreakoutDistance / BreakoutDistanceAtr
Bb10Position / Bb20Position / Bb20WidthRatio
Common metadata
```

Prior20High/Prior20Low use the prior 20 completed bars and exclude the current
bar so a breakout can exceed the boundary. ATR- and BB-derived values require
the same timeframe, configuration compatibility, and ObservationId.
Zero/negative denominators are invalid, never zero-filled.

### 7.9 VIX and VX term-structure signals

Keep VIX spot separate and add a logical `FuturesVXTermStructureSignal`,
represented in C# as `FuturesVxTermStructureSignalReadModel`, containing:

```text
FrontVxContractId / FrontExpiry / FrontVxPrice
BackVxContractId / BackExpiry / BackVxPrice
FrontBackSpread
FrontBackRatio
TermStructurePercent
TermStructureState
PriorFrontBackRatio / PriorTermStructurePercent
Common metadata for each source leg
Composite CalculatedAtUtc / IsWarm / IsValid
```

The front leg is the configured first eligible VX expiry and the back leg is
the immediately following eligible VX expiry. The securities/rollover
authority resolves both identities for every market date; the signal actor
must not infer the back contract by editing a symbol. Both live prices come
from TickAggregation and must be active, positive, identity-valid, and within
the configured source-time skew.

V1 calculations are:

```text
FrontBackSpread = BackVxPrice - FrontVxPrice
FrontBackRatio = FrontVxPrice / BackVxPrice
TermStructurePercent = (BackVxPrice / FrontVxPrice) - 1
TermStructureState = Contango when percent > epsilon,
                     Backwardation when percent < -epsilon,
                     Flat otherwise
```

The epsilon is configuration, not an actor constant. Rollover produces a new
signal identity/revision with the newly resolved front and back legs; it never
combines one pre-roll leg with one post-roll leg. Historical population uses
the contracts that were actually front and back on each historical date, not
today's front/back identities.

`VixVolatilitySignalReadModel` may compose the independent VIX spot level with
this term-structure signal for Regime Discovery. A front VX price or the ITI
event's existing `VixFuturesPrice` must not be labeled as VIX spot. Existing
VIX/VX EOD data may warm Daily support evidence but cannot silently replace
required current intraday values. Until a current VIX spot provider is
configured, a request that requires VIX spot returns RequiredMissing.

### 7.10 VWAP signal

Add a logical `FuturesVWAPSignal`, represented in C# as
`FuturesVwapSignalReadModel`. Calculation is owned by a dedicated
event-sourced `FuturesVwapSignalCommandActor`; TickAggregation never
calculates this signal. Its stateless `FuturesVwapSignalRealtimeActor` owns
only live route/stream lifecycle and translates eligible feed events into
commands.

The read model contains:

```text
SessionStartUtc / AsOfUtc
CumulativePriceVolume / CumulativeVolume / EligibleTradeCount
Vwap / PriceMinusVwap / PriceToVwapPercent
LastTradeSourceSequence / StreamEpochId / LastTradeOrdinal
IsTickExact / CalculationMethod
Common metadata
```

The realtime actor receives `FuturesMarketPriceUpdatedRealtimeEvent` and
accepts an accumulator input only when `UpdateSource` is `Trade`, the contract
and value date match, the trade is newer, and its normalized action/condition
passes the configured eligibility rules. It sends an immutable update command;
for live data the Command actor calculates:

```text
CumulativePriceVolume += LastPrice * LastSize
CumulativeVolume += LastSize
EligibleTradeCount += 1
Vwap = CumulativePriceVolume / CumulativeVolume
```

The Command actor owns durable event-sourced session state containing the
cumulative numerator, cumulative executed volume, eligible-trade count, last
accepted source identity, current value date, current VWAP, and validity. It
processes every eligible trade. The Realtime and Event actors retain no
calculation state.

The actor resets at the futures value-date session boundary and continues
across intraday observation closures without resetting for each timeframe.
A normalized trade eligibility/correction/cancellation policy is part of the
calculation configuration. Corrections reverse or replace a known prior
contribution only when the normalized source identity permits deterministic
correlation. An uncorrelatable correction invalidates the accumulator and
requires replay; it is never guessed or ignored silently.

Zero cumulative volume means not warm. Quotes, bid/ask sizes, OHLC close
prices, and the separate cumulative/official session-volume statistic are not
substituted for individual executed-trade size. A quote-originated market
price event may carry the last cached trade, but the actor ignores it because
its `UpdateSource` is not `Trade`.

For each accepted update, `StreamEpochId` and `TradeOrdinal` provide delivery
continuity. A duplicate or older ordinal is ignored. A forward gap, unexpected
epoch transition, market-feed publication failure, or inconsistent
session-close ordinal marks the VWAP signal invalid and initiates bounded
current-session recovery. The actor must not keep publishing an apparently
valid VWAP after losing an unknown trade contribution.

The Command actor commits `FuturesVwapSignalUpdatedEvent` to the PostgreSQL
event log. Its Command-folder event projector writes the ScyllaDB read model
and publishes the standard complete/fail lifecycle events. The projected read
model contains accumulator lineage for diagnostics, while command-state
reconstruction uses the durable event stream.

Historical VWAP is tick-exact only when the Databento source includes the
eligible trades needed for the numerator and denominator. A bounded bar-based
approximation may be stored only with a distinct calculation method/version
and `IsTickExact = false`; it cannot satisfy a parameter set that requires
tick-exact VWAP.

Historical data-load and current-session recovery use a private, bounded VWAP
warm-up/replay Command addressed only to `FuturesVwapSignalCommandActor`. They
do not republish historical trades as live
`FuturesMarketPriceUpdatedRealtimeEvent` messages, because other live market
consumers must not interpret replay prices as current market changes. Replay
uses the same eligibility and accumulator calculation functions as live
processing and must produce the same result for the same ordered trade set.

VWAP is included in the Analytics cache and snapshot contract. It is optional
for the first deterministic Regime Discovery parameter set until scoring rules
explicitly require it, avoiding an undocumented scoring change.

### 7.11 Optional realized volatility

When enabled, `FuturesRealizedVolatilitySignalReadModel` uses the configured
lookback (V1 default 20 completed closes), calculates close-to-close log-return
volatility, and exposes the configured annualization and percentile baseline.
It remains optional for initial Regime Discovery. Absence lowers coverage only
when the parameter set enables it.

### 7.12 ITI and TDI

The target Daily/Weekly/Monthly `FuturesItiSignalGeneratedEvent` is supplied by
the capture request and validated against contract, value date, target
horizon, schema, and event identity. Its direction, mode, BandLevel, and
ReversalLevel are retained unchanged.

TDI remains optional, intraday-only evidence. It continues to use RSI-13 and
must never consume the new RSI-14 series. No Daily, Weekly, or Monthly TDI is
introduced.

## 8. Latest-value cache

### 8.1 Ownership and structure

Add a singleton `IMarketAnalyticsLatestSignalCache` implementation owned by
Market Data Analytics. It is a bounded, process-local, latest-value store keyed
by `MarketAnalyticsSignalKey`. Each accepted mutation increments a monotonic
cache revision.

The cache stores immutable signal envelopes. It does not expose mutable
collections or references to actor-owned calculation windows. Historical
series remain in actor state and ScyllaDB, not the latest-value cache.

### 8.2 Update ordering

For a realtime calculation:

1. calculate from one shared observation;
2. validate formula output and provenance;
3. project the accepted signal to ScyllaDB;
4. confirm it into the actor's bounded calculation state;
5. update the latest-value cache; and
6. publish the normal completion event.

An invalid candidate does not reach persistence or cache. A storage failure
does not advance actor state or cache. An unexpected cache failure after a
successful projection marks cache health unhealthy and is repaired by warm-up
or the next valid observation; it does not fabricate a pipeline value.

### 8.3 Startup warming

The cache warmer reads the latest compatible Scylla projection and enough
historical observations to reconstruct calculation windows. It validates
schema/configuration/provenance before marking a key warm. A row's existence
alone is not proof of warm status.

Warm-up occurs before live Regime Discovery routing is enabled. The health
surface reports readiness by contract, timeframe, signal kind, and
configuration. Partial readiness is visible; it is not collapsed into one
misleading global boolean.

### 8.4 Contract rollover

Intraday cache keys include full contract ID. The new contract warms
independently; intraday indicator windows are not copied across contracts.
Daily roll-aware calculations additionally key by `FuturesSeriesId` and carry
the current source contract/roll metadata. A series value may survive an
expected contract roll only through the versioned continuation rule described
in section 11.3; arbitrary contract-state copying remains prohibited.
Strategy triggering for a new contract remains disabled until its configured
required signal set is ready. Old contract entries may remain for bounded
diagnostics but cannot satisfy a new-contract request.

## 9. Snapshot interface

### 9.1 Contract ownership

The consumer-facing contracts and interface live in:

```text
TomasAI.IFM.Domain.MarketData.Analytics.Shared/
  RegimeDiscovery/
    Contracts/
    Model/
    ServiceApi/
```

The implementation lives in:

```text
TomasAI.IFM.Domain.MarketData.Analytics/
  RegimeDiscovery/
    SignalCache/
    Snapshot/
    Warmup/
    Health/
```

Trade.Shared already depends on MarketData.Analytics.Shared, so Regime
Discovery can consume these contracts without a circular dependency.

### 9.2 Request

`RegimeDiscoveryMarketSignalSnapshotRequest` contains:

```text
ContractId / instrument identity / FuturesSeriesId when Daily is requested
ValueDate
TargetHorizon
FuturesItiSignalGeneratedEvent trigger
RequestedAtUtc
FutureClockSkewTolerance
SignalRequirements[]
```

Each `MarketSignalRequirement` contains SignalKind, TimeFrame,
CalculationConfigurationId, Required/Optional, and MaximumAge. Regime
Discovery maps its immutable parameter set into this provider-neutral request.
Market Data Analytics does not read ConfigurationDb or interpret strategy
weights.

### 9.3 Response

`IRegimeDiscoveryMarketSignalSnapshotProvider.CaptureAsync` returns
`RegimeDiscoveryMarketSignalSnapshotResult` with either:

- one immutable `RegimeDiscoveryMarketSignalSnapshot`; or
- structured availability failures for all unresolved requirements.

The snapshot contains:

```text
SnapshotId / CacheRevision
ContractId / ValueDate / TargetHorizon
CapturedAtUtc
Validated ITI trigger
Current price
Typed signal envelopes by requested key
Minimum/weighted/maximum signal age
Availability issues[]
IsComplete
```

Business availability does not throw. Infrastructure failure or cancellation
does. `IsComplete` is false when any required requirement fails. Optional
failures remain in AvailabilityIssues and are omitted from the typed value
set.

### 9.4 Atomic capture

Capture uses a revision-stability loop:

1. read cache revision;
2. read every requested immutable entry;
3. read cache revision again;
4. accept only when revisions match; otherwise retry; and
5. fail with a consistency issue after a configured bounded retry count.

This provides a coherent cache cut without locking indicator writers for the
duration of snapshot assembly. Individual signals need not share the exact
same ObservationId across different timeframes, but all required signals must
pass their configured freshness and compatibility rules. Signals within the
same timeframe must share the requested observation lineage or satisfy the
explicit configured skew tolerance.

## 10. Availability and validation codes

Market Data Analytics returns provider-level codes that Regime Discovery maps
to its stable `RD.DATA.*` and `RD.CONFIG.*` reasons:

| Provider code | Meaning |
| --- | --- |
| `RequiredMissing` | No entry for a required key |
| `OptionalMissing` | No entry for an enabled optional key |
| `NotWarm` | Calculation window is incomplete |
| `Stale` | MarketDataAsOfUtc exceeds request MaximumAge |
| `FutureTimestamp` | Source exceeds future-clock-skew tolerance |
| `Invalid` | Formula, value, denominator, identity, or validation failure |
| `UnsupportedSchema` | Consumer cannot accept the signal schema |
| `CalculationVersionMismatch` | Requested and cached calculation identities differ |
| `ObservationMismatch` | Same-timeframe required signals have incompatible lineage |
| `StreamInactive` | A required live source has no active stream |
| `ContractMismatch` | Signal belongs to another contract/value date |
| `SeriesMismatch` | Daily continuation identity or roll version does not match the request |
| `CalculationMethodMismatch` | For example, approximate historical VWAP was supplied where tick-exact VWAP is required |
| `HistoricalGap` | A required calculation window contains an unresolved source gap |
| `DeliveryGap` | VWAP or another cumulative signal detected a missing live source input and has not completed recovery |
| `CaptureContention` | Bounded atomic capture retries were exhausted |

Issues contain the signal key, severity, safe diagnostic text, observed
metadata, and expected requirement. Raw high-volume series are not included.

## 11. Daily support lifecycle

### 11.1 Daily calculation barrier

Daily is a completed observation interval, not a recurring intraday timer.
After the authoritative session/EOD barrier, Market Data Analytics calculates
or refreshes Daily RSI-14, ATR-14 plus baseline, ADX-14, MACD,
EMA10/20/50/200, BB10/20, range/high-low structure, VWAP, VX term structure,
and Daily volatility context from durable history.

Weekly and Monthly Strategy Workflows use the latest valid Daily signal as
support or primary evidence according to their parameter sets. They do not
create Weekly/Monthly versions of these indicator actors. The 96-hour default
freshness tolerance is evaluated by the consumer request and supports a normal
weekend; future market-calendar-aware freshness may supersede that default.

### 11.2 Futures EOD responsibility split

Refactor the Market Data Feed `FuturesEodData` path so its authoritative state
and projection contain raw completed-session facts only:

```text
ContractId / SeriesId / ValueDate
SessionStartUtc / SessionEndUtc
Open / High / Low / Close / Volume / TradeCount / PriceVolumeSum
Source dataset/schema/symbol/instrument identifiers
First/last source sequence and market timestamps
ObservationId / schema version / validity
```

`FuturesEodDataModel` and `BollingerBands` must no longer calculate EMA,
Bollinger, market direction, market volatility, or VX-derived interpretation.
The raw EOD projection publishes the authoritative Daily observation after
storage succeeds. Market Data Analytics then calculates each derived signal,
updates its hot cache, and projects its own history.

Existing UI/API consumers that still require a combined EOD shape use a query
assembler. The assembler joins the raw EOD observation with exact-key,
same-ObservationId `FuturesEmaSignalReadModel`,
`FuturesBbSignalReadModel`, and any other requested Analytics cache values.
It reports missing/not-warm analytics explicitly. The raw EOD command must not
read a timing-dependent hot-cache value and persist that value back into the
raw row.

Because there is no production data dependency, this is a development schema
cutover rather than a long-lived dual-write migration. Compatibility adapters
may remain temporarily for compile-time consumers, but there must be one
authoritative raw EOD write path and one authoritative calculation per signal.

### 11.3 Databento historical data load

Add a provider-backed, resumable Databento historical data load before Daily
EMA/BB qualification. Its initial requested interval is at least one complete
calendar year ending at the last completed market session. The normalized
Daily history target is at least 252 valid trading sessions and must contain
at least 201 consecutive valid closes before EMA200 plus its prior value can
be marked warm. If one calendar year yields fewer than the required valid
sessions, acquisition extends the start date backward until the configured
minimum is satisfied. Calendar holidays do not create synthetic observations.

Daily EMA/BB calculations require canonical Daily OHLCV. Historical session
VWAP additionally requires eligible trades, or a source carrying an exact
price-volume numerator. VX term-structure history requires both contracts
that were front and second on each historical market date. The backfill shall
therefore acquire and normalize the minimum source schemas needed by the
enabled calculations rather than pretending Daily close/volume is sufficient
for every signal.

Futures expire, so a current quarterly contract normally cannot supply a
meaningful 200-session history by itself. Daily long-window analytics use a
separate `FuturesSeriesId` for an explicit roll-aware root continuation while
each observation and signal also retains its actual source `ContractId`. The
V1 continuation rule shall:

1. resolve the eligible front contract from the historical contract calendar;
2. record every roll date and source contract;
3. backward-adjust older segments so the current active segment remains on
   the tradable price scale;
4. version the roll and adjustment method; and
5. reject an unexplained gap or ambiguous contract identity.

If a Databento continuous-series capability is used, the adapter must still
persist the provider symbol convention, roll policy, dataset, schema, and
normalization version. If per-contract data is stitched internally, the same
manifest requirements apply. A specific-contract intraday signal is never
silently substituted for a root-continuation Daily signal; both identities
are explicit in the snapshot.

Each historical acquisition is idempotent and records a manifest containing
the request interval, provider dataset/schema/symbols, returned interval,
record count, source hash/checksum where available, acquisition time,
normalization/calculation versions, gap audit, and completion status. Partial
downloads resume from checkpoints. Duplicate Daily ObservationIds are ignored
and conflicting observations fail the job.

The historical data loader replays normalized observations through the same signal
calculators used by live Daily processing, in market-time order. It must not
insert precomputed indicator rows directly. EMA initialization uses the simple
mean of the first complete period as its deterministic seed, followed by the
standard recursive EMA formula. After replay, Scylla histories and hot caches
must agree on the latest ObservationId and calculation version.

This historical data load creates the initial operational working set. It should reuse the
Databento acquisition/manifest conventions in
`Documents/system/Historical_Market_Data_Backtesting_Archive_Specification_v1.0.md`
rather than creating an incompatible historical client. The permanent monthly
archive remains a separate retention workflow.

## 12. Persistence and query model

- Existing RSI/TDI/MACD/ADX/ATR Scylla projections append provenance and
  calculation identity where missing.
- Raw Futures EOD observations are persisted independently of derived
  Analytics outputs.
- New EMA, BB, ATR Volatility, Market Structure, VWAP, VX Term Structure, and
  VIX Volatility projections are partitioned by series/contract, timeframe,
  and configuration ID and ordered by value date/market timestamp.
- New development tables use descriptive names and a SchemaVersion column;
  no `_vX` suffix is required for these new contracts.
- Query paths support exact latest-key warm-up and bounded history-window
  loading without `ALLOW FILTERING`.
- Historical data load manifests, gap reports, and checkpoints are durable
  operational records; they are not stored as indicator rows.
- Cache revision and warm status are not persisted as business history.
- Existing historical rows that lack required provenance may remain queryable
  for compatibility but cannot be marked warm for Regime Discovery.

## 13. Runtime activation and health

Add a server-owned `MarketDataAnalyticsSignalRuntime` that reacts to active
contract/feed lifecycle and owns observation plus required indicator
activation. Its configured Regime Discovery profile includes:

- RSI-14, ATR-14, ADX-14, conventional MACD, EMA10/20/50/200, BB10/20, and
  Market Structure for 15s, 1m, 5m, 15m, 1h, and 4h;
- RSI-13 on the same intraday observations for TDI compatibility;
- VIX spot and front/back VX term-structure inputs;
- `FuturesVwapSignalRealtimeActor` routing for trade-originated
  `FuturesMarketPriceUpdatedRealtimeEvent` messages; and
- Daily calculation/warm-up after the EOD barrier.

Health is Green only when the active contract's configured required keys are
warm, valid, compatible, and receiving observations. Yellow means warming or
optional degradation. Red means a required feed/signal is missing, invalid,
stale, incompatible, or the observation/cache runtime is unhealthy.

Regime Discovery live triggering remains disabled until this readiness check
passes. A later health degradation does not mutate an in-flight frozen
snapshot; it prevents a later capture from succeeding when requirements are
not met.

## 14. Observability

Structured logs and metrics include:

- contract, value date, timeframe, signal kind, configuration ID, observation
  ID, source sequence, and calculation version;
- VWAP stream epoch, trade ordinal, eligible/rejected trade count, cumulative
  volume, delivery-gap count, recovery count, and recovery duration;
- observation received/closed/dropped/duplicate/out-of-order counts;
- calculation count, duration, warm-up depth, validation failure, and
  projection failure by low-cardinality signal kind/timeframe;
- cache update/rejection, warm-up duration, cache revision, capture retry, and
  capture failure counts;
- current age and readiness by required signal family;
- contract-roll warm-up duration; and
- snapshot capture duration and issue codes.

Do not log full historical windows, opaque workflow payloads, raw tick bursts,
or unbounded contract-specific metric labels.

## 15. Compatibility and evolution

1. Existing MessagePack keys are never reordered or reused.
2. Existing RSI-13/TDI calculation identity and formulas remain unchanged.
3. RSI-14 has a distinct entity/configuration identity and cannot overwrite
   RSI-13.
4. Existing Start/Stop command/API surfaces remain during coordinator
   migration, with duplicate logical generation prevented.
5. Existing query consumers may temporarily use compatibility assemblers for
   old combined EOD shapes. New writes use the raw EOD schema and independent
   Analytics schemas. Regime Discovery accepts only configured supported
   schema/calculation versions.
6. Existing Market Outlook behavior remains independent. It may later consume
   the same cache but is not silently changed by this design.
7. V1.1 change-point signals and V2 probabilistic models add new signal kinds
   and calculation versions without changing snapshot interface semantics.

## 16. Testing requirements

### 16.1 Formula and contract tests

- Golden-vector tests for EMA10/20/50/200 and slopes; EMA-centered BB10/20,
  width baseline and position; ATR baseline/ratio; rolling prior highs/lows;
  breakout distance; session VWAP; front/back VX spread, ratio, percent and
  state; and optional realized volatility.
- Warm-up boundaries at one fewer, exact, and more than every required window.
- RSI-13 and RSI-14 identity/isolation tests.
- MessagePack compatibility and XML documentation for all public contracts.
- Old/new `FuturesMarketPriceUpdatedRealtimeEvent` payload compatibility,
  including safe defaults for newly appended normalized trade-lineage fields.
- Finite-number, denominator, configuration, schema, timestamp, and identity
  validation.

### 16.2 Observation and actor tests

- One source observation fans out to every configured bar-derived indicator
  with the same ObservationId; VWAP separately proves continuous trade
  lineage through stream epoch and trade ordinal.
- Session/timeframe boundary, maintenance break, weekend, early close, Daily,
  and contract-roll cases.
- Duplicate/out-of-order source sequences and process restart.
- Existing RSI-13 -> TDI cycle remains unchanged.
- No legacy timer and shared coordinator generate the same observation.
- EMA and BB outputs for the same timeframe carry the same ObservationId.
- VWAP resets at the exchange-session boundary, includes eligible trades, and
  handles zero volume, corrections, duplicates, and out-of-order observations.
- VWAP consumes only `UpdateSource.Trade`; quote events carrying a cached last
  trade cannot double-count that trade, and bid/ask sizes never affect VWAP.
- `LastSize` contributes as one normalized trade observation rather than a
  cumulative-volume replacement.
- Trade-ordinal gaps and unexpected stream-epoch changes invalidate the
  signal, exercise bounded recovery, and prevent an invalid cache update.
- Historical/private replay never reaches general live market-price routes.

### 16.3 Cache and snapshot tests

- Newer-wins ordering and immutable read behavior.
- Missing, not-warm, stale, future, invalid, schema mismatch, calculation
  mismatch, stream inactive, and contract mismatch.
- Concurrent writers with stable revision capture and bounded contention
  failure.
- Optional omission versus required failure.
- Scylla startup warming and rejection of legacy rows without provenance.
- Exact Daily/Weekly/Monthly Regime Discovery requirement maps, while every
  response contains only the requested target horizon plus supporting
  observation signals.

### 16.4 Historical data load and EOD migration tests

- Databento adapter contract tests for paging/batching, cancellation,
  checkpoint resume, duplicates, gaps, provider errors, and manifest content.
- At least one full year of deterministic Daily fixtures spanning ES and VX
  contract rolls, holidays, early closes, and an intentionally missing day.
- Root-continuation adjustment vectors prove the current contract segment is
  not adjusted and every historical roll is reproducible.
- The exact 200th Daily close produces the first EMA200; the 201st produces
  the prior/current pair required for the fully warm signal.
- Historical replay and equivalent live observation replay produce identical
  signal values, ObservationIds, and calculation versions.
- VX historical front/back selection changes atomically at rollover and never
  mixes legs from different roll states.
- Tick-exact VWAP and explicitly labeled bar-approximation fixtures cannot be
  confused by validation or snapshot capture.
- Raw EOD persistence contains no derived EMA, BB, market-direction,
  volatility, or VX interpretation, and the compatibility assembler obtains
  derived values only from exact-key Analytics caches.

### 16.5 Integration qualification

- Real TickAggregation -> observation -> indicator -> Scylla -> cache flow.
- Headless server activation without UI startup.
- Active contract rollover and new-contract readiness.
- Databento one-year historical data load -> normalized EOD -> calculators -> Scylla ->
  hot cache, including restart/resume and second-run idempotency.
- Raw Futures EOD insertion -> Daily observation -> EMA/BB/VWAP calculation ->
  enriched compatibility query, with no duplicate calculation.
- Databento trade -> enriched market-price event -> VWAP realtime actor ->
  generated event -> Scylla projection -> hot cache, including quote
  interleaving, one injected publication gap, and current-session recovery.
- Full Regime Discovery capture for Daily, Weekly, and Monthly target horizons.
- Restart/warm-up before Strategy Workflow live routing.
- MarketData Analytics BDD/unit/integration suites, Application Storage
  integration suites, Trade workflow integration suites, UI compatibility
  suites, and full solution build.

## 17. Design implementation sequence

| Gate | Design outcome |
| --- | --- |
| MDSI-0 | Baseline contracts, current behavior, and tests documented |
| MDSI-1 | Shared observation/bar and provenance contracts |
| MDSI-2 | Databento historical acquisition contracts, manifest/checkpoint workflow, and deterministic fixtures |
| MDSI-3 | Roll-aware FuturesSeriesId continuation and one-year Daily normalized backfill |
| MDSI-4 | Raw Futures EOD schema/actor/projector cutover and compatibility query assembler |
| MDSI-5 | Server-owned Futures Trade Session Bar Signal, actor-centric accumulation Model, and durable publication lifecycle |
| MDSI-6 | Existing RSI/ATR/ADX/MACD migration to common observations and provenance |
| MDSI-7 | RSI-14 plus preserved RSI-13/TDI path |
| MDSI-8 | EMA10/20/50/200 signal, deterministic warm-up, cache, projection, and queries |
| MDSI-9 | EMA-centered BB10/20 signal, cache, projection, and queries |
| MDSI-10 | ATR baseline/ratio volatility signal |
| MDSI-11 | Rolling range/high-low Market Structure signal composed with compatible BB/ATR values |
| MDSI-12 | VIX spot plus front/back VX term-structure signal and rollover lifecycle |
| MDSI-13 | Market-price trade-contract extension plus actor-owned session VWAP, private replay/recovery, cache, projection, and queries |
| MDSI-14 | Daily calculation barrier, historical/live parity, and warm-up lifecycle |
| MDSI-15 | Unified latest-value cache, revisioning, warm-up, and health |
| MDSI-16 | Regime Discovery snapshot interface and availability mapping |
| MDSI-17 | Scylla schema/query evolution and storage integration tests |
| MDSI-18 | Real-host Regime Discovery integration and system qualification |

MDSI-2 through MDSI-4 are prerequisites for qualifying Daily EMA200 and the
EOD responsibility split. Optional realized volatility can follow MDSI-13
without blocking the initial interface when the Regime Discovery parameter
set leaves it disabled.

## 18. Definition of done

1. All bar-derived signals originate from one coherent observation lineage,
   and session VWAP originates from one complete, gap-checked trade lineage.
2. RSI-13/TDI compatibility and RSI-14 Regime Discovery evidence coexist.
3. Every signal carries complete identity, provenance, schema, configuration,
   version, warm, and valid semantics.
4. EMA10/20/50/200, BB10/20, ATR baseline, Market Structure, session VWAP,
   and VIX/VX term-structure gaps are closed.
5. Futures EOD owns only raw session observations; derived values come from
   independently versioned Analytics calculations and hot caches.
6. A resumable, audited Databento historical data load supplies at least one complete
   calendar year and enough valid sessions to warm EMA200 deterministically.
7. Daily support is roll-aware and available without Weekly/Monthly indicator
   actors.
8. Headless server runtime, not UI startup, owns strategy signal availability.
9. Cache warm-up completes before live strategy routing.
10. Atomic snapshot capture returns all failures explicitly and never defaults
   required values.
11. Regime Discovery performs no direct tick, bar, Scylla, or Redis reads.
12. Existing consumers either migrate to raw-plus-signal contracts or use the
    bounded compatibility assembler during the development cutover.
13. All listed formula, actor, historical, cache, storage, workflow, and system
    tests pass.

## Appendix A - Required V1 matrix

| Signal | Intraday | Daily | Required/optional |
| --- | --- | --- | --- |
| Current price | TickAggregation live | Latest session close/context | Required |
| RSI-14 + slope | 15s, 1m, 5m, 15m, 1h, 4h | Yes | Required on configured primary timeframes |
| ADX-14/+DI/-DI | 15s, 1m, 5m, 15m, 1h, 4h | Yes | Required on configured primary timeframes |
| MACD-12/26/9 | 15s, 1m, 5m, 15m, 1h, 4h | Yes | Required on configured primary timeframes |
| EMA10/20/50/200 + slopes | 15s, 1m, 5m, 15m, 1h, 4h | Yes | Required on configured primary timeframes |
| ATR-14 + baseline/ratio | 15s, 1m, 5m, 15m, 1h, 4h | Yes | Required on configured primary timeframes |
| BB10/20 | 15s, 1m, 5m, 15m, 1h, 4h | Yes | Required when enabled by the parameter set |
| Range/high-low Market Structure | 15s, 1m, 5m, 15m, 1h, 4h | Yes | Required on configured primary timeframes |
| VIX spot + VX term structure | Current composite | Latest valid Daily composite | Required |
| Session VWAP | Current session | Completed session | Optional in initial V1; available to later scoring |
| Target ITI | Supplied workflow trigger | Supplied workflow trigger | Required |
| TDI from RSI-13 | Supported intraday only | No | Optional |
| Realized volatility | When enabled | When enabled | Optional in initial V1 |

## Appendix B - Consumer freshness defaults

Market Data Analytics evaluates the MaximumAge supplied in the snapshot
request. The initial Regime Discovery defaults are:

| Timeframe | Maximum age |
| --- | ---: |
| 15 seconds | 45 seconds |
| 1 minute | 3 minutes |
| 5 minutes | 15 minutes |
| 15 minutes | 45 minutes |
| 1 hour | 3 hours |
| 4 hours | 12 hours |
| Daily | 96 hours |

These are consumer configuration values, not hard-coded cache expiration
times. The cache retains the latest value; snapshot capture determines whether
that value is usable.
