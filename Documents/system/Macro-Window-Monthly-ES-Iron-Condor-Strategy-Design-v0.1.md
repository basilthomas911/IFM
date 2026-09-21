# Macro Window Monthly ES Iron Condor Strategy Design

**Version:** 0.1  
**Status:** Implementation baseline for backtesting and paper trading  
**Strategy:** `MacroWindowMonthlyIronCondorStrategy`  
**Hedge policy:** `ForwardLossIntrinsicTimeFuturesHedgePolicy`  
**Instruments:** ES futures and options on ES futures  
**Macro anchors:** FOMC rate decisions and monthly US Nonfarm Payrolls reports

## 1. Purpose

This document defines a deterministic monthly ES futures-options iron condor strategy opened after a major scheduled macroeconomic event. An FOMC rate decision or monthly Nonfarm Payrolls report opens a limited opportunity window. The strategy enters only when the post-event regime, market condition, option chain, portfolio mandate, and risk limits provide a positive eligibility decision.

The macro event is an opportunity anchor, not an automatic trade instruction. If the required conditions do not occur within the configured window, the system records a no-trade result for that opportunity.

The design also defines an optional defensive ES futures overlay. If the iron condor reaches a forward loss equal to 100 percent of its original credit, the overlay becomes armed. An adverse intrinsic-time directional-change event can then open a partial futures hedge. The hedge is managed with monotonic intrinsic-time profit floors and is intended to reduce additional directional loss. It is not required to recover the loss already incurred by the iron condor.

This design is deliberately conservative for V1. It favors determinism, explainability, bounded activity, and complete observability over frequent trading or continuous delta hedging.

## 2. Design decisions

The following decisions are part of the V1 baseline:

1. The strategy trades the portfolio's Monthly Iron Condor fund only.
2. The strategy trades ES futures options and uses ES futures for an optional defensive hedge.
3. FOMC and NFP are separate configured variants of one shared strategy implementation.
4. An event opens an eligibility window of up to seven calendar days; it does not force an entry.
5. At most one monthly iron condor may be opened unless a future strategy version explicitly permits multiple positions.
6. Regime, market condition, fund, portfolio, calendar, option-chain, and RiskManager approval are required before entry.
7. Iron-condor entry is a single multi-leg limit order whenever supported by the broker.
8. The futures hedge is armed at a forward-loss ratio of 1.00 but requires a confirming adverse intrinsic-time direction change before entry.
9. V1 uses a partial delta hedge, not continuous delta neutrality.
10. Only one hedge attempt is permitted per threatened-side forward-loss excursion.
11. The hedge uses monotonic floors at 0.50, 1.00, 1.50, and 2.00 intrinsic-time thresholds.
12. The iron condor and futures hedge are evaluated as one combined risk position.
13. A hard combined-position loss limit remains mandatory; the hedge cannot replace it.
14. No actor automatically retries a failed strategy operation. A command completes or fails and observability records the outcome.
15. All production parameter values remain configuration-controlled and versioned. Backtesting and paper trading must validate them before live use.

## 3. Goals and non-goals

### 3.1 Goals

- Create a simple and explainable first monthly ES options strategy.
- Trade the post-event market response rather than predict the FOMC or NFP result.
- Allow up to seven days for volatility, liquidity, price range, and directional structure to become suitable.
- Select balanced or directionally biased iron condors deterministically.
- Use live option-chain prices and the existing Black-76 pricer and Greeks calculator.
- Reduce tail loss from persistent directional moves through a limited futures hedge.
- Prevent hedge churn with hysteresis, episode identity, and one-attempt rules.
- Produce sufficient state, events, metrics, and explanations for backtesting and operations.

### 3.2 Non-goals

- Predicting the FOMC decision, NFP value, or immediate announcement reaction.
- Opening a trade every month.
- Recovering all losses after forward loss reaches 100 percent of credit.
- Continuous or high-frequency delta-neutral rebalancing.
- Gamma scalping.
- Multiple simultaneous futures hedge layers.
- Automatic optimization or machine-learning parameter changes in production.
- Removing the defined-risk iron-condor exit because a hedge exists.
- Allowing an LLM to approve, size, submit, modify, or close an order.

## 4. Strategy terminology

| Term | Definition |
|---|---|
| Macro anchor | The scheduled FOMC or NFP event that creates a strategy opportunity. |
| Opportunity window | Configured interval after the macro anchor during which entry may become eligible. |
| Entry eligibility | Deterministic result produced from calendar, regime, market condition, option chain, fund, portfolio, and risk inputs. |
| Initial credit | Net credit actually received for the complete iron condor, excluding later hedge P and L. |
| Forward loss | PositionMonitor estimate of prospective or current adverse position loss under the configured forward-loss model. |
| Forward-loss ratio | Forward loss divided by initial iron-condor credit. |
| Threatened side | Call spread during an upward threat or put spread during a downward threat. |
| Hedge episode | One armed or active hedge lifecycle for a position, threatened side, and forward-loss excursion. |
| Primary DC event | Directional-change event generated using the configured primary intrinsic-time threshold. |
| Hedge threshold | Intrinsic-time threshold used to enter and manage the futures hedge. It may equal the primary threshold in V1. |
| Overshoot floor | Monotonic profit-protection level for an open futures hedge. |
| Combined position | Iron condor plus any futures hedge and all associated transaction costs. |

## 5. Strategy family and configured variants

The portfolio assigns `MacroWindowMonthlyIronCondorStrategy` to the Monthly Iron Condor fund. The same implementation supports two macro-event configurations.

