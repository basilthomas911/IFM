# Actor-Based Spread Strategy Backtesting System

## Business-Domain Design and Specification

**Version:** 0.1  
**Status:** High-level business-domain specification  
**Initial strategy focus:** ES futures-option spreads and iron condors  
**Initial hedge instruments:** ES and MES futures  
**Primary architecture:** Actor-based commands, queries, and events  
**Implementation target:** Separate implementation design derived from this specification

---

## 1. Purpose

This document defines a general deterministic backtesting system for spread-based trading strategies. The first implementation is optimized for ES futures options, especially monthly iron condors, vertical spreads, and temporary ES/MES futures hedges, but the domain model must support additional strategies without replacing the backtesting foundation.

The system must answer four questions:

1. **What information was available at each historical decision time?**
2. **What would the production trading system have decided from that information?**
3. **What orders and fills would a realistic simulated broker have produced?**
4. **What financial, risk, execution, and decision-quality results followed?**

The backtester is not a simplified spreadsheet approximation of the strategy. It is a controlled simulation environment that invokes the same deterministic trading-domain policies used by the production system through replaceable adapters.

---

## 2. Design Goals

The system shall:

- Reuse production business logic wherever practical.
- Preserve production command, event, query, and result semantics.
- Use a virtual clock and deterministic event ordering.
- Prevent look-ahead and revised-data leakage.
- Reconstruct point-in-time futures and option-chain state.
- Model multi-leg option orders, ES/MES hedges, costs, slippage, partial fills, expiration, and settlement.
- Support historical replay, controlled scenarios, parameter experiments, and regression tests.
- Produce a complete explanation of every trade and every `NoTrade` decision.
- Make identical inputs, policies, code, and seed produce identical outputs.
- Allow many independent runs to execute concurrently.
- Generate artifacts suitable for human review and later machine-assisted research.
- Permit Codex to generate implementation code from a validated, declarative specification without inventing business rules.

---

## 3. Non-Goals

V1 does not attempt to:

- Predict future market behavior.
- Prove that historical profitability will continue.
- Model exchange matching engines perfectly.
- Optimize parameters automatically during an official evaluation run.
- Change production strategy policy directly.
- Use an LLM to make simulated trading decisions.
- Treat midpoint fills as executable truth.
- Hide missing or poor-quality historical data.
- Reorder production workflow stages for convenience.
- Retry failed pipeline stages indefinitely.
- Replace paper trading or controlled production validation.

---

## 4. Foundational Principles

### 4.1 Same decisions, different adapters

Production and backtesting should share deterministic domain components for:

- Regime Discovery.
- Market Condition.
- TradeStrategy/TradeSelection.
- Order Composition.
- Risk Management.
- PositionMonitor.
- ExitPosition.
- Pricing and Greeks.
- Strategy and composition policies.

Backtesting replaces only environment-facing services:

| Production dependency | Backtesting replacement |
|---|---|
| Wall clock | SimulationClock |
| Databento live stream | HistoricalMarketReplay |
| IBKR broker | SimulatedBroker |
| Live portfolio/account | SimulatedPortfolioLedger |
| Live calendar/events | PointInTimeEventCalendar |
| Live configuration resolver | FrozenBacktestPolicyManifest |
| Production persistence | Run-scoped event and result stores |

### 4.2 Deterministic run boundary

One backtest run is a deterministic ordered simulation. Parallelism occurs across runs, scenarios, strategies, or parameter sets. Events inside one run are not processed concurrently when doing so could alter business ordering.

### 4.3 Point-in-time truth

Every datum must carry the timestamp when it became available to the simulated system. Publication date, revision date, exchange event time, capture time, and processing time must not be conflated.

### 4.4 No automatic safeguard relaxation

If an input, quote, policy, or risk gate is missing, the result is `NoTrade`, `DataInvalid`, or `RunFailed` according to the applicable rule. The backtester cannot relax constraints to increase the number of trades.

### 4.5 Official and exploratory results are separate

Exploratory parameter search may inspect a training period. Official evaluation runs use frozen parameters and untouched out-of-sample periods. Results must identify which mode produced them.

---

## 5. Terminology

| Term | Meaning |
|---|---|
| Backtest specification | Immutable declaration of strategy, policies, data, scenarios, execution, metrics, and outputs |
| Run | One deterministic execution of one specification against one scenario and seed |
| Experiment | A set of related runs, commonly a parameter sweep or comparison |
| Scenario | Historical interval plus optional declared transformations or shocks |
| Replay event | A timestamped historical or scenario-generated input delivered to the simulated system |
| Virtual time | Current deterministic time owned by SimulationClock |
| Decision record | Immutable input/output evidence for one trading decision |
| Official run | Frozen, reproducible evaluation whose results are eligible for comparison or promotion review |
| Exploratory run | Research run whose output cannot activate production policy |
| Baseline | Control strategy or configuration used for comparison |
| Fill model | Deterministic rules converting simulated orders and market state into fills |
| Data manifest | Versioned list and checksums of every input dataset |
| Policy manifest | Versioned identities and checksums of every strategy, risk, and execution policy |

