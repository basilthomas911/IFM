# Intrinsic-Time ATR and EMA Continuation Update

Draft v0.1 · 2026-11-19 · Design for updating the existing C# implementation in Codex

## 1. Intended change

Update the current fixed 10-point, both-direction DC trading policy to evaluate a volatility-adjusted continuation policy. Derive the threshold from five-minute ATR, use the five-minute 20/50/200 EMA stack to permit a direction, and arm an entry at the DC with execution eligibility after a further quarter-threshold move. Evaluate whether these changes improve net results across historical periods.

This document specifies an update to the user's existing algorithm, actor workflow, replay engine, and execution path. Codex should inspect the repository and map these requirements to existing owners and contracts before making changes. The parameter examples are starting configurations for backtesting. Existing callers need an explicit policy/version choice so historical comparisons remain reproducible.

## 2. Baseline settings

| Setting | Draft value or rule |
|---|---|
| Signal instrument | ES futures trade ticks through the existing ITI generator |
| Initial execution | Fixed configured MES quantity using the existing order composer and risk approval |
| Session | Regular trading hours, normally 09:30–16:00 Eastern, using the existing exchange calendar and session cutoff |
| Volatility input | ATR(14) on completed five-minute bars |
| Direction input | EMA20, EMA50, EMA200 on completed five-minute bars |
| Candidate threshold | max(9 ES points, latest available five-minute ATR) |
| Threshold rounding | Round to the configured tick grid using one documented midpoint rule |
| Confirmation distance | 0.25 × locked wave threshold, rounded outward to a valid price |
| Directional asymmetry | Start with the same threshold and confirmation factor for both directions |
| Entry attempts | One submitted entry intent per eligible DC wave |

The EMA stack supplies the initial regime filter. The source conversation also proposed a larger intrinsic-time tracker, efficiency ratio, slopes, ADX, and specialized pullback patterns. Carry those as future research inputs after measuring this baseline. No historical market-price or ATR range quoted in the conversation is a fixed implementation requirement.

## 3. Indicator updates and threshold lifecycle

Reuse the existing tick-to-bar, Wilder ATR, and EMA implementations. Select one consistent bar-session convention and record it in the policy. The draft proposes RTH bars with history carried over completed prior RTH sessions for indicator warm-up. EMA200 requires adequate prior history under the existing indicator readiness rules. Restore or replay that history before enabling entries. A 14-bar ATR warm-up alone does not establish EMA200 readiness.

Use bar close times and actual availability to select indicators. At a bar boundary, finalize the prior bar, apply its completed indicator values, then process the new tick for structural and trading decisions under the existing sequence convention. Replay must reproduce that order. Record the input bar timestamp for each decision. Keep indicator values at calculation precision and round derived tradable distances and prices.

Preserve the current structural tracker's active threshold until it confirms the next DC. Test the reversal with that already locked threshold. At the confirmed DC, capture the latest ready ATR and lock the new threshold for the next wave. Record both the threshold that detected the DC and the newly assigned wave threshold. The new threshold supplies this wave's confirmation and stop distances and its next structural reversal threshold. Continue tracking extremes and DCs while flat or entry-blocked. Changing ATR while a wave is active never moves an already armed price level.

At session startup, reuse the existing initialization and warm-up contract. A newly initialized direction cannot alone create a tradable DC. On replay or restart, restore enough state to distinguish an existing wave from a newly confirmed one. Record the contract roll/reset treatment so a contract price gap cannot become an entry.

## 4. Three-state regime definition

| EMA ordering | Regime name | New-entry permission |
|---|---|---|
| EMA20 > EMA50 > EMA200 | Bullish | Long |
| EMA20 < EMA50 < EMA200 | Bearish | Short |
| Every other ordering, including equality | Transitional | No new entries |

These are operational classifications of EMA order. Mixed EMAs can occur during consolidation or a developing reversal. The name Transitional alone supplies no range-trading or options-entry conclusion. Update the regime at each completed bar. Keep price-location and slope conditions out of the first version so the predicate remains explicit.

In a bullish regime a downward DC still records the pullback, but cannot arm a short. When a later upward DC confirms, it can arm a long. Apply the inverse sequence in a bearish regime. If no new aligned DC occurs, a change into an aligned regime alone cannot create an entry. If an armed setup loses directional permission, invalidate it for that wave and wait for a fresh eligible DC.

## 5. Candidate, confirmation, and execution

