# Market Data ITI Realtime Timeframe Specification

## Status

This document is authoritative for live Futures ITI generation. It replaces the legacy Daily-completion-to-Weekly/Monthly derivation flow.

## Input and ownership

`FuturesItiSignalRealtimeActor` consumes every normalized current-contract ES trade from `FuturesMarketPriceUpdatedRealtimeEvent`. It owns stable ES and VX stream registrations, requires both streams to be active, and reads the latest VX trade from the provider-neutral hot-cache API.

The normalized feed `ValueDate` is authoritative. The shared futures-session policy changes value date at 18:00 US Eastern time Sunday through Thursday and accounts for Eastern daylight-saving transitions. Live processing does not query a holiday table; receiving the first trade for a value date is the evidence that the trading session exists.

## Independent timeframe state

Every ES trade is evaluated against three independent streams:

| Period | Trading-day scale | Reset boundary | Entity identity date |
| --- | ---: | --- | --- |
| Daily | 1 | New feed `ValueDate` | That `ValueDate` |
| Weekly | 5 | First observed value date in a new ISO-week bucket | First actually observed value date, including Tuesday after a Monday holiday |
| Monthly | 20 | First observed value date in a new calendar month | First actually observed value date |

The entity key is `{ContractId, TimePeriod, TimeFrameStartValueDate}`. The signal retains its observation `ValueDate` separately. A new timeframe starts in group zero. Only a trend-direction change increments the group.

## Publication rules

All accepted ticks update actor-owned hot observation state. The actor creates a one-attempt realtime source/storage/complete-or-fail projection only when one of these conditions is true:

1. the timeframe has no current state and must emit its group-zero start signal;
2. futures price crosses the current uptrend or downtrend direction trigger; or
3. futures price moves far enough to qualify a Trending, TrendExtremeChanged, or TrendReversalChanged signal.

The recurring publication band is:

```text
BandPercentage = 0.10
BandSize       = abs(CalculatedItiThreshold) * BandPercentage
```

The band is a percentage of the ITI threshold/lambda calculation, never a percentage of the futures price. Direction checks run first and remain trigger-driven. An inside-band tick creates no source event, completion, or Scylla row.

## Storage and restart

`FuturesItiSignalRealtimeActor` is the live transition authority. For each publishable evaluation it gives `FuturesItiSignalGeneratedEvent` to `FuturesItiSignalRealtimeProjector`. The projector publishes the realtime source, writes the existing canonical/query/current-timeframe projections once, and publishes the typed realtime complete or fail result. It has no event-source log, JetStream process/replay queue, outbox, checkpoint, retry, or recovery worker.

The hot anchor advances only after `ProcessRealtimeEventAsync` reports a successful storage and completion publication. A failed attempt is observable through its fail event and log entry, but the next market tick is the only retry opportunity.

Published signals are written to the existing canonical and bounded query projections and to `futures_iti_timeframe_state`. The authoritative state table stores one current row by contract, period, and deterministic calendar bucket, including:

- first observed timeframe value date;
- latest observation value date and signal timestamp;
- group, trend, mode, triggers, threshold, and lambda; and
- band anchor, percentage, and absolute size.

On actor restart, the realtime state hydrates this storage projection once per active timeframe and performs no storage read per tick. Existing canonical history is a bounded migration fallback when the versioned state row does not yet exist. The rows are durable reference/read-model data; the realtime import event itself is not replay durable.

## Event compatibility

`FuturesItiSignalGeneratedEvent` and its completed event keep their established MessagePack keys. `DeriveLongerPeriods` at key 12 is deprecated and new generated events always set it to `false`. Generated-complete handlers do not create Weekly or Monthly signals. The source VX price remains on the event for audit and downstream compatibility.

Phase 1 temporarily retains the legacy Futures Trade Signal needed by Market Outlook. An ITI realtime completion queries its existing inputs, computes `FuturesTradeSignalUpdatedEvent`, and sends it through the same realtime projector. Its successful completion publishes `FuturesTradeSignalUpdatedNotifyEvent` to the UI. This compatibility branch will be replaced during UI optimization; it does not restore a durable command/Event path.

## Required verification

Tests must cover:

- all three streams starting from one trade;
- identical and inside-band prices producing no second projected transition;
- exact/full-band extreme, reversal, and trending transitions;
- immediate direction changes and group increments;
- week/month reset to group zero;
- a Monday holiday with Tuesday as weekly frame start;
- midweek and midmonth restart reuse of persisted frame start;
- Scylla state round-trip for timeframe and band fields; and
- the 18:00 Eastern boundary in both daylight and standard time.