### 5.1 FOMC variant

**Configuration name:** `FomcMacroWindowMonthlyIronCondor`

The FOMC variant waits for the scheduled rate-decision release and any configured press-conference blackout to finish. Its purpose is to trade after the initial policy reaction has been incorporated into price and the system can evaluate the resulting regime, volatility, range, and option premiums.

### 5.2 NFP variant

**Configuration name:** `NfpMacroWindowMonthlyIronCondor`

The NFP variant waits for the scheduled employment-report release and a configured post-release stabilization interval. Its purpose is to trade the resulting monthly price and volatility structure, not the reported payroll value.

### 5.3 Monthly opportunity arbitration

V1 permits one monthly iron condor. The portfolio owns arbitration when both event variants could qualify in the same month.

Default arbitration policy:

1. Select the configured preferred macro anchor for the month.
2. FOMC receives priority when the event occurs early enough to provide the required DTE and holding window.
3. NFP may be selected when no suitable FOMC anchor exists or when portfolio configuration explicitly gives NFP priority.
4. Do not automatically fall through from a failed FOMC opportunity to NFP unless `AllowSecondaryMonthlyOpportunity` is enabled.
5. Once a monthly iron condor is open, all other monthly opportunities expire or become ineligible.
6. Record the arbitration decision and all rejected alternatives.

## 6. Macro calendar and opportunity lifecycle

### 6.1 Required calendar data

The calendar service must provide:

- Event identifier and revision.
- Event type: FOMC or NFP.
- Scheduled release timestamp in UTC.
- Press-conference start and end when applicable.
- Event status: scheduled, rescheduled, released, cancelled, or unknown.
- Data provenance and last update time.
- Adjacent high-impact events inside the prospective holding period.
- Exchange sessions and holidays.

Calendar uncertainty, cancellation, or conflicting event timestamps makes new entry ineligible until resolved.

### 6.2 Opportunity states

```text
Scheduled
  -> Blackout
  -> Stabilizing
  -> Evaluating
  -> Eligible
  -> OrderPending
  -> PositionOpen

Evaluating
  -> ExpiredNoTrade
  -> Rejected
  -> Cancelled
  -> Failed
```

### 6.3 Window timing

The default maximum window is seven calendar days after the applicable event-release boundary. Each configured variant may define:

- `PostEventBlackoutDuration`
- `StabilizationDuration`
- `OpportunityWindowDuration`
- `LatestEntryTimeOfDay`
- `AllowedSessions`
- `MinimumDteAtEntry`
- `MaximumDteAtEntry`

The window closes immediately when:

- another monthly iron condor opens;
- DTE falls outside the permitted range;
- a conflicting macro blackout begins;
- the portfolio disables the strategy family;
- the fund exhausts its risk allocation;
- the calendar becomes unreliable; or
- the seven-day deadline expires.

## 7. Entry eligibility

Entry is allowed only when every mandatory gate returns eligible against one immutable evaluation snapshot.

```text
EntryEligible =
    OpportunityWindowOpen
    AND MacroBlackoutComplete
    AND MonthlyPositionLimitAvailable
    AND FundMandateAllowsStrategy
    AND PortfolioAllocationAvailable
    AND RegimeEligible
    AND MarketConditionEligible
    AND VolatilityEligible
    AND LiquidityEligible
    AND OptionChainFresh
    AND CompositionPolicySatisfied
    AND CreditMeetsMinimum
    AND CombinedRiskWithinLimits
    AND RiskManagerApproved
```

### 7.1 Regime gate

RegimeDiscovery supplies the monthly-horizon trend and volatility classifications, confidence, supporting scores, and versioned evidence. The strategy maps the result to one of four decisions:

| Decision | Meaning |
|---|---|
| Balanced | Directional evidence is weak enough for approximately symmetric short strikes. |
| Bullish bias | Put-side risk may be placed farther or sized according to the approved bullish composition policy. |
| Bearish bias | Call-side risk may be placed farther or sized according to the approved bearish composition policy. |
| No trade | Trend, volatility, confidence, or disagreement exceeds policy limits. |

The regime result is an input to policy. It does not independently submit an order.

### 7.2 Market-condition gate

`MarketConditionCompleted` must include normalized monthly-horizon features for:

- liquidity and executable spread quality;
- session eligibility;
- realized and implied volatility context;
- post-event stabilization;
- price-range quality;
- seasonality where available;
- upcoming-event distance;
- news blackout status;
- option-chain health; and
- strategy-specific eligibility and reason codes.

The strategy differentiates `EntryIneligible` from `HoldIneligible`. Entry ineligibility prevents a new order but does not automatically close an existing position. Hold ineligibility is an explicit position-exit input.

### 7.3 Volatility and event-risk gate

V1 rejects entry when volatility is classified as extreme or the fund's configured VIX no-trade threshold is reached. It also rejects entry when an imminent scheduled event would occur inside the minimum protected holding interval unless that event is explicitly permitted by the selected policy.

### 7.4 Liquidity gate

The option chain must satisfy configured limits for:

- quote age;
- bid and ask validity;
- maximum absolute and relative spread;
- minimum displayed size when required;
- strike continuity;
- contract metadata completeness;
- selected-leg open interest or volume when used; and
- expected multi-leg fill quality.

An indicative theoretical value never replaces an executable market quote.

## 8. Iron-condor composition

Order Composer applies the `OrderCompositionPolicy` attached to the selected strategy-family definition.

### 8.1 Baseline parameters

All values below are configuration placeholders to be calibrated rather than production recommendations.

