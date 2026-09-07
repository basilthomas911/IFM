# MarketCondition High-Level Design

> **Strategy catalog direction (2026-09-06):** Reusable strategy-family/structure/variant definitions are planned in ConfigurationDb and are downstream TradeSelection concerns. Current MarketCondition remains market-only for the single ITI-triggering Daily, Weekly or Monthly horizon. Historical family hints and family-scoped rules in superseded designs do not return to the assessment path. Recorded gate evidence is unchanged and does not qualify the new catalog. TradeSelection implementation is on hold. See [ConfigurationDb strategy catalog design](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

**Document version:** 0.4\
**Revision date:** 2026-09-06\
**Status:** Assessment-only code complete; current verification is recorded in Gate Evidence v2.0\
**Timeframe correction:** One triggering timeframe per workflow; Daily, Weekly, Monthly are supported alternatives\
**Supersedes:** [Version 0.3](MarketCondition-High-Level-Design-v0.3.md)\
**Detailed specification:** [Version 2.0](MarketCondition-Specification-v2.0.md)\
**Implementation plan:** [Version 2.0](MarketCondition-Implementation-Plan-v2.0.md)


## Assessment-only revision - 2026-09-06

Market Condition now executes only `ExecuteMarketConditionAssessmentCommand` (`Assess`). The earlier `Execute` evaluator, Function state/projector, option-universe adapters, broker-readiness adapters, snapshot cache/coordinator and legacy decision-reference generator have been removed. No trade strategy family participates in market profile resolution or assessment calculation.

Each new workflow must freeze one published market profile for the triggering Daily, Weekly or Monthly timeframe and the exact matching Regime Discovery parameter ID/version. `ES.Standard` is the default profile name, shared across families; its three timeframe rows still require publication before testing live starts. Missing or mismatched profiles fail explicitly. This revision does not create or publish profiles.

`UseMarketConditionAssessment` has been removed. `Enabled` controls automatic workflow starts; disabling it pauses new starts, without enabling an alternative evaluator. Existing assessment completions remain replayable. An old unbound workflow reaching Market Condition fails with `MC.ASSESSMENT.PROFILE_REQUIRED` and must be started again with an assessment profile. Legacy `Tradeable` completions cannot advance workflows.

Historical MessagePack fields, result DTOs, stored configuration and read-only result/history queries remain for deserialization and inspection. They provide no executable legacy path or fallback. Trade Selection owns fund-authorized family suitability; exact construction and broker readiness remain downstream. This change does not upgrade the Strategy view or run the combined five-stage workflow qualification.


## 1. Purpose

MarketCondition describes the current market for **the single timeframe carried by the ITI signal that triggered the workflow**. Supported timeframes are Daily, Weekly, and Monthly. It provides market context to Trade Selector before any strategy family is considered.

Its question is:

> What are the current market conditions for this workflow's triggering timeframe, how do they relate to its accepted regime, and how reliable and current is the assessment?

It does not ask whether a particular fund can trade a particular product or whether a vertical, Iron Condor, or futures strategy is suitable. Those are downstream decisions.

One invocation evaluates one configured underlying market, initially ES, and returns one assessment for one TargetHorizon. TargetHorizon is copied from the ITI signal, not chosen by MarketCondition. Daily, Weekly, and Monthly are supported decision horizons, not a requirement to evaluate all of them together, candle intervals, expiration classes, or strategy-family mappings.

For example: Weekly ITI signal -> Weekly workflow -> Weekly RegimeDiscovery -> Weekly MarketCondition -> Trade Selector.

The earlier draft's requirement for a multi-horizon upstream bundle and three results per invocation is withdrawn. The existing single-horizon RegimeDiscovery contract is the correct upstream boundary.

## 2. Change from version 0.3

| Previous design | Revised design |
|---|---|
| Fund-selected traded-product evaluation | Market assessment for the ITI signal's single timeframe |
| Daily Futures, Weekly Verticals, Monthly Iron Condors | Daily, Weekly, Monthly with no family or product assignment |
| Select a futures or options evaluator through a family profile | One deterministic evaluator using the matching timeframe's market-analysis profile |
| Check whether a permitted strategy family is generally constructable | Trade Selector assesses family/strategy suitability; Order Composition verifies exact construction |
| Tradeable/NotTradeable opportunity decision | Available/Unavailable assessment with descriptive conditions and restrictions |
| Advisory strategy-family hints | No family hints, preferred strategies, rankings, or suitability recommendations |
| Fund allocation and family mandate passed into market evaluation | Fund authority remains downstream; routing identities have no effect on market calculations |

The existing completed-only FunctionActor transport and persistence architecture is retained. This revision changes decision ownership and data contracts, not the entire actor architecture.

## 3. Workflow responsibilities

The workflow remains RegimeDiscovery -> MarketCondition -> Trade Selector -> Order Composition -> Risk Management -> Order Execution. The repository currently names the selection stage `StrategyWorkflowStage.TradeSelection`; “Trade Selector” in these documents means that stage.

| Stage | Owns |
|---|---|
| RegimeDiscovery | Underlying trend, direction, phase, volatility regime, structure, conviction, and regime restrictions for the triggering timeframe |
| MarketCondition | Current market observations, freshness, liquidity condition, session/event context, immediate stress, confidence, and agreement/conflict with the matching accepted regime and trigger |
| Trade Selector | Fund-authorized families, strategy eligibility, suitability, ranking, and selection or a normal no-strategy outcome |
| Order Composition | Exact products, contracts, expirations, legs, quantities, and proposed prices |
| Risk Management | Portfolio/fund allocation, exposure, leverage, margin, and approval of the candidate |
| Order Execution | Final permissions and readiness of the selected emulator/broker before submission |

MarketCondition consumes the accepted regime decisions. It does not calculate a competing trend from RSI, EMA, ADX, ATR, or other regime indicators. Current observations can conflict with the regime without rewriting it.

## 4. Market scope and timeframe model

The market profile identifies the underlying root, reference contract/roll policy, exchange/calendar, required data sources, and the selected horizon profile. Initially that market is ES. Identifying a reference instrument is necessary to obtain quotes and session data; it does not choose what instrument a strategy will eventually trade.

| Trigger timeframe | Assessment produced by that workflow |
|---|---|
| Daily | One Daily assessment using the accepted Daily regime and Daily profile |
| Weekly | One Weekly assessment using the accepted Weekly regime and Weekly profile |
| Monthly | One Monthly assessment using the accepted Monthly regime and Monthly profile |

The trigger, workflow, accepted RegimeDiscovery result, parameter profile, market snapshot, and MarketCondition result must all agree on TargetHorizon. A mismatch is a contract failure, not a reason to switch timeframes.

Each invocation uses one evaluation timestamp, one sealed snapshot, and the matching frozen upstream result and configuration. Analysis-window and upstream feature-profile bindings are explicit, versioned configuration. Daily does not automatically mean a daily candle; Weekly does not mean a weekly option expiration.

MarketCondition does not request results for other horizons, wait for their availability, run pairwise horizon comparison, or select a “best timeframe.” Any supporting context already present in the accepted regime remains upstream evidence; it does not require additional MarketCondition invocations.

## 5. Inputs and ownership

Required inputs are:

- the immutable, workflow-accepted RegimeDiscovery result for the triggering timeframe;
- the original intrinsic-time trigger and its actual timeframe;
- the configured underlying market identity;
- one sealed market snapshot containing source timestamps, sequences, availability, and health;
- the frozen market-analysis parameter set for that same timeframe.

The initial snapshot covers underlying reference quotes and available trading activity, session/calendar state, scheduled economic-event context, feed/cache health, and immediate stress observations. Existing analytics may supply normalized movement observations with their lineage; MarketCondition does not recreate regime indicators.

Economic-calendar context also requires durable **download completion evidence** for the UTC dates touched by the event window. An empty event query does not establish that the calendar is clear. The existing MarketData DownloadLog command/projector/query path now supplies that evidence; the live FMP startup on September 5 was verified for operational value date September 4 (104 calendar records and one Treasury curve).

The calendar consumer checks the latest relevant FMP attempt for each required date and freezes its identity, outcome, counts, source timestamps, hash, and coverage decision in the snapshot. Successful empty imports are valid coverage. Missing, failed, stale, or not-yet-observable outcomes are unavailable; query failure or corrupt evidence is a technical capture failure. A newer failed refresh cannot be hidden by an older success. The calendar status check time and the actual download completion time remain separate.

The initial US event-calendar binding accepts `ALL` or exact `US` coverage, with a versioned daily-refresh policy allowing less than 24 hours since completion. Date coverage remains exact, including windows crossing UTC midnight. MarketCondition never initiates a refresh or waits for its completion. Startup for a previous operational date does not establish coverage for a different calendar date.

Treasury download completion is available to rate-dependent consumers, but Treasury rates are not an input to this initial MarketCondition assessment and are not added as a required gate. No new external provider, strategy family, broker, or additional timeframe is introduced by this dependency.

The DownloadLog evidence policy is implemented in the retained calendar adapter and consumed by the sole assessment provider. The earlier product/broker evaluator and its registrations are removed. Assessment profile publication and combined pipeline qualification remain deployment work.

Fund, portfolio, strategy-family, allocation, risk-budget, and permitted-strategy-set data are not evaluator inputs. Existing workflow/fund identifiers may remain in routing and audit envelopes only. Identical market inputs and parameters must produce identical assessment content regardless of those identifiers.

The initial redesign excludes option-chain selection, DTE/moneyness windows, parity, IV-surface valuation, family-specific quote coverage, and strategy constructability. Trade Selector and downstream product analysis request the product evidence they need after the market assessment.

## 6. Existing upstream contract

The current `RegimeDiscoveryResult` represents one `TargetHorizon`, matching the workflow's ITI signal. This is sufficient. The redesign does not require RegimeDiscovery to produce additional horizons or change its single-result topology.

The existing workflow dispatch copies `TriggerEvent.EntityId.TimePeriod` into both RegimeDiscovery and MarketCondition requests. MarketCondition validates that the trigger, parameters, snapshot, and accepted regime result agree. Preserve and qualify these invariants in the revised assessment path.

MarketCondition consumes that accepted result directly. It must not relabel it, calculate another regime, query “latest” regime projections during calculation, or invent healthy defaults. A missing/unaccepted or corrupt upstream result is a contract failure. A valid accepted result that has become stale is a known data limitation.

## 7. Evaluation model

One `MarketConditionFunctionActor` coordinates a deterministic calculation component. It captures inputs once, evaluates the triggering TargetHorizon, and constructs one immutable result.

For the selected timeframe it:

1. validates the matching upstream decision and horizon-specific source lineage;
2. establishes whether enough reliable, current data exists to describe that timeframe;
3. preserves upstream direction, phase, structure, volatility regime, and restrictions;
4. describes present liquidity, session state, event proximity, and market stress;
5. records agreement or conflict between the original trigger and its matching accepted regime;
6. derives descriptive condition and assessment confidence;
7. records supporting/conflicting evidence, limitations, and validity.

Conditions may include Directional, RangeBound, Transition, VolatilityExpansion, VolatilityContraction, Dislocated, or Unclassified. An unfavorable, neutral, conflicting, or low-confidence condition is still a legitimate assessment when its inputs are reliable.

Liquidity describes the reference market. It does not estimate whether a fund's intended quantity can be filled. Event windows describe proximity/risk; strategy-specific lockouts belong downstream.

## 8. Result

`MarketConditionAssessmentResult` contains:

- market and invocation identities;
- parameter version/hash and sealed snapshot identity/hash;
- one common evaluation timestamp;
- TargetHorizon matching the original ITI signal;
- one assessment with Available or Unavailable status;
- deterministic explanatory text.

The assessment contains:

- Daily, Weekly, or Monthly;
- Available or Unavailable;
- accepted regime identity and upstream direction/phase/structure/restrictions;
- descriptive condition, volatility behavior, liquidity, session, event, and stress states;
- assessment confidence and data quality;
- trigger alignment when applicable;
- evidence, conflicts, and limitation reasons;
- evaluated-at and valid-until timestamps.

Available means the market can be described reliably. It does not mean profitable, suitable, permitted, or approved for trading. There is no Tradeable flag, opportunity acceptance threshold, strategy-family hint, or recommended strategy in the new result.

## 9. Completion and continuation

Function completion means one immutable assessment was successfully produced, including an explicit Unavailable assessment when required data is known to be unfit. Technical failure means trustworthy evaluation was impossible.

The workflow preserves the ITI signal's TargetHorizon throughout the pipeline. MarketCondition produces only that timeframe's assessment.

| Outcome | Workflow behavior |
|---|---|
| Target assessment available and unexpired; no authoritative restriction prohibits continuation | Record the single matching assessment and invoke Trade Selector once |
| Target assessment known unavailable | Complete normally with NoTrade and a data-unavailable reason |
| Target result expired, or workflow deadline reached | Stop as expired/timed out without recalculation |
| Upstream NoNewTrade for the target, or an independent workflow stop/cancellation | Respect that existing authority; do not switch timeframe to bypass it |
| Invalid contracts, corrupt required metadata, calculation/persistence failure | Fail the workflow |

Trade Selector receives unfavorable but valid conditions, including weak confidence, trigger/regime conflict, closed-session observations, or elevated event risk. It applies the fund's permitted horizon and strategy policies. Receiving an assessment is not permission to submit an order.

An existing emergency stop or workflow-wide restriction remains authoritative. Removing premature strategy selection does not remove independent stop controls.

## 10. Point-in-time consistency and expiry

Market observations are sealed around one evaluation timestamp. The matching upstream result and configuration are already frozen by Workflow. The evaluator performs no live rereads.

Every source preserves its observed time, received time when available, sequence, health, and identity. The result lifetime is short even for a Monthly workflow; the horizon describes market context, not how long stale quotes remain usable.

Trade Selector must validate the assessment's timeframe and expiry before use. Results from another timeframe cannot substitute for it or extend its validity.

A later eligible trigger starts a new workflow. This stage does not retry itself or refresh individual fields inside a completed result.

## 11. Broker boundary

Actual IBKR connectivity is not implemented. The IBKR emulator will be implemented first; actual broker connectivity follows later.

MarketCondition requires only its configured market-data and analytics services. Broker/emulator session readiness is checked at Order Execution immediately before submission. An absent emulator or IBKR connection must not change a market assessment.

The former `IbkrSession` requirement and `UnavailableMarketConditionBrokerReadiness` implementation/registration are removed. Assessment has no broker-readiness dependency.

## 12. Persistence, queries, and observation

Keep the established Function lifecycle:

```text
Workflow Realtime -> Function request/reply
  -> sealed snapshot + one deterministic assessment for the trigger's timeframe
  -> synchronous idempotent Scylla projection of a completed candidate
  -> PostgreSQL completed-only Function state
  -> direct completed reply
  -> Workflow validates and durably accepts the result
```

Failures are direct typed replies; Workflow persists the authoritative failure. A projected row alone is never continuation authority. Matching completed requests return the original result; conflicting duplicates fail. Timeouts fence late workers from projection, persistence, and continuation.

The Operations view should show the selected workflow's single timeframe and assessment, with market context, source ages, conditions, confidence, restrictions, and expiry. A history view may filter independently produced results by Daily, Weekly, or Monthly; it must not imply they came from one invocation. It must not display family recommendations or relabel Available as Tradeable.

## 13. Example

Illustrative values, not trading recommendations:

A Weekly ITI signal starts a Weekly workflow. Its accepted Weekly regime is neutral and range-bound. MarketCondition captures current observations and returns:

| Field | Value |
|---|---|
| TargetHorizon | Weekly |
| Availability | Available |
| Direction / condition | Neutral / RangeBound |
| AssessmentConfidence | 0.74 |
| Evidence | Matching Weekly regime, current data, and the original Weekly trigger |

Trade Selector receives this Weekly assessment and independently applies the fund mandate to choose an eligible strategy or return no suitable strategy. No Daily or Monthly result is requested or needed.

If required Weekly inputs become stale, MarketCondition returns an Unavailable Weekly assessment with the reason. The workflow does not substitute a Daily or Monthly assessment.

## 14. Acceptance criteria

- Daily, Weekly, Monthly are the supported timeframes; each invocation produces exactly one matching assessment.
- No timeframe is mapped to a product or strategy family.
- Trigger, workflow, accepted regime, profile, snapshot, and result agree on one TargetHorizon.
- The existing single-horizon upstream result is reused; no additional-horizon production or bundle is required.
- No strategy catalog, fund mandate, broker readiness, or option-chain provider is required by the evaluator.
- Unfavorable conditions remain descriptive; strategy suitability is downstream.
- Missing/stale data, corrupt data, expired results, and inherited restrictions have distinct outcomes.
- Frozen inputs produce deterministic assessments and explanations.
- Serialization, configuration migration, workflow handoff, persistence, queries, and tests support the revised contract.
- Historical MC-00 through MC-22 qualification is not treated as qualification of this redesign.

Exact target contracts, calculations, and defaults are defined in specification v2.0. The implementation plan and [gate evidence](MarketCondition-Gate-Evidence-v2.0.md) record implementation, current tests, observation and rollout boundaries. Production starts remain an explicit deployment choice.
