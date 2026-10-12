# Monthly ES Iron Condor with ES/MES Futures Hedge

## Strategy Design and Implementation Specification

**Version:** 0.2  
**Status:** Initial backtesting and paper-trading specification  
**Primary instrument:** E-mini S&P 500 futures options  
**Hedge instruments:** ES and MES futures  
**Strategy horizon:** Monthly, normally entered at 40–50 DTE  
**Maximum frequency:** One monthly iron-condor position per calendar month

---

## 1. Purpose

This specification defines a conservative, algorithmic monthly ES futures-options iron-condor strategy with temporary directional hedging through ES or MES futures.

The strategy does not attempt to predict the market. It reacts deterministically to the market regime, option-chain conditions, position risk, directional persistence, loss velocity, and whipsaw risk observed during the life of the position.

The strategy seeks to:

1. Enter at most one liquid 40–50 DTE ES iron condor per month.
2. Support neutral, moderately bullish, and moderately bearish constructions.
3. Capture approximately 50% of net opening credit when conditions permit.
4. Exit no later than the configured time exit, initially 21 DTE.
5. Protect a deteriorating position with a temporary ES or MES futures hedge when a confirmed directional threat exists.
6. Avoid hedging losses caused primarily by implied-volatility expansion, poor quotes, or likely whipsaw.
7. Exit the complete position when the combined trade thesis or risk limit fails.
8. Keep every production decision deterministic, observable, explainable, versioned, and reproducible.

All numerical values in this document are starting parameters for backtesting. They are not assumed to be optimal.

---

## 2. Strategy Principles

### 2.1 Core thesis

The strategy sells defined-risk ES option premium when implied volatility, market regime, expected range, liquidity, and portfolio limits support a monthly iron condor. Its expected edge must come from selective volatility selling and position construction—not from the futures hedge.

The hedge is a short-lived loss-distribution control intended to reduce damage during a confirmed directional continuation. It is not intended to:

- Convert every losing iron condor into a winner.
- Eliminate all tail risk.
- Maintain continuous delta neutrality.
- Justify holding a position whose original thesis has failed.
- Guarantee the configured maximum loss during gaps or illiquid markets.

### 2.2 Bounded flexibility

The strategy may adapt strike deltas, directional bias, wing widths, hedge instrument, and hedge ratio, but only inside versioned policy limits. Hard risk rules always override scores and forecasts.

### 2.3 Maximum monthly frequency

Only one monthly iron condor may be opened in a calendar month. If the position is closed for a loss, the monthly strategy cannot re-enter that month.

### 2.4 No live self-modification

Backtesting, offline optimization, or later reinforcement learning may propose new parameter sets. A reviewed, versioned policy must be activated before production uses those values. The live strategy must not change its own limits.

---

## 3. Scope

### 3.1 Included

- ES futures options.
- Neutral and bounded directional iron condors.
- Two monthly qualification checkpoints.
- Expiration and option-chain liquidity qualification.
- Black-76 pricing, implied volatility, and first-order Greeks.
- Position monitoring using executable liquidation values.
- ES or MES futures hedging.
- Hedge activation, resizing, release, and maximum duration.
- Whipsaw, shock, volatility, liquidity, and regime-transition handling.
- Coordinated option-and-futures exit workflows.
- Backtesting, paper-trading, observability, and acceptance requirements.

### 3.2 Excluded from V1

- Continuous delta-neutral market making.
- Rolling individual iron-condor legs.
- Adding contracts to a losing iron condor.
- Multiple hedge episodes unless enabled after testing.
- Overnight discretionary hedge renewal.
- Autonomous reinforcement-learning decisions in production.
- Automatic weekly recovery trades.
- Unbounded retry loops.

---

## 4. Terminology

| Term | Meaning |
|---|---|
| `C0` | Net opening credit after estimated entry costs |
| `ExecutablePnL` | P&L calculated from prices reasonably available to liquidate the position, including costs |
| `ForwardLoss` | Deterministic projection of adverse position loss under the configured forward scenarios |
| `ICLossRatio` | Iron-condor executable loss divided by `C0` |
| `HedgeLossRatio` | Futures-hedge executable loss divided by `C0` |
| `CombinedLossRatio` | Executable loss of iron condor plus hedge divided by `C0` |
| `ThreatenedSide` | Put side during a decline or call side during a rally |
| `HedgeEpisode` | One continuous interval from initial futures-hedge entry until the hedge returns to zero |
| `QualificationCheckpoint` | A scheduled opportunity to evaluate a monthly entry |
| `NormalLossLimit` | Configured normal-market combined exit target; not a guaranteed fill price |
| `EmergencyRiskLimit` | Higher absolute fund limit used when normal liquidation cannot complete |

---

## 5. System Integration

The strategy uses the existing opening pipeline:

```text
RegimeDiscovery
    -> MarketCondition
    -> TradeStrategy
    -> CandidateBuilder / OrderComposer
    -> PortfolioRisk / RiskManager
    -> OrderExecution
```

The position-management pipeline is:

