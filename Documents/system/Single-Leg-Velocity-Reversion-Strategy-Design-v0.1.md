# Single Leg Velocity Reversion — ES/MES Futures

**Version:** 0.1  
**Date:** 2026-11-01  
**Status:** Research design for deterministic backtesting and paper trading; edge not established  
**Purpose:** Explain the strategy, entry conditions, protective-stop placement, and minimum implementation and validation requirements.

## 1. Strategy in one page

The strategy seeks a meaningful intraday retracement after a substantial directional move has entered a qualifying intrinsic-time overshoot. It uses the existing intrinsic-time trend-reversal events as its primary entry trigger.

For a short: qualify an upward overshoot, observe two consecutive downward reversal events, pass current market and risk checks, then sell futures. Place the protective-stop trigger at the pre-entry overshoot high. For a long: mirror these rules after a downward overshoot, with the stop at the pre-entry overshoot low.

The initial comparison is two versus three consecutive reversal events, with other rules unchanged. Smaller price swings do not qualify merely because reversal events occur. Strong broader trends are context, not automatic rejection reasons.

| Item | Baseline |
| --- | --- |
| Product | One outright ES or MES futures position; no option legs |
| Horizon | Intraday; predefined session exit |
| Opportunity | Large, qualified overshoot followed by progressive reversal |
| Entry trigger | Two consecutive reversal events; three-event variant for comparison |
| Stop location | Recorded overshoot high for shorts; recorded overshoot low for longs |
| Profit exit | One predefined, versioned rule; proposed fixed reward-to-risk target for initial research |
| Position limit | User's intended live ceiling: four MES; actual size also constrained by risk and available margin |
| Learning | No LLM or RL in the baseline |

No profitability, win rate, whipsaw reduction, or trade frequency is assumed. A few opportunities per day is an intention, not a quota or established observation.

## 2. Proposed edge

The hypothesis is that a sufficiently extended move, followed by the user's progressive reversal-event sequence, sometimes produces further retracement large enough to compensate for stopped trades, costs, and slippage.

An extended move can also resume. Entry after reversal events establishes retracement progress, not proof that the larger trend has ended. The strategy is allowed to capture a temporary pullback within a continuing trend.

The desired payoff could be many controlled losses and fewer larger gains, but that distribution must emerge from measured entry and exit behaviour. It is not guaranteed by overshoot qualification.

## 3. Intrinsic-time terminology and state

| Term | Meaning in this design |
| --- | --- |
| Signal threshold, delta | Existing intrinsic-time threshold, expressed consistently in points for the excursion |
| DC confirmation | Existing DirectionChanged event confirming a reversal of the required primary size |
| Overshoot | Extension in the established direction beyond the corresponding DC confirmation |
| Maintained extreme | Highest price observed in an upward excursion, or lowest in a downward excursion |
| Trend-reversal event | Existing event emitted when price crosses another configured reversal threshold, expressed as a percentage of the signal threshold in points |
| Consecutive events | Distinct qualifying crossings in the reverse direction, under the existing event producer's sequence/reset semantics |

Preserve the existing extremes implementation: the extreme changes only when exceeded in the established direction or when the DC state changes. Do not replace that behaviour with a moving average or continuously shifting stop.

The final maximum overshoot is known only retrospectively. Live decisions use the maintained extreme known at that moment. Before entry, a new extreme can update the setup reference. After entry, the original stop reference is frozen and must not be moved farther away to accommodate a losing trade.

Record excursion ID, event ID, source sequence, extreme revision, prices, and timestamps. Duplicate delivery must not increment the event count. Confirm the producer's handling of recovery between crossings and a single tick crossing multiple levels; do not silently redefine those semantics in the strategy.

For a consistent point threshold within an excursion:

- Upward overshoot extension = maintained high minus upward DC confirmation price.
- Downward overshoot extension = downward DC confirmation price minus maintained low.
- Short-setup reversal depth = maintained high minus current price.
- Long-setup reversal depth = current price minus maintained low.
- Normalized extension or reversal = corresponding points divided by signal threshold points.

If the producer uses percentage or dynamic thresholds, reuse its authoritative conversion and record the applicable threshold/version. Avoid changing the normalization midway through a measured excursion without an explicit transition rule.

## 4. Overshoot qualification and velocity

Reversal counting becomes actionable only after overshoot has qualified. Merely entering the overshoot phase is distinct from reaching the minimum extension intended to isolate large moves.

Baseline qualification requires the existing overshoot state plus a configured minimum overshoot extension in threshold units. Its numerical value is unresolved and must be selected in development testing.

Velocity describes the speed of the move. A simple diagnostic is signed price change divided by elapsed seconds. Calculate it over explicitly defined windows; distinguish whole-excursion average velocity from recent directional velocity. Slower upward progress can arm an exhaustion candidate, but cannot by itself trigger a short.