---

## 6. System Context

The backtesting platform wraps the existing trading system rather than implementing an independent strategy engine.

```text
Backtest Specification
    -> BacktestRunActor
    -> SimulationClock + MarketReplay
    -> Existing Trading Pipelines
    -> SimulatedBroker + PortfolioLedger
    -> Metrics + Decision Journal
    -> Backtest Result Package
```

The opening pipeline remains:

```text
RegimeDiscovery
    -> MarketCondition
    -> TradeStrategy
    -> OrderComposition
    -> RiskManagement
    -> OrderExecution
```

The position pipeline remains:

```text
PositionMonitor
    -> ExitPosition
    -> RiskManagement
    -> OrderExecution
```

Completed stages return valid business results. `Completed` does not mean that the next stage must run. Failed stages represent an inability to evaluate reliably and terminate the affected workflow without automatic retry.

---

## 7. Backtesting Modes

### 7.1 Historical replay

Replays recorded market and reference data exactly as available. This is the primary mode for strategy evaluation.

### 7.2 Historical scenario replay

Replays selected historical episodes such as:

- Trending rallies.
- Persistent selloffs.
- Volatility shocks.
- Whipsaw sessions.
- FOMC and Employment Situation windows.
- Low-volatility range periods.
- Expiration and roll periods.
- Liquidity degradation.

### 7.3 Declared shock overlay

Applies an explicit, auditable transformation to a historical base, for example:

- Underlying price gap.
- Implied-volatility shift.
- Skew steepening.
- Bid/ask widening.
- Quote delay.
- Partial-fill probability change.
- Broker disconnection.

Original and transformed values must both remain available. An overlay cannot masquerade as historical fact.

### 7.4 Parameter experiment

Executes multiple frozen policy variants, such as:

- Delta profiles.
- Wing widths.
- Qualification thresholds.
- Profit and time exits.
- Hedge activation and release thresholds.
- ES versus MES hedge policies.
- Combined loss limits.

### 7.5 Counterfactual comparison

Reuses identical market inputs to compare mutually exclusive decisions, for example:

- Hedge versus no hedge.
- ES versus MES hedge.
- Exit immediately versus hedge and continue.
- Neutral versus directional construction.

### 7.6 Deterministic regression

Runs a small fixed dataset and asserts exact decision hashes, event order, orders, fills, P&L, and reason codes after a code change.

---

## 8. Canonical Actors and Services

### 8.1 BacktestExperimentActor

Owns a collection of related runs.

Responsibilities:

- Validate experiment definition.
- Expand parameter matrices into individual run specifications.
- Assign run identities and seeds.
- Enforce training, validation, and test partitions.
- Start independent BacktestRun actors.
- Aggregate comparable results without merging run state.
- Emit experiment completion or failure.

### 8.2 BacktestRunActor

The event-sourced coordinator for one run.

Responsibilities:

- Validate the complete immutable specification.
- Freeze data, code, policy, scenario, cost, and execution identities.
- Coordinate run initialization.
- Start and stop virtual time.
- Track run state and terminal outcome.
- Reject duplicate starts.
- Record every attempt to begin a run.
- Ensure exactly one Completed or Failed terminal event.
- Never decide strategy actions itself.

### 8.3 SimulationClockActor

Owns virtual time and deterministic scheduling.

Responsibilities:

- Advance only to the next ordered replay or scheduled event.
- Publish current simulation time.
- Schedule qualification checkpoints, expirations, timeouts, and time exits.
- Prevent wall-clock time from entering domain decisions.
- Apply deterministic tie-breaking for identical timestamps.

### 8.4 MarketReplayActor

Reads the selected data partition and publishes canonical market events.

Responsibilities:

- Replay point-in-time instrument definitions.
- Replay ES/MES futures and option-chain data.
- Replay VIX, term structure, rates, calendars, and configured external inputs.
- Build coherent snapshot boundaries.
- Detect gaps, corrupt ordering, and unavailable inputs.
- Never synthesize missing data unless a declared scenario rule authorizes it.

### 8.5 ScenarioActor

Applies declared scenario overlays after raw data is read and before it reaches the trading system.

Responsibilities:

- Validate scenario transformations.
- Preserve original values.
- Apply transformations deterministically.
- Emit a scenario evidence record for every modified event.

### 8.6 SimulatedBrokerActor

Models broker and venue interaction.

Responsibilities:

- Accept broker-neutral approved orders.
- Apply latency, price, queue, combo-fill, partial-fill, rejection, cancel, and replace rules.
- Produce orders, fills, commissions, margin changes, exercise, assignment, and settlement events.
- Maintain broker-order identity.
- Act as simulated operational truth for positions and realized P&L.

### 8.7 SimulatedPortfolioLedgerActor

Owns simulated cash, margin, risk reservations, positions, and fund limits.

Responsibilities:

- Answer `CanOpenPosition` queries.
- Apply Portfolio/Fund mandates.
- Reserve and release risk.
- Consume fills from SimulatedBroker.
- Publish position and account state.
- Enforce maximum concurrent positions and monthly entry count.