```text
PositionMonitor
    -> ExitPosition
    -> RiskManager
    -> OrderExecution
```

Responsibilities are separated as follows:

| Component | Responsibility |
|---|---|
| RegimeDiscovery | Trend, volatility, intrinsic-time, and fused regime |
| MarketCondition | Liquidity, option surface, session, event, range, and market-quality features |
| Portfolio/Fund | Position-capacity mandate, permitted strategy family, capital allocation, and risk hints |
| TradeStrategy | First applies the Portfolio/Fund capacity gate, then selects Neutral/Bullish/Bearish or No Trade |
| OrderComposer | Applies the selected composition profile to expiration, strikes, 50-point wings, credit, Greeks, and executable candidates |
| RiskManager | Final authority for entry, hedge, resizing, and exit |
| PositionMonitor | Revalues the position and publishes a versioned `PositionTradePlan` after accepted option-leg state changes |
| ExitPosition | Owns profit, time, thesis, futures-hedge, combined-loss, and emergency-exit decisions |
| OrderExecution | Coordinated options/futures orders, fills, cancel/replace, and partial-fill handling |

Market-data ticks and option quotes remain in the high-performance data plane. Actors consume versioned snapshots and material state changes; they do not receive an actor message for every quote.

Regime Discovery and Market Condition always run before TradeStrategy, even when the fund cannot open another position. Their completed outputs are persisted and remain available to PositionMonitor and ExitPosition for existing positions. The first action inside TradeStrategy is the Portfolio/Fund capacity query. A negative response returns `NoTrade` before strategy selection or order composition.

---

## 6. Monthly Opportunity and Qualification

### 6.1 Schedule

Default starting schedule:

```text
Opportunity start:       First Friday of calendar month
Checkpoint 1:            First Friday or next eligible trading session
Checkpoint 2:            Five trading sessions after Checkpoint 1
Maximum window:          Ten trading sessions from opportunity start
Maximum entries:         One
```

The schedule is configurable and trading-calendar aware.

### 6.2 Checkpoint processing

At each checkpoint the workflow must:

1. Complete Regime Discovery.
2. Complete Market Condition.
3. Begin TradeStrategy and query Portfolio/Fund with `CanOpenPosition`.
4. Return `NoTrade` immediately when Portfolio/Fund responds negatively.
5. Discover every eligible expiration at the actual evaluation time.
6. Rank expirations by DTE and liquidity.
7. Select Neutral, Moderately Bullish, Moderately Bearish, or No Trade.
8. Compose bounded iron-condor candidates using the selected profile and Portfolio/Fund hints.
9. Apply fund, portfolio, and execution risk.
10. Open only after final Risk Manager approval.

If Checkpoint 1 does not qualify, the opportunity remains available. If Checkpoint 2 does not qualify, or the maximum window expires, the monthly opportunity completes as `NoTrade`.

### 6.3 Portfolio/Fund capacity gate

TradeStrategy must make this query before selecting a trade:

```text
CanOpenPosition(
    FundId,
    PortfolioId,
    StrategyFamily = MonthlyIronCondor,
    OpportunityId,
    QualificationCheckpoint,
    RequestedDteRange)
```

The response includes:

```text
CanOpen
ReasonCodes
AvailableRiskBudget
MaximumContracts
MaximumDefinedLoss
MaximumGrossRisk
AllowedInstruments
AllowedExpirations
PermittedBiases
CompositionHints
```

`CanOpen = false` is a mandatory `NoTrade`. TradeStrategy cannot override it. A positive response supplies constraints and hints; it does not approve the final order. RiskManager retains final authority after composition.

### 6.4 Initial Regime Discovery qualification values

These are V1 backtesting baselines. They are deliberately broad enough to permit neutral and moderately directional iron condors while rejecting strong, unstable, or extreme regimes.

| Regime feature | Neutral profile | Bullish profile | Bearish profile | Hard rejection starting point |
|---|---|---|---|---|
| Fused regime | Range-bound or neutral | Moderately bullish | Moderately bearish | Strong trend, unstable transition, or extreme |
| Fusion confidence | At least 0.60 | At least 0.60 | At least 0.60 | Below 0.50; 0.50–0.60 is deferred |
| ADX(14) | Prefer at or below 22 | Approximately 18–30 with positive trend confirmation | Approximately 18–30 with negative trend confirmation | Above 30 unless later backtesting establishes a safe exception |
| RSI(14) | Approximately 45–55 | Approximately 50–65 | Approximately 35–50 | Below 30 or above 70 without explicit override policy |
| EMA20 slope | Flat within configured ATR-normalized tolerance | Positive but not accelerating beyond moderate threshold | Negative but not accelerating beyond moderate threshold | Abrupt slope acceleration or conflicting EMA structure |
| EMA alignment | Mixed/flat or price near EMA20 | EMA20 supportive above slower reference | EMA20 supportive below slower reference | Strong adverse EMA20/50/200 alignment |
| Bollinger state | Price inside bands; prefer central range | Above EMA20 but not persistently outside upper band | Below EMA20 but not persistently outside lower band | Confirmed band walk, expanding breakout, or failed data |
| Bollinger width percentile | Prefer approximately 20th–75th percentile | Same, with bounded expansion | Same, with bounded expansion | Extreme squeeze with breakout risk or extreme expansion |
| Intrinsic-time persistence | Low to moderate, balanced | Moderate positive persistence | Moderate negative persistence | High adverse persistence or rapidly alternating instability |
| Volatility regime | Low, normal, or controlled high | Low, normal, or controlled high | Low, normal, or controlled high | Extreme; VIX at or above 30 remains No Trade |
| VIX term structure | Contango or mild/controlled backwardation | Same | Same | Severe backwardation or rapid deterioration |