For the first baseline, record velocity measurements and use the event trigger deterministically. A mandatory velocity cutoff or deceleration filter is an optional experimental addition, not an already-approved numerical rule. This lets testing determine whether velocity improves the simpler overshoot/reversal strategy.

## 5. Exact entry rules

### 5.1 Short entry after upward overshoot

1. The active signal contract is in the upward intrinsic-time phase and overshoot has reached the configured qualification level.
2. Capture the excursion identity, applicable threshold, maintained high, and extreme revision.
3. Observe the existing sequence of downward reversal events. The first event arms the setup; the second qualifies the baseline entry trigger.
4. Confirm both events belong to the valid current sequence. Do not count duplicate notifications or combine events across a reset.
5. Confirm the setup has not been superseded by a new extreme, DC transition, expiry, or other configured invalidation before submission.
6. Require valid regime context, current market tradeability, operational readiness, available strategy permission, and final risk approval.
7. Compose a sell order within an explicit entry-price tolerance. If the move has already run beyond that tolerance, skip rather than assume a historical trigger-price fill.
8. On actual fills, maintain a short position and protective buy-stop coverage for the filled quantity. Reconcile partial fills and stop acknowledgments.

The three-event variant waits for the third qualifying event. A full-DC entry is a separate benchmark. Do not mix these policies opportunistically during the same test.

### 5.2 Long entry after downward overshoot

Apply the mirror: qualify downward extension, require consecutive upward reversal events, buy futures, and protect the position with a sell-stop trigger at the recorded overshoot low.

### 5.3 Broader context and tradeability

A bullish broader regime can coexist with a valid short-retracement setup. It does not need to become bearish first. Regime direction, strength, volatility, and structure remain available for analysis and later conditional rules.

Hard entry checks concern current data fitness, spread/depth, session eligibility, configured event exclusions, operational health, available risk budget, and whether the trigger remains actionable. Do not add many indicator filters before measuring the baseline.

Proposed research session: U.S. cash-market hours, 09:30–16:00 America/New_York, with a configured last-entry time and flatten time before the session ends. These are strategy choices, not statements of IBKR discounted-margin hours. Use the exchange calendar, holidays, early closes, and daylight-saving rules. Evaluate opening minutes and scheduled-news windows separately.

## 6. Protective stop: location versus execution

### 6.1 Baseline stop placement

| Position | Protective order side | Baseline stop trigger |
| --- | --- | --- |
| Short | Buy | Pre-entry upward overshoot high |
| Long | Sell | Pre-entry downward overshoot low |

Freeze the stop-reference extreme when the entry is accepted and revalidate before submission. If it changes while the entry is pending, invalidate/cancel the stale entry safely and reconcile any fills. Do not widen the protective boundary after a fill because the market moved against the position.

The extreme is the invalidation reference: a return there means the immediate retracement thesis has failed under the baseline rule. It is not a prediction of the fill price. A small fixed buffer beyond the extreme is a separately versioned experiment, not the baseline.

### 6.2 Preferred simple exit behaviour

The user's priority is to exit a failed trade, rather than hold an unfilled order to obtain an exact price. Use an execution-prioritizing protective stop configuration supported by IBKR for the exact contract and route, after confirming its trigger and handling semantics. Do not assume a tight stop-limit provides a maximum monetary loss.

- A stop-limit activates a limit order and may remain unfilled as price moves away.
- A stop-market style exit prioritizes execution, but does not guarantee the stop price or immediate execution.
- CME native Stop with Protection has an exchange-defined execution range; unfilled quantity may rest at the protection boundary.
- IBKR may simulate certain stop orders. Exact contract, routing, trigger method, trading session, and order acknowledgments must be verified.
- A halt, unavailable liquidity, exchange protection, connectivity failure, or order rejection can prevent or delay an exit.

Adaptive Market/Limit can be evaluated separately for execution. It does not decide whether a reversal is real or prevent premature stop-outs. Do not assume Adaptive Stop Limit support for ES/MES from support for ordinary Adaptive futures orders.

### 6.3 Minimum exit lifecycle

The strategy issues an exit intention; OrderExecution owns order state and confirmed fills. An accepted stop or triggered order is not proof of a completed exit.

Track broker order identity, acknowledgment, status, cumulative fills, remaining quantity, and current broker position. If an exit is rejected, canceled unexpectedly, partially filled, or remains unfilled after triggering, invoke a predefined bounded escalation policy and operational alert. Its timing and permitted order changes require implementation testing; no numerical timeout is prescribed here.

Before replacing an exit, reconcile fills and cancellation status. Never submit overlapping full-size exits blindly. Coordinate profit and stop exits through supported OCA/bracket behaviour plus fill reconciliation. Stop coverage must follow the remaining position quantity. The position is closed only when fills and broker position confirm flatness.