At each DC capture the observed ES trigger price Pdc, direction s (+1 long, −1 short), wave ID, locked new threshold δ, indicator snapshot, policy version, and time. If flat, session-eligible, and direction-permitted, arm the candidate at Pdc + s × 0.25δ. Round long confirmation prices upward and short prices downward to the tick grid. Use the actual observed DC trade as the anchor when price jumps through the theoretical boundary, consistent with the repository detector.

A later tick reaching or crossing the confirmation level makes the candidate eligible for the normal workflow. Check wave validity, current regime, session eligibility, and order/position state again before submission. An opposing DC, session cutoff, or loss of regime permission invalidates the candidate. One submission consumes the wave's attempt. The next same-wave price change cannot generate a second order while the first is working or after a stop-out.

An entry instruction is distinct from a fill. Mark entry working after submission and update position quantity and average price from broker events. Run every intent through existing risk approval and idempotency handling. Use the existing execution adapter's order policy. Native stop-entry versus local confirmation is a repository-specific execution choice to measure in replay and paper trading.

### Long example

Suppose the five-minute EMA stack is bullish and a pullback low is 5800. The tracker's previously locked reversal distance is 12 points, so a trade at 5812 confirms an upward DC. Assume the latest ATR is also 12, making the new locked wave threshold 12. Arm at 5812 + 3 = **5815**. An observed trade at or above 5815 can submit a long candidate if permission still holds. The actual fill and risk can differ from that trigger.

This quarter-threshold extension is the entry confirmation in this policy. The user's earlier one-full-threshold overshoot milestone is a separate measurement and can retain its existing name in ITI events. Record the numeric distance and policy so terminology cannot change execution behavior.

## 6. Initial protection and trailing exit

The conversation proposes an initial stop 0.5δ against the DC anchor and a 0.65δ trail behind the favorable extreme. The following provisional policy resolves the unspecified activation timing for an implementable first experiment. These choices should be exposed as strategy configuration and compared with the existing exit policy.

| Stage | Draft behavior |
|---|---|
| Initial protection | Long trigger at Pdc − 0.5δ, short trigger at Pdc + 0.5δ, translated to the execution instrument under the existing ES/MES price mapping |
| Watermark | Initialize from actual execution fill and track the execution instrument's favorable high or low |
| Trail activation | Start when favorable excursion from the fill reaches 1.0δ |
| Active trail | Long proposed trigger = favorable high − 0.65δ. Short proposed trigger = favorable low + 0.65δ |
| Structural invalidation | An opposing structural DC requests an exit under this draft policy |
| EMA regime change | Invalidates pending setups. An existing position continues under protection and structural/session exits |
| Session cutoff | Cancel pending entries and close filled exposure using the existing session-exit workflow |

Round the stop in the protective direction and apply a monotonic ratchet. Retain the tighter existing stop when a new proposal would loosen it. Broker submission/acknowledgment determines the effective stop. Handle an entry fill already beyond the intended protective level through the execution adapter's existing immediate-protection/exit handling. Model stop trigger and actual exit fill separately.

In the 5812 DC and 5815 fill example, the initial stop is **5806**, nine points or **0.75δ** below the fill before costs. At an execution high of **5827**, excursion from fill is 12 points and activates the trail. The unrounded proposal is 5827 − 7.8 = **5819.20**, rounded upward to **5819.25** on a 0.25 grid. Use actual MES prices and observed ES/MES basis for execution protection. ES supplies structural levels and MES supplies executable fill and watermark observations.

## 7. Mapping to existing actors

| Existing responsibility | Required update |
|---|---|
| Market data / indicators | Supply ready, completed five-minute ATR and EMA snapshots with their timestamps |
| IntrinsicTime signal owner | Maintain current wave, threshold version, extremes, and DC events under the existing serialized processing contract |
| Regime Discovery | Publish or return the Bullish/Bearish/Transitional classification appropriate to the strategy horizon |
| Market Condition | Resolve the ready indicator snapshot, session condition, and current structural context |
| Trade Strategy | Arm and invalidate confirmation candidates, enforce directional permission, and consume one attempt per wave |
| Order Composer / Risk Manager | Compose fixed MES orders and protection parameters, assess fill-to-stop risk, and approve within current limits |
| Position Monitor / Order Execution | Maintain watermark and acknowledged stops, process structural/session exits, and reconcile execution events |