No individual soft indicator selects the trade. The fused result and hard gates govern eligibility; the underlying features determine the profile and score.

### 6.5 Initial Market Condition qualification values

| Market-condition feature | Initial qualification baseline |
|---|---|
| Market Condition confidence | At least 0.65; 0.55–0.65 may defer; below 0.55 rejects |
| DTE | Prefer 40–50 with 45 target; hard research range 35–55 |
| Session | Prefer the configured liquid ES options execution session |
| Chain readiness | Selected expiration, underlying, all candidate shorts, and every permitted 50-point wing must be ready |
| Quote freshness | Candidate quotes must pass the configured live-session age threshold; initial research target is no older than 5 seconds |
| Executable pricing | Use bid/ask liquidation and entry prices; midpoint is analytic only |
| Combination friction | Estimated entry plus stressed exit friction must remain below the configured percentage of opening credit; initial research ceiling 10% |
| Displayed liquidity | Must support the proposed child-order size without assuming the entire monthly position fills at one level |
| IV versus forecast realized volatility | Positive volatility edge required; initial research preference is at least 10% relative edge or 2 volatility points, subject to whichever policy proves more stable |
| Volatility surface | Complete and internally consistent around all candidate legs |
| Skew | Within backtested bounds; no surface dislocation or isolated bad quote |
| Expected range | Both short strikes outside the applicable technical/implied range unless a bounded directional profile explicitly permits otherwise |
| Event proximity | No new entry inside the configured FOMC, Employment Situation, CPI, or equivalent blackout; initial research value is 24 hours |
| Market quality | No halt, abnormal spread expansion, stale underlying, feed degradation, or broker-contract mismatch |

These values form the initial `SemiMonthlyQualificationPolicy`. Backtesting must report results for the hard gates separately from the weighted qualification score so that a high aggregate score can never conceal a failed safety requirement.

---

## 7. Expiration Qualification

### 7.1 DTE policy

Initial research values:

```text
TargetDte:       45
PreferredMinDte: 40
PreferredMaxDte: 50
HardMinDte:      35
HardMaxDte:      55
TimeExitDte:     21
```

Expiration selection must be repeated at every checkpoint. An expiration selected one or two weeks earlier must not be assumed still suitable.

### 7.2 Liquidity score

Each eligible expiration receives an `ExpirationQualityScore` from normalized components:

```text
DteSuitability
CandidateStrikeBidAskQuality
QuoteFreshness
DisplayedSize
Volume
OpenInterest
StrikeCoverage
FourLegExecutableCredit
EstimatedStressExitCost
```

An expiration must meet every hard minimum even if its aggregate score is high.

### 7.3 Expiration mechanics

Quarterly, serial, weekly, and end-of-month contracts may have different exercise and settlement behaviour. The chosen option expiration, exercise style, settlement process, and underlying ES futures contract must be stored with the candidate and validated before order submission.

---

## 8. Entry Eligibility

The initial mandatory entry gates are:

```text
Monthly opportunity is active
No monthly position has opened this month
Expiration is inside configured DTE limits
Option-chain snapshot is ready and fresh
VIX is below the configured no-trade threshold
Regime is not extreme or unstable
Directional strength is compatible with an iron condor
No prohibited event or session condition exists
Minimum executable credit is available
Maximum loss and stress loss fit fund limits
Bid/ask, size, and exit-liquidity requirements pass
Risk Manager approves
```

Failure of a mandatory gate produces `NoTrade` or `Deferred`, never an automatic relaxation of limits.

---

## 9. Directional Construction

### 9.1 Strategy classifications

```text
NeutralIronCondor
ModeratelyBullishIronCondor
ModeratelyBearishIronCondor
NoTrade
```

A strong directional or unstable regime normally produces `NoTrade` rather than an extremely biased condor.

### 9.2 Initial short-delta profiles

| Classification | Short put absolute delta | Short call absolute delta |
|---|---:|---:|
| Neutral | 0.16 | 0.16 |
| Moderately bullish | 0.20 | 0.10 |
| Moderately bearish | 0.10 | 0.20 |

These are the initial V1 target deltas. OrderComposer selects the nearest liquid strike that satisfies the applicable target and configured tolerance. The starting `TargetDeltaTolerance` is 0.02 absolute delta. If a candidate cannot meet the delta tolerance, liquidity gates, expected-range boundary, and credit requirement together, composition returns `NoCandidate`; it does not silently weaken the policy.

