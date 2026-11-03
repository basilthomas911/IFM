# Velocity Reversion — Retracement and ITI Signal Investigation Plan

**Date:** 2026-11-02

**Status:** Planned; investigation has not started

**Scope:** Research using existing November 2025–October 2026 ES/VX observations. No paid data acquisition or live trading.

**Objective:** Explain the observed long/short performance difference and identify observable retracement patterns that could improve entry selection, exit timing or a bounded stop override.

## 1. Decisions and research boundaries

- Analyse longs and shorts separately in every phase, including monthly results.
- Use gross dollar P&L and returns in planned-risk units (R); include cost-adjusted results where assumptions are specified. Do not use combined net points as the headline measure.
- Treat the directional difference as an observed property of this sample and configuration. Investigate market behaviour, indicator construction and exit policy as possible causes.
- The user declined the estimated paid 2024–2025 data acquisition. Do not acquire paid data for this investigation.
- Existing ticks can support new features and additional ITI timeframe replay. Do not substitute invented observations or fill unavailable features with favourable defaults.
- Preserve baseline evidence. Experimental policies run independently with their own position lifecycle; entry filtering or longer holding can change later opportunities.
- Research stop overrides as explicit, bounded alternative policies. They do not automatically replace the baseline stop.
- Do not start application services, place orders, deploy strategy changes or change trading settings as part of this plan.

The older strategy design describes earlier retracement-direction rules. For this investigation, the accepted experimental baseline below is authoritative; do not silently revert to the older entry direction or stop reference.

## 2. Frozen baseline and evidence

| Setting | Baseline |
| --- | --- |
| Period | 2025-11-01 inclusive to 2026-11-01 exclusive |
| Inputs | Existing immutable ES/VX tick snapshots and authoritative ITI replay |
| Entry | After an overshoot, second progressive reversal event; enter against the retracement, in the original trend direction, on the next observed raw tick |
| Entry band | 20% of the entry signal threshold; freeze for stop and watermark calculations |
| Long protective stop | Two frozen entry bands, inward rounded to 0.25 points |
| Short protective stop | 7.5 frozen entry bands, equivalent to 1.5 entry thresholds, inward rounded |
| Profit watermark | Arms strictly above two entry bands of favourable movement; long floor has a one-band buffer, short floor has zero buffer; floor never retreats; exit on first strict adverse cross |
| Other exits | Session flatten; reversal-signal profit exit disabled |
| Session | 09:45 <= entry < 15:45 America/New_York |
| Position rules | One combined position at a time per policy, one attempt per excursion, no daily loss cap |
| Fills | Raw observed prices, retaining stop-crossing gaps; protective stop has baseline priority |
| Reporting quantity | One ES contract baseline, $50 per point; risk-normalised comparisons reported separately |

Baseline artifact directory: `outputs/iti-nov2025-oct2026-long-two-band-stop-profit-buffer-one-band`.

Existing proof covers 258 source trading dates, 118,631,783 ticks, 86,366 Daily ITI signals and 590 trades. Longs: 276 trades, 145 wins, 131 losses, +$6,425 gross, approximately +0.091 average R. Shorts: 314 trades, 235 wins, 78 losses, one flat, +$50 gross, approximately +0.002 average R. These are in-sample results before costs.

Reference comparison: `outputs/iti-nov2025-oct2026-long-profit-buffer-one-band`, with four-band long stops and otherwise matching settings.

## 3. Phase A — Reproduce and prepare the investigation ledger

