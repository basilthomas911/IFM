# Market Condition Detailed Specification v2.0

> **Strategy catalog direction (2026-09-06):** Reusable strategy-family/structure/variant definitions are planned in ConfigurationDb and are downstream TradeSelection concerns. Current MarketCondition remains market-only for the single ITI-triggering Daily, Weekly or Monthly horizon. Historical family hints and family-scoped rules in superseded designs do not return to the assessment path. Recorded gate evidence is unchanged and does not qualify the new catalog. TradeSelection implementation is on hold. See [ConfigurationDb strategy catalog design](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

| Item | Value |
|---|---|
| Status | Implemented specification; controlled qualification recorded in MarketCondition-Gate-Evidence-v2.0.md |
| Revised | 2026-09-05 |
| Source design | [High-Level Design v0.4](MarketCondition-High-Level-Design-v0.4.md) |
| Implementation plan | [Implementation Plan v2.0](MarketCondition-Implementation-Plan-v2.0.md) |
| Supersedes | [Specification v1.0](MarketCondition-Specification-v1.0.md) for new assessment-mode workflows |
| Workflow stage | StrategyWorkflowStage.MarketCondition |
| Assessment scope | One underlying market and one ITI-triggered TargetHorizon: Daily, Weekly, or Monthly |
| Initial market | ES reference market |
| Runtime architecture | Completed-only FunctionActor using Core NATS request/reply |

This is a semantic replacement for the old opportunity/tradeability model. MC-00 through MC-22 qualified the previous implementation. They do not qualify these requirements. Document version 2.0 is distinct from the version numbers of the new wire contracts.

**Timeframe correction:** Each invocation assesses only the ITI signal's timeframe. The earlier draft's multi-horizon bundle, three-result collection, aggregate coverage, and pairwise comparison requirements are withdrawn. Proposed new wire types below are still unimplemented; existing runtime schemas remain unchanged.


## Assessment-only revision - 2026-09-06

Market Condition now executes only `ExecuteMarketConditionAssessmentCommand` (`Assess`). The earlier `Execute` evaluator, Function state/projector, option-universe adapters, broker-readiness adapters, snapshot cache/coordinator and legacy decision-reference generator have been removed. No trade strategy family participates in market profile resolution or assessment calculation.

Each new workflow must freeze one published market profile for the triggering Daily, Weekly or Monthly timeframe and the exact matching Regime Discovery parameter ID/version. `ES.Standard` is the default profile name, shared across families; its three timeframe rows still require publication before testing live starts. Missing or mismatched profiles fail explicitly. This revision does not create or publish profiles.

`UseMarketConditionAssessment` has been removed. `Enabled` controls automatic workflow starts; disabling it pauses new starts, without enabling an alternative evaluator. Existing assessment completions remain replayable. An old unbound workflow reaching Market Condition fails with `MC.ASSESSMENT.PROFILE_REQUIRED` and must be started again with an assessment profile. Legacy `Tradeable` completions cannot advance workflows.

Historical MessagePack fields, result DTOs, stored configuration and read-only result/history queries remain for deserialization and inspection. They provide no executable legacy path or fallback. Trade Selection owns fund-authorized family suitability; exact construction and broker readiness remain downstream. This change does not upgrade the Strategy view or run the combined five-stage workflow qualification.


## 1. Decision authority

MarketCondition SHALL produce one descriptive, time-sensitive market assessment for the workflow's triggering timeframe, which SHALL be Daily, Weekly, or Monthly. It SHALL NOT choose a strategy family, rank strategies, recommend a product, impose family-specific entry requirements, estimate fillability for a fund's size, or approve trading.

RegimeDiscovery remains authoritative for underlying direction, phase, trend, structure, volatility regime, conviction, and restrictions. MarketCondition qualifies that context with current observations and data fitness. Trade Selector (the repository's TradeSelection stage) owns permitted-family and strategy suitability decisions.

Fund/portfolio/strategy identifiers retained in outer workflow envelopes are correlation data only. They SHALL NOT influence profile resolution, market capture, classification, confidence, or reasons. Assessment payload content, excluding correlation identifiers, must be invariant under changes to those identities.

There is no Daily/Futures, Weekly/VerticalSpread, or Monthly/IronCondor rule. The new calculation/result contains no OutputHints, Preferred/Eligible/Avoid suitability, Tradeable flag, or minimum opportunity score.

## 2. Fixed scope

One invocation produces one result for one MarketProfileId, InstrumentRoot, and TargetHorizon. The ITI signal is the timeframe authority:

```text
TriggerEvent.EntityId.TimePeriod
  = Workflow.TargetHorizon (explicit or derived from its frozen trigger)
  = RegimeDiscoveryResult.TargetHorizon
  = ParameterSet.TargetHorizon
  = Snapshot.TargetHorizon
  = MarketConditionAssessmentResult.TargetHorizon
```

All values must be Daily, Weekly, or Monthly and must match. A mismatch fails contract validation; no layer substitutes a default or selects another horizon.

A timeframe is a decision horizon, not a candle interval, option maturity, trade structure, or holding period. The selected horizon profile binds an explicit upstream analysis profile; MarketCondition does not infer that binding from a family or calculate new upstream indicators.

The workflow evaluates only the triggering timeframe. It does not wait for other horizons, collect their current results, calculate pairwise alignment, or choose a best timeframe. Supporting multi-timeframe evidence already contained in the accepted regime remains upstream context and does not expand this stage's scope.

Initial required providers are underlying reference quotes, market-data/cache health, exchange/calendar state, and economic-event calendar state. Optional normalized movement/trade observations may enrich the assessment. Option-chain, parity, IV-surface, expiration selection, product-specific constructability, and broker readiness are excluded.

## 3. Existing single-horizon upstream result

Consume the immutable RegimeDiscoveryResult already accepted in WorkflowView.RegimeDiscovery.Result. Preserve its envelope, payload hash, profile/version, workflow and trigger identities, produced time, and market-data timestamp. Do not introduce an upstream bundle.

The current RegimeDiscoveryResult schema V2 already has one TargetHorizon. Workflow dispatch copies TriggerEvent.EntityId.TimePeriod into both RegimeDiscovery and MarketCondition requests. Existing MarketCondition validation matches request/trigger/parameters and matches snapshot/regime/parameters. This is the correct timeframe boundary to preserve in the assessment path.

Required validation:

- RegimeDiscovery completed successfully and Workflow durably accepted the result before dispatching MarketCondition.
- The envelope type/schema/hash, workflow/entity/trigger identity, market scope, and frozen parameter lineage are valid.
- TargetHorizon matches the original ITI signal and selected MarketCondition parameters.
- A missing/unaccepted result, invalid hash, wrong timeframe, or contradictory identity is ContractInvalid.
- A structurally valid accepted result that becomes stale is a known assessment data limitation.
- Restrictions retain their original scope; MarketCondition does not erase them or substitute another timeframe.
- The trigger is the original event for this workflow, with its actual timestamp and sequence.

No RegimeDiscovery fan-out, additional horizon production, or result-topology change is required. MarketCondition must not calculate missing regimes, query mutable “latest” projections, or borrow another workflow's result. Restart reconstruction uses the exact accepted single result.

## 4. Configuration freeze and persistence

Before accepting a new assessment-mode workflow, resolve and freeze:

1. market profile and reference-instrument/roll-policy identity;
2. the upstream regime profile binding for the ITI signal's TargetHorizon;
3. MarketConditionAssessmentParameterSet and its canonical SHA-256;
4. workflow mode/version and TargetHorizon copied from the original ITI signal.

Fund eligibility and risk configuration remain owned by Workflow/Portfolio and downstream stages. They do not enter MarketCondition's calculation parameters.

Use a new typed parameter payload and table, leaving the published legacy rows unchanged:

```sql
CREATE TABLE IF NOT EXISTS reference_configuration.market_condition_assessment_parameter_set (
    parameter_set_id uuid NOT NULL,
    version integer NOT NULL CHECK (version > 0),
    schema_version smallint NOT NULL CHECK (schema_version > 0),
    market_profile_id text NOT NULL,
    instrument_root text NOT NULL,
    target_horizon smallint NOT NULL,
    status smallint NOT NULL,
    effective_from_utc timestamptz NULL,
    retired_at_utc timestamptz NULL,
    payload_json jsonb NOT NULL,
    payload_sha256 text NOT NULL CHECK (length(payload_sha256) = 64),
    description text NOT NULL DEFAULT '',
    created_utc timestamptz NOT NULL,
    created_by text NOT NULL,
    PRIMARY KEY (parameter_set_id, version)
);

CREATE INDEX IF NOT EXISTS ix_market_condition_assessment_effective
ON reference_configuration.market_condition_assessment_parameter_set
(market_profile_id, instrument_root, target_horizon, status, effective_from_utc DESC);
```

ConfigurationDb SHALL expose draft insert, exact get, publish, retire, and effective resolution by market profile/root/TargetHorizon/time. Reuse the established immutable lifecycle and canonical serialization conventions. Add the new table to the closed parameter-kind map with an appended enum value; table names never come from caller strings.

There must be exactly one effective published match for the requested timeframe. Configuration for other timeframes is not required to accept this workflow. Zero/ambiguous matches prevent workflow start. Validate duplicated indexed metadata against the typed payload. Published payloads and hashes cannot be updated or deleted; retirement changes lifecycle metadata only. Inflight workflows retain their frozen versions.

The typed parameter set contains identity/version/schema, MarketProfileId, InstrumentRoot, TargetHorizon, reference/calendar bindings, required-source definitions, snapshot limits, descriptive classification thresholds, execution limits, and one HorizonProfile matching TargetHorizon. It contains no FundId, family identifiers, allowed-strategy set, allocation, option-universe settings, or tradeability thresholds.

The selected HorizonProfile includes Horizon, accepted RegimeProfileId/version, required observation bindings with maximum ages, result lifetime, and summary-template version. Profile bindings must resolve at activation; an implementation cannot silently invent Daily/Weekly/Monthly analysis-window mappings. TargetHorizon is validated against the existing TimeFrameType values, including the indexed database metadata; no new numeric timeframe values are assigned.

## 5. Initial deterministic defaults

These are engineering defaults for reproducible qualification, not calibrated strategy entry rules. They may change only through a new parameter version.

| Parameter | Default |
|---|---:|
| Instrument root | ES |
| Future timestamp tolerance | 2 seconds |
| Maximum bounded snapshot capture attempts | 3 |
| Reference quote maximum age | 2 seconds |
| Optional last-trade maximum age | 5 seconds |
| Optional normalized stress observation maximum age | 15 seconds |
| Session maximum age | 60 seconds |
| Required feed/cache health maximum age | 15 seconds |
| Economic-calendar coverage-check maximum age | 900 seconds |
| Economic-calendar download maximum age | 86,400 seconds; positive validity required |
| Accepted regime decision maximum age | 120 seconds for the selected horizon |
| Trigger corroboration maximum age | 30 seconds |
| Function execution maximum | 5 seconds |
| Transport reply grace | 5 seconds |
| Daily assessment lifetime | 30 seconds |
| Weekly assessment lifetime | 60 seconds |
| Monthly assessment lifetime | 90 seconds |
| ES quote tick size | 0.25 |
| Healthy spread maximum | 1 tick |
| Elevated spread maximum | 2 ticks |
| Healthy minimum best-side size | 10 contracts |
| Thin-market minimum best-side size | 5 contracts |
| Immediate normalized movement stress threshold | Greater than 1.50 ATR units |
| Five-minute volatility-index relative increase stress threshold | Greater than 0.15 |

Regime result age measures freshness of the published decision, not the length of its historical analysis window. Candle/history cadence remains governed by the frozen upstream profile. Short quote ages are not relaxed simply because the assessment is Monthly.

A source age equal to its maximum remains valid; greater is stale. Future timestamps within tolerance use age zero and retain an explicit clock-skew reason; beyond tolerance are invalid metadata.

General event-context windows are 15 minutes before/10 after a configured high-impact event and 30 before/20 after a rate decision. These set an Elevated observation; they do not create a MarketCondition trading prohibition. Market calendar state comes from authoritative holiday/DST/early-close-aware providers, not weekday assumptions. No fund entry window is configured here.

There are no minimum opportunity-strength or confidence thresholds, no trade-family weights, and no option-DTE windows in this parameter set.

## 6. Request and identity

Add ExecuteMarketConditionAssessmentCommand with verb Assess to the existing MarketCondition Function actor. It uses the same FunctionActor parse/validation/exact-type receive conventions and direct request/reply; do not add Command/Event/Realtime actors or a terminal publication route.

Use a versioned execution identity namespace:

```text
{WorkflowEntityId.Format()}.MarketCondition.AssessmentV2.{WorkflowId}
```

This separates new completed-only Function streams from legacy executions. Matching logical requests reuse CommandId and the full immutable request fingerprint. A conflicting completed request returns a contract failure.

New request contract schema 1 uses these MessagePack keys:

| Key | Field |
|---:|---|
| 0 | SchemaVersion |
| 1 | CommandId |
| 2 | Subject |
| 3 | PostEvents |
| 4 | EntityId |
| 5 | ErrorCode |
| 6 | RouteTo |
| 7 | InputWorkflowRevision |
| 8 | WorkflowView |
| 9 | TriggerEvent |
| 10 | CorrelationId |
| 11 | CausationId |
| 12 | RequestedAtUtc |
| 13 | ExpiresAtUtc |
| 14 | ParameterSet |
| 15 | ParameterPayloadSha256 |
| 16 | RegimeResultEnvelope |
| 17 | RegimePayloadSha256 |
| 18 | MarketProfileId |
| 19 | InstrumentRoot |
| 20 | TargetHorizon |

PostEvents remains an interface field and cannot disable required completed projection. WorkflowView and the explicit regime result/parameters must match their frozen counterparts exactly. Existing workflow routing identities may contain fund/strategy context, but the evaluator receives a separate market-only context.

The request carries the one accepted upstream result for TargetHorizon; no asynchronous query reconstructs it during calculation.

## 7. Snapshot and source semantics

MarketConditionAssessmentSnapshot contains SnapshotId/schema/hash, MarketProfileId, InstrumentRoot, TargetHorizon, actual ReferenceInstrumentId, EvaluationTimestampUtc, and the observations required by the selected profile. Each source is captured and counted once; the snapshot represents only this invocation's timeframe.

Every observation includes SourceId, feature code, observed/received timestamps, sequence or stable revision, value/unit, availability, validity, and relevant provider health. KnownUnavailable must be a typed provider report with a timestamp and reason, not a default false/zero or swallowed exception.

Capture is bounded and revision-stable. A failed consistency attempt may retry capture up to the configured limit; a completed calculation is never automatically retried. Freeze the successful snapshot before evaluation. Normalize valid numerics to finite decimals, defensively copy collections, and order sources/features deterministically before hashing.

| Input situation | Required treatment |
|---|---|
| A valid quote or matching regime decision is known stale | Assessment Unavailable |
| Required feed/cache explicitly reports unavailable/degraded | Assessment Unavailable with source reason |
| A required provider reliably reports no observation | Assessment Unavailable |
| Optional trade/stress observation absent or stale | Keep assessment if required inputs are fit; publish Unknown/limitation for that feature |
| Valid observed crossed reference market | Descriptive Dislocated market; not an exception |
| Required timestamp missing, wrong root, invalid hash, non-finite required value, contradictory metadata | Failed RequiredInputInvalid/ContractInvalid |
| Transport exception or unexplained capture failure | Failed; do not pretend the provider reported a known market outage |
| No option data or no broker connection | No effect; neither is an input |

Malformed supplied optional data is rejected as invalid input rather than used as evidence. An optional provider's explicit unavailability remains a limitation. Lack of required data makes this assessment Unavailable. Conditions or missing data in other timeframes do not participate in this invocation.

Snapshot hashes include authoritative evaluation time, lineage, values, and availability. Diagnostic elapsed durations are excluded. No full order book, option chain, broker payload, or credentials are stored in the result.

### 7.1 FMP calendar download evidence

The assessment calendar adapter SHALL consume `IDownloadLogQueryApi` during bounded capture, before reading calendar events. It SHALL NOT call FMP, submit an import, repair a log, poll for projection completion, or introduce business retries. Reuse the implemented `MarketConditionCalendarCoverage` policy and preserve these rules when migrating the legacy adapter to assessment mode:

1. Derive the exact UTC dates intersecting `[evaluation time - maximum after-window, evaluation time + maximum before-window]`. These are source-calendar dates, not automatically the operational/trading date. The initial binding is US events; accept `EconomicCalendar/FMP/ALL` and `EconomicCalendar/FMP/US` only. Treasury, another country, and another date cannot establish coverage.
2. Read one latest attempt per partition through `GetHistoryAsync(pageSize: 1)`. Initial coverage is bounded to three dates and two scopes (at most six queries). Do not page backwards to find an older success. Across covering scopes select the newest requested attempt; equal request timestamps conservatively prefer failure, then earlier completion, then deterministic import ID order.
3. Validate the outcome schema, partition, deterministic logging ID, payload hash, UTC timestamps, and projected-at provenance. A query error or corrupt/mismatched reply is a technical failure. Missing latest rows are known NotConfirmed, not an inferred successful empty download. Older pages cannot change this latest-attempt policy.
4. Require the selected outcome to be Completed, projected by the evaluation timestamp, and to have positive remaining download validity. Policy `FMP.CalendarCoverage.v1` uses 86,400 seconds from `FinishedAtUtc`, independently of the 900-second coverage-check freshness limit. Exactly at download expiry there is no positive validity. A later failed refresh invalidates coverage even if an earlier Completed row remains queryable; partial writes must not be treated as the earlier complete dataset.
5. A Completed download with zero records is valid evidence. Read the calendar event window only after all required dates are confirmed. Classify its actual rows; counts in a log are not a substitute for event data or proof that all existing rows belong to that import.
6. Seal the coverage decision and actual download provenance in the snapshot. `CheckedAtUtc` is the status observation time, not the provider download time. Keep each selected/latest outcome's original IDs, timestamps, counts, status and hash. A new status read cannot rejuvenate `FinishedAtUtc`.
7. Cap available-result expiry at the earliest accepted download expiry and at the instant a new uncovered date would enter the event window. A sealed snapshot whose coverage has expired is unfit. Calculation and duplicate/restart reconstruction do not requery DownloadLog.

The initial implementation appends nullable `DownloadEvidence` at key 4 of `MarketConditionEventRiskState`; keys 0–3 remain unchanged. Nullable `CalendarDownloadEvidence` at legacy result key 35 carries that same frozen evidence through the completed-event payload, Scylla result projection and restart; existing result keys 0–34 retain their meanings. The nested `MarketConditionCalendarDownloadEvidence` manifest is: 0 PolicyVersion, 1 CheckedAtUtc, 2 FromDate, 3 ToDate, 4 Country, 5 MaximumDownloadAgeSeconds, 6 CoverageConfirmed, 7 Reason, 8 ValidUntilUtc, 9 Attempts. Attempts are defensively copied `MarketDataDownloadLogReadModel` values. Null evidence is omitted from JSON so historical snapshot hashes retain their previous shape. Old persisted snapshots/results remain readable; new production captures require the query dependency.

The shared calendar adapter reports missing coverage as Unknown event risk with an Unavailable observation. Assessment translates this into an Unavailable descriptive result; technical query failures remain Failed. Elevated event proximity is descriptive, not a trading prohibition. Frozen assessment profiles carry the versioned coverage policy.

DownloadLog is terminal-attempt evidence, not an in-progress import ledger or an atomic version of the mutable calendar table. An import that has not produced a terminal outcome cannot be inferred from it. Full import-generation consistency would require a separate data-generation/ownership contract; these checks do not claim that guarantee. Likewise, a September 4 startup download does not satisfy a September 5/6 event window. Refresh scheduling remains outside MarketCondition.

TreasuryCurve has a verified DownloadLog path but is not an initial required assessment source. Add it only with an explicit rate-dependent feature and frozen source policy, not merely because its download log exists.

## 8. Ordered calculation

Process in this order:

1. Validate command, workflow revision/mode, frozen parameters, hashes, market identity, accepted regime envelope, and TargetHorizon equality.
2. Seal and validate the snapshot for the selected timeframe.
3. Evaluate data fitness and preserve the matching upstream context.
4. Derive descriptive observations, condition, confidence, and expiry.
5. Order evidence/reasons and construct one deterministic assessment and summary.
6. Validate the result, including equality with the original trigger's timeframe.
7. Apply Function deadline fencing before projection/persistence/reply.

There is no loop over Daily, Weekly, and Monthly. Invalid contracts or untrustworthy supplied metadata fail the invocation rather than manufacturing a usable assessment.

### 8.1 Availability

Available requires a valid matching upstream decision and every configured required source for that horizon to be known, valid, within maximum age, and covered by reliable feed/cache health. Unavailable is a successfully classified known absence/staleness of required data.

Available does not depend on directional conviction, confidence thresholds, family liquidity, event proximity, a fund entry window, or broker status. A trustworthy description of a poor market is Available.

An unavailable assessment uses null for current condition/confidence/expiry and carries reasons. It may retain a separately labeled last-known upstream reference for diagnosis, but that value is not current assessment authority.

### 8.2 Upstream and trigger fields

Preserve Direction, TrendPhase, TrendStrength, DirectionalScore, RiskAdjustedConviction, StructureClassification, VolatilityLevel/Change, Breakout, Confidence, and Restrictions from the matching accepted RegimeDiscoveryDecision. They are upstream context, not newly calculated MarketCondition outputs.

The trigger must match TargetHorizon; a different timeframe is a contract failure. For a fresh matching trigger, TriggerAlignment is Aligned for matching non-neutral directions, Conflicted for opposing non-neutral directions, and Neutral when the accepted direction is neutral. A stale trigger's corroboration is NotApplicable with an explicit reason; it does not replace or invalidate an otherwise fresh regime/market assessment.

A conflict is evidence. MarketCondition does not rewrite direction or return NoOpportunity because of it.

### 8.3 Descriptive liquidity and stress

For a valid non-crossed quote:

```text
SpreadTicks = (Ask - Bid) / TickSize
BestSideSize = min(BidSize, AskSize)
Healthy = SpreadTicks <= 1 and BestSideSize >= 10
Degraded = not Healthy and SpreadTicks <= 2 and BestSideSize >= 5
Poor = otherwise
```

Use the corresponding configured thresholds, not embedded constants. Tick alignment and positive prices must be validated. A locked market (zero spread) is separately flagged; size rules still apply. A reliably observed crossed market has liquidity Unknown and a market-integrity stress flag.

These labels describe reference-market conditions. They do not certify execution for any order size and do not block selection.

Immediate stress is Elevated when either fresh normalized movement exceeds its threshold, fresh volatility-index change exceeds its threshold, or a reliably observed market-integrity anomaly exists. Normal requires all stress inputs configured for that determination to be fresh and below thresholds; otherwise Unknown. Missing optional stress inputs never become an assumed Normal.

No ATR, trend indicator, volatility regime, or option surface is independently rebuilt in this component.

### 8.4 Condition precedence

For an Available horizon, select the first applicable condition:

1. Dislocated: current reliably measured stress/integrity condition is Elevated.
2. Transition: accepted regime structure/restriction explicitly indicates transition.
3. VolatilityExpansion: accepted volatility change is Expanding.
4. VolatilityContraction: accepted volatility change is Contracting.
5. RangeBound: accepted structure is ranging and direction is neutral.
6. Directional: accepted direction is bullish or bearish.
7. Unclassified: none applies.

VolatilityBehavior is Shock for measured elevated immediate stress, otherwise the accepted Expanding/Contracting/Stable state. Unknown upstream values remain Unknown. Preserve underlying classifications alongside the derived condition so precedence does not discard information.

Session state and event context are separate fields, not direction forecasts. Elevated event proximity does not automatically mean volatility Shock.

### 8.5 Confidence

Confidence expresses confidence in the assessment, not probability of profit or strategy suitability. For each required observation:

```text
FreshnessFactor = clamp(1 - AgeSeconds / MaximumAgeSeconds, 0, 1)
FitnessFactor = minimum(required observation FreshnessFactors,
                        matching regime decision FreshnessFactor)
AssessmentConfidence = round(RegimeDecision.Confidence * FitnessFactor, 6)
```

Use decimal arithmetic and MidpointRounding.AwayFromZero. All confidence inputs must be in [0,1]; invalid ranges fail validation. Low confidence is reported without a MarketCondition acceptance threshold. Known-stale required input makes the assessment Unavailable rather than producing a numeric current confidence.

Each required source participates once in the assessment. Optional source absence is explicit evidence; it does not silently add a numerical penalty or new weighting. Upstream strength/conviction remains clearly labeled upstream; no new opportunity-strength composite is generated.

### 8.6 Supporting upstream context

The accepted RegimeDiscoveryDecision may already include trend timeframe-agreement or other supporting evidence. Preserve its lineage and meaning as upstream context. MarketCondition does not independently fetch or evaluate Daily, Weekly, or Monthly results beyond its own TargetHorizon, and does not create a cross-timeframe result collection or ranking.

## 9. Result contract

Use a new payload type MarketConditionAssessmentResult, schema 1. It does not reinterpret legacy MarketConditionResult keys or numeric enum meanings.

| Key | Field |
|---:|---|
| 0 | SchemaVersion |
| 1 | ResultId |
| 2 | WorkflowId |
| 3 | EntityId |
| 4 | CommandId |
| 5 | InputWorkflowRevision |
| 6 | MarketProfileId |
| 7 | InstrumentRoot |
| 8 | ParameterSetId |
| 9 | ParameterSetVersion |
| 10 | ParameterPayloadSha256 |
| 11 | RegimeResultId |
| 12 | RegimePayloadSha256 |
| 13 | SnapshotId |
| 14 | SnapshotSha256 |
| 15 | EvaluatedAtUtc |
| 16 | TargetHorizon |
| 17 | Assessment |
| 18 | SummaryText |
| 19 | CalendarEvidence |

Assessment is one non-null HorizonAssessment object, not an array. Its Horizon equals the outer TargetHorizon and all triggering workflow inputs. Its Availability is Available or Unavailable; aggregate coverage and pair-alignment contracts are not used.

The new AssessmentAvailability enum reserves zero as invalid/unset:
Undefined=0, Available=1, Unavailable=2.
Do not change the existing TimeFrameType values. The implemented new result keys above are frozen; no persisted legacy keys or meanings are changed.

HorizonAssessment schema 1 uses keys in the following order:

| Key | Field |
|---:|---|
| 0 | SchemaVersion |
| 1 | Horizon |
| 2 | Availability |
| 3 | RegimeResultId |
| 4 | RegimePayloadSha256 |
| 5 | UpstreamContext (typed, nullable when unavailable) |
| 6 | ConditionType (nullable when unavailable) |
| 7 | VolatilityBehavior |
| 8 | LiquidityCondition |
| 9 | SessionState |
| 10 | EventRiskState |
| 11 | StressState |
| 12 | TriggerAlignment |
| 13 | AssessmentConfidence (nullable when unavailable) |
| 14 | DataQuality |
| 15 | EvaluatedAtUtc |
| 16 | ValidUntilUtc (nullable when unavailable) |
| 17 | EvidenceItems |
| 18 | ConflictingEvidenceItems |
| 19 | LimitationReasons |
| 20 | InheritedRestrictions |
| 21 | SummaryText |

UpstreamContext embeds the matching accepted typed Decision without changing its serialized meanings. InheritedRestrictions remain visible even when that decision is now too stale for a current assessment. Other nested new records require key-order manifests and round-trip fixtures at MC-R02; fields append within their own new types.

Available requires non-null condition/confidence and ValidUntilUtc > EvaluatedAtUtc. Unavailable requires at least one limitation reason and no current numeric confidence. Unknown is explicit for unavailable optional observations. No default value may imply a positive assessment.

Evidence contains TargetHorizon, source and feature identifiers, value/unit, observed time/sequence, availability/freshness, and stable reason. All evidence is scoped to the selected TargetHorizon. Order canonically by source identity and feature; observations are unique per source and retain their original timestamp and sequence. Record applied thresholds and intermediate confidence terms.

## 10. Validity and Function deadlines

For an Available horizon:

```text
ValidUntilUtc = min(
    EvaluatedAtUtc + HorizonProfile.ResultLifetime,
    earliest expiry of each required source including the accepted regime)
```

A snapshot exactly at a required-source age boundary can be valid evidence but has no positive remaining lifetime. Such an assessment is Unavailable with MC.ASSESS.NO_VALIDITY_REMAINING; do not manufacture a future expiry.

The completed reply carries this assessment's validity for Workflow's fast check; an unavailable assessment has no validity. Apply only the selected profile's lifetime. No result from another timeframe may substitute for or extend it.

Function ExpiresAtUtc is min(workflow hard deadline, requested time + configured Function maximum). Transport grace does not extend calculation, source freshness, or result validity. Workflow checks its own clock at acceptance. Trade Selector rechecks expiry at use.

## 11. Terminal and persistence contracts

Add typed MarketConditionAssessmentCompletedEvent and MarketConditionAssessmentFailedEvent as direct Function replies. Their logical fields retain the established correlation, workflow, revision, deadline, and failure categories, with the new result discriminator and parameter/regime/snapshot hashes.

The StrategyStageResultEnvelope must identify both the payload type and schema. If its existing contract lacks a discriminator suitable for the new type, append a field without reusing keys. Legacy schema V2 is not enough to distinguish the new payload.

Only a validated completed candidate may follow:

```text
synchronous idempotent Scylla projection
  -> PostgreSQL completed-only Function state
  -> direct Completed reply
  -> Workflow Command acceptance and authoritative state update
```

Completed includes Available and Unavailable assessments. Failed includes invalid contracts/required metadata, calculation/invariant, projection/persistence errors, and timeout. Failed attempts do not create completed Function state or successful result projections.

Projection failure prevents Function state. A PostgreSQL append failure after Scylla projection can leave an observational orphan; the row does not authorize continuation. Matching completed duplicates return the original result without recapture. Conflicting fingerprints fail. Retain timeout/late-worker fencing and workflow supersession checks. No automatic business retries, private event publication, replay, or new child actors.

## 12. Workflow handoff

Workflow validates result discriminator/schema/hash, execution identity, revision, frozen market/parameter/regime identities, single-assessment invariants, and exact agreement with the original ITI timeframe.

Apply in order:

1. Reject stale/superseded invocation or already-terminal workflow without advancing it.
2. Respect workflow cancellation/global stop and hard deadline.
3. Reject invalid result contracts as failure.
4. Respect inherited authoritative NoNewTrade for the target or explicitly global scope, recording normal NoTrade.
5. If target is known Unavailable, record normal NoTrade with its data limitation.
6. If target assessment has expired, stop TimedOut with an expiry reason.
7. Otherwise record the whole result, complete MarketCondition, and dispatch TradeSelection exactly once.

No other timeframe is required or considered for continuation. The selection stage receives the one matching assessment and the original fund mandate independently. It cannot rescue an unavailable assessment by silently switching timeframe.

No condition-class, confidence, liquidity label, trigger conflict, or event proximity in this specification independently prevents Trade Selector invocation. That stage owns suitability and entry-policy checks. It must reject a strategy that lacks the current data it requires and may return a normal no-strategy result. It cannot disregard inherited restrictions or independently switch the fund's permitted horizon.

Neither MarketCondition completion nor Trade Selector dispatch authorizes an order.

## 13. Stable reasons

Use new descriptive MC.ASSESS codes without repurposing legacy MC.BLOCK or MC.NO_OPPORTUNITY meanings.

| Code | Meaning |
|---|---|
| MC.ASSESS.AVAILABLE | Required information supports a current assessment |
| MC.ASSESS.REGIME_STALE | Matching upstream decision too old |
| MC.ASSESS.SOURCE_UNAVAILABLE | Required observation/provider reliably unavailable |
| MC.ASSESS.SOURCE_STALE | Required observation too old |
| MC.ASSESS.NO_VALIDITY_REMAINING | No positive current validity interval |
| MC.ASSESS.OPTIONAL_UNKNOWN | Optional observation absent/stale |
| MC.ASSESS.CLOCK_SKEW | Future timestamp accepted within tolerance |
| MC.ASSESS.SESSION_CLOSED | Closed-session observation |
| MC.ASSESS.EVENT_ELEVATED | Configured event-context window active |
| MC.ASSESS.LIQUIDITY_POOR | Reference-market liquidity described as Poor |
| MC.ASSESS.STRESS_ELEVATED | Current stress/integrity condition observed |
| MC.ASSESS.TRIGGER_CONFLICT | Relevant trigger opposes its regime |
| MC.ASSESS.UPSTREAM_RESTRICTION | Restriction preserved with original scope |

Technical failures retain their established typed categories and MC.FAIL codes; MC.RESULT.EXPIRED remains an expiry reason. Free-form text never controls Workflow. New reasons and nested enum manifests must be frozen in contract fixtures before implementation enablement.

## 14. Queries, Operations UI, and decision reference

New typed exact/latest/history queries use market profile/instrument/TargetHorizon; they do not calculate or resolve live parameters. Latest resolution must include the requested timeframe so a Monthly result cannot satisfy a Weekly query. Exact result queries validate their stored timeframe. Historical comparisons may show independent results only with their separate timestamps and provenance.

Project one assessment per result, including TargetHorizon, availability/condition/confidence/expiry, observations, evidence, inherited restrictions, hashes, and correlation identities. Historical legacy results retain their old schema and are explicitly labeled legacy.

Operations presents the selected workflow's single timeframe and assessment. Display Available/Unavailable and current/expired separately. Remove Tradeable badges and family hints for the new mode. Display unavailable optional features as Unknown. A timeframe filter may select Daily, Weekly, or Monthly; it must not imply one invocation evaluated all three.

`GetMarketConditionAssessmentReferenceQuery` and the assessment CSV export use the production assessment calculator for separate Daily, Weekly and Monthly examples. The old decision-reference generator and query handler are removed. Historical DTO meanings are preserved; references have no continuation authority.

Metrics use bounded timeframe/availability/reason labels; workflow/fund/entity/result identifiers remain in traces/logs, not metric labels.

## 15. Broker and product boundary

IBKR is not implemented. Implement the IBKR emulator first and actual connectivity later through shared broker contracts. Order Execution checks the selected emulator/broker immediately before submission.

The IbkrSession requirement and UnavailableMarketConditionBrokerReadiness implementation are removed. Option-feed availability and selected strategy family are absent from assessment calculation and its required-health list.

Historical workflows/configuration remain readable with their original schema. Published historical versions are not rewritten; no old evaluator is available.

## 16. Compatibility and rollout

The Assess verb and AssessmentV2 Function stream are the only executable Market Condition path. Historical MessagePack keys, enum values, parameters and result DTOs retain their meanings for stored-data reads. Legacy Execute requests and Tradeable completions are rejected.

Workflow stores its frozen assessment profile. New starts without it are rejected; historical unbound workflows reaching Market Condition fail explicitly. There is no assessment-mode enablement flag. Deployment requires published matching profiles and qualified required sources.

Use `Enabled=false` to pause automatic new starts. This does not rewrite accepted state, erase rows, or restore the removed evaluator. Assessment retries use frozen payloads and completed-only state.

## 17. Verification requirements

Required unit/contract fixtures:

- one result for one trigger; separate Daily, Weekly, and Monthly cases, with unsupported timeframe rejection;
- equality of trigger/workflow/regime/parameter/snapshot/result timeframes and failure on any mismatch;
- consumption of the existing accepted single RegimeDiscovery result with no additional-horizon production or queries;
- no dependency on configuration or data availability for non-triggering timeframes;
- absence of family, fund-policy, option-chain, and broker dependencies;
- identical assessment content for different routing fund/family metadata;
- source age/future skew/expiry boundaries and explicit known-unavailable versus corrupt input;
- every condition precedence and liquidity/stress boundary;
- exact confidence calculation, rounding, and no suitability thresholds;
- required source outage, optional Unknown, stale accepted regime, inherited restrictions, and missing/unaccepted regime rejection;
- trigger/regime conflict without choosing another timeframe;
- deterministic snapshot hash, evidence order, serialization, and concurrent calculation equality;
- old/new mode separation, default sentinel validation, and legacy deserialization.

BDD and real topology tests must cover independent Daily, Weekly, and Monthly workflows, unavailable selected-timeframe data, absent other-timeframe data/configuration, poor but valid market passed to Trade Selector, selection returning no suitable strategy, upstream NoNewTrade, missing optional observations, failure/timeout/expiry, matching/conflicting duplicate, restart, projection/persistence failures, and late result fencing.

Infrastructure qualification uses actual NATS, PostgreSQL Function/workflow/configuration storage, and Scylla projections. A controlled later-stage test receiver may observe handoff but cannot substitute for the MarketCondition Function or its persistence. New enablement also requires Trade Selector consumer contract tests proving timeframe, expiry, mandate, and restrictions are honored.

## 18. Definition of done

All MC-R00 through MC-R09 gates in plan v2.0 must pass. The implementation must produce one assessment for the ITI signal's timeframe, preserve the existing single-result upstream boundary and actor/storage guarantees, remove early family/product/broker decisions, maintain old-state readability, and expose timeframe-correct queries and observation views.

The existing one-horizon routing and matching validation are retained. Option-quality gates, family hints, fund-keyed resolution and broker placeholders are removed from execution. See [gate evidence](MarketCondition-Gate-Evidence-v2.0.md) for current verification and remaining profile publication/live qualification.

## 19. Implemented nested wire manifests and storage

The following lists give the exact fields in increasing MessagePack key order, starting at zero. The new contract manifests are tested for unique contiguous keys. These are separate new types; legacy keys are unchanged.

| Type | Keys, in order |
|---|---|
| MarketConditionAssessmentParameterSet (0–29) | SchemaVersion, ParameterSetId, Version, MarketProfileId, InstrumentRoot, TargetHorizon, ReferencePolicy, CalendarBinding, HorizonProfile, Sources, FutureClockSkewSeconds, SnapshotCaptureAttempts, MaximumExecutionMilliseconds, TriggerMaximumAgeSeconds, TickSize, HealthySpreadTicks, DegradedSpreadTicks, HealthyBestSize, DegradedBestSize, MovementStressThreshold, VolatilityChangeStressThreshold, CalendarDownloadMaximumAgeSeconds, HighImpactBeforeMinutes, HighImpactAfterMinutes, RateDecisionBeforeMinutes, RateDecisionAfterMinutes, EconomicCalendarDataset, EconomicCalendarProvider, EconomicCalendarScopes, CalendarCoveragePolicy |
| MarketConditionAssessmentHorizonProfile (0–5) | Horizon, RegimeProfileId, RegimeProfileVersion, RegimeMaximumAgeSeconds, ResultLifetimeSeconds, SummaryTemplateVersion |
| AssessmentSourceBinding (0–2) | SourceId, Required, MaximumAgeSeconds |
| MarketConditionAssessmentBinding (0–2) | ModeVersion, Parameters, PayloadSha256 |
| MarketConditionAssessmentSnapshot (0–12) | SchemaVersion, SnapshotId, MarketProfileId, InstrumentRoot, TargetHorizon, ReferenceInstrumentId, EvaluatedAtUtc, Quote, SessionState, EventContext, Observations, CalendarEvidence, PayloadSha256 |
| AssessmentReferenceQuote (0–3) | Bid, Ask, BidSize, AskSize |
| AssessmentObservation (0–8) | SourceId, ObservedAtUtc, ReceivedAtUtc, Sequence, Availability, Validity, Value, Unit, Reason |
| AssessmentEvidence (0–9) | Horizon, SourceId, Feature, Value, Unit, ObservedAtUtc, AgeSeconds, Availability, Reason, Sequence |
| MarketConditionAssessmentReferenceRow (0–5) | Mode, SchemaVersion, CaseCode, CoverageKind, IsAuthoritative, Result |

New enum numeric manifests:

- AssessmentCondition: Undefined=0, Directional=1, RangeBound=2, Transition=3, VolatilityExpansion=4, VolatilityContraction=5, Dislocated=6, Unclassified=7.
- AssessmentLiquidity: Unknown=0, Healthy=1, Degraded=2, Poor=3.
- AssessmentStress: Unknown=0, Normal=1, Elevated=2.
- AssessmentEventContext: Unknown=0, Clear=1, Elevated=2.
- AssessmentTriggerAlignment: Unknown=0, Aligned=1, Conflicted=2, Neutral=3, NotApplicable=4.
- AssessmentVolatility: Unknown=0, Stable=1, Expanding=2, Contracting=3, Shock=4.

The shared StrategyStageResultEnvelope already supplies type/schema/content-type/hash discrimination, so its keys 0–7 are unchanged. Workflow assessment binding appends at ExecuteIntrinsicTimeStrategyWorkflowCommand key 18, IntrinsicTimeStrategyWorkflowView key 26 and public workflow state key 22. Null binding identifies historical unbound state and is invalid for new execution. New assessment completion retains its own typed result and sealed snapshot, with RequestFingerprint at key 20 and Snapshot at key 21; it is not the legacy completion type.

Calendar authority is explicitly frozen as EconomicCalendar/FMP, scopes `ALL,US`, policy `FMP.CalendarCoverage.v1`; this revision rejects other bindings. CME is the separate exchange-session binding. Canonical hashes normalize decimal scale, sort source bindings and include the complete frozen source policy. Request fingerprints normalize older trigger constructor defaults before canonical hashing.

Scylla `market_condition_assessment` is keyed by workflow UUID. `market_condition_assessment_by_profile` partitions by market profile/root/horizon and orders by evaluated time descending then workflow UUID ascending. Both store the typed completed payload plus a SHA-256 checksum. Reads verify the checksum, envelope and sealed snapshot. Exact lookup, bounded before-time history (1–100 rows) and latest-within-horizon are distinct from the legacy fund-based read paths; the current UI loads the latest 25 entries and does not offer cursor pagination.

The completed-only PostgreSQL Function stream remains authoritative. Neither Scylla table alone proves workflow acceptance. Observation compares the completed result ID/hash with accepted workflow state and marks unaccepted projections explicitly.
