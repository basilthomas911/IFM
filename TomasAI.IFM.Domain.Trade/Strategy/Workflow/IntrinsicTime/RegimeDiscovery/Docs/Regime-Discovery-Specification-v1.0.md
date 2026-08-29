Regime Discovery Specification

Design Specification v1.0

| Item \| Value \|

| --- \| --- \|

| Status \| Approved deterministic core plus RD-20 through RD-25 Decision V2 amendment; implemented \|

| Date \| 2026-08-29 \|

| Purpose \| Authoritative business/architectural contract for Regime
  Discovery \|

| Consumer \| Repository implementation, qualification tests, and later
  specification revisions \|

| Companion \| Intrinsic Time Strategy Workflow Implementation
  Specification v1.0 \|

| Target \| Deterministic RegimeDiscoveryResult schema V2 / .NET 10 actor application \|

This document defines WHAT Regime Discovery must do. Repository-specific
HOW belongs in `Regime-Discovery-Implementation-v1.0.md`. The approved
implementation sequence builds the deterministic contracts/models and actor
skeleton first through an injected snapshot boundary, then attaches
configuration and live market-signal infrastructure before qualification.

The 2026-08-28 FNC amendment is authoritative for actor topology: Regime
Discovery is a completed-only FunctionActor that directly returns a typed
Completed/Failed result. References below to a Regime CommandActor, private
terminal events, EventProjector publication, or Regime RealtimeActor describe
the superseded RD-19 implementation, not the current execution path.

## RD-20 through RD-25 authoritative decision amendment

This amendment defines the input-maximizing behavior required before Market
Condition consumes Regime Discovery. It supersedes any earlier wording that
treated the trigger, a reduced subset of specialist fields, or a downstream
trade preference as the decision itself.

1. Regime Discovery is evidence-first. It uses every relevant, available,
   qualified field already present in its immutable trigger and market-signal
   snapshot. It emits no trade-type or timeframe hints. Downstream hints belong
   to Market Condition and must not alter the Regime Discovery market decision.
2. The exact target-horizon `FuturesItiSignalGeneratedEvent` is authoritative
   for current price, ITI direction, band progress, reversal progress, source
   sequence/time, and front-VX value. Supporting cache observations remain
   authoritative for other horizons and indicator families.
3. TDI is optional signed trend-confirmation evidence on configured
   observation frames. When present, it receives 25 percent of the configured
   ITI component weight; ITI retains the full component weight when TDI is
   unavailable. Missing optional TDI is explicit quality evidence, never a
   fabricated numeric default.
4. Spot VIX and front VX are different signals. `VixLevel` is optional Daily
   spot-volatility evidence. `VxFrontLevel` is the target-horizon futures value
   carried by the ITI event. `VxFrontSecondRatio` is Daily term-structure
   evidence. Front VX must never be stored or described as spot VIX.
5. Market Structure derives breakout distance from current price, rolling
   high/low, and ATR. The supplied breakout signal is retained as corroborating
   evidence and affects confidence through agreement; it is not allowed to
   override contradictory direct price/range inputs.
6. The final public value is `RegimeDiscoveryDecision`, nested at
   `RegimeDiscoveryResult.Decision`. Result schema V2 preserves the existing
   MessagePack key for wire compatibility and adds the decision-driving trend
   phase/strength/agreement, volatility level/change/term structure, and market
   structure/breakout language. `Fusion` remains only an obsolete source alias.
7. Trend phase and volatility change affect risk-adjusted conviction:
   `Reversing`, `Exhausting`, and expanding volatility reduce conviction, and
   reversing/exhausting states add `Transition`. Extreme volatility continues
   to add `NoNewTrade`; disagreement and low confidence remain explicit.

The minimum reasonable decision-language qualification matrix is pairwise,
not a Cartesian explosion. It covers established bullish/bearish trends,
quiet range contraction, emerging direction, exhausting and reversing trends
under expansion, trend/structure conflict, breakout led direction, structural
transition, extreme-volatility blocking, low-confidence mixed markets, and
neutral compression. Boundary, missing-data, and three-horizon golden tests are
orthogonal to that matrix.

# 1. Purpose

Regime Discovery is the first calculation pipeline of the Intrinsic Time
Strategy Workflow. Each workflow calculates one target strategy horizon
(Daily, Weekly, or Monthly) plus the configured supporting observation
context for that horizon. It converts the latest valid market-signal state into
a deterministic, explainable regime result for that workflow horizon. It
does not select a trade, compose an order, approve portfolio risk, or
perform broker operations.

V1 is deterministic. ML.NET DetectIidChangePoint is deferred to V1.1;
HMMs, adaptive learning, and LLM/probabilistic classification are
deferred to V2 or later.

# 2. Workflow Boundary

``` text

FuturesItiSignalGeneratedEvent
 -> Strategy Workflow
 -> WorkflowStrategyStateUpdatedEvent(Started, RegimeDiscovery, ExpiresAtUtc)
 -> ExecuteRegimeDiscoveryPipelineCommand
 -> Regime Discovery Function actor (Core NATS request/reply)
 -> Trend + Volatility + Market Structure calculation models
 -> Fusion calculation model
 -> typed Completed OR Failed candidate
 -> completed candidate only: synchronous Function projector -> ScyllaDB
 -> completed candidate only: PostgreSQL completed-state append
 -> direct typed Function reply to Strategy Workflow Realtime
 -> CompleteRegimeDiscoveryCommand OR FailRegimeDiscoveryCommand
 -> Strategy Workflow Command actor
 -> exact-type receive map -> command-named CompleteRegimeDiscovery OR FailRegimeDiscovery extension
 -> WorkflowStrategyStateUpdatedEvent
```