| Parameter | Purpose |
|---|---|
| `MinimumDte` and `MaximumDte` | Restrict expiration selection, initially targeting the previously proposed 30 to 45 day region. |
| `TargetShortPutDelta` | Baseline put-side short-strike delta. |
| `TargetShortCallDelta` | Baseline call-side short-strike delta. |
| `PutWingWidth` | Distance between short and long put strikes. |
| `CallWingWidth` | Distance between short and long call strikes. |
| `MinimumCredit` | Minimum absolute net credit. |
| `MinimumCreditToWidthRatio` | Minimum compensation relative to maximum spread width. |
| `MaximumBidAskWidth` | Maximum permitted executable spread. |
| `MaximumContracts` | Fund and portfolio size ceiling. |
| `MaximumGrossRisk` | Maximum defined risk before hedge effects. |
| `MinimumDistanceFromExpectedMove` | Optional short-strike distance control. |

### 8.2 Bias application

The directionally biased iron condor remains a defined-risk four-leg structure. Bias is expressed only through configuration-approved changes such as:

- asymmetric short-strike deltas;
- asymmetric wing widths;
- unequal distance from the current futures price; or
- constrained side-specific contract quantities if the strategy family permits them.

V1 should prefer strike and wing adjustments over unequal quantities because equal quantities preserve simpler risk accounting and order handling.

### 8.3 Black-76 integration

The existing C# Black-76 calculator supplies theoretical values and first-order Greeks for every candidate leg. Order Composer uses live futures and option-chain inputs to calculate:

- theoretical option values;
- leg and structure delta;
- gamma, theta, and vega where available;
- theoretical versus executable credit;
- maximum defined loss;
- scenario P and L; and
- hedge-ratio inputs.

The pricer supports candidate evaluation but does not override stale or invalid quotes. Every calculation records the market-data snapshot, volatility input, rate input, model version, and timestamp.

### 8.4 Candidate ranking

Candidate ranking is deterministic. A suggested ordering is:

1. Reject candidates violating a hard constraint.
2. Prefer candidates satisfying target DTE and delta bands.
3. Prefer adequate executable credit and credit-to-width ratio.
4. Penalize wide or unreliable quotes.
5. Penalize excessive delta, gamma, or event exposure.
6. Prefer the smallest deviation from the selected balanced or biased policy.
7. Resolve exact ties using stable strike and expiration ordering.

## 9. Iron-condor order execution

The complete iron condor is submitted as one multi-leg limit order whenever supported. Legging into the position is outside V1 scope.

Execution rules:

- RiskManager gives the final approval immediately before submission.
- Approval contains a short validity interval and references the evaluated snapshot.
- OrderExecution owns broker identifiers, submission, acknowledgement, fills, cancel, and replace.
- Cancel and replace may improve the limit price only within configured bounds.
- Partial fills are managed as an execution-risk state and may not be treated as an open completed strategy position.
- A new market snapshot or material risk change can invalidate the approval before submission.
- Broker-reported positions and P and L are the operational book of truth.

## 10. Position monitoring and forward loss

### 10.1 Position state

PositionMonitor maintains the existing Green, Yellow, and Red health classification and adds the fields required by this strategy:

```text
InitialCredit
CurrentMark
BrokerUnrealizedPnL
ForwardLoss
ForwardLossRatio
CombinedPnL
CombinedLossRatio
NetDelta
NetGamma
NetTheta
NetVega
ThreatenedSide
DistanceToShortPut
DistanceToShortCall
HedgeEpisodeState
ActiveHedgeQuantity
ActiveOvershootFloor
```

### 10.2 Forward-loss normalization

```text
ForwardLossRatio = max(0, ForwardLoss) / InitialCredit
```

The calculation must specify whether `ForwardLoss` represents current mark loss, projected loss under a deterministic scenario, or the maximum of multiple approved measures. The selected definition must remain stable within a backtest run and be versioned with the strategy configuration.

### 10.3 Threatened-side determination

The threatened side is not inferred from P and L alone.

```text
CallSideThreatened = NetDelta < -DeltaThreatThreshold
                     AND price is approaching or beyond the short call region

PutSideThreatened  = NetDelta > DeltaThreatThreshold
                     AND price is approaching or beyond the short put region
```

If neither side is unambiguous, the hedge remains unarmed even when the forward-loss ratio is at least 1.00. Volatility expansion without clear directional exposure is not sufficient reason to trade futures.

## 11. Forward Loss Intrinsic Time Futures Hedge Policy

### 11.1 Objective

The hedge policy attempts to reduce the rate at which a persistent adverse ES move increases the combined loss after the iron condor is already stressed. It is a conditional loss-offset mechanism, not an independent profit strategy and not a promise to recover the initial loss.

### 11.2 Arming conditions

```text
HedgeArmed =
    ForwardLossRatio >= 1.00
    AND ThreatenedSideIsClear
    AND IronCondorStatus == Open
    AND NoActiveHedge
    AND HedgeAttemptAvailable
    AND RegimeAllowsThreatDirection
    AND MarketConditionAllowsFuturesHedge
    AND PortfolioRiskAllowsHedge
```

Crossing 1.00 creates or advances a hedge episode. It does not immediately submit a futures order.

### 11.3 Direction confirmation and entry

The hedge opens only after a primary directional-change event confirms continued movement toward the threatened side.

| Threatened side | Required DC direction | Hedge action |
|---|---|---|
| Call spread | Up | Buy ES futures |
| Put spread | Down | Sell ES futures |