1. Verify source manifests, hashes, date coverage, timezone/DST handling and signal-to-tick matching. Reproduce baseline trade fields and totals before experiments.
2. Retain stable opportunity keys: date, contract, ITI group, actual side, signal ordinal and fill ordinal. Record complete event and raw-tick ordering around entries and exits.
3. Create two ledgers: executed baseline trades and all eligible entry opportunities, including those blocked by an existing position. This prevents entry filters from being judged only on trades the baseline happened to take.
4. Record features at the qualifying reversal, entry decision, actual fill and predefined adverse checkpoints. Retain exact first stop touch/cross and watermark events.
5. Attach feature timestamps, data freshness and availability flags. Indicators use completed bars or already-published ITI events only. A decision made from a new observation cannot obtain a fill earlier than that observation.
6. Separate entry-band normalisation from contemporaneous ITI bands. Preserve the frozen band used by the trade even if later signals change threshold or band size.

**Deliverables:** versioned protocol, baseline reproduction proof, opportunity ledger, trade ledger, feature definitions and source coverage report.

## 4. Phase B — Local retracement and ITI features

| Workstream | Measurements available at decision time | Research question |
| --- | --- | --- |
| Retracement speed | Band displacement per second/minute; durations between reversal events; recent versus earlier speed | Do slowing retracements recover more often than accelerating retracements? |
| Local structure | Consecutive adverse pushes, failed rebounds, prior confirmed pivots, higher lows/lower highs, churn and path efficiency | Which price sequences distinguish temporary retracement from continued adverse movement? |
| Preceding overshoot | Maximum BandLevel so far, extension count, age, duration, distance from extreme and extension speed | Does overshoot strength or exhaustion change continuation probability? |
| ITI event sequence | `TrendOvershootExtended`, `TrendOvershootReversal`, ordinary extreme/reversal changes and direction changes; time since each event | Does renewed extension support holding, while continued reversal supports exiting? |
| Location in ITI move | Current BandLevel and ReversalLevel, distance to current extreme and direction-change boundary, group age | Does the same two-band entry behave differently depending on where it occurs in the larger move? |
| Trend efficiency and velocity | Signed displacement/path length; whole-excursion and recent velocity, separately | Is the retracement corrective and inefficient, or an efficient move against the trade? |
| RSI | RSI level, slope, and price/RSI divergence calculated from closed bars | Does momentum improvement distinguish recoveries from continued losses? |

Start with event-sequence, speed and price-structure features. Use a small, recorded set of windows and cutoffs; report unavailable measurements separately. Do not search unrestricted combinations of features and thresholds.

For pivots and divergence, record when a pivot becomes confirmable. Do not label a local high/low at its earlier price timestamp when confirmation requires later observations. A developing pattern may be used only as a separately defined real-time feature.

Existing code in `scripts/Backtest/discover_iti_entry_signals.py` provides closed-bar RSI slope, reversal-speed and trend-efficiency diagnostics. Its earlier opportunity labels and stop rules differ from this baseline: reuse validated calculations, then reconstruct labels and outcomes for the current rules. Do not import its conclusions as new results.

## 5. Phase C — Other ITI views and available indicators

1. Replay Weekly and Monthly ITI from the same ticks through the authoritative calculator. The retained annual signal cache contains Daily signals only.
2. Match existing cold-start, contract-roll and timeframe rules. Document incomplete initial buckets and missing warmup; do not invent pre-November state.
3. At each Daily decision, attach the last Weekly/Monthly state actually published by that time. Higher-timeframe state must never come from the eventual completed week/month.
4. Measure alignment/opposition of Daily, Weekly and Monthly trend, modes, BandLevel, ReversalLevel, extremes and distance to their boundaries.
5. Separate alignment from maturity: a newly changed higher-timeframe trend can differ from an old, extended one.
6. Audit existing ITI RSI, MDI, predicted-trend-delta and range view implementations for historical availability, formula meaning and causal replay requirements. Include only reproducible, timestamped views; document exclusions. Do not interpret a field name as proof of predictive value.

**Deliverables:** timeframe coverage and parity proof; timestamped context ledger; separate long/short outcome tables by context.

## 6. Phase D — Study stop encounters and counterfactual recovery

For every baseline stop encounter, record observable state approaching the stop and at its first touch. Examine all stopped trades, including those that never recover.