The Strategy Workflow owns ordering, dispatch, authoritative workflow
state, immutable workflow view, deadline enforcement, and continuation. One
Regime Discovery Function actor owns completed-only event-sourced idempotency
state. Trend, Volatility, Market Structure, and Fusion are actor-owned
deterministic calculation models, not actors. Strategy Workflow Realtime
translates the direct Function reply into a typed Workflow Command message.
The detailed sequence is authoritative in section 6.4 of
`Regime-Discovery-Implementation-v1.0.md`.

# 3. Fixed V1 Decisions

1.  Realtime, single-attempt processing; no automatic business retries.

2.  Each Execute attempt returns exactly one Completed or Failed result. Only
    successful completion may be projected and saved in Function state.

3.  The Strategy Workflow is the authority for effective
    strategy/pipeline configuration.

4.  ExecuteRegimeDiscoveryPipelineCommand carries a complete immutable
    RegimeDiscoveryParameterSet.

5.  Configuration is frozen for the execution; later updates apply only
    to later workflows.

6.  Indicators are calculated upstream and read from a hot latest-value
    cache.

7.  Observation intervals are 15s, 1m, 5m, 15m, 1h, 4h, and Daily.

8.  Observation intervals are bucketed into Daily, Weekly, and Monthly
    strategy horizons.

9.  V1 specialist domains are Trend, Volatility, and Market Structure.

10. Fusion must succeed before Regime Discovery may complete.

11. No Regime terminal event is published. Only a completed candidate is
    synchronously projected, then saved, and returned directly; a failed
    result is returned directly without projection or Function persistence.

12. Private pipeline/specialist state never becomes Strategy Workflow
    state.

13. The typed RegimeDiscoveryResult is serialized into the existing
    opaque StrategyStageResultEnvelope.

14. The hard execution deadline and lazy workflow expiry are mandatory and
    never cause retry. Manual cancellation remains optional.

15. One workflow calculates only its own Daily, Weekly, or Monthly target
    horizon. Supporting timeframes are evidence for that result and never
    create additional workflow-horizon results.

16. PostgreSQL ConfigurationDb is authoritative for immutable, versioned
    strategy and pipeline parameter sets. Any future ScyllaDB configuration UI
    projection is non-authoritative and outside the Regime V1 implementation.

17. Trend, Volatility, Market Structure, and Fusion are sealed, actor-owned
    calculation models under the Regime Discovery `Model` folder. They have no
    actor identities, mailboxes, command contracts, Realtime events, or
    independently persisted state.

18. `ExecuteRegimeDiscoveryPipelineCommand` is dispatched by the Function
    actor's explicit `_receiveMap` to the asynchronous
    `ExecuteRegimeDiscoveryPipeline.ExecuteAsync` Function extension. The
    direct completed or failed reply is translated by Strategy Workflow
    Realtime into `CompleteRegimeDiscoveryCommand` or
    `FailRegimeDiscoveryCommand`. Each Workflow command is then handled by its
    own command-named extension file; the Workflow Command actor contains no
    command-type switch.

19. The three independent component calculations may use ordinary .NET thread
    pool work and be awaited together only if an approved benchmark shows a
    material advantage over sequential execution with identical results.

# 4. Scope

## 4.1 Included

-   Pipeline lifecycle and ownership boundary.

-   Versioned RegimeDiscoveryParameterSet and specialist
    sub-configuration.

-   Hot-cache signal acquisition and immutable point-in-time signal
    snapshots.

-   Observation-timeframe to strategy-horizon bucketing.

-   Trend, Volatility, and Market Structure calculation models, the Fusion
    model, and their actor-owned calculation coordinator.

-   Typed RegimeDiscoveryResult and deterministic summary/reason codes.

-   High-level state, events, queries, persistence, observability,
    validation, and testing expectations.

-   Mandatory hard execution deadline, lazy workflow expiry, and optional
    manual cancellation design hooks.

## 4.2 Excluded

-   Repository-specific file paths, concrete base classes, MessagePack
    key numbers, CQL names, and implementation gates.

-   Later strategy pipeline business logic and broker operations.

-   Raw indicator calculation from ticks/bars.

-   ML.NET DetectIidChangePoint (V1.1), HMMs, adaptive weighting, and
    LLM-controlled classification.

-   Automatic retry/restart/business replay after failure or timeout.

-   Final production timeout value and cancellation UI/authorization design.

# 5. Pipeline Lifecycle

``` text

WorkflowStrategyStateUpdatedEvent(Started, RegimeDiscovery, ExpiresAtUtc)
 -> ExecuteRegimeDiscoveryPipelineCommand
 -> Regime Discovery Function actor through Core NATS request/reply
 -> parse, validate, and load completed-only Function state
 -> asynchronous Function extension with fixed private deadline
 -> successful Fusion -> completed candidate
    -> synchronous Function projector -> ScyllaDB completed read model
    -> PostgreSQL completed Function-state append
    -> direct RegimeDiscoveryPipelineCompletedEvent reply
 OR required failure/hard timeout/exception -> direct RegimeDiscoveryPipelineFailedEvent reply
 -> CompleteRegimeDiscoveryCommand OR FailRegimeDiscoveryCommand
 -> Strategy Workflow Command actor
 -> WorkflowStrategyStateUpdatedEvent
```

V1 publishes no Regime Processing or terminal event. The completed and failed
contracts are typed Function replies addressed to `ActorType.Function`, not
Realtime event routes. Only a successfully projected completion is appended to
completed-only Function state. Failed, timed-out, malformed, projection-failed,
and persistence-failed attempts return failure and do not enter Function state.
A matching retry after completion returns the original completion without
recalculation, projection, or persistence. No failed or interrupted attempt is
automatically retried. A later eligible ITI event may create a new workflow
after the authoritative Execute handler expires any previous workflow whose
hard deadline has passed.

# 6. Configuration