### 9.3 Strike boundaries

Candidate short strikes must respect the most conservative applicable boundary derived from:

- Bollinger price envelope.
- Expiration-specific implied expected move.
- Target delta.
- Directional bias limits.
- Volatility skew.
- Event risk.
- Fund risk budget.

### 9.4 Initial 50-point wing width

The initial monthly iron-condor wing width is 50 ES index points on both sides:

```text
PutWingWidth:  50
CallWingWidth: 50
```

This reflects the naturally available strike structure and practical composition choices near 45 DTE. It is a versioned starting parameter, not a permanent constant.

V1 obtains directional bias through the short-strike delta profile rather than unequal wings. Backtesting may later enable asymmetric or alternative widths within explicit limits. Weekly vertical-spread wing widths are strategy-family-specific and are not inherited from the monthly iron-condor policy.

Permitted wing-width adjustment must consider:

- Maximum defined loss.
- Minimum executable credit.
- Strike liquidity.
- Downside skew.
- Stress loss.
- Portfolio margin and available liquidity.

Asymmetric wings must be independently enabled and backtested.

### 9.5 Order-composition decision hierarchy

Order composition is driven by the following hierarchy:

1. **Portfolio/Fund mandate:** supplies capacity, allowed instruments, contract maximums, risk budget, and bounded hints.
2. **Regime Discovery:** selects whether Neutral, Moderately Bullish, Moderately Bearish, or No Trade is eligible.
3. **Market Condition:** qualifies expiration, option-chain quality, volatility edge, expected range, skew, liquidity, events, and execution conditions.
4. **Composition policy:** supplies the 16/16, 20/10, or 10/20 delta profile, 50-point wings, DTE range, credit requirement, and tolerances.
5. **OrderComposer:** creates and ranks executable candidates without exceeding any upstream constraint.
6. **RiskManager:** gives final approval or rejection.

Portfolio/Fund inputs are hints only when explicitly marked as hints. They cannot relax strategy or RiskManager hard limits.

---

## 10. Position Objectives and Phases

### 10.1 Standard objectives

```text
ProfitTargetRatio: 0.50 of C0
TimeExitDte:       21
```

The policy may accept less than 50% when deterioration makes further holding unattractive.

### 10.2 Establishment phase

From entry through the configured early-defense period, initially ten trading sessions:

- Regime changes receive increased weight.
- Loss velocity receives increased weight.
- Directional continuation receives increased weight.
- A failed entry thesis can cause early closure.
- Hedge initiation is permitted.

### 10.3 Theta-harvest phase

After the establishment phase and before 21 DTE:

- Protect accumulated profit.
- Permit early profit acceptance below the nominal 50% target.
- Reduce maximum new-hedge size.
- Raise the threshold for initiating a new hedge.
- Exit on material thesis deterioration.

### 10.4 Mandatory-exit phase

At or below the configured time-exit DTE:

- Close the iron condor.
- Do not initiate a new discretionary hedge.
- Permit only temporary execution-risk protection needed to complete liquidation.

---

## 11. Position Valuation

Let `C0` be total net opening credit.

```text
ICLossRatio       = max(0, -ExecutableIronCondorPnL / C0)
HedgeLossRatio    = max(0, -ExecutableHedgePnL / C0)
CombinedLossRatio = max(0, -(ExecutableIronCondorPnL + ExecutableHedgePnL) / C0)
```

Executable P&L must use reasonably marketable liquidation prices, commissions, fees, and configured slippage. Midpoint marks may be retained for analytics but cannot drive hard exits alone.

Loss velocity must be calculated over multiple configurable horizons, for example 1, 5, 15, and 60 minutes.

Forward Loss remains distinct from current mark-to-market loss and must record the scenarios that produced it.

### 11.1 Position-monitor trigger and output

Every accepted `OptionStrategyLegChanged` state transition triggers PositionMonitor evaluation. A leg change includes a material change to a leg's bid, ask, executable mark, implied volatility, delta, quote quality, fill state, or tradability status.

Changes arriving in the same option-chain update batch may be coalesced into one evaluation so that all four legs, the underlying future, Regime Discovery, and Market Condition share a consistent snapshot version. Coalescing cannot skip the final accepted state of any changed leg.

PositionMonitor publishes:

```text
PositionTradePlanUpdated
```

PositionMonitor does not submit a futures hedge or close order. It calculates and reports position state. `ExitPosition` consumes the plan and owns the hedge/exit decision.

### 11.2 PositionTradePlan contents

The versioned plan must include at least:

```text
WorkflowId
PositionId
FundId
PortfolioId
PolicyVersion
PositionVersion
OptionChainSnapshotVersion
EvaluatedAt
PositionPhase
EntryRegimeResult
CurrentRegimeResult
RegimeChangeVector
EntryMarketCondition
CurrentMarketCondition
MarketConditionChangeVector
LegStates[4]
ExecutableIronCondorPnL
ExecutableHedgePnL
ExecutableCombinedPnL
ICLossRatio
HedgeLossRatio
CombinedLossRatio
ForwardLossScenarios
LossVelocityScores
OptionGreeks
NetOptionDelta
NetCombinedDelta
DirectionalThreatScore
WhipsawRiskScore
RegimeDeteriorationScore
ExitUrgencyScore
HedgeActionScore
ProfitTargetStatus
TimeExitStatus
LiquidityStatus
DataQualityStatus
SuggestedAction
ReasonCodes
```

`SuggestedAction` is advisory to ExitPosition and is one of:

```text
Maintain
ObserveDefense
OpenMesHedge
OpenEsHedge
OpenMixedHedge
ReduceHedge
CloseHedge
ExitAll
```

ExitPosition must independently apply its policy, current state, and RiskManager authorization before creating an executable command.

---

## 12. Threat Scenarios

The V1 strategy explicitly handles the following scenario families.

### 12.1 Persistent directional continuation

Price moves toward a short strike with increasing delta, trend strength, and directional persistence. A partial futures hedge may be appropriate.

### 12.2 Whipsaw or failed breakout

Price crosses a boundary and then reverses or alternates direction. Hedge entry must be delayed, reduced, or quickly removed.

### 12.3 Shock or gap

Loss accelerates too quickly to establish a controlled hedge. The appropriate action may be immediate complete-position exit.

### 12.4 Implied-volatility expansion without directional threat

The condor loses value while ES remains centered. A futures hedge is normally inappropriate because it does not hedge vega exposure.

### 12.5 Two-sided volatility expansion

Large alternating movement threatens both sides. Avoid repeatedly changing futures direction; prefer thesis exit when instability exceeds policy limits.

### 12.6 Material regime or market-condition change

The entry regime becomes invalid. Depending on severity and loss velocity, reduce exposure, hedge temporarily, or exit.

### 12.7 Slow directional drift

Delta risk accumulates without a sharp loss event. The system may hedge if persistence and exposure pass thresholds, but must not wait solely for a dramatic price move.

### 12.8 Liquidity or data degradation

Stale quotes, widened option markets, inconsistent Databento/IBKR state, or incomplete chain coverage make scores unreliable. New hedge initiation is prohibited unless explicitly required for emergency protection.

### 12.9 Execution failure or partial fill

The hedge or close order does not fill as expected. OrderExecution must coordinate remaining directional exposure and escalate without an endless retry loop.

---

## 13. Scoring Model

V1 scores range from 0 to 100. They are deterministic risk scores, not statistical probabilities until calibrated by out-of-sample data.

### 13.1 Directional Threat Score

Inputs may include:

- Net option delta.
- Threatened short-option delta.
- Distance to short strike in ATR and implied-standard-deviation units.
- Intrinsic-time directional persistence.
- EMA slope and alignment.
- ADX level and slope.
- Bollinger breakout persistence.
- Realized-volatility expansion.
- Loss velocity.

### 13.2 Whipsaw Risk Score

Inputs may include:

- Intrinsic-time direction-change frequency.
- Failed breakout and return inside the prior range.
- Falling or weak ADX.
- Rapid aggregate-delta sign changes.
- Long rejection wicks.
- Two-sided realized volatility.
- Lack of continuation after the initial move.

### 13.3 Regime Deterioration Score

Inputs may include:

- Difference between entry and current fused regime.
- Volatility-regime transition.
- Market-condition confidence deterioration.
- VIX level and term-structure change.
- Liquidity-condition deterioration.
- New macro-event risk.

### 13.4 Exit Urgency Score

Inputs may include:

- Proximity to combined loss limit.
- Loss acceleration.
- Gap magnitude.
- Distance through the short strike.
- Option exit liquidity.
- Data or execution integrity.
- Time remaining to control the position.

### 13.5 Hedge Action Score

Illustrative initial formula:

```text
HedgeActionScore =
      0.35 * DirectionalThreatScore
    + 0.20 * DeltaExposureScore
    + 0.20 * LossVelocityScore
    + 0.25 * RegimeDeteriorationScore
    - 0.30 * WhipsawRiskScore
```

Weights, features, and thresholds are versioned configuration.

---

## 14. Hedge Eligibility

A discretionary hedge is eligible only when all mandatory conditions pass:

```text
Position is in Establishment or ThetaHarvest phase
No complete exit is already required
ForwardLoss or ICLossRatio meets consideration threshold
DirectionalThreatScore meets minimum
HedgeActionScore meets activation threshold
WhipsawRiskScore is below maximum
Loss velocity is controllable
Net option delta exceeds minimum exposure
ES or MES market is tradable
Margin and fund limits pass
Market data is fresh
Maximum hedge episodes has not been exceeded
Risk Manager approves
```

Loss at the consideration threshold is necessary but not sufficient. The initial value may be 1.00 times `C0`.

---

## 15. ES Versus MES Hedge Selection

### 15.1 Delta-equivalent target

