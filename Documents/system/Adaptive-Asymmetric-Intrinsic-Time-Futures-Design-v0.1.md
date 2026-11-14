# Adaptive Asymmetric Intrinsic Time Futures Strategy

Draft v0.1 · 2026-11-14 · Codex implementation handoff

## Purpose

Build an intraday ES trend-following strategy that evaluates one probability at each confirmed Directional Change (DC). The research question is whether the score identifies more favorable follow-through and improves trading results after costs. Start with one model, one entry cutoff, fixed MES quantity, and a deterministic progressive trailing stop. Use the same strategy and execution assumptions to compare filtered and unfiltered entries.

This draft specifies the **DC-entry variant** of the intrinsic-time strategy. A confirmed DC is the entry evaluation point. The additional move of one active threshold beyond the DC is the model's target and, measured independently from the fill, the first milestone for tightening protection. The earlier overshoot-entry strategy and gamma calibration can be evaluated as separate experiments.

## 1. Signal and trade policy

| Item | Draft specification |
|---|---|
| Signal instrument | ES futures trade data from Databento, replayed through the existing ITI signal generator |
| Execution instrument | MES initially, with a fixed configured quantity. Keep ES execution configurable for later testing. |
| Session | Regular trading hours in Eastern Time, normally 09:30–16:00. Use the existing session calendar, including early closes. |
| DC thresholds | Separate upward and downward reversal thresholds. Start replay experiments at **8 points up** and **16 points down**. Use integer ticks for price comparisons. |
| Entry | When flat and eligible, score the confirmed DC. A score at or above the configured cutoff allows the normal risk and execution workflow to consider entry in the DC direction. |
| Repeat entries | At most one order intent per DC. After a stop, stay flat for the remainder of that DC wave and resume eligibility when an opposite DC confirms. |
| Session exit | Block new entries at the configured cutoff, cancel working entry orders, and flatten filled exposure. **15:55 ET** is the example for a normal session. Reconcile any fills that arrive during cancellation. |

Use the existing ITI definition for anchor, initialization, sequence, and reversal tracking. The upward threshold is the move required off the tracked low to confirm an upward DC. The downward threshold is the move required off the tracked high to confirm a downward DC. Record the actual ES trigger price, direction, and the threshold used for each DC. Lock that threshold for the candidate and resulting position. Position exits leave the structural tracker running.

The backtest and paper trader should support three modes of the same policy:

- **Baseline.** Take every otherwise eligible DC with the configured fixed stop policy.
- **Shadow.** Execute the baseline and log which decisions the model would have accepted.
- **Filtered.** Require a valid score meeting the configured cutoff. A missing, stale, or incompatible model yields no model-approved entry. Continue managing any open position.

## 2. One prediction target

At a confirmed DC, let P be the observed ES trigger price, s be +1 for up or −1 for down, and δ be the threshold that triggered the DC. Freeze two barriers:

| Barrier | Price |
|---|---|
| Favorable extension | P + s × δ |
| Adverse move | P − s × δ |

The binary target is **whether the favorable barrier is reached first, by the configured session flatten cutoff**. Use the ordered ES trade sequence after the DC to determine the first touch or crossing. Label favorable first as 1. Label adverse first or a fully observed timeout as 0. Exclude rows with incomplete data before resolution and record why. Save the outcome time and when the label became available. Keep favorable and adverse excursion statistics for analysis, with an explicit observation window.

For example, an upward DC at **5808** with **δ = 8** has a favorable barrier at **5816** and an adverse barrier at **5800**. If the price reaches 5816 first within the horizon, the label is 1. This label is anchored to the ES signal. The stop and realized P&L are anchored to the MES fill and subsequent MES prices.

### Feature snapshot at the DC

Use one ordered, versioned schema in training and inference. Reuse the existing ITI snapshot and indicators where they provide point-in-time values.

| Feature group | Inputs from the conversation |
|---|---|
| ITI structure | Direction, prior completed overshoot divided by its threshold, elapsed time from the prior extreme to the current DC, and DC-leg volume normalized by a historical volume rate over an equivalent duration |
| Price and volatility | Signed distance from session VWAP divided by δ, alignment with VWAP, and five-minute ATR divided by δ |
| Momentum | Last closed one-minute RSI, its change since the prior extreme, last closed five-minute RSI, and the difference between one- and five-minute RSI |
| Session | Minutes since the cash open |

Use only values available when the triggering tick is processed. A new DC can confirm the previous wave's final extreme. The current wave's eventual extreme belongs in outcome data. Use a single readiness rule for required feature warm-up, with the same treatment in training, replay, and live scoring. Record candidate and wave IDs so a historical score can be reproduced.

## 3. Position management

The model score controls entry eligibility. The initial stop distance is fixed by policy for each direction and both experiments use the same policy. Start with **one active δ** as the draft initial setting. Use the actual execution fill and favorable extreme in the execution instrument to manage protection.