Versioned strategy configuration is stored authoritatively in PostgreSQL
through ConfigurationDb. Configuration belongs to the Reference bounded
context under the following logical hierarchy:

``` text

Reference
 Configuration
  Trade
   StrategyWorkflow
    IntrinsicTimeStrategyWorkflowParameterSet
    Pipeline
     RegimeDiscoveryParameterSet
     MarketConditionParameterSet
     TradeSelectionParameterSet
     OrderCompositionParameterSet
     RiskManagementParameterSet
```

Configuration is partitioned first by owning domain. Future Fund, MarketData,
Securities, or other configuration families receive sibling domain sections
under Reference/Configuration rather than sharing the Trade hierarchy.

ConfigurationDb has one append-only table for each strategy-workflow or
pipeline parameter-set type. A row is identified by ParameterSetId and
Version and includes SchemaVersion, lifecycle status/effective timestamps,
the complete typed JSON payload, a deterministic payload hash, creation
metadata, and optional description. Published parameter identity, version,
schema, payload, and hash are immutable. Guarded lifecycle metadata may
publish or retire a row; changing a published payload always inserts a new
version.

The Strategy Workflow resolves the effective strategy configuration before
executing Regime Discovery, records the selected configuration identities and
immutable payload/hash in its authoritative PostgreSQL event stream, and
supplies the complete typed RegimeDiscoveryParameterSet with the Execute
command. State reconstruction never re-resolves historical configuration or
redispatches work.

``` text

RegimeDiscoveryParameterSet
 ParameterSetId / Version
 StrategyParameterSetId / Version
 TargetHorizon
 TargetHorizonConfiguration
 TrendConfiguration
 VolatilityConfiguration
 MarketStructureConfiguration
 FusionConfiguration
 SignalFreshnessConfiguration
 DataQualityConfiguration
```

The parameter set is immutable for the execution. Internal actors
receive relevant immutable sub-configuration and do not query changing
workflow configuration. ExecuteRegimeDiscoveryPipelineCommand must be
extended append-only with this parameter set; exact repository changes
belong in the implementation specification.

# 7. Hot Signal Cache

All deterministic market indicators are produced upstream. At pipeline
start, Regime Discovery reads the latest valid/warm values and freezes
them into one immutable RegimeDiscoverySignalSnapshot for the target horizon
and its supporting context. The same frozen snapshot is shared by all
specialist calculations.

| Interval \| V1 use \|

| --- \| --- \|

| 15s \| Very fast context; ITI/execution and optional confirmation \|

| 1m \| Fast confirmation/context \|

| 5m \| Daily fast context \|

| 15m \| Daily/Weekly context \|

| 1h \| Daily/Weekly/Monthly context \|

| 4h \| Weekly/Monthly context \|

| Daily \| Slow Weekly/Monthly structural context \|

-   Cached signals include instrument, signal type, timeframe, value(s),
    MarketDataAsOfUtc, CalculatedAtUtc, sequence/version, IsWarm,
    IsValid, and calculation version.

-   Required missing/stale/not-warm/invalid signals are never silently
    defaulted.

-   Optional unavailable signals may reduce confidence only when
    configuration permits.

-   Freshness is relative to the observation timeframe.

# 8. Strategy-Horizon Bucketing

Observation timeframes are not trading horizons. The workflow horizon selects
exactly one configured decision context from the mapping below.

| Horizon \| Primary \| Fast confirmation \| Slow/support \|

| --- \| --- \| --- \| --- \|

| Daily \| 15m, 1h \| 5m (1m optional) \| 4h \|

| Weekly \| 1h, 4h \| 15m \| Daily \|

| Monthly \| 4h, Daily \| 1h \| 15m optional \|

These are V1 defaults only. Membership, role, weight, required/optional
status, and freshness tolerance are configuration-driven.
Cross-timeframe agreement/disagreement is retained as evidence.

Default observation-timeframe weights are:

| Target horizon \| Observation weights \|

| --- \| --- \|

| Daily \| 15m 0.45; 1h 0.35; 5m 0.10; 4h 0.10; 1m disabled \|

| Weekly \| 1h 0.40; 4h 0.40; 15m 0.10; Daily 0.10 \|

| Monthly \| 4h 0.45; Daily 0.40; 1h 0.15; 15m disabled \|

Primary observation timeframes are required. Supporting timeframes are
optional by default. Enabling an optional timeframe gives it a positive
configured weight; unavailable optional evidence is not scored as zero and
reduces confidence through coverage.

## 8.1 Required-signal and freshness rules

The target-horizon ITI trigger is always required. The following V1 signal
sets are required unless a later approved parameter-set version explicitly
changes a required item:

| Specialist \| Required evidence \| Optional evidence \|

| --- \| --- \| --- \|

| Trend \| current price, EMA20/50/200 values and slopes, RSI(14) and slope, ADX(14)/+DI/-DI, conventional MACD(12,26,9), and the target-horizon ITI trigger \| TDI and enabled supporting timeframes \|

| Volatility \| ATR(14), ATR baseline ratio, current VIX level, and front/second VIX-futures term-structure ratio \| realized-volatility percentile and enabled supporting timeframes \|

| Market Structure \| Bollinger(20,2) width/position and width baseline, EMA20/centerline interaction, ATR-normalized range, rolling 20-observation high/low, and the target-horizon ITI trigger \| enabled supporting timeframes and additional longer-window context \|

All required evidence for every primary observation timeframe must be present,
warm, valid, schema-compatible, configuration-compatible, and fresh. Failure
of any required check fails Regime Discovery. Optional evidence that fails a
check is omitted and creates a data-quality reason code.

