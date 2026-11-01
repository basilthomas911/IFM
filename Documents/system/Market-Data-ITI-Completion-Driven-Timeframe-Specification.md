# Market Data ITI Completion-Driven Timeframe Specification

## Status

This document is authoritative for Futures ITI generation. It replaces the
former stateful tick-driven Futures ITI actor and its realtime projector with
a thin realtime ingress plus the conventional event-sourced command path.

## Input and ownership

`FuturesItiSignalRealtimeActor` consumes
`FuturesMarketPriceUpdatedRealtimeEvent`. Its only responsibility is to filter
for a usable current on-the-run ES trade, read the current VX trade from the
market-data hot cache, and request one Daily
`GenerateFuturesItiSignalCommand`. It owns no ITI state, database access,
stream lease, hydration, projection, longer-timeframe generation, or workflow
admission. Missing VX data is a degraded outcome that retries naturally on the
next ES trade; it does not create an exception or a background retry loop.

The ingress uses one atomic Free/Busy generation gate. A valid tick starts a
Daily command only when the gate is Free. Ticks received while Busy are ignored
immediately and are never retained, queued, replayed, or processed later. The
active operation clears Busy in `finally`, and orderly actor shutdown waits for
that one operation. Bounded telemetry counts busy skips without writing a log
per tick. The only normal realtime log records that a Daily command was
generated; actual failures remain Error logs.

`FuturesItiSignalCommandActor` is the single mutation authority for Generate,
Set Hold, and Clear Hold. It reconstructs the stream state, evaluates the
explicit business rules, and commits one typed source event when state changes.

## Timeframe identity

The three supported streams are independent:

| Period | Trading-day scale | Stream bucket | Entity identity date |
| --- | ---: | --- | --- |
| Daily | 1 | Value date | That value date |
| Weekly | 20 | Calendar week | Monday of the value date's week |
| Monthly | 60 | Calendar month | First day of the value date's month |

The entity key is `{ContractId, TimePeriod, TimeFrameStartValueDate}`. The
signal retains the current observation `ValueDate` separately. Holidays do not
change the deterministic Weekly stream key.

## Structural bands and overshoot

Recurring structural movements use a band of **20% of the calculated ITI
threshold** (`BandPercentage = 0.20`). The trading-day scales above document
the existing calculation defaults; this extension does not change threshold
mathematics or timeframe partitioning.

A band-qualified extreme or reversal is classified from the **new candidate's**
calculated BandLevel. A finite level strictly greater than 1.0 maps
`TrendExtremeChanged` to `TrendOvershootExtended` (enum 7), and
`TrendReversalChanged` to `TrendOvershootReversal` (enum 8). At 1.0 or below,
ordinary modes apply immediately. Direction changes retain priority; generic
`Trending`, hold and trade-state behavior remain unchanged. A level crossing
alone does not generate an event or reset the current group.

An uptrend extreme advances only at higher prices, and a downtrend extreme
only at lower prices. **Every qualifying extreme resets TrendReversal to that
new extreme**, including during overshoot. Subsequent qualifying reversals
move away from that latest extreme. They need not exceed a reversal reached
before the extreme reset. The downtrend rules mirror the uptrend rules.
Whipsawing around BandLevel 1.0 does not latch overshoot or reset these anchors.

Each accepted observation generates at most one existing Generated source
event. A multi-band price gap does not synthesize intermediate signals. Both
new modes retain the same projection, completion, workflow and period fan-out
boundaries. Historic enum values 0–6 and MessagePack field keys remain stable.
Text mode columns require no schema migration. Latest extreme/reversal category
queries merge ordinary and overshoot rows by sequence within the existing
trend boundary; MDI and history include both modes.

## Durable command and projection flow

```text
eligible current ES trade-price realtime event
  -> thin Futures ITI realtime ingress
  -> Generate Daily command
  -> command state calculation
  -> durable FuturesItiSignalGeneratedEvent
  -> FuturesItiSignalEventProjector
  -> MarketDataDb projection
  -> FuturesItiSignalGeneratedCompleteEvent
  -> FuturesItiSignalEventActor
       -> publish the ITI UI notification
       -> update Market Outlook inputs
       -> send ExecuteIntrinsicTimeStrategyWorkflowCommand
       -> if Daily, request Weekly and Monthly Generate commands
```