```text
RawTargetEsEquivalent = -NetOptionDelta * TargetHedgeRatio
```

The sign of the hedge opposes aggregate option delta:

- Negative option delta during an upside threat: buy futures.
- Positive option delta during a downside threat: sell futures.

### 15.2 Instrument policy

Use MES when:

- The required hedge is smaller than one ES contract.
- ES rounding would materially over-hedge.
- Fine-grained reduction or release is required.
- MES liquidity and transaction-cost gates pass.

Use ES when:

- Aggregate exposure supports whole ES increments.
- ES provides materially better execution efficiency.
- The position size makes MES contract count unnecessarily large.

Use a bounded ES-plus-MES combination when enabled:

```text
EsQuantity  = truncate(RawTargetEsEquivalent)
Residual    = RawTargetEsEquivalent - EsQuantity
MesQuantity = round(Residual * EsToMesRatio)
```

Contract multipliers and the ES-to-MES ratio must come from validated instrument definitions, not hard-coded assumptions.

### 15.3 Hedge-size limits

Initial research parameters:

```text
InitialHedgeRatioMin: 0.25
InitialHedgeRatioMax: 0.40
AbsoluteHedgeRatioMax: 0.50–0.75
MaximumHedgeDuration: One trading session
MaximumHedgeEpisodes: One
```

The hedge ratio may increase only when threat persistence rises, whipsaw risk remains acceptable, and combined risk stays below the mandatory exit region.

---

## 16. Hedge Activation, Maintenance, and Release

### 16.1 Activation

The decision output must be one of:

```text
Observe
OpenMesHedge
OpenEsHedge
OpenMixedHedge
ExitAll
```

Every decision records scores, inputs, thresholds, selected instrument, target quantity, and reason codes.

### 16.2 Maintenance

While hedged, continuously evaluate:

- Combined executable P&L.
- Futures P&L.
- Net combined delta.
- Directional persistence.
- Whipsaw risk.
- Regime deterioration.
- Loss velocity.
- Remaining hedge duration.
- Option and futures liquidity.

Permitted actions are:

```text
MaintainHedge
IncreaseHedgeWithinCap
ReduceHedge
CloseHedge
ExitAll
```

### 16.3 Hysteresis

Separate thresholds prevent repeated hedge entry and removal:

```text
HedgeActivationScore: initial 65
HedgeReleaseScore:    initial 40–45
```

### 16.4 Release conditions

Close or reduce the hedge when any configured condition occurs:

- Directional Threat falls below release threshold.
- Whipsaw Risk exceeds its limit.
- Net combined delta returns inside its safe band.
- A breakout fails.
- Price crosses the configured hedge floor or trailing boundary.
- Forward Loss materially recovers.
- Maximum hedge duration expires.
- Complete-position liquidation begins.

Closing the hedge does not automatically close a recovering iron condor unless a separate exit rule requires it.

---

## 17. Loss Limits

Illustrative initial values:

```text
HedgeConsiderationLossRatio: 1.00
HedgeStandaloneLossLimit:    0.20
NormalCombinedLossLimit:     1.20
TraditionalUnhedgedLimit:    2.00
EmergencyFundLimit:          Configured separately
```

The authoritative decision uses combined executable risk:

```text
ExitAll when:
    CombinedLossRatio >= NormalCombinedLossLimit
    OR ExitUrgencyScore >= EmergencyThreshold
    OR EmergencyFundLimit is breached
    OR position control cannot be assured
```

The hedge standalone loss limit is an additional warning or exit input. It cannot replace the combined loss limit because the hedge and iron condor may offset one another.

The Normal Combined Loss Limit is a target in normal markets, not a guarantee. Gaps, halts, slippage, partial fills, and spread widening can produce a larger realized loss.

---

## 18. Exit Rules

### 18.1 Standard iron-condor exit

Exit when any mandatory condition is true:

```text
Profit reaches configured target
DTE reaches time exit
Combined loss reaches normal limit
Original regime thesis materially fails
Option liquidity becomes unsafe
Hedge cannot be controlled
Market-data or execution integrity becomes unreliable
Fund or portfolio emergency limit is reached
Manual authorized exit is received
```

### 18.2 Scenario-dependent exit ordering

There is no universal hedge-first rule.

#### Reversal or hedge whipsaw

1. Close futures hedge first.
2. Recalculate iron-condor risk immediately.
3. Retain or close the condor according to its independent exit decision.

#### Continuing directional move

1. Submit a marketable iron-condor closing combination.
2. Retain protective futures exposure while option risk remains.
3. Reduce futures proportionally as option legs fill.
4. Close residual futures immediately after option exposure is removed.

#### Emergency combined exit

1. Freeze further strategy adjustments.
2. Stage option and futures exits as one coordinated workflow.
3. Select the sequence that minimizes current directional exposure.
4. Escalate partial fills and timeouts without automatic retry loops.

---

## 19. State Model