Freshness age is `SnapshotCapturedAtUtc - MarketDataAsOfUtc`. A timestamp more
than the configured FutureClockSkewTolerance (default five seconds) after the
capture time is invalid. Default maximum ages are 45 seconds for 15s, three
minutes for 1m, 15 minutes for 5m, 45 minutes for 15m, three hours for 1h,
12 hours for 4h, and 96 hours for Daily. All values are parameter-set fields.
The 96-hour Daily default deliberately spans a normal weekend; a future
exchange-calendar-aware rule may replace it through a new parameter-set
version.

For a valid signal, its freshness factor is
`clamp(1 - FreshnessAge / MaximumAge, 0, 1)`. The snapshot records the minimum,
weighted mean, and maximum age across included signals. No missing or invalid
value is replaced with a numeric default.

## 8.2 Common deterministic score and confidence rules

All signed directional component scores use `[-1, 1]`; zero is neutral.
Unsigned severity scores use `[0, 1]`. `clamp` limits a value to the stated
range. Enabled configured weights must be non-negative and have a positive
sum.

For available evidence values `x[i]` with configured weights `w[i]`:

``` text

Score      = sum(w[i] * x[i]) / sum(w[i])
Coverage   = available configured weight / total enabled configured weight
Freshness  = sum(w[i] * freshnessFactor[i]) / sum(w[i])
Agreement  = 1 - (sum(w[i] * abs(x[i] - Score)) / (2 * sum(w[i])))
Confidence = clamp(Coverage *
                   (0.45 * Agreement + 0.35 * Freshness + 0.20), 0, 1)
```

The constant 0.20 represents the already-enforced warm/valid/schema/config
gate; it is never awarded to invalid evidence. Optional missing evidence
reduces Coverage. Conflicting evidence reduces Agreement. Near-stale evidence
reduces Freshness. Exact decimal calculations are rounded to six decimal
places using midpoint-to-even before persistence and comparison.

Confidence bands are Low below 0.35, Moderate from 0.35 to below 0.60, High
from 0.60 to below 0.80, and VeryHigh at or above 0.80. Low confidence may
still produce Completed when all required evidence is valid, but Fusion adds
an explicit low-confidence restriction.

# 9. Trend Regime Calculation Model

Determines directional trend, strength, phase, normalized score,
confidence, and cross-timeframe agreement for the workflow's target horizon.

-   Signals: price vs EMA20/50/200; EMA alignment/separation/slopes;
    RSI(14) and slope; ADX(14), +DI, -DI; centerline context; relevant
    Intrinsic Time direction/context.

-   Output: direction, strength, phase, score, confidence, component
    evidence, and reason codes.

Trend component scores and default weights are:

-   EMA alignment (0.25): the mean of `sign(Price-EMA20)`,
    `sign(EMA20-EMA50)`, and `sign(EMA50-EMA200)`.

-   EMA slopes (0.15): the mean of the three EMA slope values normalized by
    their configured positive ATR-based slope scales.

-   RSI (0.15): `0.70 * clamp((RSI-50)/20,-1,1) + 0.30 *
    clamp(RSISlope/ConfiguredRsiSlopeScale,-1,1)`.

-   ADX (0.20): `sign(PlusDI - MinusDI) *
    clamp((ADX-15)/25,0,1)`.

-   MACD (0.15): the MACD histogram divided by ATR and normalized by the
    configured positive MACD/ATR scale.

-   ITI (0.10): ITI direction (`+1` up, `-1` down) multiplied by
    `clamp(BandLevel,0,1) * (1-clamp(ReversalLevel,0,1))`.

Component scores are combined per observation timeframe, then timeframe
scores are combined using the target-horizon weights. Direction is Up at or
above 0.20, Down at or below -0.20, and Neutral otherwise. Directional
strength is Weak for absolute score 0.20 to below 0.40, Moderate for 0.40 to
below 0.65, Strong for 0.65 to below 0.85, and Extreme at or above 0.85.

Phase precedence is Reversing when ITI ReversalLevel is at least 0.50 and two
or more non-ITI momentum components oppose the ITI direction; Exhausting when
ReversalLevel is at least 0.25 or both RSI and MACD oppose an otherwise
directional score; Emerging when directional but ITI BandLevel is below 1.0
or ADX is below 20; Established for any remaining directional result; and
RangeBound for Neutral.

# 10. Volatility Regime Calculation Model

Classifies volatility level, expansion/contraction, VIX term structure,
score, confidence, and risk/trade-restriction evidence.

-   Signals: VIX, VIX futures term structure, ATR, ATR ratio, realized
    volatility when available, expansion/contraction evidence.

| VIX baseline \| Regime \|

| --- \| --- \|

| \<12 \| Low \|

| 12 to \<20 \| Normal \|

| 20 to \<30 \| High \|

| \>=30 \| Extreme \|

Thresholds are configuration-driven. Extreme volatility creates explicit
no-new-trade evidence for Fusion; Trade Selection remains a later
pipeline responsibility.

The VIX-level score maps the configured Low/Normal/High/Extreme boundaries to
0.00/0.25/0.50/0.75 and increases linearly to 1.00 at the configured maximum
(default VIX 50). The ATR-ratio score is piecewise linear through
`(0.75,0.00)`, `(1.00,0.40)`, `(1.50,0.75)`, and `(2.00,1.00)`. The
front/second VIX-futures ratio score is piecewise linear through
`(0.95,0.10)`, `(1.00,0.30)`, `(1.05,0.60)`, and `(1.10,0.90)`, clamped to
`[0,1]`. Realized volatility uses its upstream percentile directly.