The DC event must occur after the hedge episode is armed or remain valid under an explicitly configured maximum signal age. V1 should default to requiring a new event after arming to prevent using a stale signal.

### 11.4 Hedge sizing

The hedge is based on the current net iron-condor delta expressed in ES-futures equivalents:

```text
RawHedgeQuantity = -HedgeFraction * IronCondorNetDelta
```

The executable quantity is rounded according to the configured contract policy and capped by fund, portfolio, margin, and maximum-hedge limits.

V1 uses a configurable partial hedge fraction, initially evaluated over a research range such as 0.25 to 0.50. The final production value must come from backtesting and paper trading.

If one ES future is too coarse, the strategy must either:

- remain unhedged;
- use MES if the fund mandate explicitly permits it; or
- apply a documented rounding rule that does not exceed the maximum permitted hedge ratio.

V1 must never round a small required hedge away from zero into a materially over-hedged position without explicit policy approval.

### 11.5 Hedge episode identity

```text
HedgeEpisodeId = PositionId + ThreatenedSide + ForwardLossExcursionNumber
```

Only one futures hedge attempt is permitted per episode. An episode ends when:

- forward loss recovers below the disarm threshold;
- the threatened side changes;
- the iron condor closes; or
- RiskManager permanently disables further hedging.

### 11.6 Hysteresis

V1 arms at a forward-loss ratio of 1.00 and uses a configurable lower disarm threshold, initially 0.75 for research.

```text
Arm when ForwardLossRatio >= 1.00
Disarm when ForwardLossRatio <= 0.75
```

The separation prevents repeated arm and disarm behavior around 1.00.

## 12. Intrinsic-time hedge management

### 12.1 Reference values

At the futures fill, record:

```text
HedgeEntryPrice
HedgeDirection
HedgeThresholdDelta
CurrentExtremePrice
MaximumFavourableExcursion
HighestActivatedFloorLevel
ActiveFloorPrice
```

For a long hedge:

```text
MFE = CurrentExtremePrice - HedgeEntryPrice
```

For a short hedge:

```text
MFE = HedgeEntryPrice - CurrentExtremePrice
```

### 12.2 Floor activation

| MFE reached | Activated level | Long floor | Short floor |
|---:|---:|---:|---:|
| `0.50 delta` | 0.50 | `Entry + 0.50 delta` | `Entry - 0.50 delta` |
| `1.00 delta` | 1.00 | `Entry + 1.00 delta` | `Entry - 1.00 delta` |
| `1.50 delta` | 1.50 | `Entry + 1.50 delta` | `Entry - 1.50 delta` |
| `2.00 delta` | 2.00 | `Entry + 2.00 delta` | `Entry - 2.00 delta` |

The active floor is monotonic. It can only move in the profitable direction. Floor activation and floor violation must use executable price-side rules appropriate to closing the position, not midpoint-only prices.

### 12.3 Before the first floor

Before MFE reaches `0.50 delta`, close the futures hedge when either occurs:

- an opposite primary `DirectionChanged` event; or
- regime or market condition explicitly changes to `HoldIneligible`.

The hedge also remains subject to all global exit and risk conditions in Section 13.

### 12.4 After a floor activates

After `0.50 delta` activates, close when the first of these occurs:

- executable price crosses the active floor;
- an opposite primary directional-change event;
- regime or market condition becomes `HoldIneligible`;
- the iron condor recovers below the disarm threshold;
- the threatened side changes;
- the iron condor closes; or
- a risk or emergency exit occurs.

The system must not wait for a full opposite directional-change event after a floor is crossed. The floor itself is an immediate exit trigger.

### 12.5 Behavior beyond 2.00 delta

V1 activates and retains the `2.00 delta` floor. Additional profit may accrue while ES continues in the hedge direction, but the floor does not advance beyond 2.00 unless a future configuration adds further levels. An alternative immediate take-profit at 2.00 may be backtested but is not the baseline policy.

## 13. Exit policy and risk precedence

### 13.1 Iron-condor exits

The iron condor may close because of:

- normal profit target;
- maximum holding period or DTE exit;
- configured forward-loss or combined-loss limit;
- threatened short strike or spread risk;
- explicit `HoldIneligible` regime or market-condition decision;
- event blackout or calendar risk;
- broker, margin, data-quality, or operational risk;
- manual exit through the same RiskManager-controlled path; or
- portfolio kill switch.

### 13.2 Futures-hedge exits

The hedge closes because of:

- emergency or risk exit;
- combined-position hard-loss limit;
- iron-condor close;
- `HoldIneligible` decision;
- forward-loss recovery below the disarm threshold;
- threatened-side change;
- overshoot-floor violation;
- opposite primary directional change; or
- hedge-specific maximum loss or maximum duration.

### 13.3 Reason precedence

When multiple conditions exist in the same evaluation cycle, use this order for the recorded primary reason:

1. Emergency, kill switch, or broker-risk exit.
2. Combined-position hard-loss exit.
3. Iron-condor termination.
4. Strategy `HoldIneligible`.
5. Forward-loss recovery or threatened-side change.
6. Overshoot-floor violation.
7. Opposite primary directional change.
8. Time-based hedge exit.

Precedence selects the recorded reason but must not delay execution. Once an exit decision changes an entity to `ClosePending`, later signals are recorded as secondary evidence and cannot create duplicate orders.

### 13.4 Combined-position hard stop

```text
CombinedPnL = IronCondorPnL + FuturesHedgePnL - AllTransactionCosts
CombinedLossRatio = max(0, -CombinedPnL) / InitialCredit
```