### 8.8 DecisionJournalActor

Records immutable decision envelopes for all stages.

Responsibilities:

- Persist input references, policy versions, output, scores, reasons, and decision hash.
- Record NoTrade and rejection decisions with the same care as trades.
- Link opening and exit decisions to the resulting orders, fills, and P&L.

### 8.9 MetricsActor

Calculates incremental and final metrics from canonical events. It does not influence the run.

### 8.10 ResultPublisherActor

Builds the final result package after the run reaches a terminal state.

---

## 9. Deterministic Ordering

Every replayable item has a deterministic ordering key:

```text
SimulationTimestamp
SourcePriority
SourceSequence
InstrumentId
RecordTypePriority
StableTieBreaker
```

Initial source priority should be explicitly defined, for example:

1. Instrument-definition changes.
2. Session/status changes.
3. Market-data events.
4. Broker fills and acknowledgements.
5. Scheduled strategy triggers.
6. Position-monitor evaluations.
7. Actor timeouts.

The final implementation design may refine this ordering, but it must remain versioned and included in the run manifest.

Events with equal exchange timestamps must retain source sequence where available. If source sequence is unavailable, the data adapter must use a documented stable tie-breaker and mark the limitation.

---

## 10. Run Lifecycle

```text
Created
  -> Validating
  -> DataPreparing
  -> Ready
  -> Running
  -> Draining
  -> CalculatingResults
  -> Completed | Failed | Cancelled
```

### 10.1 Validation

The run cannot start until it validates:

- Specification schema and checksum.
- Data manifest and file checksums.
- Time coverage and required instruments.
- Point-in-time definitions.
- Strategy and policy identities.
- Portfolio/Fund mandate.
- Scenario compatibility.
- Execution and cost model.
- Metric definitions.
- Output destination.
- Random seed when stochastic models are enabled.

### 10.2 Ready

`Ready` means every required input is frozen and resolvable. It does not mean the strategy will find a trade.

### 10.3 Running

SimulationClock advances through ordered events until the configured end boundary. The run may contain zero, one, or many strategy opportunities depending on its specification.

### 10.4 Draining

At the end boundary, the run processes outstanding broker events and applies the configured end-of-run position policy:

- Require all positions already closed.
- Force liquidation using the execution model.
- Mark open positions without realizing them.

Official spread-strategy results should normally require liquidation or a natural strategy exit.

---

## 11. Backtest Specification Input

The authoritative `BacktestSpecification` includes:

```text
SpecificationId
SpecificationVersion
SpecificationChecksum
Name
Description
RunMode
StrategyScope
SimulationPeriod
WarmupPeriod
MarketDataManifestId
EventDataManifestId
PortfolioFundMandateSnapshotId
AllowedStrategySetId
RegimePolicyReferences
MarketConditionPolicyReferences
TradeStrategyPolicyReferences
OrderCompositionPolicyReferences
RiskPolicyReferences
ExitPositionPolicyReferences
ExecutionModelId
CostModelId
ScenarioSetId
MetricSetId
OutputPolicyId
RandomSeedPolicy
CodeVersion
CreatedBy
CreatedAt
```

No official run may resolve `latest` policy or data aliases at execution time. Every reference must identify a frozen version or content hash.

---

## 12. Strategy Scope Input

The strategy scope defines:

- Portfolio and Fund.
- Decision horizon.
- Allowed underlying instruments.
- Allowed option products.
- Strategy families.
- Strategy definitions.
- Maximum concurrent positions.
- Maximum entries per opportunity, month, or period.
- Qualification calendar.
- Entry and exit pipeline configuration.
- Hedge instruments.

For the initial monthly iron-condor strategy:

```text
Underlying: ES futures
Options: ES futures options
Hedges: ES and MES futures
TargetDte: 45
PreferredDteRange: 40–50
TimeExitDte: 21
MaximumMonthlyEntries: 1
QualificationCheckpoints: 2
ProfitTargetRatio: 0.50
NeutralShortDeltas: Put 0.16 / Call 0.16
BullishShortDeltas: Put 0.20 / Call 0.10
BearishShortDeltas: Put 0.10 / Call 0.20
InitialWingWidths: Put 50 / Call 50
```

These values are referenced from the strategy policy rather than duplicated as independent backtester logic.

---

## 13. Scenario Definition

A `BacktestScenarioDefinition` contains:

```text
ScenarioId
Version
Name
ScenarioType
BasePeriod
WarmupPeriod
RequiredData
EventWindows
Transformations
FaultInjections
ExpectedInvariants
Tags
Checksum
```

### 13.1 Scenario types

```text
HistoricalUnmodified
HistoricalWindow
HistoricalWithOverlay
ExecutionStress
DataQualityStress
BrokerFailure
SyntheticPath
```

### 13.2 Spread-strategy scenario catalog

The initial catalog should include:

- Quiet range with theta decay.
- Gradual bullish drift.
- Gradual bearish drift.
- Fast upside breakout threatening calls.
- Fast downside break threatening puts.
- Breakout followed by whipsaw.
- Multiple alternating directional changes.
- IV expansion without large underlying movement.
- Skew steepening.
- Volatility crush.
- Wide option markets.
- Sparse long-wing liquidity.
- Partial iron-condor fill.
- Hedge fill delay.
- ES hedge over-rounding versus MES precision.
- Gap through short strike.
- FOMC/NFP/CPI event window.
- Position reaching 21 DTE.
- Profit target reached before time exit.
- Combined loss-limit overshoot.

---

## 14. Market Data Requirements

### 14.1 Required datasets

The initial system should support:

- Point-in-time ES, MES, and ES option instrument definitions.
- ES and MES futures trades and top-of-book data.
- ES option top-of-book data for all subscribed historical strikes and expirations.
- Option volume and open interest when used by policy.
- VIX and configured VIX futures inputs.
- Interest-rate inputs required by Black-76.
- Trading calendars, holidays, sessions, and expiration rules.
- Scheduled macro events when used by Market Condition.

### 14.2 Point-in-time option universe

The backtester must reconstruct which strikes and expirations existed at each simulated time. It cannot load a final static option chain and expose strikes that were listed later.

### 14.3 Warmup

The warmup period must be long enough to construct every required indicator and regime feature without using precomputed future-aware values. Warmup events update state but cannot place trades unless explicitly allowed.

### 14.4 Data fitness

Each run reports:

- Missing intervals.
- Stale quote duration.
- Crossed or invalid markets.
- Definition gaps.
- Timestamp anomalies.
- Unavailable external inputs.
- Percentage of opportunities invalidated by data quality.

Official results cannot silently forward-fill mandatory option quotes.

---

## 15. Snapshot Semantics

Every pipeline decision consumes an immutable `BacktestMarketSnapshot` containing:

```text
SnapshotId
SimulationTime
SourceWatermarks
UnderlyingState
OptionChainState
VolatilityState
RateState
EventCalendarState
SessionState
DataQualityState
ScenarioEvidence
Checksum
```

Multi-leg strategies require a coherent snapshot. Each option leg records its quote timestamp and age. A candidate is invalid when any required leg exceeds the configured age or quality limit.

The snapshot must never incorporate an event with an availability timestamp later than the decision time.

---

## 16. Production Pipeline Reuse

### 16.1 Regime Discovery

Receives historical snapshots and emits the same typed result used in production, including parameter version, confidence, evidence, and summary.

### 16.2 Market Condition

Evaluates historical tradeability, liquidity, option surface, skew, event, and session state. `NotTradeable` is a successful result and must be recorded.

### 16.3 TradeStrategy

First queries SimulatedPortfolioLedger with `CanOpenPosition`. It then applies the same allowed strategy set, deterministic eligibility, ranking, and NoTrade rules as production.

### 16.4 Order Composition

Uses the same family-owned, versioned composition policy and Black-76 implementation. It receives a coherent historical option snapshot and returns the same broker-neutral candidate.

### 16.5 Risk Management

Uses simulated account, fund, margin, and position state. It approves or rejects the unchanged candidate and creates a run-scoped risk reservation.

### 16.6 PositionMonitor

Revalues positions after accepted option-leg, hedge, fill, time, regime, or Market Condition changes and publishes `PositionTradePlanUpdated`.

### 16.7 ExitPosition

Uses the same profit, time, hedge, whipsaw, combined-loss, and emergency policies as production. It never receives future path information.

---

## 17. Simulated Execution

### 17.1 Execution model profiles

At minimum support:

```text
ConservativeExecutable
StressedExecutable
DiagnosticMidpointOnly
```

`DiagnosticMidpointOnly` cannot be used for official profitability claims.

### 17.2 Multi-leg option entry and exit

The default iron-condor and vertical model treats the order as an atomic combo with one net limit price. It must model:

- Order submission latency.
- Market movement during latency.
- Net bid/ask and leg-market validation.
- Price improvement rules.
- Fill probability or deterministic fill threshold.
- Partial combo fills if supported by the selected model.
- Cancel/replace.
- Order expiration and rejection.

The model cannot assume that all legs fill independently at favorable prices.

### 17.3 ES/MES futures hedge execution

Model:

- Contract multiplier from point-in-time definitions.
- Bid/ask spread.
- Submission and acknowledgement latency.
- Marketable versus passive orders.
- Partial fills.
- ES/MES rounding and residual exposure.
- Commissions and exchange fees.
- Overnight session changes.

### 17.4 Fill-model determinism

Pure deterministic models produce the same fill from identical inputs. A stochastic model must use a stored seed and record every random draw. Official comparisons must use common random numbers where possible.

### 17.5 Broker truth

Only SimulatedBroker fill, exercise, assignment, expiration, and settlement events change simulated positions and realized P&L.

---

## 18. Spread-Specific Domain Requirements

The initial backtester must understand:

- Calls and puts.
- Long and short legs.
- Contract ratios.
- Vertical spreads.
- Iron condors.
- Equal and asymmetric wings.
- Futures-option exercise style.
- Underlying futures mapping.
- Expiration and settlement.
- Net credit and debit.
- Maximum defined risk.
- Aggregate delta, gamma, theta, vega, and first-order Greeks supported by the production pricer.
- Executable combination P&L.
- ES/MES hedge overlays.
- Position-level profit, loss, and time exits.

Every spread position must retain leg-level and aggregate state. Aggregate P&L alone is insufficient for reconstruction and debugging.

---

## 19. Decision Inputs and Outputs

This section defines the minimum business contract needed to understand every backtest decision.

| Decision | Required inputs | Authoritative output |
|---|---|---|
| Qualification | Clock, calendar, Portfolio/Fund capacity, regime, Market Condition, policy | Qualified, Deferred, NoTrade, or Failed with reasons |
| Strategy selection | Allowed strategy set, regime, Market Condition, fund mandate | Selected strategy definition or NoTrade |
| Order composition | Selected strategy, composition policy, option snapshot, pricer | Exact broker-neutral candidate or NoCandidate |
| Risk approval | Candidate, fund/account state, reservations, risk policy | Approved reservation or Rejected |
| Entry execution | Approved candidate, market state, fill model | Orders, fills, rejection, timeout, or cancellation |
| Position monitoring | Leg/hedge state, market snapshot, entry/current regime and condition | Versioned PositionTradePlan |
| Hedge decision | PositionTradePlan, ExitPosition policy, account state | Open/resize/close ES/MES hedge, maintain, or ExitAll |
| Profit/time exit | PositionTradePlan, profit target, DTE | Maintain or ExitAll |
| Emergency exit | Combined risk, data/execution integrity, fund limits | Coordinated liquidation instruction |

Every decision output includes:

```text
RunId
WorkflowId
StageInvocationId
SimulationTime
InputSnapshotIds
PolicyIdsAndVersions
Outcome
Scores
ReasonCodes
DeterministicSummary
DecisionHash
```

---

## 20. Commands

### 20.1 Experiment and run commands

```text
CreateBacktestExperimentCommand
StartBacktestExperimentCommand
CreateBacktestRunCommand
ValidateBacktestRunCommand
StartBacktestRunCommand
CancelBacktestRunCommand
FinalizeBacktestRunCommand
```

### 20.2 Simulation commands

```text
InitializeSimulationClockCommand
AdvanceSimulationClockCommand
ScheduleSimulationEventCommand
StartMarketReplayCommand
ApplyScenarioTransformationCommand
CompleteMarketReplayCommand
```

### 20.3 Simulated broker commands

```text
SubmitSimulatedOrderCommand
CancelSimulatedOrderCommand
ReplaceSimulatedOrderCommand
ProcessSimulatedExpirationCommand
ForceLiquidateRunCommand
```

Production pipeline commands remain unchanged where practical. A backtest context envelope identifies the run and virtual clock without changing the business meaning of the command.

---

## 21. Events

### 21.1 Run lifecycle events

```text
BacktestRunCreatedEvent
BacktestRunValidationCompletedEvent
BacktestRunValidationFailedEvent
BacktestRunStartedEvent
BacktestRunCancelledEvent
BacktestRunCompletedEvent
BacktestRunFailedEvent
```

### 21.2 Replay events

```text
SimulationTimeAdvancedEvent
HistoricalMarketEventReplayedEvent
HistoricalDefinitionChangedEvent
ScenarioTransformationAppliedEvent
ReplayDataGapDetectedEvent
MarketSnapshotPublishedEvent
MarketReplayCompletedEvent
```

### 21.3 Execution events

```text
SimulatedOrderAcceptedEvent
SimulatedOrderRejectedEvent
SimulatedOrderPartiallyFilledEvent
SimulatedOrderFilledEvent
SimulatedOrderCancelledEvent
SimulatedOrderExpiredEvent
SimulatedExerciseAssignedEvent
SimulatedPositionSettledEvent
```

### 21.4 Result events

```text
DecisionRecordCreatedEvent
MetricUpdatedEvent
RunMetricsCompletedEvent
BacktestResultPackageCreatedEvent
```

Production-domain events generated by reused actors retain their production meaning and include BacktestRunId in trace context or the backtest envelope.

---

## 22. Queries

Queries are read-only and do not alter simulation order:

```text
GetBacktestExperimentQuery
GetBacktestRunStateQuery
GetBacktestRunProgressQuery
GetSimulationClockQuery
GetDecisionJournalQuery
GetSimulatedOrdersQuery
GetSimulatedFillsQuery
GetSimulatedPositionsQuery
GetTradeLifecycleQuery
GetRunMetricsQuery
GetBacktestResultManifestQuery
CompareBacktestRunsQuery
```

Query projections may be eventually consistent. Actor state and canonical event streams remain authoritative.

---

## 23. Output Result Package

Every completed run produces a `BacktestResultPackage` containing:

```text
RunManifest
DataQualityReport
DecisionJournal
OpportunityJournal
NoTradeJournal
OrderJournal
FillJournal
PositionJournal
HedgeJournal
PnLSeries
RiskSeries
MetricResults
ScenarioEvidence
FailureAndWarningReport
ReproducibilityManifest
HumanReadableSummary
```

### 23.1 Run manifest

Includes:

- Specification and checksum.
- Code commit or build identity.
- Data manifests and checksums.
- Policy identities and versions.
- Scenario and transformations.
- Execution and cost models.
- Seed.
- Ordering-policy version.
- Start and end virtual times.
- Host/runtime metadata required for reproducibility.

### 23.2 Opportunity journal

Records every scheduled or market-triggered opportunity, including those blocked before order composition.

### 23.3 Trade lifecycle

One lifecycle links:

```text
Opportunity
-> Regime
-> Market Condition
-> Portfolio/Fund capacity
-> Strategy selection
-> Composition
-> Risk
-> Entry orders and fills
-> PositionTradePlans
-> Hedge decisions and fills
-> Exit decision and fills
-> Final P&L and metrics
```

---

## 24. Metrics

### 24.1 Financial metrics

- Net and gross P&L.
- Return on maximum risk.
- Return on allocated capital.
- Win rate and payoff ratio.
- Profit factor.
- Maximum drawdown.
- Expected shortfall/CVaR.
- Worst trade and worst clustered loss.
- Monthly and annualized results where statistically meaningful.

### 24.2 Spread metrics

- Opening credit and realized retained credit.
- Maximum adverse and favorable excursion.
- Short-strike breach frequency.
- Profit-target frequency.
- 21-DTE exit frequency.
- Expiration-selection distribution.
- Delta-profile and wing-width results.
- Results by neutral, bullish, and bearish construction.

### 24.3 Hedge metrics

- Hedge eligibility and activation frequency.
- ES versus MES usage.
- Initial and maximum hedge ratio.
- Hedge duration.
- Hedge P&L.
- Tail-loss reduction.
- Whipsaw loss introduced by hedge.
- Combined loss-limit overshoot.
- Counterfactual P&L without hedge.
- Number of hedge episodes.

### 24.4 Decision metrics

- Opportunities evaluated.
- Qualified, deferred, NoTrade, NoCandidate, rejected, and failed counts.
- Reason-code distribution.
- Score distributions.
- Policy gate sensitivity.
- Regime and Market Condition transition results.

### 24.5 Execution metrics

- Fill rate.
- Time to fill.
- Slippage versus decision price.
- Cancel/replace count.
- Partial-fill exposure.
- Commission and fee burden.
- Stress-exit friction.
- Option-versus-futures execution contribution.

### 24.6 Statistical reporting

Report sample size, confidence intervals or bootstrap intervals where applicable, parameter count, and partition identity. Do not present an annualized percentage without the underlying sample and period.

---

## 25. Parameter Experiments

An experiment defines:

```text
BaseSpecification
ParameterDimensions
AllowedValues
TrainingPeriod
ValidationPeriod
TestPeriod
SelectionMetric
RiskConstraints
MaximumRuns
SeedPolicy
```

Rules:

- Parameter values must be declared before the experiment starts.
- The test partition cannot select parameters.
- Failed and NoTrade runs remain in results.
- A parameter set violating a risk constraint cannot win on P&L alone.
- Prefer broad stable regions over a single sharp optimum.
- Promotion creates a new policy version; it never mutates the tested version.

---

## 26. Backtest Specification Skill Design

The recommended Codex workflow uses a repository skill named conceptually:

```text
.codex/skills/trading-backtest/SKILL.md
```

The skill does not contain one specific trading strategy. It defines how Codex converts an approved strategy specification into validated backtest artifacts and implementation work.

### 26.1 Skill modes

```text
author-spec
validate-spec
generate-implementation
generate-scenarios
generate-tests
review-results
```

### 26.2 Required skill inputs

- This backtesting-system specification.
- The selected strategy specification.
- Current command/event/query catalog.
- Production actor and pipeline contracts.
- Market-data schemas.
- Portfolio/Fund mandate schema.
- Broker-neutral order schema.
- Pricing and Greeks interfaces.
- Repository coding and test standards.
- Target dataset manifest.

### 26.3 Skill rules

The skill must instruct Codex to:

1. Read all referenced specifications before generating code.
2. Identify unresolved business rules explicitly.
3. Never invent financial thresholds, fill behavior, or data fields.
4. Preserve production workflow ordering and responsibility boundaries.
5. Reuse production deterministic components through interfaces.
6. Replace only environment-facing adapters.
7. Generate strongly typed configuration and messages.
8. Generate deterministic unit, integration, replay, and regression tests.
9. Record policy and schema versions in generated artifacts.
10. Prevent look-ahead in every data adapter.
11. Make NoTrade, rejection, and failure testable outcomes.
12. Keep official and exploratory run modes separate.
13. Produce a requirements-to-code traceability matrix.
14. Stop and request clarification when an unresolved rule changes financial behavior materially.

### 26.4 Generated specification bundle