Default volatility weights are volatility level 0.35, ATR ratio 0.35, term structure
0.20, and optional realized volatility 0.10. The common unsigned scoring and
confidence formulas apply. Volatility is Low below 0.25, Normal from 0.25 to
below 0.50, High from 0.50 to below 0.75, and Extreme at or above 0.75.
The level component uses qualified Daily spot VIX when available and otherwise
uses required target-horizon front VX; evidence states which source was used.
Expansion means the composite score increased by at least 0.10 from its prior
warm observation; contraction means it decreased by at least 0.10; otherwise
it is stable. NoNewTrade evidence is set when VIX is at or above its Extreme
boundary, the composite score is at least 0.75, or the configured severe
backwardation ratio is met (default 1.05).

# 11. Market Structure Regime Calculation Model

Classifies how price is behaving independent of directional bias:
trending, ranging, compressing, expanding, breaking out, transitioning,
or unknown.

-   Signals: Bollinger width/position, EMA20/centerline interaction,
    ATR-normalized range, recent highs/lows, breakout distance, and
    relevant Intrinsic Time
    direction-change/extreme/reversal/persistence behavior.

-   Output: structure classification, optional direction, breakout
    state, score, confidence, component evidence, and reason codes.

Default Market Structure evidence weights are Bollinger width/position 0.25,
EMA20/centerline interaction 0.20, ATR/range state 0.20, rolling high/low and
breakout distance 0.20, and ITI persistence/reversal 0.15. The following
classification precedence is deterministic:

1.  BreakingOut when price is at least 0.50 ATR above the rolling high or
    below the rolling low; direction is the breakout sign.

2.  Compressing when Bollinger width is at most 0.75 of its baseline and ATR
    ratio is at most 0.85.

3.  Expanding when Bollinger width is at least 1.25 of baseline or ATR ratio
    is at least 1.25.

4.  Trending when absolute EMA/ITI organization score is at least 0.50 and
    ITI persistence `clamp(BandLevel,0,1) *
    (1-clamp(ReversalLevel,0,1))` is at least 0.50.

5.  Ranging when absolute organization score is below 0.25, no breakout is
    present, and Bollinger width ratio is between 0.75 and 1.25 inclusive.

6.  Transitioning for every other complete valid result.

Market Structure score is signed breakout distance normalized at two ATR for
BreakingOut, the signed EMA/ITI organization score for Trending, the
organization direction for directional Expanding, and zero for Compressing,
Ranging, or non-directional Transitioning. Unknown is reserved for an
incomplete diagnostic result and cannot produce pipeline Completed.

The breakout distance is derived from direct current price, rolling high/low,
and positive ATR. The upstream `BreakoutDistanceAtr` value remains separate.
Absolute agreement between derived and supplied values multiplies structure
confidence by `0.75 + 0.25 * agreement`; contradiction can reduce confidence
but cannot replace direct data.

# 12. Market Regime Fusion Model

Combines complete Trend, Volatility, and Market Structure results into
the canonical market regime for the workflow's target strategy horizon.

-   Validate specialist completeness, freshness, schema/configuration
    compatibility.

-   Combine specialist scores using deterministic configuration-driven
    rules.

-   Calculate confidence and cross-domain alignment/conflict.

-   Apply deterministic restrictions such as Extreme volatility.

-   Preserve specialist results and structured evidence.

-   Generate final reason codes and deterministic summary inputs.

-   Do not recalculate raw indicators.

Successful Fusion is required before Regime Discovery may publish
Completed.

Fusion calculates directional score as `0.65 * TrendScore + 0.35 *
MarketStructureScore`; Volatility never changes its sign. Direction uses the
Trend thresholds. Risk-adjusted conviction is
`abs(DirectionalScore) * (1 - 0.50 * VolatilityScore)`, then multiplied by
trend-phase conviction (`Reversing=0.50`, `Exhausting=0.75`, `Emerging=0.90`,
otherwise `1.00`) and by `0.85` when volatility is expanding.

Base fusion confidence is `0.40 * TrendConfidence + 0.30 *
VolatilityConfidence + 0.30 * MarketStructureConfidence`. Trend/Structure
alignment is `1 - abs(TrendScore-MarketStructureScore)/2`. Final fusion
confidence is `clamp(BaseConfidence * (0.75 + 0.25 * Alignment),0,1)` and is
then multiplied by `0.85` for Reversing or `0.92` for Exhausting.

Fusion emits deterministic restrictions: NoNewTrade for specialist Extreme
volatility evidence; DirectionConflict when Trend and Market Structure are
both directional and have opposite signs; LowConfidence below 0.55; and
Transition when Market Structure is Transitioning or Trend is Reversing or
Exhausting. Restrictions are evidence
for later pipelines and do not themselves fail Regime Discovery.

Overall quality is High when confidence is at least 0.80 with no missing
optional evidence or restrictions, Acceptable when confidence is at least
0.60 with no data-quality fault, Degraded when required evidence is valid but
optional evidence is absent, a conflict/restriction exists, or confidence is
0.35 to below 0.60, and Low below 0.35. Invalid required evidence produces
Failed instead of a quality value.

## 12.1 Reason-code rules

Reason codes are stable machine-readable strings, not free text. Their format
is `RD.<AREA>.<REASON>`, with an optional timeframe suffix in structured
evidence rather than in the code. V1 reserves these families:

-   `RD.CONFIG.INVALID`, `RD.CONFIG.NOT_FOUND`, `RD.CONFIG.VERSION_MISMATCH`,
    `RD.CONFIG.HASH_MISMATCH`.

-   `RD.DATA.REQUIRED_MISSING`, `RD.DATA.OPTIONAL_MISSING`, `RD.DATA.STALE`,
    `RD.DATA.NOT_WARM`, `RD.DATA.INVALID`, `RD.DATA.FUTURE_TIMESTAMP`,
    `RD.DATA.SCHEMA_UNSUPPORTED`, `RD.DATA.CALCULATION_VERSION_MISMATCH`.