The maximum combined-loss ratio is configuration-controlled. Research should compare limits such as 1.25, 1.50, 1.75, and 2.00, but this document does not select a production value.

RiskManager evaluates the combined position and has the final authority to close both components. The presence or profitability of the hedge never disables the hard stop.

## 14. Actor and pipeline integration

### 14.1 Open pipeline

```text
MacroCalendar / IntrinsicTime trigger
  -> IntrinsicTimeStrategyWorkflowCommandActor
  -> RegimeDiscovery
  -> MarketCondition
  -> TradeStrategy
  -> CandidateBuilder / OrderComposer
  -> PortfolioRisk / RiskManager
  -> OrderExecution
```

`TradeStrategy` selects `MacroWindowMonthlyIronCondorStrategy` and its FOMC or NFP configuration. `OrderComposer` applies the associated order-composition policy using the live chain and Black-76 calculations.

### 14.2 Position and close pipeline

```text
PositionMonitor
  -> ExitDecision
  -> RiskManager
  -> OrderExecution
```

PositionMonitor also requests hedge evaluation when a new forward-loss excursion is armed. Hedge entry follows the normal strategy and risk controls; it is not submitted directly by PositionMonitor.

### 14.3 Ownership

| Component | Responsibility |
|---|---|
| Macro Calendar | Authoritative scheduled-event and blackout data. |
| Portfolio | Select monthly opportunity, allocate capital, enforce one-position policy, and own cross-fund limits. |
| Fund | Define allowed instruments, strategy families, sizing ranges, and fund loss limits. |
| RegimeDiscovery | Produce trend and volatility regime evidence. |
| MarketCondition | Produce liquidity, session, event, range, and strategy-eligibility evidence. |
| TradeStrategy | Select balanced, bullish-biased, bearish-biased, or no-trade outcome. |
| OrderComposer | Select expiration and legs and produce an executable candidate. |
| PositionMonitor | Revalue the position, calculate forward loss, determine threatened side, and monitor hedge floors. |
| ExitDecision | Apply deterministic close policies and reason precedence. |
| RiskManager | Give final approval for every open, hedge, replace, and close action. |
| OrderExecution | Own broker interaction and order lifecycle. |

## 15. Commands, events, and queries

The following names are implementation proposals and should follow the trading system's established message envelope, workflow ID, causation ID, correlation ID, entity ID, schema version, timestamp, and idempotency conventions.

### 15.1 Opportunity commands

```text
OpenMacroIronCondorOpportunityCommand
EvaluateMacroIronCondorOpportunityCommand
ExpireMacroIronCondorOpportunityCommand
CancelMacroIronCondorOpportunityCommand
```

### 15.2 Opportunity events

```text
MacroIronCondorOpportunityOpened
MacroIronCondorOpportunityEvaluationCompleted
MacroIronCondorOpportunityEligible
MacroIronCondorOpportunityRejected
MacroIronCondorOpportunityExpired
MacroIronCondorOpportunityCancelled
MacroIronCondorOpportunityFailed
```

### 15.3 Composition and order events

```text
MacroIronCondorCandidateComposed
MacroIronCondorCandidateRejected
MacroIronCondorRiskApproved
MacroIronCondorRiskRejected
MacroIronCondorOrderSubmitted
MacroIronCondorOpened
MacroIronCondorOpenFailed
```

### 15.4 Monitoring and hedge commands

```text
RevalueMacroIronCondorCommand
EvaluateForwardLossHedgeCommand
OpenForwardLossFuturesHedgeCommand
UpdateIntrinsicTimeHedgeFloorCommand
CloseForwardLossFuturesHedgeCommand
```

### 15.5 Monitoring and hedge events

```text
MacroIronCondorRevalued
ForwardLossThresholdCrossed
ThreatenedSideIdentified
ForwardLossHedgeArmed
ForwardLossHedgeEntryApproved
ForwardLossHedgeEntryRejected
ForwardLossFuturesHedgeOpened
IntrinsicTimeHedgeFloorActivated
IntrinsicTimeHedgeFloorViolated
ForwardLossFuturesHedgeCloseRequested
ForwardLossFuturesHedgeClosed
ForwardLossHedgeDisarmed
ForwardLossHedgeEpisodeCompleted
ForwardLossHedgeFailed
```

### 15.6 Queries

```text
GetMacroIronCondorOpportunityQuery
GetMacroIronCondorPositionQuery
GetForwardLossHedgeStateQuery
GetMonthlyStrategyEligibilityQuery
GetCombinedPositionRiskQuery
```

## 16. Event-sourced state

### 16.1 Opportunity aggregate

```text
OpportunityId
PortfolioId
FundId
StrategyDefinitionId
StrategyConfigurationVersion
MacroEventId
MacroEventType
MacroEventRevision
WindowStartUtc
WindowEndUtc
State
EvaluationCount
LatestEvaluationSnapshotId
EligibilityDecision
ReasonCodes
SelectedPositionId
```

### 16.2 Position strategy state

```text
PositionId
OpportunityId
IronCondorOrderId
LegContractIds
LegQuantities
Expiration
InitialCredit
MaximumDefinedLoss
EntryGreeks
CurrentGreeks
CurrentForwardLoss
ForwardLossRatio
ThreatenedSide
CombinedPnL
CombinedLossRatio
PositionHealth
CloseState
```

### 16.3 Hedge episode state