- Measure additional adverse excursion, time to return to entry, time to each favourable-band milestone and whether recovery occurs before the same session cutoff.
- Retain the full adverse-path distribution, including gaps, continuing declines/rallies and session-ending unrecovered trades. Recovery rate alone is insufficient.
- Compare feature distributions with trades that approach the same adverse checkpoint but subsequently recover without touching the stop.
- Examine whether deceleration, failed adverse extensions, confirmed local structure, RSI behaviour or a new ITI event precedes recovery.
- Use fixed, declared horizons and favourable/adverse barriers in entry-band units. Future recovery is an outcome label, never a decision feature.
- Generate representative tick/ITI charts for recovered stops, continued losses, false recovery signals and successful baseline winners. Select cases systematically, rather than showing only attractive examples.

Evaluate two questions separately: whether a feature explains the observed post-stop path, and whether a causal policy using that feature improves whole-session results.

## 7. Phase E — Controlled alternative policies

Evaluate individual mechanisms first, then only a small number of justified combinations. Predeclare each rule and record every trial, including failures.

| Alternative | Required definition and comparison |
| --- | --- |
| Skip entry | Reject only when a feature available at the entry decision meets a defined condition; replay later opportunities |
| Delay entry | Specify confirmation event, maximum wait, cancellation/invalidation condition and next-tick fill |
| Earlier invalidation exit | Exit when adverse ITI/local structure invalidates the setup; preserve causal event/fill ordering |
| Bounded stop override | Define eligibility before or at first stop touch, replacement adverse boundary, maximum holding time, reset/re-arm rules and next-tick execution |
| Recovery exit after override | Define recovery watermark activation, ratchet and pullback/re-cross exit explicitly; include nonrecoveries |
| Signal-informed profit exit | Compare continuation confirmation and exhaustion exits against the existing buffered watermark |

Every stop override must specify a finite adverse boundary, a finite time limit, session flatten, and precedence when multiple exits trigger. If an override condition requires observing the stop-touch tick, it cannot retroactively cancel a stop assumed executed on that same tick. Define eligibility in advance or model an explicit alternative software-stop policy with its own causal fill convention.

For overrides, compare with both the existing two-band long stop and a simple wider fixed stop. This reveals whether complexity adds value beyond merely accepting more adverse exposure. Mirror definitions for shorts, but estimate results separately rather than assuming symmetry.

## 8. Phase F — Explain the directional difference

1. Preserve the accepted asymmetric baseline as the primary comparison.
2. Test a small matched set of stop and profit-exit policies on both directions. Separate entry-direction effects from exit-policy effects.
3. Report equal planned-dollar-risk returns alongside one-contract results. Label continuous risk-normalised scaling as analytical; any integer ES/MES sizing example needs explicit rounding, cost and contract assumptions.
4. Audit the production threshold factors: uptrend 0.85 and downtrend 1.15. Distinguish indicator trend from actual trade side. Record thresholds and bands as observed at entry.
5. A symmetric-threshold experiment, if justified, requires regenerated authoritative ITI signals and a new manifest. Changing only the trade stop does not test threshold symmetry.
6. Break results down by entry-known volatility, trend efficiency, broader ITI alignment, entry time, month and contract-roll proximity. Avoid regimes defined from the subsequent full session.

## 9. Phase G — Stability, costs and execution sensitivity

- **Costs:** commissions and a declared range of adverse execution adjustments; report gross and cost-adjusted results separately. Existing trade-only ticks do not establish bid/ask spread or order-book capacity.
- **Execution delay:** apply a small predefined set of decision-to-fill delays and preserve next-available-tick fills. Recompute dependent exits and later entries.
- **Nearby parameters:** assess small neighbourhoods around selected stop, watermark and feature values. Prefer broad stability over a single best value.
- **Concentration:** show results with the best day, several best days and best month removed; retain the complete primary result.
- **Path diagnostics:** show favourable/adverse excursions, surrendered profit, stop gaps and holding time by side and policy.
- **Resampling:** resample whole days and weeks, keeping both directions together within each block; report expectancy uncertainty, drawdown and losing-streak distributions. Resampling is not additional market history and does not remove selection bias.
- **Chronological stability:** report fixed subperiods and leave-period-out diagnostics. Since the full year has already informed rule selection, do not label a retrospective split as untouched out-of-sample evidence.