-   `RD.TREND.UP`, `RD.TREND.DOWN`, `RD.TREND.NEUTRAL`,
    `RD.TREND.TIMEFRAME_CONFLICT`, `RD.TREND.MOMENTUM_DIVERGENCE`,
    `RD.TREND.REVERSING`.

-   `RD.VOL.LOW`, `RD.VOL.NORMAL`, `RD.VOL.HIGH`, `RD.VOL.EXTREME`,
    `RD.VOL.EXPANDING`, `RD.VOL.CONTRACTING`, `RD.VOL.CONTANGO`,
    `RD.VOL.BACKWARDATION`.

-   `RD.STRUCT.TRENDING`, `RD.STRUCT.RANGING`, `RD.STRUCT.COMPRESSING`,
    `RD.STRUCT.EXPANDING`, `RD.STRUCT.BREAKOUT_UP`,
    `RD.STRUCT.BREAKOUT_DOWN`, `RD.STRUCT.TRANSITIONING`.

-   `RD.FUSION.ALIGNED`, `RD.FUSION.DIRECTION_CONFLICT`,
    `RD.FUSION.LOW_CONFIDENCE`, `RD.FUSION.NO_NEW_TRADE`,
    `RD.FUSION.TRANSITION`.

-   `RD.PIPELINE.SPECIALIST_FAILED`, `RD.PIPELINE.FUSION_FAILED`,
    `RD.PIPELINE.CONSISTENCY_FAULT`.

Every reason has a configured severity (Information, Warning, Restriction, or
Failure). Failure codes cannot appear in Completed. Output removes duplicates
and orders codes by area ordinal, reason ordinal, timeframe ordinal, and
signal identity so state reconstruction and summary text are byte-for-byte
deterministic.

# 13. RegimeDiscoveryResult schema V2

``` text

RegimeDiscoveryResult
 SchemaVersion
 StrategyParameterSetId / Version
 RegimeDiscoveryParameterSetId / Version
 SignalSnapshotId
 Trigger / Instrument identity
 MarketDataAsOfUtc / ProducedAtUtc
 TargetHorizon
 Trend
 Volatility
 MarketStructure
 Decision: direction/score/confidence/conviction/restrictions
           + trend phase/strength/timeframe agreement
           + volatility level/change/term structure
           + structure classification/breakout
 SupportingObservationEvidence[]
 OverallQuality / OverallConfidence
 ReasonCodes[]
 SummaryText
```

Structured fields are authoritative. SummaryText is a deterministic
human-readable explanation derived from those fields for operations,
paper-trading review, diagnostics, and later advisory use. It is never
the only record of why a regime was produced.

`Decision` occupies the same MessagePack key previously used by the smaller
fusion object. Its appended fields preserve old positional payload
compatibility. `Fusion` is a non-serialized obsolete source alias only; new
producers, consumers, tests, and documentation use `Decision`. The workflow
result envelope advertises `RegimeDiscoveryResult.CurrentSchemaVersion`; it is
therefore schema V2 whenever it contains a schema-V2 Regime Discovery result.
The projected Regime Discovery read model stores that same envelope version.

# 14. Function Results and Workflow Handoff

Any required internal model may fail its calculation. The Regime Discovery
Function actor catches validation, calculation, timeout, projection, and
persistence failures and returns a typed failed result when a reply remains
possible. The Strategy Workflow Realtime actor is the caller and translates the
direct Function result into a Strategy Workflow Complete or Fail command.

``` text

Internal failure/timeout -> Function failed reply -> FailRegimeDiscoveryCommand
Final Decision success -> completed candidate -> Scylla projection -> PostgreSQL completion -> Function completed reply -> CompleteRegimeDiscoveryCommand
Projection/persistence failure -> Function failed reply -> FailRegimeDiscoveryCommand
```

-   Failure examples: invalid parameter set; required signal
    unavailable/stale/not warm; incompatible versions; specialist
    calculation failure; fusion validation failure; consistency fault.

-   Completed means calculation completed successfully; it does not mean
    the Strategy Workflow must continue.

-   Failed means a valid required result could not be produced.

-   Timeout is represented as a direct Failed Function result with stable
    timeout reason metadata and permanently outranks any later worker result.

-   A Function reply is not authoritative workflow state. Only a committed
    `WorkflowStrategyStateUpdatedEvent` proves that Workflow Complete or Fail
    was accepted.

# 15. State Ownership and Persistence

-   The Regime Discovery Function actor is the only Regime Discovery actor that
    owns event-sourced execution state. That state contains completed results
    only and exists solely for idempotency.

-   There is no Regime Discovery Command actor, Event actor, Realtime actor,
    processing event, private terminal-event family, or terminal publication
    route.

-   Component and Fusion models are stateless with respect to actor
    persistence. They receive immutable input and return immutable results.

-   Completed-only Function state is event-sourced in PostgreSQL. Failed
    Function results are never persisted there.

-   ScyllaDB contains non-authoritative operational/query projections.

-   The synchronous Function projector writes only a completed read model. It
    owns no mailbox, queue, checkpoint, retry, publication, or replay.

-   Projection succeeds before the completed Function event is appended. ScyllaDB
    and PostgreSQL do not share an ACID transaction; a PostgreSQL failure after
    projection can leave an observational orphan, but it cannot advance the
    Strategy Workflow.

-   Strategy Workflow owns one authoritative
    `WorkflowStrategyStateUpdatedEvent` snapshot containing machine state,
    immutable accumulated pipeline view, revision, stage, and hard deadline.

-   PostgreSQL Function-state reconstruction supports completed-result
    idempotency, audit, testing, and diagnosis; it never republishes,
    redispatches, or resumes calculation work.

-   Unbounded history is not retained in live actor state.

# 16. Queries

-   Current Regime Discovery pipeline state by workflow/execution.

-   Current/last Trend, Volatility, Market Structure, and Fusion result from
    the final Regime Discovery projection.