## 7. Profit-taking and other exits

Profit-taking has not yet been selected by the user. A complete backtest needs an explicit rule. Proposed initial research choice: a fixed target at a configured multiple k of actual entry-to-stop distance, with k selected in development testing.

- Short target = actual average entry minus k times stop distance.
- Long target = actual average entry plus k times stop distance.

Use this unchanged when initially comparing two and three reversal events. Because the entry price changes, actual risk distance and the resulting target change. Record that consequence rather than treating both entries as identical trades.

Also define:

- A follow-through timeout measured from the first fill; exit a stalled trade according to a configured progress rule.
- A scheduled session flatten time.
- A maximum holding time and response to operational invalidation.

Numerical values remain research parameters. Reaching primary DC after entry does not automatically close the position or move its frozen stop reference; it can support continued retracement. Any different DC-based exit must be a separately specified variant.

Trailing exits, scale-outs, and intrinsic-time profit ladders are later candidates. They are excluded from the initial fixed-target comparison to keep results interpretable.

## 8. Worked examples

All prices and thresholds below are illustrative, not recommended settings or observed results. Assume an 8-point primary threshold and reversal events every additional 2 points from the maintained extreme.

### Short

| Step | Price / action |
| --- | --- |
| Upward DC confirmation | 6,000 |
| Qualified overshoot high | 6,020; 20 points beyond confirmation |
| First downward reversal | 6,018; arm |
| Second downward reversal | 6,016; evaluate sell |
| Assumed actual fill | Short four MES at 6,016 |
| Protective buy-stop trigger | 6,020 |
| Planned stop distance | 4 points |
| Illustrative 2R target | 6,008 |

At four MES, planned price risk is 4 × 4 × USD 5 = USD 80. Gross target profit is 8 × 4 × USD 5 = USD 160. Realized results include commissions, spread, and slippage; actual loss can exceed USD 80.

If the trade fills after the third event at 6,014, the same extreme gives 6 points of stop distance and USD 120 planned price risk for four MES. Under equal monetary risk, quantity would need to be reduced, subject to whole-contract sizing.

### Long

After a qualified downward overshoot to 6,000, upward reversal events occur at 6,002 and 6,004. An illustrative long fill at 6,004 has a protective sell-stop trigger at 6,000, four points of planned risk, and a 2R target at 6,012.

## 9. Position sizing and capital

MES point value is USD 5 per contract; its 0.25-point tick is USD 1.25. ES point value is USD 50. Four MES therefore gain or lose USD 20 per index point before costs.

Proposed sizing calculation:

`Quantity = min(configured contract cap, risk-budget quantity, margin-supported quantity)`

Calculate risk-budget quantity using stop distance, point value, estimated round-trip costs, and an explicit slippage allowance. Reject zero/invalid distance and skip when even one contract exceeds the risk budget. The allowance is an estimate, not a guaranteed loss cap.

The intended ceiling is four MES, not a requirement to enter four every time. Margin is collateral and changes with broker requirements and portfolio positions. Obtain current account-specific margin impact rather than embedding earlier quoted dollar figures. Leave capacity for adverse P&L and changing requirements.

For the baseline, no averaging into losses, no automatic reversal into the opposite position after a stop, and one attempt per identified qualified excursion are proposed controls. A new high alone must not silently create unlimited attempts; explicitly define excursion/rearm identity. Daily loss limits and maximum simultaneous exposure are independently enforced by risk controls.

## 10. Actor integration

This section is a proposed integration outline based on this strategy discussion, not a replacement or amendment to existing authoritative actor specifications.

| Component | Responsibility |
| --- | --- |
| Intrinsic-time producer / TrendState | Maintain authoritative DC, overshoot, extreme, and reversal-event state |
| Strategy Workflow | Admit eligible reversal triggers, preserve ordering and one inflight evaluation per entity, freeze inputs/configuration |
| RegimeDiscovery | Supply broader trend, volatility, structure, and quality evidence |
| MarketCondition | Supply current executable-product tradeability, freshness, liquidity, and trigger-validity evidence |
| SingleLegVelocityReversionStrategy | Qualify overshoot, apply event-count policy, propose direction and frozen stop reference |
| OrderComposer | Build exact contract, quantity, entry tolerance, and protective-order intent |
| RiskManager | Approve candidate risk and margin; retain final execution authority |
| OrderExecution | Submit and reconcile entries, protective orders, exits, and broker positions |
| PositionMonitor / ExitCondition | Monitor target, timeout, session exit, and strategy invalidation |

If workflows currently start only on primary DC, add explicit eligible reversal-trigger routing. Evaluations should complete rather than keep a calculation stage waiting indefinitely for another event. A later eligible event may start another evaluation; no automatic business retry is implied.