```text
HedgeEpisodeId
PositionId
ForwardLossExcursionNumber
ThreatenedSide
ArmedAtUtc
ArmingForwardLossRatio
DirectionChangeEventId
AttemptConsumed
HedgeOrderId
HedgePositionId
Direction
Quantity
EntryPrice
ThresholdDelta
ExtremePrice
MaximumFavourableExcursion
HighestActivatedFloor
ActiveFloorPrice
HedgePnL
State
CloseReason
CompletedAtUtc
```

All state changes occur through persisted events. Rehydration must recreate identical decisions without querying current market data.

## 17. Idempotency, concurrency, and failure behavior

- One opportunity aggregate exists for each portfolio, fund, macro event, and strategy configuration.
- Only one evaluation is active per opportunity entity.
- Only one open iron-condor order may be pending for an opportunity.
- Only one hedge order may be pending or open for a position.
- Command idempotency keys prevent duplicate broker submissions.
- Broker execution IDs and permanent order IDs are persisted and reconciled.
- A completed hedge attempt remains consumed after replay.
- Stale evaluation snapshots cannot receive new risk approval.
- A pipeline actor emits exactly one `Completed` or `Failed` terminal event for each invocation.
- Failures stop the current workflow. V1 performs no automatic actor retry.
- Optional actor timeouts and manually initiated cancellation use the established workflow design.
- Recovery reconciles broker positions before issuing any new position-changing command.

## 18. Configuration model

```yaml
strategy:
  name: MacroWindowMonthlyIronCondorStrategy
  macroEventType: FOMC # or NFP
  opportunityWindow: 7d
  postEventBlackout: TBD
  stabilizationPeriod: TBD
  allowSecondaryMonthlyOpportunity: false
  maximumMonthlyPositions: 1

composition:
  minimumDte: 30
  maximumDte: 45
  targetShortPutDelta: TBD
  targetShortCallDelta: TBD
  putWingWidth: TBD
  callWingWidth: TBD
  minimumCredit: TBD
  minimumCreditToWidthRatio: TBD
  maximumBidAskWidth: TBD

hedge:
  enabled: true
  armForwardLossRatio: 1.00
  disarmForwardLossRatio: 0.75
  requireNewDirectionChangeAfterArming: true
  hedgeFraction: TBD
  maximumHedgeContracts: TBD
  attemptsPerExcursion: 1
  floorLevels: [0.50, 1.00, 1.50, 2.00]
  retainTwoThresholdFloor: true

risk:
  maximumCombinedLossRatio: TBD
  maximumHedgeLoss: TBD
  maximumHedgeDuration: TBD
  vixNoTradeThreshold: 30
```

Every configuration record includes an immutable version, effective interval, author or approving authority, reason for change, and backtest or paper-trading evidence reference.

## 19. Deterministic decision pseudocode

### 19.1 Monthly opportunity evaluation

```csharp
Decision EvaluateOpportunity(EvaluationSnapshot snapshot)
{
    if (!snapshot.Window.IsOpen) return NoTrade("WindowClosed");
    if (snapshot.Portfolio.HasMonthlyIronCondor) return NoTrade("MonthlyLimitReached");
    if (!snapshot.Calendar.IsReliable) return NoTrade("CalendarUnavailable");
    if (snapshot.Calendar.IsBlackout) return NoTrade("MacroBlackout");
    if (!snapshot.Regime.EntryEligible) return NoTrade(snapshot.Regime.Reason);
    if (!snapshot.MarketCondition.EntryEligible) return NoTrade(snapshot.MarketCondition.Reason);
    if (!snapshot.OptionChain.IsFreshAndExecutable) return NoTrade("OptionChainInvalid");
    if (!snapshot.Fund.Allows(snapshot.Strategy)) return NoTrade("FundMandateRejected");

    var bias = SelectBias(snapshot.Regime, snapshot.MarketCondition);
    var candidate = ComposeIronCondor(snapshot, bias);

    if (!candidate.SatisfiesAllConstraints) return NoTrade(candidate.Reason);
    return SubmitForRiskApproval(candidate);
}
```

### 19.2 Hedge evaluation

```csharp
HedgeDecision EvaluateHedge(HedgeSnapshot snapshot)
{
    if (!snapshot.HedgePolicy.Enabled) return NoHedge("Disabled");
    if (!snapshot.IronCondor.IsOpen) return NoHedge("PositionNotOpen");
    if (snapshot.ForwardLossRatio < snapshot.Policy.ArmRatio) return NoHedge("BelowArmRatio");
    if (snapshot.ThreatenedSide == ThreatenedSide.None) return NoHedge("NoDirectionalThreat");
    if (snapshot.ActiveOrPendingHedge) return NoHedge("HedgeAlreadyExists");
    if (snapshot.Episode.AttemptConsumed) return NoHedge("AttemptConsumed");
    if (!snapshot.Regime.HoldEligible) return NoHedge("RegimeIneligible");
    if (!snapshot.MarketCondition.FuturesHedgeEligible) return NoHedge("MarketIneligible");
    if (!snapshot.DirectionChange.Confirms(snapshot.ThreatenedSide)) return ArmOnly();

    var quantity = CalculatePartialDeltaHedge(snapshot);
    if (quantity == 0) return NoHedge("QuantityBelowMinimum");
    return SubmitForRiskApproval(quantity, snapshot.ThreatenedSide);
}
```

### 19.3 Floor update and exit