```text
Dormant
  -> OpportunityActive
  -> QualificationPending
  -> Qualified | NoTrade
  -> EntryPending
  -> CondorOpen
  -> HedgeCandidate
  -> HedgePending
  -> HedgeActive
  -> HedgeReleased | ExitRequired
  -> Closing
  -> ClosedProfit | ClosedLoss | ClosedEmergency
  -> MonthlyCooldown
```

Mandatory invariants:

- Only one active monthly opportunity per fund and month.
- Only one open monthly condor per configured entity.
- At most one hedge workflow can be in flight.
- Every started pipeline stage emits Completed or Failed.
- A Failed hedge command does not imply that the position is safe.
- Risk Manager has final authority.
- No automatic pipeline retries.

---

## 20. Commands, Events, and Decisions

### 20.1 Commands

```text
CreateMonthlyIronCondorOpportunity
EvaluateMonthlyQualification
CanOpenPosition
OpenMonthlyIronCondor
EvaluateExitPosition
OpenFuturesHedge
ResizeFuturesHedge
CloseFuturesHedge
ExitCombinedPosition
CancelPositionWorkflow
```

### 20.2 Events

```text
MonthlyOpportunityCreated
MonthlyQualificationCompleted
MonthlyQualificationRejected
MonthlyIronCondorOpened
PositionTradePlanUpdated
ExitPositionEvaluated
FuturesHedgeRequested
FuturesHedgeOpened
FuturesHedgeResized
FuturesHedgeReleased
CombinedExitRequired
CombinedPositionClosed
PositionWorkflowFailed
MonthlyOpportunityExpired
DirectionalFollowUpOpportunityDetected
```

### 20.3 ExitPosition decision payload

At minimum:

```text
WorkflowId
PositionId
PolicyVersion
SnapshotVersion
DecisionTimestamp
PositionPhase
ThreatenedSide
ICLossRatio
HedgeLossRatio
CombinedLossRatio
ForwardLossRatio
LossVelocityScores
NetOptionDelta
NetCombinedDelta
DirectionalThreatScore
WhipsawRiskScore
RegimeDeteriorationScore
ExitUrgencyScore
HedgeActionScore
Decision
SelectedInstrument
TargetQuantity
MaximumDuration
ReasonCodes
```

---

## 21. Data and Snapshot Requirements

Every decision must use a consistent, versioned snapshot containing:

- ES futures price and timestamp.
- ES and MES hedge quotes and timestamps.
- Complete four-leg option quotes.
- Current executable liquidation cost.
- Entry and current Greeks.
- Volatility surface and skew.
- Regime Discovery result.
- Market Condition result.
- Intrinsic-time state.
- Position fills and commissions.
- Upcoming events and session status.
- Account margin and available liquidity.

Stale, incomplete, or internally inconsistent snapshots cannot authorize a new discretionary hedge.

---

## 22. Configuration Model

All parameters belong to an immutable `MonthlyIronCondorHedgePolicy` identified by version.

Parameter groups:

### Qualification

- Checkpoint schedule.
- Maximum opportunity window.
- Target and permitted DTE.
- Expiration liquidity thresholds.
- Event and session exclusions.
- Regime Discovery hard gates and scoring thresholds.
- Market Condition hard gates and scoring thresholds.
- Portfolio/Fund capacity-query requirements.

### Construction

- Neutral target deltas: put 0.16, call 0.16.
- Bullish target deltas: put 0.20, call 0.10.
- Bearish target deltas: put 0.10, call 0.20.
- Target delta tolerance: initially 0.02 absolute delta.
- Monthly put and call wing width: initially 50 points.
- Approved future alternative or asymmetric wing widths.
- Minimum credit.
- Maximum defined loss.
- Range and skew adjustments.

### Profit and time management

- Profit target.
- Early-profit acceptance rules.
- Establishment-phase duration.
- Time-exit DTE.

### Hedge

- Consideration loss ratio.
- Score weights and thresholds.
- Delta exposure threshold.
- Minimum and maximum hedge ratios.
- ES/MES selection rules.
- Maximum hedge duration.
- Maximum hedge episodes.
- Hysteresis thresholds.
- Hedge standalone loss limit.

### Complete exit

- Normal combined loss limit.
- Traditional unhedged limit.
- Loss-velocity emergency threshold.
- Emergency fund limit.
- Partial-fill timeout.
- Exit sequencing policy.

Configuration changes apply only according to an explicit policy activation rule. Existing positions normally retain their entry policy version unless an emergency risk policy explicitly overrides it.

---

## 23. Weekly Follow-Up Handoff

A stopped monthly position may reveal a short-term directional regime, but the monthly strategy must not automatically attempt to recover its loss.

After a monthly loss:

1. Enter monthly cooldown.
2. Prohibit monthly re-entry for the remainder of the month.
3. Require an overnight or configured cooldown before any new directional strategy.
4. Run a new Regime Discovery and Market Condition workflow.
5. Emit `DirectionalFollowUpOpportunityDetected` when appropriate.
6. Require an independent weekly strategy, risk budget, and approval.

Automated weekly vertical-spread entry remains outside this strategy specification.

---

## 24. Backtesting Requirements