-   RegimeDiscoveryResult by workflow/result identity.

-   Component evidence and reason codes.

-   Parameter-set identity/version used.

-   Signal snapshot identity and data-quality summary.

-   Pipeline timeline/history and failure details.

-   Operational health/current processing status.

Queries are read-only and diagnostic. The Strategy Workflow does not query
component models or Regime Discovery to reconstruct a continuation result;
the direct completed Function reply carries the full opaque result envelope.

# 17. Mandatory Hard Timeout and Lazy Workflow Expiry

Every Regime Discovery execution has one immutable `ExpiresAtUtc`, derived from
its start time and fixed maximum execution duration. Timeout is a terminal
safety boundary, not a retry mechanism.

-   `ExecuteRegimeDiscoveryPipelineCommand` carries `ExpiresAtUtc`.

-   The Function execution extension bounds snapshot capture and calculation by
    the remaining duration. At `now >= ExpiresAtUtc`, it may return only a
    Failed result with timeout metadata; a late worker cannot reach projection
    or persistence.

-   The caller allows a five-second reply-only transport grace after the
    calculation deadline. This does not extend calculation time.

-   The Workflow Command actor independently checks its persisted deadline
    before accepting `CompleteRegimeDiscoveryCommand`. It does not trust a
    producer-supplied completion timestamp to extend the deadline.

-   If the Function reply or resulting Workflow Fail command is lost, the next
    Execute workflow command loads the authoritative workflow snapshot. An unexpired active
    workflow is busy. An expired workflow is terminalized as TimedOut and the
    new workflow is started in the same PostgreSQL event batch.

-   Any later Function result for an expired or superseded workflow identity,
    revision, or stage is stale and cannot advance the immutable workflow view.

-   The exact V1 duration is a runtime option while the common versioned
    execution-policy configuration is deferred.

This deadline pattern is the candidate for later pipeline specifications after
Regime Discovery proves it end to end.

# 18. Optional Manual Cancellation - Deferred Implementation Detail

An authorized user may eventually cancel an active Strategy Workflow
while a pipeline is processing. Cancellation is terminal and never means
skip the stage and continue.

-   Cancellation may attempt cooperative cancellation of in-flight work
    where supported.

-   Late terminal events after cancellation are stale.

-   No automatic retry follows cancellation.

-   Exact command shape, authorization, propagation, and UI interaction
    are optional for the initial Regime Discovery implementation.

# 19. Observability

Regime Discovery is a high-value V1 business-intelligence path and
should provide detailed observability without logging raw high-volume
market data.

-   Structured fields: WorkflowId, WorkflowRevision, stage, instrument,
    horizon, parameter-set IDs/versions, signal snapshot ID, result ID,
    correlation/causation IDs, outcome, reason codes.

-   Metrics: requested/completed/failed/timed-out counts, duration, signal-cache
    acquisition latency, stale/missing signal counts, component/fusion
    duration, selected sequential/parallel execution mode, and
    low-cardinality result quality/confidence metrics.

-   Tracing may use the system-wide tracing architecture when finalized;
    no pipeline-specific TraceId architecture is invented here.

-   Do not log full opaque payload bytes or excessive raw signal
    payloads.

The observation view must identify workflows still Started beyond their hard
deadline as `ExpiredWithoutTerminalOutcome` until a later Start atomically
terminalizes them. It should also make a projected completion without matching
Function state or Workflow completion visible as an observational consistency
issue. Observation is read-only and never resumes or redispatches work.

# 20. Validation and Idempotency

-   Validate workflow ID, input revision, trigger identity,
    parameter-set identity/version, required horizon configuration, and
    signal snapshot integrity.

-   A matching Execute received after completed Function state exists returns
    the original completed event without another calculation, projection, or
    save. A previous failed attempt has no Function state and may be attempted
    again while the Workflow command remains current and unexpired.

-   Workflow Complete/Fail command IDs are deterministic. Duplicate logical
    terminal commands are idempotent at the Workflow boundary.

-   Conflicting duplicate result identity/payload is a consistency
    fault.

-   Unsupported signal/result schemas are never silently accepted.

-   A workflow completion is accepted only when status, WorkflowId, input
    revision, current stage, and hard deadline all match authoritative Workflow
    Command state.

# 21. Testing Requirements

-   Parameter-set serialization, immutability, and version selection.

-   Hot-cache acquisition and missing/stale/not-warm/invalid handling.

-   Daily/Weekly/Monthly horizon bucketing and configuration overrides.

-   Trend classification boundary cases and cross-timeframe
    disagreement.

-   Volatility thresholds, term structure, expansion/contraction, and
    Extreme restriction evidence.

-   Market Structure
    trending/ranging/compression/expansion/breakout/transition cases.

-   Fusion completeness, conflict, confidence, restrictions, and
    deterministic summary generation.

-   Execute -\> completed Function reply, Execute -\> failed Function reply,
    hard timeout -\> failed Function reply, and exception -\> failed Function
    reply paths.

-   Function `_parseMap`, `_validationMap`, and exact-type `_receiveMap`
    dispatch to the asynchronous Execute extension and propagation of its typed
    `FunctionResult<CompletedEvent,FailedEvent>`.

-   Sequential and thread-pool-parallel component execution produce
    byte-for-byte equivalent normalized results. Benchmark tests cover typical
    and maximum snapshots across Daily, Weekly, and Monthly workflows and
    justify the selected execution mode.

-   Function lifecycle ordering: completed candidate, synchronous ScyllaDB
    projection, PostgreSQL completed-state append, then direct completed reply.
    Failed results never reach projection or Function persistence.

-   Strategy Workflow Realtime translation of direct Function results to
    Complete/Fail Workflow commands, timeout precedence at the exact boundary,
    late-result rejection, immutable-view accumulation, and atomic
    expired-workflow replacement on the next Execute.