```csharp
HedgeAction EvaluateOpenHedge(HedgeSnapshot snapshot)
{
    if (snapshot.EmergencyExit) return Close("EmergencyExit");
    if (snapshot.CombinedLossLimitReached) return CloseAll("CombinedLossLimit");
    if (!snapshot.IronCondor.IsOpen) return Close("IronCondorClosed");
    if (!snapshot.Regime.HoldEligible || !snapshot.MarketCondition.HoldEligible)
        return Close("HoldIneligible");
    if (snapshot.ForwardLossRatio <= snapshot.Policy.DisarmRatio)
        return Close("ForwardLossRecovered");
    if (snapshot.ThreatenedSideChanged) return Close("ThreatenedSideChanged");

    var raisedFloor = DetermineHighestReachedFloor(snapshot);
    if (raisedFloor > snapshot.ActiveFloor) return ActivateFloor(raisedFloor);
    if (snapshot.ActiveFloor.HasValue && snapshot.ExecutablePriceCrossedFloor)
        return Close("OvershootFloorViolated");
    if (snapshot.OppositePrimaryDirectionChange) return Close("OppositeDirectionChange");
    if (snapshot.MaximumHedgeDurationReached) return Close("MaximumDuration");

    return Hold();
}
```

## 20. Observability

### 20.1 Logs and traces

Every decision records:

- portfolio, fund, opportunity, workflow, position, and hedge-episode identifiers;
- macro event and calendar revision;
- strategy and configuration version;
- evaluation snapshot identifier;
- all gate results and reason codes;
- selected regime, market condition, and bias;
- option-chain age and selected quotes;
- theoretical and executable values;
- RiskManager decision;
- broker order and fill identifiers;
- forward-loss calculation inputs;
- threatened-side evidence;
- DC event and threshold;
- floor activation and violation; and
- combined P and L before and after every hedge action.

### 20.2 Metrics

At minimum, publish:

- opportunities opened, eligible, expired, rejected, and failed;
- trades opened by macro-event type and bias;
- time from macro release to entry;
- candidate and order rejection reasons;
- fill ratio, slippage, and cancel-replace count;
- forward-loss threshold crossings;
- hedge arms, entries, exits, and rejected entries;
- hedge P and L and combined-position P and L;
- maximum adverse and favourable excursions;
- floor level reached and retained profit;
- avoided loss estimate with clearly documented counterfactual limitations;
- event-processing latency, queue depth, and p99 latency; and
- reconciliation differences between internal and broker state.

## 21. Backtesting requirements

The strategy does not have demonstrated positive expected value until tested with realistic historical data and out-of-sample validation.

### 21.1 Required data

- Accurate FOMC and NFP release timestamps and revisions.
- ES futures trades or quotes sufficient to reproduce intrinsic-time events.
- Historical ES futures option chains with bid and ask data.
- Contract specifications, expirations, strike listings, and settlement behavior.
- VIX and term-structure inputs used by the production policy.
- Regime and market-condition inputs without future leakage.
- Realistic commissions, exchange fees, spread, slippage, and latency assumptions.

### 21.2 Test partitions

Report results separately for:

- FOMC and NFP;
- balanced, bullish-biased, and bearish-biased positions;
- low, normal, high, and excluded volatility regimes;
- upward and downward threatened sides;
- hedge enabled and hedge disabled;
- each combined-loss limit;
- each tested hedge fraction;
- each intrinsic-time threshold; and
- early versus late entry within the seven-day window.

### 21.3 Mandatory comparisons

Compare at least:

1. Iron condor with no futures hedge.
2. Iron condor with the proposed one-attempt hedge.
3. Immediate futures hedge at 100 percent forward loss.
4. DC-confirmed futures hedge at 100 percent forward loss.
5. Floor ladder versus opposite-DC-only exit.
6. FOMC-only, NFP-only, and portfolio-arbitrated opportunities.
7. Seven-day eligibility window versus fixed post-event entry delays.

### 21.4 Evaluation statistics

- Net expectancy and confidence interval.
- Win rate, average win, and average loss.
- Profit factor and return on defined risk.
- Maximum drawdown and time under water.
- Tail loss percentiles and expected shortfall.
- Maximum adverse excursion.
- Combined loss relative to initial credit.
- Hedge contribution conditional on market continuation and reversal.
- Turnover, commissions, slippage, and hedge churn.
- Percentage of months with no trade.
- Stability across rolling and out-of-sample periods.

### 21.5 Backtest integrity

- Use only information available at each simulated decision time.
- Build intrinsic-time events from sequential prices without hindsight.
- Use executable bid or ask assumptions rather than midpoint fills.
- Model multi-leg fill uncertainty and rejected orders.
- Freeze a parameter set before each out-of-sample period.
- Preserve losing, expired, rejected, and failed opportunities in results.
- Do not treat an unfilled candidate as a completed trade.

## 22. Paper-trading rollout

### Phase 1: Shadow decisions

Run macro opportunities, composition, forward-loss monitoring, and hedge decisions without submitting orders. Verify event timing, deterministic replay, Greeks, forward loss, and reason codes.

### Phase 2: Paper iron condors without hedge execution

Submit paper iron condors. Continue calculating hypothetical hedge actions and compare them with the unhedged position.

### Phase 3: Paper iron condors with one-attempt hedge

Enable paper futures hedging with the smallest approved size. Confirm combined-position accounting, floor behavior, hysteresis, and broker reconciliation.

### Phase 4: Extended validation

Operate for the planned six-to-twelve-month paper period. Do not promote based on a small number of favorable macro events.

### Phase 5: Small production consideration

Production requires explicit approval based on stable out-of-sample and paper results, operational readiness, margin capacity, kill-switch testing, and explainable parameter selection.

## 23. Testing and acceptance criteria

### 23.1 Unit tests