Before code generation, the skill should produce:

```text
BacktestSpecification.yaml
ScenarioCatalog.yaml
ExecutionModel.yaml
CostModel.yaml
MetricSet.yaml
MessageCatalog.md
AcceptanceTestCatalog.md
RequirementsTraceability.md
```

The YAML names are illustrative; the implementation design may choose JSON or strongly typed configuration, but a human-readable canonical form is required.

### 26.5 Generated implementation bundle

After validation, Codex may generate:

```text
Domain contracts
Actor commands/events/queries
Run coordinator
Simulation clock
Market replay adapters
Scenario transformations
Simulated broker
Portfolio ledger
Decision journal
Metrics calculators
Result package writer
Unit tests
Integration tests
Golden replay tests
Sample run configuration
Implementation README
```

Generated code must compile before the skill reports completion. Tests required by the selected mode must run and their results must be included in the handoff.

---

## 27. Example Declarative Backtest Specification

The following is conceptual and intentionally implementation-neutral:

```yaml
backtest:
  name: monthly-es-iron-condor-hedge-baseline
  mode: official
  period:
    start: 2018-01-01
    end: 2026-01-01
    warmupTradingDays: 250

strategy:
  specification: monthly-es-iron-condor-futures-hedge-v0.2
  portfolioFundMandate: monthly-income-fund-v1
  maximumMonthlyEntries: 1

policies:
  regimeDiscovery: regime-policy-v1
  marketCondition: es-options-monthly-v1
  tradeStrategy: monthly-iron-condor-selection-v1
  orderComposition: monthly-iron-condor-50-wide-v1
  risk: monthly-fund-risk-v1
  exitPosition: monthly-iron-condor-hedge-v1

data:
  manifest: es-options-point-in-time-2017-2026-v1
  macroCalendar: macro-events-point-in-time-v1

execution:
  model: conservative-combo-v1
  costs: ibkr-cme-estimate-v1
  liquidationAtEnd: required

scenario:
  set: historical-unmodified

metrics:
  set: spread-and-hedge-core-v1

reproducibility:
  seed: 1001
  orderingPolicy: deterministic-order-v1
```

All identifiers must resolve to immutable versions before the run reaches `Ready`.

---

## 28. Validation and Testing

### 28.1 Unit tests

- Virtual clock ordering.
- Point-in-time visibility.
- Scenario transformation.
- Option-chain reconstruction.
- Combo pricing.
- Commission and slippage.
- ES/MES hedge sizing.
- Position P&L.
- Exercise and settlement.
- Metric calculations.

### 28.2 Actor contract tests

- Exactly one terminal event per invocation.
- No automatic retry.
- Duplicate command handling.
- State recovery.
- Cancel behavior.
- Query non-interference.

### 28.3 Pipeline tests

- Tradeable to completed trade.
- NotTradeable to normal stop.
- NoTrade and NoCandidate.
- Risk rejection.
- Partial entry fill.
- Hedge activation and release.
- Whipsaw.
- Emergency combined exit.
- End-of-run liquidation.

### 28.4 Look-ahead tests

- Future strike definitions are unavailable.
- Later quote updates cannot enter prior snapshots.
- Revised macro data is unavailable before revision time.
- Later policy versions cannot affect earlier runs.
- Test partition cannot affect parameter selection.

### 28.5 Golden replay tests

A small immutable dataset must produce exact expected:

- Event sequence.
- Decision hashes.
- Orders and fills.
- Position states.
- P&L.
- Metrics.
- Result manifest hash.

---

## 29. Storage and Reproducibility

### 29.1 Immutable inputs

Store content hashes for:

- Market data partitions.
- Instrument definitions.
- Event calendars.
- Strategy policies.
- Scenario definitions.
- Execution and cost models.
- Code/build identity.

### 29.2 Run identity

Use a stable `BacktestRunId`, preferably GUID v7 for operational ordering, plus a deterministic `RunFingerprint` derived from immutable inputs. Two runs with identical fingerprints should be comparable as equivalent specifications even if their operational run IDs differ.

### 29.3 Event storage

The backtest event stream records orchestration and business decisions. Raw historical ticks remain in the market-data store and are referenced by manifest and sequence; they should not be duplicated into the run event stream.

### 29.4 Checkpoints

Long runs may create deterministic replay checkpoints containing actor snapshots and source watermarks. Resuming from a checkpoint must produce the same final result as replaying from the beginning.

---

## 30. Performance Model

Correctness and determinism take priority over single-run speed.

Performance strategy:

- Stream market data rather than loading all records into managed memory.
- Use the existing high-performance tick and option-chain components where safe.
- Update only instruments and Greeks affected by a replay event.
- Avoid actor messages for every raw tick when production also aggregates them.
- Reuse cached immutable definitions and policies.
- Parallelize independent runs across CPU cores.
- Keep one run assigned to a deterministic logical partition.
- Bound result-detail retention through output policy without discarding required decision evidence.

The run must report simulated-time throughput, wall-clock duration, event counts, queue depth, and slowest stage.

---