-   Matching completed retry, conflicting completed input, and deterministic
    Workflow terminal-command behavior.

-   PostgreSQL state reconstruction and idempotent Scylla projection writes;
    neither may redispatch workflow work.

-   Integration with the real Intrinsic Time Strategy Workflow boundary.

-   Mandatory hard-timeout/lazy-expiry tests and optional manual-cancel tests;
    automatic retry tests are not applicable because retries are prohibited.

# 22. Version Evolution

| Version \| Scope \|

| --- \| --- \|

| V1 \| Deterministic Trend, Volatility, Market Structure, Fusion;
  hot-cache signals; one target-horizon result per workflow. \|

| V1.1 \| Optional ML.NET DetectIidChangePoint statistical change
  evidence, initially observational and separately validated. \|

| V2+ \| HMM specialist models, richer fusion, additional regime
  domains, adaptive/statistical enhancements, optional LLM advisory
  context. \|

Future models must preserve the stable pipeline boundary so the Strategy
Workflow does not need to know how Regime Discovery is implemented
internally.

# 23. Codex Implementation-Specification Instructions

Codex must treat this document as the authoritative domain/design
contract and inspect the current repository before proposing
implementation details.

-   Reuse the generic FunctionActor lifecycle and the command-ingress
    `_parseMap`, `_validationMap`, exact-type `_receiveMap`, validation,
    MessagePack, logging, and testing conventions. The request remains an
    `ICommand<TEntityId>` even though a Function actor handles it.

-   Do not create a Regime Discovery Command, Event, or Realtime actor. Strategy
    Workflow Realtime calls the Function and translates its direct result. Do
    not create private component actors. Put actor-centric computation in sealed
    types under `Model`.

-   Implement the approved single `WorkflowStrategyStateUpdatedEvent` snapshot
    contract with separate machine state and immutable accumulated workflow
    view. Do not add alternative workflow authorities.

-   Identify the minimum append-only workflow changes needed to carry
    RegimeDiscoveryParameterSet and expose configuration
    identity/version.

-   Produce a repository-specific file-by-file implementation plan and
    implementation gates before production code generation.

-   Do not infer additional regime algorithms, thresholds, retries,
    HMMs, ML.NET logic, or continuation rules not approved here.

-   Treat the hard execution deadline, lazy expiry, and late-result fencing as
    mandatory. Manual cancellation remains optional.

-   Preserve the rule that only a completed candidate may be synchronously
    projected and saved. Neither completed nor failed Regime results are
    published; they are returned directly to Strategy Workflow Realtime. V1 has
    no Regime Discovery Processing event or message replay path.

# 24. Definition of Done for this Design

1.  Pipeline boundary and ownership are unambiguous.

2.  Configuration authority and immutable parameter delivery are
    defined.

3.  Hot-cache acquisition and seven observation intervals are defined.

4.  Daily/Weekly/Monthly aggregation is configuration-driven.

5.  Trend, Volatility, Market Structure, and Fusion responsibilities are
    defined.

6.  The typed RegimeDiscoveryResult contains structured evidence and
    deterministic summary.

7.  Failure is terminal and no automatic retry exists.

8.  Hard timeout, lazy expiry, and late-result fencing are mandatory; manual
    cancellation is documented but not mandatory for initial implementation.

9.  State, persistence, queries, observability, validation, and testing
    expectations are sufficient for Codex to create the
    repository-specific Implementation Specification.

10. V1.1 and V2 extensions are explicitly separated from V1.

11. The completed-only Function lifecycle, direct Workflow handoff,
    projection-before-save ordering, and cross-database atomicity boundary are
    explicit.

# Appendix A - Terminology

| Term \| Meaning \|

| --- \| --- \|

| Observation timeframe \| Upstream indicator interval: 15s, 1m, 5m,
  15m, 1h, 4h, Daily. \|

| Strategy horizon \| Daily, Weekly, or Monthly decision horizon. \|

| Hot signal cache \| Latest-value store of precomputed market-signal
  snapshots. \|

| Specialist regime \| Trend, Volatility, or Market Structure
  classification. \|

| Fusion \| Deterministic combination of specialist results into a
  canonical market regime. \|

| Pipeline boundary \| Regime Discovery lifecycle/state owner
  communicating with the Strategy Workflow. \|

| Completed \| Complete valid Regime Discovery result produced;
  continuation remains a workflow decision. \|

| Failed \| Valid required result could not be produced. \|

| Timeout \| Mandatory fixed execution boundary for incomplete
  realtime processing; never a retry. \|

| Manual cancellation \| Optional operator-requested terminal stop;
  never a retry or skip-to-next-stage instruction. \|

# Appendix B - Source Alignment Note

This design is intentionally aligned with the supplied Intrinsic Time Strategy
Workflow Implementation Specification v1.0: the Regime Discovery Function
actor receives immutable workflow context and the original ITI trigger through
a command-shaped request, owns completed-only idempotency state, never decides
workflow continuation, and returns one typed completed or failed result
directly to Strategy Workflow Realtime. Its internal component models are
implementation details and own no actor state or message contracts. The
implementation must preserve those established boundaries.
## Non-authoritative Decision reference query amendment

`GetRegimeDiscoveryDecisionReferenceQuery` is a Core NATS request/reply query owned by
`RegimeDiscoveryPipelineQuery`. It returns the stable representative pairwise Decision DTO rows generated in memory
through `MarketRegimeFusionModel`. The query performs no storage access, live specialist request, event publication,
or workflow continuation. Its output is a design and verification reference only, never a whitelist or calculation
input. See `../../Docs/Pipeline-Decision-Reference-Queries-v1.0.md` for the shared PDR contract.