- Opportunity-window boundaries and event rescheduling.
- FOMC and NFP arbitration.
- Every eligibility gate and reason code.
- Balanced and directional bias selection.
- Candidate ranking and deterministic tie breaking.
- Forward-loss normalization.
- Threatened-side detection.
- Hedge sizing and rounding.
- Episode identity and one-attempt enforcement.
- Hysteresis at arm and disarm boundaries.
- Long and short floor activation.
- Floor monotonicity.
- Exit precedence and duplicate suppression.
- Combined P and L and loss ratios.

### 23.2 Integration tests

- Calendar through completed or expired opportunity.
- Live-chain snapshot through multi-leg paper order.
- Position revaluation through hedge arming.
- DC event through futures hedge fill.
- Price continuation through every floor.
- Price reversal before the first floor.
- Recovery below the disarm level.
- Iron-condor exit while hedge is open.
- Broker disconnect, reconnect, and reconciliation.
- Actor failure without automatic retry.
- Replay producing identical aggregate state.

### 23.3 Acceptance criteria

The implementation baseline is complete when:

1. Every decision can be replayed from persisted inputs and events.
2. No macro event directly forces a trade.
3. No order bypasses RiskManager.
4. The system cannot open more than one monthly position under V1 policy.
5. The system cannot open more than one hedge per episode.
6. Hedge floors never move backward.
7. A floor violation does not wait for a later DC confirmation.
8. Combined-position risk includes both instruments and all known costs.
9. Closing the iron condor cannot leave an unmanaged futures hedge.
10. Broker reconciliation prevents duplicate or orphan positions.
11. All no-trade, rejection, failure, and exit outcomes have stable reason codes.
12. Backtests reproduce the production decision code or a verified equivalent library.

## 24. Principal risks and controls

| Risk | Control |
|---|---|
| Post-event whipsaw | Stabilization interval, adverse DC confirmation, one hedge attempt per excursion, and hedge loss limit. |
| Hedge over-sizing | Partial delta hedge, conservative rounding, contract cap, and RiskManager approval. |
| Changing option delta | Revalue Greeks continuously but do not continuously rebalance in V1. |
| Volatility-only loss mistaken for directional threat | Require a clear threatened side and adverse DC confirmation. |
| Hedge loss after mean reversion | Intrinsic-time exit, floor ladder, opposite DC exit, and combined hard stop. |
| Repeated arm and disarm | Separate 1.00 arm and lower disarm ratios. |
| Stale option quotes | Chain freshness and spread gates. |
| Macro calendar error | Versioned source, uncertainty rejection, and reschedule handling. |
| Event overlap | Upcoming-event blackout and portfolio opportunity arbitration. |
| Orphan futures position | Parent-child position identity, close coordination, and broker reconciliation. |
| Backtest overfitting | Small parameter set, walk-forward tests, frozen out-of-sample configuration, and paper validation. |
| Assumed 200 percent loss protection | Mandatory combined hard stop; hedge described only as a conditional offset. |

## 25. Deferred decisions

The following require research and configuration approval:

- FOMC and NFP blackout and stabilization durations.
- FOMC versus NFP monthly priority rules.
- Balanced and biased short-strike delta targets.
- Wing widths and minimum credit requirements.
- Exact forward-loss definition and scenarios.
- Threatened-side delta and distance thresholds.
- Hedge fraction and ES versus MES permission.
- Primary and hedge intrinsic-time thresholds.
- Combined-position maximum-loss ratio.
- Hedge maximum loss and maximum duration.
- Normal iron-condor profit target and DTE exit.
- Whether a secondary monthly opportunity is ever allowed.
- Whether the 2.00 floor remains fixed or closes immediately.

## 26. Implementation sequence for Codex

1. Add immutable configuration records and validation.
2. Implement macro opportunity and arbitration aggregates.
3. Add eligibility snapshot contracts and stable reason codes.
4. Integrate strategy selection with existing RegimeDiscovery and MarketCondition outputs.
5. Add the macro iron-condor order-composition policy to the existing trade-strategy family.
6. Integrate option-chain snapshots and the Black-76 pricer.
7. Implement opportunity, position, and hedge episode event-sourced states.
8. Add forward-loss normalization and threatened-side detection to PositionMonitor.
9. Implement hedge arming, DC confirmation, sizing, and one-attempt enforcement.
10. Implement the intrinsic-time floor state machine.
11. Implement combined-position ExitDecision and RiskManager checks.
12. Add OrderExecution integration and broker reconciliation.
13. Add observability and operations-summary UI fields.
14. Build deterministic unit, replay, integration, and backtest fixtures.
15. Run shadow mode before enabling paper orders.

## 27. Final strategy statement

`MacroWindowMonthlyIronCondorStrategy` uses FOMC or NFP as a monthly opportunity anchor and waits up to seven days for an approved post-event regime and market condition. It opens a defined-risk balanced or directionally biased ES futures-options iron condor only when executable option-chain and portfolio-risk requirements are satisfied.

If forward loss reaches 100 percent of initial credit and the position develops a clear directional threat, `ForwardLossIntrinsicTimeFuturesHedgePolicy` may open one partial ES futures hedge after an adverse intrinsic-time directional-change confirmation. The hedge uses monotonic 0.50, 1.00, 1.50, and 2.00 threshold profit floors and closes on the first applicable recovery, reversal, invalidation, parent-position, or risk condition.

The strategy does not assume that the hedge recovers prior loss or guarantees a loss below 200 percent of credit. Its edge and loss-reduction value must be demonstrated independently for FOMC and NFP through realistic backtesting, shadow operation, and extended paper trading.
