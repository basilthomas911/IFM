# Market Condition Detailed Specification v1.0

> **Strategy catalog direction (2026-09-06):** Reusable strategy-family/structure/variant definitions are planned in ConfigurationDb and are downstream TradeSelection concerns. Current MarketCondition remains market-only for the single ITI-triggering Daily, Weekly or Monthly horizon. Historical family hints and family-scoped rules in superseded designs do not return to the assessment path. Recorded gate evidence is unchanged and does not qualify the new catalog. TradeSelection implementation is on hold. See [ConfigurationDb strategy catalog design](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

> Historical design only. The earlier Market Condition executable implementation was removed on 2026-09-06. See [assessment-only design v0.4](MarketCondition-High-Level-Design-v0.4.md) for current behavior.


> **Superseded for new assessment-mode workflows on 2026-09-05 by
> [Specification v2.0](MarketCondition-Specification-v2.0.md).**
> This specification records legacy single-horizon Tradeable/NotTradeable behavior,
> including family hints. It remains relevant to legacy code and persisted schemas;
> it does not define the revised one-assessment-per-ITI-timeframe model. The new
> specification is planned, not implemented or qualified by the results below.

| Item | Value |
|---|---|
| Status | MC-00 through MC-22 core implementation qualified; 2026-09-05 broker-boundary alignment pending |
| Created | 2026-08-28 |
| Source design | `MarketCondition-High-Level-Design-v0.1.md` |
| Workflow stage | `StrategyWorkflowStage.MarketCondition` |
| Primary instrument | ES futures and ES futures options |
| Architecture | Completed-only FunctionActor request/reply aligned with Regime Discovery |

**Implementation plan:** `MarketCondition-Implementation-Plan-v1.0.md`

### Broker implementation clarification — 2026-09-05

Actual IBKR connectivity is not implemented. The IBKR emulator will be
implemented first, with the actual broker connection following later. Market
Condition's completed calculation and workflow gates do not imply that either
broker implementation exists.

Market analysis must not require an actual IBKR connection. Broker/emulator
readiness belongs to Order Execution immediately before order submission,
using the selected adapter behind the shared broker contracts. Market
Condition retains feed, quote, session, cache, and data-quality checks.

This is the target boundary. Current code still includes `IbkrSession` in the
default required health sources and registers
`UnavailableMarketConditionBrokerReadiness`, which always reports unavailable.
It can therefore produce `NotTradeable / OperationsUnavailable` solely because
of that placeholder. Removing the Market Condition dependency and introducing
execution-stage emulator readiness are pending implementation work; no runtime
behavior is changed by this document.

## 1. Purpose

Market Condition is the second Intrinsic Time Strategy Workflow stage. It consumes the immutable Regime Discovery result, the original ITI trigger, a frozen point-in-time ES futures and futures-option snapshot, workflow/fund context, and an immutable parameter set. It deterministically answers:

> Is there a sufficiently current, liquid, operational, and coherent opportunity to consider for this fund and decision horizon now?

Market Condition describes the opportunity. It does not choose a trade structure, compose an order, allocate capital, approve risk, or interact with a broker to place an order. It may emit bounded, non-binding output hints so Trade Selection understands the intended downstream context; those hints never replace or constrain the primary market decision.

The three valid business outcomes are:

| Function result | Business meaning | Workflow action |
|---|---|---|
| Completed + Tradeable | Evaluation succeeded and a usable opportunity exists | Commit the result and continue to Trade Selection |
| Completed + NotTradeable | Evaluation succeeded and a measurable blocker or insufficient opportunity exists | Commit the result and terminate normally with `StrategyWorkflowOutcome.NoTrade` |
| Failed | A reliable result could not be produced | Commit workflow failure and terminate |

`NotTradeable` is not an error. `Completed` does not independently authorize continuation; the Strategy Workflow remains the sole continuation authority.

## 2. Authoritative V1 execution topology

Market Condition must follow the completed Regime Discovery Function pattern.

```text
Workflow revision 2: Started / MarketCondition / Processing
  -> Workflow Realtime resolves the already-frozen MarketConditionParameterSet
  -> ExecuteMarketConditionPipelineCommand
  -> Core NATS request/reply to MarketConditionFunctionActor
  -> parse map, validation map, exact-type receive map
  -> load completed-only PostgreSQL Function state
  -> capture one immutable MarketConditionSnapshot
  -> hard-gate evaluation
  -> deterministic classification and scoring when gates permit
  -> completed candidate only:
       synchronous MarketConditionFunctionProjector -> ScyllaDB
       completed-only Function-state append -> PostgreSQL
       direct MarketConditionPipelineCompletedEvent reply
  -> failed/exception/timeout:
       direct non-durable MarketConditionPipelineFailedEvent reply
  -> Workflow Realtime translates the reply:
       completed -> CompleteMarketConditionCommand
       failed    -> FailMarketConditionCommand
  -> Workflow Command actor commits one WorkflowStrategyStateUpdatedEvent
       Tradeable    -> revision 3 / TradeSelection / Processing
       NotTradeable -> revision 3 / Completed / NoTrade
       Failed       -> revision 3 / Failed / PipelineFailed
       Timeout      -> revision 3 / TimedOut
```

There is no Market Condition Command actor, Event actor, Realtime actor, processing event, terminal publication route, replay route, or child evaluator actor. The Strategy Workflow Realtime actor calls the Function directly and handles its typed reply.

The legacy `StartMarketConditionPipelineCommand`, `MarketConditionPipelineProcessingEvent`, and terminal-event comments that describe Command/Event/Realtime publication are placeholders from the prior topology. V1 replaces the Start command with `ExecuteMarketConditionPipelineCommand`, removes the processing event, and updates Completed/Failed contracts for direct Function semantics.

## 3. Fixed V1 decisions

1. `MarketConditionFunctionActor` is the only Market Condition actor.
2. The Function request remains an `ICommand<MarketConditionExecutionEntityId>`.
3. The Function returns `FunctionResult<MarketConditionPipelineCompletedEvent, MarketConditionPipelineFailedEvent>` directly to Workflow Realtime.
4. Only a completed candidate may be synchronously projected and stored in completed-only Function state.
5. A completed `NotTradeable` result is projected and persisted exactly like a completed `Tradeable` result.
6. Failed, timed-out, malformed, projection-failed, and persistence-failed attempts do not enter Function state.
7. Failed results are not projected and are not published for replay.
8. ES futures and ES futures-option market quality are both mandatory V1 inputs.
9. Required provider metadata that is missing, corrupt, or internally inconsistent causes `Failed`.
10. A provider that reliably reports an unhealthy or unavailable market condition produces `Completed + NotTradeable`.
11. A fixed maximum Function execution duration and late-result fencing are mandatory.
12. Manual Market Condition cancellation is deferred. Workflow-wide cancellation remains authoritative.
13. A Market Condition result is short-lived and carries an immutable validity deadline.
14. Expiry at workflow acceptance outranks a completed result and cannot cause recalculation.
15. There are no automatic processing retries. A later eligible ITI signal may start a new workflow after the prior workflow becomes terminal or expires.
16. Configuration is resolved from PostgreSQL ConfigurationDb and frozen before workflow revision 1 is committed.
17. A published Market Condition parameter version is immutable and is never deleted.
18. Deterministic structured fields and reason codes are authoritative; summary text is diagnostic only.
19. An LLM cannot classify, block, permit, or alter a Market Condition result.
20. `StrategyWorkflowOutcome.NoTrade` is a distinct normal terminal outcome.
21. `RegimeDiscoveryDecision` is the primary upstream market-language authority. The exact ITI trigger and frozen futures/options observations corroborate, conflict with, or qualify it; they do not silently replace it.
22. Market Condition uses every relevant populated decision field: direction, directional score, conviction, confidence, trend phase/strength/agreement, volatility level/change/term structure, structure classification, breakout, and restrictions.
23. Output hints are generated only after the primary decision is complete and cannot change classification, strength, confidence, blockers, or tradeability.
24. The minimum hint mapping is Daily/Futures, Weekly/VerticalSpread, and Monthly/IronCondor. The collection contract is append-only so later trade families can be added without redefining Market Condition language.

## 4. Scope and responsibility boundaries

### 4.1 Included

- immutable configuration resolution by fund, ES instrument root, and Daily/Weekly/Monthly horizon;
- point-in-time snapshot capture from bounded providers or latest-value caches;
- trigger, Regime result, futures quote, option-chain quality, session, event-risk, operational-health, and workflow-eligibility validation;
- hard tradeability gates;
- deterministic direction, phase, condition, volatility behavior, liquidity quality, strength, confidence, evidence, conflicts, blockers, and summary;
- completed-only Function state and synchronous ScyllaDB projection;
- direct typed Function result handoff to Strategy Workflow Realtime;
- Tradeable continuation, normal NoTrade termination, failure termination, expiry, deduplication, and hard timeout;
- read-only query and Operations UI projections;
- Unit, BDD, integration, and schema-V2 verification coverage.

### 4.2 Excluded

- selecting a binding futures-option strategy type, strike, expiry, quantity, or price; advisory trade-family/timeframe hints are permitted;
- portfolio allocation, margin approval, exposure limits, or final risk authorization;
- order routing, broker submission, modification, or cancellation;
- unrestricted order-book or full-chain persistence in workflow events;
- actor retries, business replay, restart/resume of incomplete calculations, or terminal-event republishing;
- a private Market Condition actor graph;
- machine learning, probabilistic model fitting, or LLM decision authority;
- manual stage cancellation in the initial implementation;
- calibration claims that the V1 defaults are economically optimal.

The V1 numeric defaults are conservative, deterministic starting values. They are operational rules to be verified and calibrated through captured fixtures and paper-trading observations; they are not immutable business truths.

## 4.3 MC-17 through MC-22 authority and hint amendment

The calculation follows this authority order:

1. Validate immutable identities, result completeness, snapshot lineage, source fitness, and workflow eligibility.
2. Treat the accepted `RegimeDiscoveryDecision` as the primary upstream market interpretation.
3. Use its direction, score, conviction, confidence, trend phase/strength/timeframe agreement, volatility level/change/term structure, structure classification, breakout, and restrictions wherever relevant to classification, scoring, gating, or structured evidence.
4. Use the original exact ITI event independently for direction agreement, trigger quality, and timing phase fallback when the appended Regime field is absent.
5. Use the sealed futures quote/trade and option-chain aggregate independently for integrity, liquidity, freshness, data quality, opportunity strength/confidence, and hint confidence.
6. Complete the primary Tradeable/NotTradeable decision before deriving any output hint.

This can be stated more directly:

> Use as much reliable input information as is available to decide the market condition. After that decision exists,
> add best-effort hints about likely downstream use. A hint is context, not truth and not authorization.

### Minimum output hints

| Evaluated horizon | Trade-family hint | Preferred examples | Non-preferred behavior |
|---|---|---|---|
| Daily | Futures | Directional or volatility-expansion market | `Eligible` when tradeable; `Avoid` when blocked |
| Weekly | VerticalSpread | Directional or volatility-expansion market | `Eligible` when tradeable; `Avoid` when blocked |
| Monthly | IronCondor | Range-bound or volatility-contraction market | `Eligible` when tradeable; `Avoid` when blocked |

`OutputHints[]` is deliberately a collection even though the minimum implementation emits one item per horizon.
Adding a later trade family is an append-only policy extension. Trade Selection remains responsible for permitted product
sets, final compatibility, strategy selection, strikes, expiries, quantities, and prices.

### Result schema V2

`MarketConditionResult.CurrentSchemaVersion` is 2. MessagePack key 34 appends `OutputHints[]`; keys 0 through 33 are
unchanged. The workflow result envelope advertises the same schema version. A schema-V1-shaped payload remains readable
with an empty hint collection, while all new completed results must contain one valid advisory hint matching their
target horizon.

## 5. Identities and routing

### 5.1 `MarketConditionExecutionEntityId`

The Function entity identity mirrors `RegimeDiscoveryExecutionEntityId`:

```text
MarketConditionExecutionEntityId
  WorkflowEntityId : IntrinsicTimeStrategyWorkflowEntityId
  WorkflowId       : StrategyWorkflowId
```

Its stable formatted value is:

```text
{WorkflowEntityId.Format()}.MarketCondition.{WorkflowId}
```

This creates one completed-only Function stream for one Market Condition execution and prevents collision with another workflow for the same strategy entity.

`StageInvocationId` is represented by `CommandId` in the established Function convention. The same logical invocation must reuse the same `CommandId`; a different `CommandId` is a different attempt.

### 5.2 Actor route

| Item | Value |
|---|---|
| Actor type | `ActorType.Function` |
| Actor name | `MarketConditionPipelineFunction` |
| Command verb | `Execute` |
| Bounded context | `MarketConditionPipelineBoundedContext` |
| Completed verb | `MarketConditionPipelineCompleted` |
| Failed verb | `MarketConditionPipelineFailed` |

## 6. Workflow configuration freeze

Before accepting a new workflow, Workflow Realtime resolves all configuration required to start Regime Discovery and later execute Market Condition. At minimum it resolves:

- strategy workflow identity/version;
- `FundId` assigned to the requested horizon;
- `RegimeDiscoveryParameterSet` and canonical hash;
- `MarketConditionParameterSet` and canonical hash.

The workflow start command and the authoritative workflow snapshot are extended append-only to contain:

```text
FundId
MarketConditionParameterSet
MarketConditionParameterPayloadSha256
```

No implicit horizon-to-fund mapping exists inside Market Condition. The strategy workflow configuration owns that mapping. A missing or ambiguous Fund, Regime, or Market Condition configuration prevents workflow acceptance; no Started state is committed.

The workflow view also gains an explicit `Outcome` field. Deriving every completed machine state as `StrategyWorkflowOutcome.Completed` is no longer sufficient because a normal terminal `NoTrade` must remain distinguishable from completion of all trading stages.

Append-only MessagePack keys must be assigned after existing keys. Existing keys must never be renumbered or reused.

## 7. Execute command contract

`ExecuteMarketConditionPipelineCommand : ICommand<MarketConditionExecutionEntityId>` contains:

| Field | Requirement |
|---|---|
| `CommandId` | Non-empty logical Function invocation identity |
| `Subject` | Function actor/name/verb and exact formatted execution entity |
| `PostEvents` | Retained interface field; completed projection is controlled by the Function lifecycle |
| `EntityId` | Composite workflow entity and workflow execution identity |
| `ErrorCode` | Stable Market Condition command error code |
| `RouteTo` | Market Condition bounded context |
| `InputWorkflowRevision` | Must equal the immutable workflow view revision; normally revision 2 |
| `WorkflowView` | Complete immutable accumulated workflow view |
| `TriggerEvent` | Original complete `FuturesItiSignalGeneratedEvent` |
| `CorrelationId` | Workflow correlation identity |
| `CausationId` | Workflow state update that selected Market Condition |
| `RequestedAtUtc` | UTC Function request timestamp |
| `ExpiresAtUtc` | Fixed calculation deadline; never extended |
| `ParameterSet` | Complete immutable `MarketConditionParameterSet` |
| `ParameterPayloadSha256` | Canonical parameter payload hash |
| `TargetHorizon` | Daily, Weekly, or Monthly; must match trigger, workflow, and parameters |
| `FundId` | Positive fund identity frozen by workflow configuration |
| `InstrumentRoot` | `ES` for V1 |

The command carries the accepted Regime result indirectly through `WorkflowView.MarketCondition`'s preceding accumulated state. The implementation must deserialize and validate the accepted `RegimeDiscoveryResult` from `WorkflowView.RegimeDiscovery.Result`. It must not query Regime Discovery to reconstruct it.

### 7.1 Command MessagePack keys

| Key | Field |
|---:|---|
| 0 | `CommandId` |
| 1 | `Subject` |
| 2 | `PostEvents` |
| 3 | `EntityId` |
| 4 | `ErrorCode` |
| 5 | `RouteTo` |
| 6 | `InputWorkflowRevision` |
| 7 | `WorkflowView` |
| 8 | `TriggerEvent` |
| 9 | `CorrelationId` |
| 10 | `CausationId` |
| 11 | `RequestedAtUtc` |
| 12 | `ExpiresAtUtc` |
| 13 | `ParameterSet` |
| 14 | `ParameterPayloadSha256` |
| 15 | `TargetHorizon` |
| 16 | `FundId` |
| 17 | `InstrumentRoot` |

Keys 0 through 15 intentionally align with `ExecuteRegimeDiscoveryPipelineCommand`; Market Condition fields are appended.

## 8. ConfigurationDb contract

### 8.1 Authoritative table

Market Condition has its own append-only PostgreSQL table:

```sql
CREATE TABLE IF NOT EXISTS reference_configuration.market_condition_parameter_set (
    parameter_set_id uuid NOT NULL,
    version integer NOT NULL,
    schema_version smallint NOT NULL,
    status smallint NOT NULL,
    effective_from_utc timestamptz NULL,
    retired_at_utc timestamptz NULL,
    payload_json jsonb NOT NULL,
    payload_sha256 text NOT NULL,
    description text NOT NULL DEFAULT '',
    created_utc timestamptz NOT NULL,
    created_by text NOT NULL,
    CONSTRAINT pk_market_condition_parameter_set
        PRIMARY KEY (parameter_set_id, version),
    CONSTRAINT ck_market_condition_parameter_set_version CHECK (version > 0),
    CONSTRAINT ck_market_condition_parameter_set_schema CHECK (schema_version > 0),
    CONSTRAINT ck_market_condition_parameter_set_hash CHECK (length(payload_sha256) = 64)
);
```

Required effective-resolution index:

```sql
CREATE INDEX IF NOT EXISTS ix_market_condition_parameter_set_effective
ON reference_configuration.market_condition_parameter_set
(
    (CAST(payload_json ->> 'FundId' AS integer)),
    (payload_json ->> 'InstrumentRoot'),
    (CAST(payload_json ->> 'TargetHorizon' AS smallint)),
    status,
    effective_from_utc DESC
);
```

### 8.2 Lifecycle

ConfigurationDb exposes:

- `InsertMarketConditionDraftAsync`;
- `PublishAsync(StrategyParameterSetKind.MarketCondition, ...)`;
- `RetireAsync(StrategyParameterSetKind.MarketCondition, ...)`;
- `GetMarketConditionAsync(parameterSetId, version)`;
- `ResolveEffectiveMarketConditionAsync(effectiveAtUtc, fundId, instrumentRoot, targetHorizon)`.

Resolution must return exactly zero or one effective published version. Multiple matching published versions are a configuration ambiguity and must fail workflow acceptance. Draft and retired versions are never selected for a new workflow. Existing workflows retain their frozen version after retirement.

The canonical JSON serializer and SHA-256 mechanism follow `RegimeDiscoveryParameterPayload`. Market Condition introduces `MarketConditionParameterPayload` with deterministic property ordering and invariant numeric formatting.

## 9. `MarketConditionParameterSet` v1

The typed parameter set contains:

```text
MarketConditionParameterSet
  ParameterSetId / Version / SchemaVersion
  StrategyParameterSetId / StrategyParameterSetVersion
  FundId
  InstrumentRoot
  TargetHorizon
  Snapshot
  Session
  EventRisk
  MarketIntegrity
  FuturesLiquidity
  OptionLiquidity
  OperationalReadiness
  WorkflowEligibility
  Classification
  Scoring
  Execution
  SummaryTemplateVersion
```

All decimal weights use `decimal`, all durations are stored as positive integer milliseconds or seconds, and all timestamps are UTC except configured exchange-local entry windows.

### 9.1 Parameter-set MessagePack keys

| Key | Field |
|---:|---|
| 0 | `ParameterSetId` |
| 1 | `Version` |
| 2 | `SchemaVersion` |
| 3 | `StrategyParameterSetId` |
| 4 | `StrategyParameterSetVersion` |
| 5 | `FundId` |
| 6 | `InstrumentRoot` |
| 7 | `TargetHorizon` |
| 8 | `Snapshot` |
| 9 | `Session` |
| 10 | `EventRisk` |
| 11 | `MarketIntegrity` |
| 12 | `FuturesLiquidity` |
| 13 | `OptionLiquidity` |
| 14 | `OperationalReadiness` |
| 15 | `WorkflowEligibility` |
| 16 | `Classification` |
| 17 | `Scoring` |
| 18 | `Execution` |
| 19 | `SummaryTemplateVersion` |

Each nested configuration is its own MessagePack record with keys assigned in the field order defined in its subsection. Future fields append; they never reuse or reorder keys.

The required nested properties are:

| Configuration record | V1 properties |
|---|---|
| `MarketConditionSnapshotConfiguration` | FutureClockSkewSeconds, SnapshotCaptureAttempts, FuturesQuoteMaximumAgeSeconds, FuturesTradeMaximumAgeSeconds, OptionQuoteMaximumAgeSeconds, OptionChainMaximumAgeSeconds, VolatilityMaximumAgeSeconds, SessionMaximumAgeSeconds, HealthMaximumAgeSeconds, EventRiskMaximumAgeSeconds |
| `MarketConditionSessionConfiguration` | ExchangeTimeZoneId, EligibleWeekdays, EntryWindowStart, EntryWindowEnd, RequireOpenExchangeState |
| `MarketConditionEventRiskConfiguration` | HighImpactBeforeMinutes, HighImpactAfterMinutes, RateDecisionBeforeMinutes, RateDecisionAfterMinutes, RequiredEventCategories |
| `MarketConditionMarketIntegrityConfiguration` | MaximumOneMinuteMoveAtr, MaximumFiveMinuteVolatilityIncrease, PermitCrossedMarket, RequirePositiveTwoSidedQuote |
| `MarketConditionFuturesLiquidityConfiguration` | TickSize, HealthySpreadTicks, MaximumTradeableSpreadTicks, MinimumBidSize, MinimumAskSize, HealthyBestSideSize |
| `MarketConditionOptionLiquidityConfiguration` | MinimumDte, MaximumDte, MaximumAbsoluteMoneyness, RequireCallsAndPuts, MinimumEligibleExpirations, MinimumCandidateContracts, MinimumValidQuoteCoverage, HealthyValidQuoteCoverage, MaximumMedianRelativeSpread, MaximumP90RelativeSpread, MinimumMedianBidSize, MinimumMedianAskSize, MaximumUnderlyingMismatch |
| `MarketConditionOperationalReadinessConfiguration` | RequiredHealthSources, TreatReportedDegradedAsBlocked |
| `MarketConditionWorkflowEligibilityConfiguration` | MaximumRegimeAgeSeconds, MaximumTriggerAgeSeconds, RequireEntriesEnabled, BlockingRegimeRestrictions |
| `MarketConditionClassificationConfiguration` | WeakeningReversalLevel, ExhaustingReversalLevel, ConfirmedBandLevel, HealthyLiquidityScore, HealthyDataQualityScore |
| `MarketConditionScoringConfiguration` | RegimeAlignmentWeight, TriggerQualityWeight, FuturesLiquidityWeight, OptionLiquidityWeight, DataQualityWeight, EntryTimingWeight, MinimumStrength, MinimumConfidence, penalty values |
| `MarketConditionExecutionConfiguration` | MaximumExecutionMilliseconds, TransportReplyGraceMilliseconds, ResultLifetimeSeconds |

Arrays are defensively copied and deterministically ordered. Validation requires non-empty required arrays, finite bounded decimals, positive durations, `MinimumDte <= MaximumDte`, entry-window start before end, and each declared weight group to sum to exactly one within `0.000001`.

### 9.2 V1 common defaults

| Parameter | Default |
|---|---:|
| Schema version | `1` |
| Instrument root | `ES` |
| Exchange timezone | `America/New_York` |
| Future clock skew tolerance | 2 seconds |
| Snapshot capture attempts | 3 |
| Maximum Function execution duration | 5 seconds |
| Function transport reply grace | 5 seconds |
| Maximum accepted Regime result age | 120 seconds |
| Maximum accepted trigger age at evaluation | 30 seconds |
| Deterministic summary template version | 1 |

The workflow-wide hard deadline still takes precedence. Function `ExpiresAtUtc` is:

```text
min(WorkflowView.ExpiresAtUtc, RequestedAtUtc + MaximumFunctionExecutionDuration)
```

### 9.3 Horizon defaults

| Target horizon | Minimum strength | Minimum confidence | Result lifetime |
|---|---:|---:|---:|
| Daily | 55 | 0.65 | 30 seconds |
| Weekly | 60 | 0.68 | 60 seconds |
| Monthly | 65 | 0.70 | 90 seconds |

These values affect opportunity acceptance, not hard data-integrity validation.

### 9.4 Snapshot freshness defaults

| Source/input | Maximum age |
|---|---:|
| ES best bid/ask and sizes | 2 seconds |
| ES last trade | 5 seconds |
| ES option quote | 5 seconds |
| Option-chain aggregate | 10 seconds |
| Volatility/shock observation | 15 seconds |
| Exchange session state | 60 seconds |
| Feed and broker health heartbeat | 15 seconds |
| Event-risk calendar status | 15 minutes |

Known observations older than the threshold are valid evidence for `NotTradeable`. Missing or invalid timestamps that prevent age calculation are failures.

### 9.5 Session defaults

| Parameter | Default |
|---|---|
| Eligible weekdays | Monday through Friday |
| Exchange state | Must report Open |
| Entry window start | 09:35 America/New_York |
| Entry window end | 15:30 America/New_York |
| Holiday/early-close handling | Authoritative session provider overrides static window |

The five-minute opening delay and thirty-minute closing buffer are calibration defaults. Session status must come from a provider that understands holidays and early closes; local weekday/time checks alone are insufficient authority.

### 9.6 Event-risk defaults

| Parameter | Default |
|---|---:|
| High-impact lockout before event | 15 minutes |
| High-impact lockout after event | 10 minutes |
| Central-bank/rate-decision lockout before | 30 minutes |
| Central-bank/rate-decision lockout after | 20 minutes |

The event provider returns a typed state and event identity. A known active exclusion is `NotTradeable / EventRiskBlocked`. A missing or corrupt mandatory provider response is Failed.

### 9.7 Market-integrity defaults

| Parameter | Default |
|---|---:|
| Maximum one-minute absolute move in ATR units | 1.50 |
| Maximum five-minute volatility-index relative increase | 0.15 |
| Crossed market permitted | No |
| Non-positive ES bid or ask permitted | No |
| Ask below bid permitted | No |

A known threshold breach is `NotTradeable / MarketDislocated`. Non-finite or structurally invalid source values that make evaluation unreliable are Failed.

### 9.8 ES futures-liquidity defaults

| Parameter | Default |
|---|---:|
| ES minimum tick size | 0.25 |
| Healthy spread | At most 1 tick |
| Maximum tradeable spread | 2 ticks |
| Minimum best bid size | 5 contracts |
| Minimum best ask size | 5 contracts |
| Healthy best-side size | 10 contracts |

Spread ticks are calculated as `(AskPrice - BidPrice) / TickSize` and rounded only after exact tick-alignment validation. A spread above two ticks or either best-side size below five is `NotTradeable / FuturesLiquidityInsufficient`.

### 9.9 ES futures-option quality defaults

The snapshot provider builds a bounded candidate chain around the underlying price and supported downstream maturities. V1 stores aggregates, not the full chain.

| Parameter | Daily | Weekly | Monthly |
|---|---:|---:|---:|
| Minimum days to expiration | 1 | 7 | 21 |
| Maximum days to expiration | 14 | 45 | 90 |
| Maximum absolute moneyness from ES underlying | 5% | 5% | 5% |
| Calls and puts required | Yes | Yes | Yes |

Quality thresholds are common to all three horizons:

| Parameter | Default |
|---|---:|
| Minimum eligible expirations | 1 |
| Minimum candidate option contracts | 12 |
| Minimum valid two-sided quote coverage | 0.80 |
| Healthy valid quote coverage | 0.90 |
| Maximum median relative spread | 0.20 |
| Maximum 90th-percentile relative spread | 0.35 |
| Minimum median bid size | 1 contract |
| Minimum median ask size | 1 contract |
| Maximum underlying-price mismatch | 0.25% |

For a positive two-sided quote:

```text
Mid = (Bid + Ask) / 2
RelativeSpread = (Ask - Bid) / Mid
```

Zero-bid contracts are not valid two-sided quotes and reduce coverage. A chain that is valid but below any hard threshold is `NotTradeable / OptionLiquidityInsufficient`. Missing mandatory chain-health metadata, an unidentifiable underlying, or corrupt aggregate math is Failed.

The bounded universe is a liquidity sample, not a strategy selection. No contract, strike, expiration, or option structure is recommended or reserved by Market Condition. Contract metadata used to calculate expiration, option type, strike, and moneyness must come from the authoritative Securities/reference model rather than parsing display symbols inside the evaluator.

### 9.10 Operational-readiness defaults

The following typed health states must be known and healthy:

- Databento or configured primary futures feed;
- futures-option feed;
- required latest-value cache path.

A reliable Unavailable/Degraded status is `NotTradeable / OperationsUnavailable`. Missing, expired, or contradictory mandatory health metadata is Failed.

These requirements cover market-data readiness. Under the 2026-09-05 boundary
clarification, broker session readiness is excluded from this stage and is
required before order submission instead. The current `IbkrSession` default
and placeholder registration must be migrated; they are not evidence that
IBKR connectivity has been implemented. Published parameter versions remain
immutable, so any required configuration change uses a new version.

## 10. Immutable snapshot model

`IMarketConditionSnapshotProvider.CaptureAsync` returns either a sealed snapshot, a known blocked snapshot, or a typed capture failure.

```text
MarketConditionSnapshot
  SnapshotId
  SchemaVersion
  WorkflowId / EntityId / FundId / InstrumentRoot / TargetHorizon
  EvaluationTimestampUtc
  MarketDataAsOfUtc
  SourceSequenceWatermark
  FuturesQuote
  OptionChainQuality
  SessionState
  EventRiskState
  VolatilityShockState
  OperationalHealth
  WorkflowEligibility
  DataQualityItems[]
  SnapshotSha256
```

Every source item retains source identity, source timestamp, received timestamp where available, sequence identity, availability, validity, and calculated age. The provider:

1. reads each required latest-value source once;
2. uses one `EvaluationTimestampUtc`;
3. applies future-skew and compatibility validation;
4. performs at most the configured number of bounded revision-stable capture attempts;
5. seals immutable values before evaluation;
6. creates deterministic aggregate option metrics;
7. creates a canonical snapshot hash excluding diagnostic wall-clock fields that are not authoritative.

The evaluator never rereads Redis, ScyllaDB, a feed, a broker, or a query API after capture starts. Redis or process caches may supply latest values but are not authoritative history.

## 11. Input validity versus business blockers

The following distinction is mandatory:

| Observation | Outcome |
|---|---|
| Quote timestamp exists and is too old | Completed + NotTradeable + `DataUnfit` |
| Feed health explicitly reports unavailable | Completed + NotTradeable + `OperationsUnavailable` |
| Session provider reports closed | Completed + NotTradeable + `SessionBlocked` |
| Option chain is valid but coverage is 0.70 | Completed + NotTradeable + `OptionLiquidityInsufficient` |
| Required quote has no timestamp | Failed + `RequiredInputInvalid` |
| Provider response is corrupt or contradictory | Failed + `RequiredInputInvalid` |
| Parameter set missing or hash mismatched | Failed + `ConfigurationUnavailable` or `ContractInvalid` |
| Evaluator throws or violates an invariant | Failed + `CalculationFailed` or `InvariantViolation` |

Failures must never be converted to NotTradeable to keep the workflow running. Known market blockers must never be reported as technical failures.

## 12. Ordered hard gates

Gates execute in a fixed order so the primary reason is deterministic:

1. Workflow eligibility and upstream result validity.
2. Data fitness and source compatibility.
3. Session and fund entry-window eligibility.
4. Event-risk exclusion.
5. Market integrity and shock detection.
6. ES futures liquidity.
7. ES futures-option chain quality.
8. Operational readiness.

All gates may contribute ordered evidence and blockers, but the first failing gate supplies `PrimaryReasonCode`. Evaluation does not run opportunity scoring after a hard blocker is established.

### 12.1 Workflow/upstream gate

This gate requires:

- workflow status Started, current stage MarketCondition, and exact revision;
- Regime stage Completed with a valid result envelope and hash;
- Regime result identities matching workflow, entity, trigger, horizon, and frozen parameter set;
- Regime result age within the configured maximum;
- no `RegimeRestriction.NoNewTrade`;
- trigger age within the configured maximum;
- fund entries enabled.

An upstream `NoNewTrade` restriction or disabled-entry flag is a known `NotTradeable / WorkflowIneligible`. A malformed or identity-conflicting envelope is Failed.

### 12.2 Direction conflict

A directional ITI trigger that directly opposes a directional fused Regime result is not a technical failure. V1 returns `Completed + NotTradeable + NoOpportunity` with `MC.BLOCK.REGIME_TRIGGER_CONFLICT`. Market Condition does not override either upstream input.

## 13. Deterministic opportunity model

Scoring occurs only after every hard gate passes. All intermediate values are clamped to `[0,1]` and final decimals are rounded to six decimal places using `MidpointRounding.AwayFromZero`.

### 13.1 Direction

| ITI trend | Candidate direction |
|---|---|
| `UpTrend` | Bullish |
| `DownTrend` | Bearish |

The candidate becomes final only if it does not conflict with a directional Regime result. A neutral Regime may support a RangeBound, VolatilityContraction, Transition, or NoOpportunity result but cannot independently produce a directional result.

### 13.2 Phase

Rules are evaluated in order:

1. `TrendDirectionChanged` -> Initiating.
2. `TrendReversalChanged` -> Reversing.
3. `ReversalLevel >= 0.70` -> Exhausting.
4. `ReversalLevel >= 0.40` -> Weakening.
5. `TrendExtremeChanged` -> Continuing.
6. `abs(BandLevel) >= 1.00` -> Confirmed.
7. `Trending` or `PredictedIntervalChanged` -> Confirmed.
8. Otherwise -> Undefined.

Non-finite Band or Reversal values are invalid required trigger inputs and cause Failed.

### 13.3 Volatility behavior

1. A hard shock threshold breach -> Shock and MarketDislocated blocker.
2. Regime volatility change Expanding -> Expanding.
3. Regime volatility change Contracting -> Contracting.
4. Otherwise -> Stable.

### 13.4 Condition classification

Rules are evaluated in order:

1. Market-integrity blocker -> Dislocated.
2. Direction conflict -> NoOpportunity.
3. Regime Transition restriction or Transitioning structure -> Transition.
4. Expanding volatility with no `NoNewTrade` restriction -> VolatilityExpansion.
5. Contracting volatility with neutral/ranging structure -> VolatilityContraction.
6. Ranging structure with neutral fused direction -> RangeBound.
7. Directional Regime aligned with directional trigger -> Directional.
8. Otherwise -> NoOpportunity.

### 13.5 Normalized features

Direction agreement is `1.0` for aligned directional inputs, `0.5` when the Regime is neutral, and `0.0` for conflict.

```text
RegimeAlignment = clamp(
    0.70 * DirectionAgreement
  + 0.30 * RegimeOverallConfidence)
```

ITI mode factors are:

| Mode | Factor |
|---|---:|
| TrendDirectionChanged | 1.00 |
| TrendExtremeChanged | 0.85 |
| Trending | 0.75 |
| PredictedIntervalChanged | 0.70 |
| TrendReversalChanged | 0.60 |
| HoldTradeChanged or InTradeChanged | 0.40 |

```text
BandProgress = clamp(abs(BandLevel) / 1.00)
ReversalIntegrity = clamp(1 - ReversalLevel)
TriggerQuality = clamp(
    0.50 * ModeFactor
  + 0.30 * BandProgress
  + 0.20 * ReversalIntegrity)
```

Futures liquidity:

```text
SpreadScore = clamp((MaximumTradeableSpreadTicks - SpreadTicks)
                    / (MaximumTradeableSpreadTicks - 1))
DepthScore = clamp(min(BidSize, AskSize) / HealthyBestSideSize)
FuturesLiquidityScore = 0.60 * SpreadScore + 0.40 * DepthScore
```

Option liquidity:

```text
CoverageScore = clamp(ValidQuoteCoverage / HealthyValidQuoteCoverage)
SpreadScore = clamp(1 - MedianRelativeSpread / MaximumMedianRelativeSpread)
SizeScore = clamp(min(MedianBidSize, MedianAskSize) / 1)
ExpiryScore = clamp(EligibleExpirationCount / MinimumEligibleExpirations)
OptionLiquidityScore =
    0.40 * CoverageScore
  + 0.35 * SpreadScore
  + 0.15 * SizeScore
  + 0.10 * ExpiryScore
```

For each required source:

```text
FreshnessFactor = clamp(1 - Age / MaximumAge)
DataQualityScore = arithmetic mean of required FreshnessFactors
```

Entry Timing is `1.0` at the midpoint of the configured entry window and decreases linearly to `0.8` at either allowed boundary.

### 13.6 Strength

Default weights sum to one:

| Feature | Weight |
|---|---:|
| Regime alignment | 0.30 |
| Trigger quality | 0.25 |
| Futures liquidity | 0.15 |
| Option liquidity | 0.15 |
| Data quality | 0.10 |
| Entry timing | 0.05 |

```text
RawStrength =
    0.30 * RegimeAlignment
  + 0.25 * TriggerQuality
  + 0.15 * FuturesLiquidityScore
  + 0.15 * OptionLiquidityScore
  + 0.10 * DataQualityScore
  + 0.05 * EntryTimingScore

Strength = round(100 * clamp(RawStrength), 0)
```

### 13.7 Confidence

```text
BaseConfidence =
    0.40  * RegimeOverallConfidence
  + 0.20  * TriggerQuality
  + 0.15  * DataQualityScore
  + 0.125 * FuturesLiquidityScore
  + 0.125 * OptionLiquidityScore
```

Penalties are additive and capped at `0.35`:

| Condition | Penalty |
|---|---:|
| Each optional input category missing | 0.05, maximum 0.15 |
| Upstream LowConfidence restriction | 0.10 |
| Transition classification | 0.10 |
| Non-terminal conflicting evidence | 0.10 each, maximum 0.20 |

```text
Confidence = round(clamp(BaseConfidence - min(0.35, Penalties)), 6)
```

### 13.8 Final Tradeability rule

The result is Tradeable only when all are true:

- every hard gate passed;
- ConditionType is not Dislocated or NoOpportunity;
- Strength is at least the horizon minimum;
- Confidence is at least the horizon minimum;
- phase is not Exhausting or Undefined;
- `EvaluatedAtUtc < ValidUntilUtc`;
- the result contains no BlockingReason.

A strength or confidence miss is `Completed + NotTradeable + NoOpportunity` with a stable below-threshold reason. It is not Failed.

## 14. Result contract

`MarketConditionResult` is a MessagePack immutable record with append-only keys:

```text
SchemaVersion
ResultId
WorkflowId
EntityId
FundId
InstrumentRoot
TargetHorizon
TriggerEventId
InputWorkflowRevision
StrategyParameterSetId / Version
MarketConditionParameterSetId / Version
SnapshotId / SnapshotSha256
EvaluatedAtUtc
ValidUntilUtc
MarketDataAsOfUtc
Tradeability
ConditionType
Direction
Phase
Strength
Confidence
VolatilityBehavior
LiquidityQuality
DataQuality
UpstreamAlignment
EvidenceItems[]
ConflictingEvidenceItems[]
BlockingReasons[]
PrimaryReasonCode
Reasons[]
SummaryText
```

### 14.1 Result MessagePack keys

| Key | Field | Key | Field |
|---:|---|---:|---|
| 0 | `SchemaVersion` | 17 | `MarketDataAsOfUtc` |
| 1 | `ResultId` | 18 | `Tradeability` |
| 2 | `WorkflowId` | 19 | `ConditionType` |
| 3 | `EntityId` | 20 | `Direction` |
| 4 | `FundId` | 21 | `Phase` |
| 5 | `InstrumentRoot` | 22 | `Strength` |
| 6 | `TargetHorizon` | 23 | `Confidence` |
| 7 | `TriggerEventId` | 24 | `VolatilityBehavior` |
| 8 | `InputWorkflowRevision` | 25 | `LiquidityQuality` |
| 9 | `StrategyParameterSetId` | 26 | `DataQuality` |
| 10 | `StrategyParameterSetVersion` | 27 | `UpstreamAlignment` |
| 11 | `MarketConditionParameterSetId` | 28 | `EvidenceItems` |
| 12 | `MarketConditionParameterSetVersion` | 29 | `ConflictingEvidenceItems` |
| 13 | `SnapshotId` | 30 | `BlockingReasons` |
| 14 | `SnapshotSha256` | 31 | `PrimaryReasonCode` |
| 15 | `EvaluatedAtUtc` | 32 | `Reasons` |
| 16 | `ValidUntilUtc` | 33 | `SummaryText` |

### 14.2 Enum numeric assignments

All enums reserve zero for an invalid/unset sentinel so default-deserialized values cannot accidentally authorize trading.

```text
MarketTradeability: Undefined=0, Tradeable=1, NotTradeable=2
MarketConditionType: Undefined=0, Directional=1, RangeBound=2, Transition=3,
                     VolatilityExpansion=4, VolatilityContraction=5,
                     Dislocated=6, NoOpportunity=7
MarketConditionDirection: Undefined=0, Bullish=1, Bearish=2, Neutral=3
MarketConditionPhase: Undefined=0, Initiating=1, Confirmed=2, Continuing=3,
                      Weakening=4, Exhausting=5, Reversing=6
MarketConditionVolatilityBehavior: Undefined=0, Contracting=1, Stable=2,
                                   Expanding=3, Shock=4
MarketConditionLiquidityQuality: Unknown=0, Healthy=1, Degraded=2, Unusable=3
MarketConditionDataQuality: Unknown=0, Healthy=1, Degraded=2, Unusable=3
MarketConditionUpstreamAlignment: Unknown=0, Aligned=1, Neutral=2, Conflict=3
```

Liquidity is Healthy when both liquidity scores are at least `0.75`, Degraded when all hard gates pass but either score is below `0.75`, and Unusable when a liquidity gate blocks. Unknown is used only on a failed/incomplete internal result and is never valid in a completed Tradeable result.

Data quality is Healthy at or above `0.75`, Degraded below `0.75` when all mandatory observations remain within their hard maximum ages, and Unusable when a data-fitness gate blocks. Unknown is invalid in a completed Tradeable result.

### 14.3 Evidence

Each `MarketConditionEvidenceItem` contains:

- area and stable feature code;
- observed decimal/string value and unit;
- normalized value and weighted contribution when applicable;
- source identity, source timestamp, and sequence identity;
- availability and freshness state;
- reason code.

Evidence is ordered by configured gate/feature priority and then stable feature code. Conflicting evidence uses the same contract in a separate ordered array. Free-form text is never the only evidence.

### 14.4 Summary

The deterministic template is:

```text
{Horizon} {InstrumentRoot} condition is {Tradeability}: {Direction} {ConditionType},
{Phase} phase, strength {Strength}, confidence {Confidence:0.00}.
{PrimaryReasonSentence}
```

Summary text is derived after the structured result is complete and has no decision authority.

## 15. Function terminal contracts

### 15.1 Completed

`MarketConditionPipelineCompletedEvent` mirrors the Regime completed Function event and adds:

- fixed `ExpiresAtUtc` calculation deadline;
- `ParameterPayloadSha256`;
- `MarketConditionSnapshotId`;
- `EvaluatedAtUtc` and `ValidUntilUtc` for fast workflow validation.

Its `StrategyStageResultEnvelope` contains the serialized `MarketConditionResult`, result schema version, market-data timestamp, produced timestamp, payload SHA-256, and result identity.

The event is projected, saved in completed-only Function state, and returned directly. It is not published for replay.

Completed-event MessagePack keys are:

| Key | Field | Key | Field |
|---:|---|---:|---|
| 0 | `Subject` | 10 | `CorrelationId` |
| 1 | `Id` | 11 | `CausationId` |
| 2 | `EntityId` | 12 | `PipelineStage` |
| 3 | `EventId` | 13 | `Result` |
| 4 | `CommandId` | 14 | `CompletedAtUtc` |
| 5 | `AggregateId` | 15 | `ExpiresAtUtc` |
| 6 | `EventSource` | 16 | `ParameterPayloadSha256` |
| 7 | `ReceivedOn` | 17 | `MarketConditionSnapshotId` |
| 8 | `WorkflowId` | 18 | `EvaluatedAtUtc` |
| 9 | `InputWorkflowRevision` | 19 | `ValidUntilUtc` |

### 15.2 Failed

`MarketConditionPipelineFailedEvent` mirrors the Regime failed Function event and carries the fixed deadline plus stable failure metadata.

Failure categories are:

- `ContractInvalid`;
- `ConfigurationUnavailable`;
- `RequiredInputInvalid`;
- `CalculationFailed`;
- `InvariantViolation`;
- `ProjectionFailed`;
- `PersistenceFailed`;
- `Timeout`.

Numeric assignments are append-only:

```text
MarketConditionFailureCategory:
  Undefined=0
  ContractInvalid=1
  ConfigurationUnavailable=2
  RequiredInputInvalid=3
  CalculationFailed=4
  InvariantViolation=5
  ProjectionFailed=6
  PersistenceFailed=7
  Timeout=8
```

The failed event is a direct non-durable reply. The durable failure authority is the workflow snapshot committed by `FailMarketConditionCommand`.

Failed-event MessagePack keys are:

| Key | Field | Key | Field |
|---:|---|---:|---|
| 0 | `Subject` | 13 | `CommandName` |
| 1 | `EntityId` | 14 | `CommandData` |
| 2 | `Id` | 15 | `RouteTo` |
| 3 | `ErrorDate` | 16 | `WorkflowId` |
| 4 | `EventId` | 17 | `InputWorkflowRevision` |
| 5 | `CommandId` | 18 | `CorrelationId` |
| 6 | `EventSource` | 19 | `CausationId` |
| 7 | `ErrorMessage` | 20 | `PipelineStage` |
| 8 | `ErrorCode` | 21 | `ExpiresAtUtc` |
| 9 | `ErrorType` | 22 | `FailureCategory` |
| 10 | `ErrorData` | 23 | `MarketConditionSnapshotId` |
| 11 | `ReceivedOn` | 24 | `ParameterPayloadSha256` |
| 12 | `AggregateId` | 25 | `ProcessingStarted` |

## 16. Workflow continuation and NoTrade

`CompleteMarketConditionCommand` must deserialize and validate the typed result before selecting a transition. Its current unconditional transition to Trade Selection is replaced.

### 16.1 Tradeable

When the result is valid, unexpired, and Tradeable:

- increment workflow revision exactly once;
- record the complete result envelope in MarketCondition stage state;
- set MarketCondition status Completed and continuation Proceed;
- set CurrentStage TradeSelection;
- initialize TradeSelection as Processing with the new input revision;
- remain `WorkflowStrategyMachineStatus.Started` and `StrategyWorkflowOutcome.None`;
- dispatch exactly one Execute/Start Trade Selection command according to that stage's implemented topology.

### 16.2 NotTradeable

When the result is valid and NotTradeable:

- increment workflow revision exactly once;
- record the complete result envelope;
- set MarketCondition status Completed and continuation Stop;
- retain CurrentStage MarketCondition;
- set `WorkflowStrategyMachineStatus.Completed`;
- set explicit `StrategyWorkflowOutcome.NoTrade`;
- set `TerminalAtUtc`;
- set `StopReasonCode` to the result PrimaryReasonCode;
- do not initialize or dispatch Trade Selection.

`NoTrade = 7` is appended to `StrategyWorkflowOutcome` without changing existing numeric values. Query/read-model conversion must preserve the explicit workflow-view Outcome rather than infer `Completed` from machine status.

### 16.3 Expired completed result

If workflow time, Function deadline, or `ValidUntilUtc` has been reached when the command actor evaluates completion, timeout/expiry takes precedence. The workflow becomes terminal TimedOut with a stable Market Condition expiry reason. It does not rerun Market Condition and does not continue.

### 16.4 Failed

`FailMarketConditionCommand` records an ordinary Market Condition failure as workflow Failed with outcome PipelineFailed. A failed Function result whose typed `FailureCategory` is Timeout records MarketCondition TimedOut and workflow TimedOut. Typed failure category is authoritative; an error-code check may be retained only for explicitly documented compatibility. Neither transition dispatches Trade Selection.

## 17. Function state, projection, and storage

### 17.1 Completed-only Function state

`MarketConditionFunctionState` contains only the accepted completed event and immutable request fingerprint required for idempotency. `SaveFunctionStateAsync` is reachable only for a completed candidate after successful projection.

A matching Execute after completion returns the original completed event without snapshot capture, calculation, projection, or state append. A matching previous failed attempt has no Function state and may be attempted again only if the caller explicitly sends another request before the authoritative workflow expires; there is no automatic retry.

A completed stream with a different request fingerprint returns a conflict failure.

### 17.2 Projection ordering

```text
completed candidate
  -> synchronous idempotent Scylla projection
  -> PostgreSQL completed Function-state append
  -> direct completed reply
```

If projection throws, return Failed and do not save Function state. If PostgreSQL state append throws after projection, return Failed; the Scylla row may remain an observational orphan but cannot advance the workflow. PostgreSQL and ScyllaDB do not share an ACID transaction.

### 17.3 Scylla read model

`MarketConditionReadModel` is keyed by workflow/result identity and includes:

- workflow/entity/fund/instrument/horizon identities;
- Function command and result identities;
- input workflow revision;
- parameter identity/version/hash;
- snapshot identity/hash;
- Tradeability, condition, direction, phase, strength, and confidence;
- volatility, liquidity, and data-quality classifications;
- primary reason, blockers, evidence, conflicts, and deterministic summary;
- evaluation, validity, market-data, projection, and completion timestamps;
- serialized typed result payload and SHA-256.

Projection is idempotent by logical completed-event/result identity. It has no mailbox, durable queue, checkpoint, replay, or publication responsibility.

## 18. Hard timeout and late-result fencing

The Function extension follows `ExecuteRegimeDiscoveryPipeline.ExecuteAtomicAsync` semantics:

1. Reject execution immediately when `now >= ExpiresAtUtc`.
2. Race the complete capture/evaluation worker against the remaining deadline.
3. When timeout wins, cancel the worker cooperatively and return one failed timeout result.
4. Observe any late worker fault only to prevent an unobserved exception.
5. Never allow a late worker to reach projection or persistence.
6. Recheck the deadline after the worker completes and before constructing a completed candidate.

Workflow Command independently checks its persisted workflow and result-validity deadlines. A caller-supplied timestamp cannot extend either deadline. A five-second transport reply grace permits reply delivery only; it does not extend calculation or result validity.

If a Function reply or workflow terminal command is lost, the next workflow Execute command lazily expires the stale active workflow according to the established strategy-workflow rule. Any later result for a superseded workflow, revision, stage, or invocation is ignored.

## 19. Validation conventions

`MarketConditionFunctionActor` uses:

- `_parseMap` keyed by message verb and the common `ParseMappedFunction` base implementation;
- `_validationMap` keyed by exact request type;
- `_receiveMap` keyed by exact request type and `ResolveMappedFunctionHandler`;
- one aggregated `CommandValidationException` containing all validation errors.

Every command validates `CommandId` visibly through the common command validation extension. Reference-type payloads use FluentValidation rules colocated with their shared contracts. Entity validation is colocated after `MarketConditionExecutionEntityId`.

Validation includes:

- command, subject, route, entity, and actor type;
- workflow/entity/invocation/revision consistency;
- workflow Started/MarketCondition state;
- Regime completed envelope type/schema/hash and identities;
- trigger identity, sequence, timestamps, numeric finiteness, horizon, and instrument;
- positive FundId and exact ES root;
- parameter schema, identity, fund, instrument, horizon, weights, thresholds, and canonical hash;
- `RequestedAtUtc < ExpiresAtUtc <= WorkflowView.ExpiresAtUtc`;
- MessagePack round-trip and append-only contract compatibility.

## 20. Stable reason-code catalog

Reason codes use uppercase dotted namespaces. V1 reserves:

| Code | Meaning |
|---|---|
| `MC.DATA.FIT` | Required inputs are fit |
| `MC.DATA.STALE` | Known required market input is stale |
| `MC.DATA.OPTIONAL_MISSING` | Optional input category is absent |
| `MC.BLOCK.DATA_UNFIT` | Data fitness blocks trading |
| `MC.BLOCK.SESSION` | Exchange or fund entry window blocks trading |
| `MC.BLOCK.EVENT_RISK` | Configured event-risk window blocks trading |
| `MC.BLOCK.MARKET_DISLOCATED` | Integrity or shock rule blocks trading |
| `MC.BLOCK.FUTURES_LIQUIDITY` | ES futures liquidity is insufficient |
| `MC.BLOCK.OPTION_LIQUIDITY` | ES option-chain quality is insufficient |
| `MC.BLOCK.OPERATIONS` | Feed, cache, or broker health blocks trading |
| `MC.BLOCK.WORKFLOW_INELIGIBLE` | Workflow or fund entries are disabled/ineligible |
| `MC.BLOCK.REGIME_NO_NEW_TRADE` | Upstream Regime forbids a new trade |
| `MC.BLOCK.REGIME_TRIGGER_CONFLICT` | Directional trigger conflicts with Regime |
| `MC.NO_OPPORTUNITY.STRENGTH` | Strength is below the horizon minimum |
| `MC.NO_OPPORTUNITY.CONFIDENCE` | Confidence is below the horizon minimum |
| `MC.CONDITION.DIRECTIONAL` | Directional condition classified |
| `MC.CONDITION.RANGE_BOUND` | Range-bound condition classified |
| `MC.CONDITION.TRANSITION` | Transition condition classified |
| `MC.CONDITION.VOLATILITY_EXPANSION` | Volatility expansion classified |
| `MC.CONDITION.VOLATILITY_CONTRACTION` | Volatility contraction classified |
| `MC.FAIL.CONTRACT_INVALID` | Function request contract is invalid |
| `MC.FAIL.CONFIGURATION` | Parameter configuration cannot be resolved or validated |
| `MC.FAIL.REQUIRED_INPUT` | Mandatory provider/input metadata is invalid |
| `MC.FAIL.CALCULATION` | Deterministic evaluation failed |
| `MC.FAIL.INVARIANT` | Internal result invariant failed |
| `MC.FAIL.PROJECTION` | Completed projection failed |
| `MC.FAIL.PERSISTENCE` | Completed Function state failed to persist |
| `MC.FAIL.TIMEOUT` | Fixed Function deadline elapsed |
| `MC.RESULT.EXPIRED` | Completed result expired before workflow acceptance |

Reason arrays are ordered by gate area, configured feature priority, severity, code, source timestamp, and source identity. Free-form exception messages are never used as continuation rules.

## 21. Queries and Operations UI

Read-only queries are:

- `GetMarketConditionQuery` by workflow/result identity;
- `GetLatestMarketConditionQuery` by fund/instrument/horizon;
- `GetMarketConditionHistoryQuery` over a bounded interval.

Queries read projections and never participate in Function execution or workflow continuation.

The Strategy Observation view displays stage status/duration, Tradeability, condition, direction, phase, strength, confidence, volatility behavior, liquidity/data quality, primary reason, blockers, evidence/conflicts, parameter version, snapshot identity, evaluation/expiry times, and correlation identities.

`NoTrade` is displayed as a normal completed business outcome. Failed and TimedOut are operational warnings/errors.

## 22. Observability

Recommended spans:

- Function request handling;
- snapshot capture;
- data-fitness evaluation;
- each hard-gate group;
- classification/scoring;
- completed projection;
- completed Function-state append;
- workflow continuation decision.

Metrics include processing count/outcome, Tradeable/NotTradeable count, blocker and failure code counts, duration percentiles, source age, expiry-before-acceptance count, strength/confidence distributions, and timeout count. Workflow, entity, invocation, and result identities are trace/log fields, never metric labels.

Structured logs emphasize transitions, blockers, failures, expiry, conflict, and unusual latency. Full option chains and per-feature information logs are prohibited.

## 23. Testing requirements

### 23.1 Unit tests

- parameter defaults, validation, canonical JSON, and hash stability;
- ConfigurationDb insert/publish/retire/get/effective-resolution and ambiguity;
- snapshot sealing, source age, hash, revision stability, and aggregate option math;
- every hard gate at below/equal/above threshold;
- known unavailable versus invalid/unknown provider semantics;
- direction, phase, volatility behavior, condition precedence, strength, confidence, and rounding;
- stable evidence/reason ordering and deterministic summary;
- result invariants and MessagePack round trips;
- Function parse/validation/receive maps and exact-type dispatch;
- completed-only projection/state ordering;
- timeout race and late-worker fencing;
- idempotent match and conflicting duplicate behavior;
- Tradeable, NotTradeable, Failed, expired, stale, and duplicate workflow transitions;
- explicit NoTrade mapping into command state and read models.

### 23.2 BDD scenarios

- Given healthy aligned inputs, when Market Condition completes Tradeable, then workflow selects Trade Selection.
- Given a measurable hard blocker, when evaluation succeeds, then workflow completes NoTrade.
- Given invalid mandatory metadata, when evaluation cannot be trusted, then workflow fails.
- Given a completed result that expires before acceptance, then workflow times out and does not rerun.
- Given duplicate terminal data, then workflow revision changes at most once.

### 23.3 Integration and verification tests

The production path must be tested from a completed Regime workflow state through the real Market Condition Function and back into the Strategy Workflow.

Positive fixtures include aligned bullish/bearish Directional, RangeBound, Transition, VolatilityExpansion, and VolatilityContraction conditions. Hard-block fixtures include every gate reason. Failure fixtures include corrupt snapshot metadata, configuration/hash mismatch, evaluator exception, projector exception, persistence exception, and fixed timeout.

Every Tradeable case proves exactly one Trade Selection command. Every NotTradeable case proves `StrategyWorkflowOutcome.NoTrade` and no Trade Selection command. Every failure proves no successful Market Condition projection, no completed Function state, and no later pipeline command.

Captured immutable fixtures are permitted for deterministic rule comparison. They are test data, not a production replay feature.

## 24. Repository alignment and required replacement work

The implementation plan generated from this specification must inventory and address:

1. Replace `StartMarketConditionPipelineCommand` with `ExecuteMarketConditionPipelineCommand` and append the Market Condition execution identity.
2. Update Market Condition Completed/Failed contracts to Function semantics and remove processing-event/publication assumptions.
3. Remove `MarketConditionPipelineProcessingEvent` if no compatibility consumer requires it.
4. Implement Market Condition configuration contracts, payload hashing, validation, PostgreSQL table SQL, and ConfigurationDb CRUD/resolution.
5. Freeze FundId and Market Condition parameters/hash into workflow start state and immutable views.
6. Add explicit workflow Outcome to the authoritative view and append `StrategyWorkflowOutcome.NoTrade`.
7. Implement snapshot provider abstractions and bounded adapters over existing ES futures/option data and health sources.
8. Implement sealed deterministic gate/evaluator models.
9. Implement Function actor/context/extension/projector/state/repository and Query actor.
10. Update Workflow Realtime Function invocation and result translation.
11. Replace unconditional `CompleteMarketCondition` continuation with typed Tradeable/NotTradeable/expired branching.
12. Extend workflow projectors, read models, queries, observation views, and tests.

No production implementation begins until a repository-specific gated implementation plan has been reviewed.

## 25. Version evolution

V1.1 may calibrate parameter values, add optional evidence, or enrich option-chain aggregation without changing terminal semantics.

V2 may add richer order-book microstructure, advanced cross-horizon fusion, additional instruments, or authorized manual cancellation. Any new serialized field is append-only. Any formula change requires a new parameter version and, when contract meaning changes, a new schema version.

Historical workflows are reconstructed from their frozen typed parameter payload and hashes. They never resolve newer configuration.

## 26. Definition of done for the specification

This specification is sufficient for an implementation plan when:

1. FunctionActor topology and no-replay semantics are authoritative.
2. Tradeable, NotTradeable, Failed, NoTrade, timeout, and expiry semantics are unambiguous.
3. ES futures and ES futures-option V1 inputs are explicit.
4. Known market blockers and invalid mandatory inputs cannot be confused.
5. ConfigurationDb table, lifecycle, selection key, payload, defaults, and hashes are specified.
6. Snapshot atomicity, required metadata, and provider boundaries are specified.
7. Gate order, formulas, weights, thresholds, classification precedence, and rounding are deterministic.
8. Result, evidence, reason, terminal event, Function state, projection, and query contracts are specified.
9. Workflow continuation and explicit NoTrade persistence are specified.
10. Hard timeout, late-result fencing, idempotency, and cross-database ordering are specified.
11. Testing requirements prove both positive functionality and fail-closed behavior.

## Appendix A: terminology

| Term | Meaning |
|---|---|
| Blocker | A known measured condition that produces Completed + NotTradeable |
| Failure | An inability to produce a trustworthy Market Condition result |
| Snapshot | One sealed point-in-time set of all required evaluation inputs |
| Tradeable | Market Condition permits Trade Selection to consider a trade |
| NoTrade | Normal workflow terminal outcome after a valid NotTradeable result |
| Function completion | Completed candidate projected and stored before direct reply |
| Result validity | Short interval during which a completed result may be accepted by workflow |
| Workflow deadline | Maximum lifetime of the complete strategy workflow; always authoritative |

## Appendix B: source alignment

This document specializes `MarketCondition-High-Level-Design-v0.1.md` and adopts the completed Regime Discovery V1 implementation conventions as the authoritative technical pattern. Where the high-level document referred to `MarketConditionActor`, Start commands, private publication, or event replay, this specification replaces those mechanics with the generic completed-only FunctionActor lifecycle while preserving the accepted Market Condition business responsibilities.
## Non-authoritative Decision reference query amendment

`GetMarketConditionDecisionReferenceQuery` is owned by `MarketConditionPipelineQuery` and transported exclusively by
Core NATS request/reply. It constructs deterministic representative inputs, evaluates them with the production
`MarketConditionCalculationModel`, and returns typed rows including the decision, restrictions, evidence features,
and advisory hint. It reads and writes no external state and cannot continue a workflow. The catalog is a reference,
not a whitelist, policy table, or complete enumeration.