### 24.1 Required strategy variants

Backtest at least:

1. Iron condor without a hedge.
2. Iron condor with a direct combined-loss exit.
3. Iron condor with MES-only partial hedge.
4. Iron condor with ES-only partial hedge.
5. Iron condor with mixed ES/MES hedge.
6. Iron condor with combined hedge and exit policy.
7. Alternative whipsaw thresholds.
8. Alternative establishment-phase lengths.

### 24.2 Required realism

- Point-in-time option definitions.
- Historical bid/ask quotes, not settlement-only pricing.
- Correct underlying futures contract.
- Commissions, fees, and conservative slippage.
- Four-leg execution assumptions.
- Futures hedge latency and slippage.
- Partial-fill modelling.
- Intraday path dependency.
- Trading calendar, events, and session boundaries.
- No use of future regime, volatility, or revised data.

### 24.3 Required metrics

- Net P&L and return on maximum risk.
- Maximum drawdown.
- Expected shortfall/CVaR.
- Worst trade and worst clustered losses.
- Profit-target and time-exit frequency.
- Hedge activation frequency.
- Hedge P&L distribution.
- Tail-loss reduction attributable to hedge.
- Loss created by whipsaw.
- Combined loss after simulated execution.
- Maximum loss-limit overshoot.
- Time spent hedged.
- ES versus MES execution cost.
- Results by regime, bias, DTE, volatility, and position phase.

### 24.4 Validation process

- Use walk-forward and out-of-sample testing.
- Prefer coarse stable parameter regions over isolated optima.
- Preserve every policy version and result set.
- Paper trade for execution validation after historical testing.
- Do not interpret 6–12 monthly paper trades as statistical proof.

---

## 25. Observability

Record structured logs, metrics, and traces for:

- Qualification outcome and rejection reasons.
- Expiration and strike selection.
- Entry policy version.
- Score values and feature contributions.
- Current, forward, and combined loss.
- Hedge eligibility and rejection reasons.
- Requested and filled ES/MES quantities.
- Hedge duration and P&L.
- Whipsaw detection.
- Exit reason and sequencing decision.
- Order latency, partial fills, cancel/replace, and slippage.
- Queue depth, processing latency, and stale-snapshot counts.

The workflow ID must correlate the qualification, entry, monitoring, hedge, exit, and execution traces.

---

## 26. Failure Handling

- No automatic actor retry loops.
- A failed hedge request triggers immediate risk reassessment.
- A data-quality failure blocks new discretionary risk.
- A broker or execution failure escalates to the configured emergency workflow.
- A partial fill recalculates actual remaining delta before the next order.
- Optional stage timeouts and manual cancellation may be added without changing the core strategy.
- IBKR position and P&L remain the operational book of truth.

---

## 27. Acceptance Criteria for Paper Trading

The implementation is ready for paper trading when:

1. Qualification produces deterministic results from recorded snapshots.
2. Only one monthly entry can occur.
3. Expiration mechanics and underlying mapping are validated.
4. Neutral, bullish, and bearish candidates respect policy bounds.
5. Executable P&L agrees with independently reconstructed values.
6. Hedge scores include complete reason codes.
7. ES, MES, and mixed hedge sizing pass unit and property tests.
8. Hysteresis prevents rapid repeated hedge transitions.
9. Whipsaw, shock, IV-only loss, continuation, and liquidity-failure scenarios pass deterministic simulations.
10. Combined exits preserve or minimize protective exposure during partial fills.
11. Profit and 21-DTE exits operate correctly.
12. All actions are correlated through observability.
13. Risk Manager can veto every entry, hedge, resize, and exit order.
14. Manual emergency exit uses the same execution and risk controls.

---

## 28. Initial Strategy Summary

The initial strategy is:

> Once per month, evaluate two scheduled qualification checkpoints and open a liquid, defined-risk ES iron condor near 45 DTE only when regime, market condition, option-chain quality, and fund risk permit. Construct it as neutral, moderately bullish, or moderately bearish within bounded delta and wing policies. Seek approximately 50% of opening credit and exit by 21 DTE. During the vulnerable early phase, consider a temporary partial ES or MES futures hedge only when loss, directional persistence, delta exposure, and regime deterioration outweigh whipsaw risk. Remove the hedge when the threat fails or normalizes, and close the complete position when the combined thesis or configured risk ceiling fails.

---

## 29. Reference Material

- CME Group, E-mini S&P 500 Options contract specifications: https://www.cmegroup.com/markets/equities/sp/e-mini-sandp500.contractSpecs.options.html
- CME Group, Weekly and End-of-Month Options FAQ: https://www.cmegroup.com/trading/equity-index/weekly-eom-options-faq.html
- CME Group, ES options product codes and listing calendar: https://www.cmegroup.com/trading/equity-index/files/es-options-product-codes-and-listing-calendar.pdf
- CME Group, Hedging with Micro E-mini futures: https://www.cmegroup.com/education/courses/micro-e-mini-futures/hedging-with-the-micro-e-mini-futures