Weekly and Monthly commands use the Daily completion's persisted signal price,
timestamp, value date, and VX futures price. Their command identifiers are
stable hashes of the Daily completion identity and target period. A repeated
Daily completion therefore addresses the same child commands. Weekly and
Monthly completion events use the same handler but never create more ITI
commands, which prevents recursive fan-out.

Every successfully projected Generate completion starts one Strategy Workflow
for its own timeframe. The workflow command carries the persisted ITI signal as
its immutable trigger and uses the completion event identity for deduplication.
The Strategy Workflow no longer registers an external ITI realtime route.

## Hold-state flow

Set Hold and Clear Hold do not reuse the Generated event family:

```text
SetFuturesItiSignalHoldTradeCommand
  -> FuturesItiSignalHoldTradeSetEvent
  -> durable projection
  -> FuturesItiSignalHoldTradeSetCompleteEvent
  -> FuturesItiSignalEventActor -> UI notification

ClearFuturesItiSignalHoldTradeCommand
  -> FuturesItiSignalHoldTradeClearedEvent
  -> durable projection
  -> FuturesItiSignalHoldTradeClearedCompleteEvent
  -> FuturesItiSignalEventActor -> UI notification
```

Both source and completion contracts carry the resulting full signal snapshot.
Set/Clear completions do not generate longer periods and do not start Strategy
Workflows.

## Storage and replay

All three source event types use the conventional durable Event projector. The
projector applies the signal snapshot to the existing MarketDataDb projection
and emits the matching complete or fail contract. Durable replay repeats the
idempotent projection and terminal delivery through the standard event
projector machinery. There is no ITI-specific signal state, stream ownership,
database polling, recovery loop, or realtime projector. The realtime ingress
reads the shared market-data hot cache and sends the normal event-sourced
command.

The command state reducer recognizes Generated, HoldTradeSet, and
HoldTradeCleared source events, so a later command reconstructs the exact last
persisted ITI state.

## Contract compatibility

Existing MessagePack keys on `FuturesItiSignalGeneratedEvent` and its completed
event remain unchanged. `DeriveLongerPeriods` at key 12 is retained only for
wire compatibility and new events set it to `false`; behavior is determined by
the completed event's timeframe. Hold Set and Hold Clear snapshots are appended
at key 10 on their existing contracts.

`FuturesMarketPriceUpdatedRealtimeEvent` remains the shared normalized
market-data contract. Futures ITI adds one independently routed consumer; the
contract and its other consumers are unchanged.

## Required verification

Tests must cover:

- Daily projection followed by exactly one Weekly and one Monthly command;
- Weekly and Monthly completions producing no recursive commands;
- deterministic, distinct child command identifiers across redelivery;
- both child commands being attempted when one request is rejected;
- one Strategy Workflow command for each Generate completion timeframe;
- missing or mismatched completion snapshots being rejected;
- Generate, Set Hold, and Clear Hold using their dedicated source and terminal
  contracts;
- command-state replay across Generate, Set Hold, and Clear Hold;
- MarketDataDb projection and notification for all three operations;
- one thin Futures ITI realtime actor with one market-price mapping, no durable
  state or projector dependency, and a supervisor-managed route/reset target;
- one active Daily generation operation, immediate busy-tick rejection, no
  retained tick queue, and release of the gate after success or failure;
- missing VX, filtered events, accepted no-change commands, committed events,
  projections, completions, and workflow starts represented distinctly in
  bounded health telemetry; and
- API-server and dependent Trade workflow projects compiling against the new
  boundary.

Overshoot verification additionally covers both trends and all three periods,
strict floating-point boundaries around 1.0, inclusive 20% movement gates,
extreme/reversal anchor resets, leaving and re-entering overshoot, multi-band
gaps, replay parity, mode persistence and Rust numeric/name compatibility.
See [implementation and validation](Futures-ITI-Trend-Overshoot-Implementation-Plan-2026-11-01.md).