## 31. Observability and Failure Handling

Every run emits OpenTelemetry-compatible traces, metrics, and structured logs linked by ExperimentId, RunId, WorkflowId, and StageInvocationId.

Track:

- Events replayed.
- Simulation-time progress.
- Stage latency.
- Actor mailbox depth.
- Invalid and stale snapshots.
- Opportunities and decisions.
- Orders and fills.
- Data gaps.
- Scenario transformations.
- Warnings and failures.

Failure principles:

- No automatic actor retry loops.
- A failed production-equivalent pipeline remains failed.
- Infrastructure restart may resume only from a verified deterministic checkpoint.
- Data gaps follow explicit policy: fail run, invalidate opportunity, or continue with warning.
- A warning cannot be silently promoted to valid data.

---

## 32. Governance

Backtest results may recommend a new strategy-policy version but cannot activate it.

Promotion workflow:

```text
Exploratory result
-> reviewed candidate
-> out-of-sample validation
-> implementation/regression validation
-> paper trading
-> explicit policy approval
-> production activation
```

Every promoted parameter must trace to:

- Source experiment.
- Training and validation periods.
- Test results.
- Risk constraints.
- Reviewer decision.
- New immutable policy version.

---

## 33. Phased Delivery

### Phase 1: deterministic monthly iron-condor replay

- One ES monthly iron-condor strategy.
- Historical qualification checkpoints.
- Point-in-time option chain.
- Conservative atomic-combo fills.
- Profit and 21-DTE exits.
- No hedge baseline plus ES/MES hedge variant.
- Core metrics and journals.

### Phase 2: scenario and experiment framework

- Whipsaw, shock, liquidity, and execution scenarios.
- Parameter matrices.
- Counterfactual comparisons.
- Walk-forward partitions.
- Golden replay suite.

### Phase 3: weekly vertical spreads

- Strategy-specific wing widths.
- Weekly qualification and exits.
- Monthly-loss directional handoff research.

### Phase 4: portfolio-level testing

- Multiple funds and strategy families.
- Cross-fund capital and risk.
- Concurrent positions.
- Portfolio drawdown and allocation policies.

### Phase 5: offline learning assistance

- Feature datasets.
- Offline model or reinforcement-learning research.
- Policy recommendations inside hard safety bounds.
- No autonomous live activation.

---

## 34. Acceptance Criteria for V1

V1 is acceptable when:

1. A frozen run is reproducible from its manifest.
2. Identical inputs and code produce identical decision and result hashes.
3. No future data is visible to any decision.
4. The production strategy actors or their deterministic components are reused.
5. The virtual clock fully replaces wall-clock decision time.
6. Point-in-time option definitions and chains are reconstructed correctly.
7. The simulated broker supports atomic multi-leg entry and exit.
8. ES and MES hedge orders, fills, and P&L are represented correctly.
9. Portfolio/Fund capacity, risk reservation, and one-entry-per-month rules work.
10. NoTrade, NoCandidate, rejection, failure, and trade outcomes are all journaled.
11. PositionMonitor and ExitPosition receive no future path information.
12. Profit target, 21-DTE exit, hedge, whipsaw, and combined-loss scenarios pass tests.
13. Costs and conservative execution are included in official results.
14. The result package explains every trade from opportunity through final P&L.
15. The generated implementation compiles and passes the required test catalog.

---

## 35. Initial Implementation Recommendation

The first implementation should not begin with a generic Monte Carlo engine or optimizer. It should replay one monthly ES iron-condor strategy end to end through the real business pipeline using a virtual clock, point-in-time option chain, simulated broker, and simulated fund.

The first comparison should contain:

1. Iron condor without a hedge.
2. Iron condor with a direct combined-loss exit.
3. Iron condor with MES hedge.
4. Iron condor with ES hedge.
5. Iron condor with mixed ES/MES hedge.

This establishes whether the trading pipeline, exit logic, and hedge reduce tail loss before the platform expands into broad parameter optimization.

---

## 36. Summary

The backtesting system is a deterministic actor-based simulation host for the existing trading system. A declarative, immutable Backtest Specification selects historical data, strategy and policy versions, Portfolio/Fund mandates, scenarios, execution assumptions, costs, metrics, and outputs. SimulationClock and MarketReplay replace live time and feeds; SimulatedBroker and SimulatedPortfolioLedger replace IBKR and the live account. Production decision logic remains authoritative.

Every opportunity—including NoTrade—must be reconstructable. Every order and fill must trace back to a versioned decision. Every result must identify its data, policies, code, scenario, costs, seed, and ordering rules. A repository `SKILL.md` then gives Codex a controlled process to author specifications, validate missing rules, generate implementation code and tests, and deliver compile-verified artifacts without silently inventing trading behavior.

---

## 37. Related Specifications

- Monthly ES Iron Condor with ES/MES Futures Hedge Strategy Specification.
- MarketCondition High-Level Design v0.3.
- TradeSelection High-Level Design.
- OrderComposition High-Level Design.
- Intrinsic Time Strategy Workflow Design.