Use a shared immutable excursion snapshot containing contract identity, event/cycle IDs, extreme revision, DC direction and confirmation, threshold, overshoot extension, reversal depth/count, velocity measurements, timestamps, source sequence, and parameter version.

If signals come from ES and execution uses MES, verify matching contract month/reference and explicitly record the mapping. Evaluate MES quotes for fills and translate the ES extreme into the configured MES stop reference; any direct same-price mapping is a declared assumption to test. ES and MES have separate books. MES support must be explicit where current evaluator contracts are ES-only.

## 11. Backtesting and measurement

Use the available ES historical data to test signal behaviour causally. For MES execution analysis, obtain MES quotes/trades over its available history. MES launched in 2019, so ten years of ES data is not ten years of actual MES fills. ES-only replay can use a clearly labeled execution approximation; it cannot establish actual MES slippage.

Reconstruct state from data available at each decision time. Never use the eventual maximum overshoot to generate an earlier entry, fill at an already-passed trigger price, or use future indicators/news classifications. Respect feed/order latency and source event ordering. Bar data alone cannot reliably resolve entry/stop/target ordering within a bar.

First experiment: fixed overshoot qualification, two versus three reversal events, equal monetary risk, one exit policy, and identical session/risk constraints. Compare full-DC entry as a benchmark if useful. Change one policy dimension at a time and record all trials.

Use chronological development, validation, and untouched test periods, followed by walk-forward evaluation and paper trading. Prevent leakage from overlapping excursions or positions across period boundaries. Costs include commissions, exchange/broker fees, crossing the spread, slippage, partial fills, missed entries, and exit failures. Stress execution assumptions.

Record:

- Number of independent qualifying excursions and trades, frequency, and exposure time.
- Net expectancy, win rate, average win/loss, payoff ratio, profit factor, and net return on allocated capital.
- Maximum drawdown, losing streaks, tail losses, worst day/month, and uncertainty around results.
- Maximum favourable/adverse excursion; stop slippage and trigger-to-flat time.
- Stop-outs followed by subsequent favourable retracement within a predefined observation horizon.
- Performance by direction, session, volatility, regime, scheduled-event conditions, and calendar period.
- Profit concentration in a few trades and sensitivity to nearby parameter settings.

Net expectancy = win probability × average gross win − loss probability × average gross loss − average total costs. Positive historical expectancy does not establish future profitability. Compounding also depends on sizing, drawdowns, and changing conditions.

## 12. Open decisions before a complete executable baseline

| Decision | Current status |
| --- | --- |
| Minimum overshoot qualification | Required; numerical setting unresolved |
| Reversal-event count | Two baseline; three comparison |
| Existing producer sequence/reset semantics | Preserve and document precisely |
| Mandatory velocity filter | Deferred; record features first |
| Protective-stop reference | Prior maintained overshoot extreme |
| IBKR order type, route, trigger method | Verify for the exact contract; prioritize exit execution |
| Profit-taking | Proposed fixed-R research baseline; k unresolved |
| Timeout/progress threshold | Required for selected baseline; values unresolved |
| Entry tolerance, last entry, flatten time | Configurable; unresolved |
| Risk budget and daily loss cap | Required; unresolved |
| Signal/execution instrument mapping | Explicit ES-to-MES policy if used |
| Exit escalation | Bounded, fill-aware handling; numerical rules unresolved |

The design is clear enough to implement a configurable research harness. It is not a finalized production trading policy until these settings and execution behaviour are resolved and validated.

## 13. Later refinements

Only add features when measured evidence warrants them: buffered stops, confirmed-invalidation exits, velocity filters, flow evidence, regime-conditioned event counts, alternative profit exits, or learned policy selection. LLM advisory, contextual models, and reinforcement learning remain outside the deterministic baseline. Weekly option-spread adaptation is a separate strategy with separate execution and payoff validation.

## 14. Primary references

Checked 2026-11-01. Strategy rules above are proposed research rules; these references support product and order mechanics.

1. [CME — MES contract information](https://www.cmegroup.com/markets/equities/sp/micro-e-mini-sandp-500.html): multiplier and tick size.
2. [CME — Micro E-mini FAQ](https://www.cmegroup.com/articles/faqs/frequently-asked-questions-micro-e-mini-equity-index-futures.html): micro/mini sizing and launch background.
3. [IBKR Canada — Order Types and Algos](https://www.interactivebrokers.ca/en/trading/ordertypes.php): stop, stop-limit, Adaptive, simulated-order and Globex protection mechanics.
4. [CME — Glossary](https://www.cmegroup.com/education/glossary): Stop with Protection and unfilled quantity.
5. [IBKR — Adaptive Stop Limit lesson](https://www.interactivebrokers.com/campus/trading-lessons/adaptive-stop-limit/): adaptive execution after the stop trigger; exact product support must be verified.