## 10. Tests and verification

Add focused unit tests for any new feature or policy behaviour, including:

- Long/short mirroring, frozen entry bands at the 20% threshold setting, and changed later thresholds.
- Overshoot boundaries above/at/below BandLevel 1.0; extreme changes resetting reversal state; distinct ITI groups.
- Closed-bar indicator availability, sparse ticks, zero elapsed time, stale/unavailable indicators and confirmed-pivot availability.
- Exact stop touches versus strict watermark crosses; simultaneous signal/stop conditions; gap fills and policy precedence.
- Override eligibility, replacement boundary, expiry, recovery, repeated stop-zone visits and session cutoff; include never-recovering paths.
- DST transitions, session boundaries, contract rolls and higher-timeframe publication ordering.

Verify source checksums and timestamp ordering; independently reconstruct selected feature calculations and all candidate-policy fills/exits from raw ticks. Reconcile trade, daily and monthly totals. Repeat baseline parity checks when shared calculations change. Use targeted raw-day regressions before annual replay.

Record elapsed time for baseline reproduction, feature extraction, additional ITI replay, policy analysis and independent verification. Compare identical date slices and workloads when reporting before/after performance timings; do not compare runtimes of different experiments as a speed improvement.

## 11. Reporting and decision criteria

For every policy, show long and short monthly and full-year results separately: trades, wins/losses/flats, win rate, gross and cost-adjusted dollar P&L, average R, average winner/loss, worst loss, maximum closed-trade drawdown, adverse excursion and holding time. Full-period average R is trade weighted, not a sum of monthly averages. For changed stops, explain that each policy's R denominator changes; retain a baseline-risk-normalised comparison as well.

Use matched-opportunity comparisons when entries match. When entries differ, distinguish common entries, added entries, removed entries and lifecycle effects; do not splice independently simulated direction ledgers into a claimed combined replay.

A candidate advances only when its rule is causal and reproducible, its full downside is accounted for, and any improvement is assessed across costs, subperiods and nearby parameters. Report the trade-off explicitly if higher P&L requires more adverse risk or longer holding. No fixed profitability or significance threshold is invented after examining outcomes.

Negative findings and unavailable views are valid investigation outputs. No result from this year alone establishes a persistent market bias or authorises deployment.

## 12. Execution order and completion checklist

| Priority | Work package | Completion evidence |
| --- | --- | --- |
| 1 | Baseline reproduction and causal opportunity ledger | Hashes, parity proof and timestamp checks |
| 2 | Retracement speed, local structure, ITI sequences and stop encounters | Feature tables, complete recovery/downside distributions and representative charts |
| 3 | Overshoot maturity, ITI location, efficiency and RSI | Separate-side diagnostic tables and feature verification |
| 4 | Weekly/Monthly ITI and audit of other existing views | Authoritative replay, as-of joins and availability report |
| 5 | Small set of entry, invalidation and bounded-override experiments | Versioned rules, raw verified ledgers and baseline/wider-stop comparisons |
| 6 | Directional attribution, costs, delay and robustness | Monthly comparisons, risk-normalised results, sensitivity and uncertainty reports |
| 7 | Investigation conclusion | Supported/rejected hypotheses, trade-offs, remaining questions and frozen candidate definitions |

Final artifacts should include a versioned investigation protocol, reproducible scripts and hashes, tests and verification results, feature/opportunity/trade CSVs, chart examples, experiment register, timing ledger and a concise findings document. Preserve all existing yearly results and use new artifact directories for this work.