| Favorable excursion from fill | Trail distance behind the favorable extreme |
|---|---|
| Less than 1δ | Configured initial stop distance |
| At least 1δ and up to 2δ | 0.5δ |
| Beyond 2δ | 2.5 points as a starting test setting |

For a long, trail below the running high. For a short, trail above the running low. Initialize the watermark at the fill and ratchet protection in one direction only. A stage transition must preserve or tighten the current effective stop, including after price rounding. Evaluate skipped levels correctly when one tick crosses both stage milestones. A stop trigger is an execution instruction and can fill at a different price.

With a long fill at **5808** and δ = **8**, the initial trigger is **5800**. If the execution instrument reaches **5816**, the second stage puts the proposed stop at **5812**. Use execution events to confirm fills and flat state. Keep protective management active while an exit is working and reconcile partial fills and working orders using the existing broker workflow.

## 4. Trainer, scoring, and actor integration

1. **Replay and export in C#.** Start from a rolling **12 months** of Databento ES history. Use your backtester's normal market-data and ITI path to create one feature row per eligible DC and attach outcomes once known. Export a simple versioned dataset for the trainer. Preserve source coverage, contract-roll policy, schema version, and session boundaries. For performance evaluation, replay the corresponding MES data and realistic execution costs.
2. **Train one classifier in Rust.** Use Forust as the proposed gradient-boosted tree trainer. Verify the supported crate API and score format before choosing a dependency version. Train on completed historical labels in chronological order. Persist the model with feature schema, training cutoff, metrics, and configuration. Convert a raw margin to probability if the selected API returns margins. Check probability calibration on future data.
3. **Score at DC through Rust inference.** Expose a small C ABI to the C# host with model load, prediction, and release. Use fixed-layout numeric inputs and an explicit success or failure status. Keep one model version for each decision, validate the feature schema before activation, and verify prediction parity between the trainer and the DLL. The DLL returns the probability. The existing C# workflow owns entry, stop management, and broker state.
4. **Schedule with your built-in C# scheduler.** Use a weekly refresh, initially Friday around **16:15 ET** on normal sessions, after the configured data cutoff and ingestion completion. Train a candidate artifact, validate it, and activate it before a subsequent session when approved. Keep the prior accepted model if a replacement fails. Record actual activation time so replay selects the version available at each DC.
5. **Route through the existing workflow.** The ITI signal path supplies the confirmed DC and feature snapshot. Trade Strategy consumes the score and applies the entry cutoff. Order Composer and Risk Manager handle the configured contract, fixed quantity, and approval. Position Monitor and Order Execution continue to manage protection and broker reconciliation. Include DC ID, score, model version, threshold, and policy decision in the audit record.

## 5. Backtest and acceptance

Run chronological walk-forward folds. At each training cutoff include only labels already available. Keep outcomes that cross a fold boundary out of that fold's training data until they mature. Choose the entry cutoff on earlier validation periods and evaluate it on later periods. Keep the fixed quantity, stop, cost assumptions, and session rules identical between baseline and filtered strategy runs. Report both candidate-level model results and the resulting strategy path, since skipping a trade changes when the system is free to take another.

| Question | Test |
|---|---|
| Does the score rank opportunities? | Report realized follow-through by probability band, with sample counts and separate long and short results. Compare accuracy with the majority-class baseline and Brier score with a constant probability estimated from prior data. |
| Does it help trading? | Compare net expectancy per trade and net P&L per session, trade count, drawdown, and the large winners skipped. Use MES-specific fills, fees, spread, and stop slippage assumptions. |
| Does the effect hold over time? | Compare multiple held-out periods and observe paper-trading results before changing quantity or stops. Define minimum sample and acceptance thresholds before the reserved evaluation. |

More than 50% correctness is an encouraging starting point when the classes are balanced. The expected evidence is that higher-scored trades follow through more often and that the filter improves the strategy after costs. The draft leaves the probability cutoff configurable until replay shows which values are useful.

## 6. Codex implementation checklist

- Locate the existing ITI signal, replay, calendar, scheduler, workflow, and execution interfaces. Reuse them for the DC-entry strategy.
- Add the candidate export and barrier-label generator. Test long and short cases, favorable/adverse order, timeout, and data gaps. Verify that changing future ticks changes the label while leaving the DC snapshot intact.
- Build and test the Rust trainer and native inference adapter. Verify schema order, artifact reload, train/inference score parity, and the model's activation time in replay.
- Implement the baseline, shadow, and filtered entry modes with shared stops. Test threshold boundaries, stop stages, monotonically tighter protection, duplicate DCs, partial fills, stop-outs, and session flattening.
- Produce a draft end-to-end run from historical replay through model scoring and a baseline versus filtered comparison. Keep probability-based stop width, dynamic quantity, and gamma features as later experiments after measuring the entry model.