Locate the current workflow entry point before implementing confirmation. If it runs only on DirectionChanged, it needs an authorized continuation event or equivalent callback when the armed level is reached. Reuse the existing routing and one-in-flight-per-entity rule. Keep per-tick comparisons at the existing signal/monitor owner, and invoke the workflow when a candidate becomes eligible. A later rejection or stale candidate must not create a competing workflow.

Persist or reconstruct candidate status, consumed attempts, policy version, and working position/order state through the existing state/recovery mechanism. On restart reconcile broker state before admitting an entry. Record rejection and invalidation reasons plus DC, indicator, and workflow IDs so replay can explain each decision.

## 8. Probability model compatibility

The ATR and EMA policy is a baseline experiment. The existing probability-based AAIT variant can be tested using the same execution and risk infrastructure. Choose one explicit scoring point per experiment. For scoring at confirmation, construct features available at that time and define the target relative to that candidate. For scoring at DC, freeze that earlier score and retain the DC-based target definition. Evaluate the filtered result on the actual delayed-entry strategy.

Keep the simple one-model, one-cutoff research framework. Record eligible and skipped candidates for comparison, and test whether score bands separate outcomes on unseen sessions. A direct-DC model trained with fixed asymmetric thresholds requires a newly versioned dataset and validation before being applied to these ATR-driven confirmation candidates.

## 9. Verification and backtesting

| Check | Expected evidence |
|---|---|
| Threshold lock | ATR changes mid-wave leave the active δ and armed level fixed. The next DC uses the correct old threshold for detection and new threshold for the next wave. |
| Regime classification | All strict orderings and equality resolve to the specified states using completed-bar values. |
| Confirmation lifecycle | DC arms the correct signed level, rounded outward. Opposing DC or regime loss invalidates it. A rejected/working/closed attempt cannot duplicate in the same wave. |
| Risk calculations | Reproduce the 5812 → 5815 → 5806 example and the 5827 trail at 5819.25, plus mirrored shorts and price jumps. |
| Execution consistency | Partial fill, cancellation/fill race, duplicate event, restart, and stop modification follow existing broker semantics. |
| Point-in-time parity | Replay and live processing see the same indicator availability and policy versions, including bar and session boundaries. |

Replay a fixed historical dataset with realistic MES costs and fills. First reproduce the current fixed 10-point baseline. Then compare ATR sizing, EMA permission, and confirmation in documented incremental variants. Hold risk and exit rules constant while measuring each entry change, and run the new exit policy as a separately identified variant. Report net expectancy, P&L per session, trade count, drawdown, exposure, and long/short results. Reserve chronological periods for evaluating selected parameters. Claims about a particular year's regimes or an expected percentage trade reduction need evidence from the actual replay.

## 10. Options extension from the same discussion

The discussion also proposes daily-bar regime inputs for the existing options strategy family. Preserve these as a separately selectable options policy, with its own testing and execution rules.

| Daily EMA regime | Proposed added condition | Candidate structure |
|---|---|---|
| Transitional | ADX < 20 and VX < 19 | Symmetric iron condor, 28–32 DTE, short deltas around 12–14 on each side |
| Bullish | ADX 20–35 and VX < 19 | Bullish-skewed iron condor, 42–48 DTE, short put around 20 delta and short call around 10 delta |
| Bullish | ADX > 35, rising, VX < 19, and price within 1.5 daily ATR of EMA20 | Research 7–14 DTE bull put spread, short put around 20–25 delta |
| Bearish or VX >= 19 | Any | No new entries under this options policy |

All these ranges are hypotheses for backtesting. The source suggests 50% credit profit exits and remaining-DTE exits of 14 for the shorter condor, 21 for the longer condor, and 3–5 for the weekly vertical. Existing approved option exit rules supply the baseline until the separate options experiment defines and validates its risk exits, width, minimum credit, and portfolio allocation. Specify the VX contract and freshness policy explicitly. ADX gives a strength measurement without establishing a fixed lifespan for a trend. Monthly daily-bar regime and intraday five-minute regime are independent observations of the same underlying, and combined exposure is assessed by the existing risk owner.

## 11. Codex handoff

Inspect current repository instructions and map this draft to the actual signal, indicator, actor, projection, execution, and backtest types. Implement a versioned futures policy, its ATR lifecycle, EMA permission, armed confirmation event, and execution integration. Add focused tests for the changed behavior and reproduce the old baseline before comparing results. Report any repository constraints or existing semantics that require a different mapping. Record the chosen provisional trail activation, structural exit, and session conventions in the implementation specification so they can be reviewed alongside replay results.
