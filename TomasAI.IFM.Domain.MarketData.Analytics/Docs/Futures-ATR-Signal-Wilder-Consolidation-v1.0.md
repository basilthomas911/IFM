# Futures ATR Signal Wilder Consolidation v1.0

Date: 2026-08-26  
Status: Implemented; qualification recorded below

## 1. Outcome

`FuturesAtrSignal` is the single business owner of Average True Range. The
duplicate ATR calculation and projection formerly embedded in
`MarketSignals` has been removed. ATR is event-sourced command state;
realtime actors remain stateless routers and do not own checkpoints or
projectors.

The implementation retains the existing `futures_atr_signal` table because
this is a development schema. It adds columns in place and does not create a
`_v2` or parallel ATR table.

## 2. Command topology

Two command families deliberately share one Wilder calculation model:

- `GenerateFuturesAtrSignalCommand` processes completed intraday bars for
  15 seconds, 1 minute, 5 minutes, 15 minutes, 1 hour, and 4 hours. Actor
  identity includes contract, value date, timeframe, and period.
- `GenerateFuturesAtrDailySignalCommand` processes completed daily bars. Its
  actor identity is cross-value-date and uses a Daily, Weekly, or Monthly
  horizon plus the ATR period. `PeriodLength` always counts completed daily
  observations; Weekly and Monthly are workflow horizons, not seven-day or
  thirty-day bar substitutions.

New callers use the observation-bearing command APIs. The older price-only
constructor path remains temporarily for wire and test compatibility and
uses the legacy calculation. It is not the authoritative live or historical
Wilder path and must not receive new callers.

## 3. Formula and event-sourced state

For each completed OHLC observation:

```text
TR = max(high - low, abs(high - previousClose), abs(low - previousClose))
seed ATR = mean(first PeriodLength true ranges)
next ATR = ((previous ATR * (PeriodLength - 1)) + TR) / PeriodLength
ATR baseline = mean(the 20 completed ATR values preceding the current ATR)
ATR ratio = current ATR / prior-only ATR baseline
```

The immutable `FuturesAtrAccumulatorCheckpoint` is serialized into every
generated domain event. It contains the previous close, incomplete seed,
current ATR, bounded prior ATR window, observation identity/sequence, and
observation count. Command-state replay therefore reconstructs the exact
next calculation without mutable realtime state or a database read.

Duplicate and stale observations are ignored by observation identity, source
sequence, and event time. ATR-14 becomes formula-warm on observation 14 and
baseline-ready on observation 34. Historical loaders therefore need at least
`PeriodLength + 20` chronological completed observations.

## 4. Projection and storage

`FuturesAtrSignalReadModel` now includes:

- current and previous ATR;
- current true range;
- prior-only 20-value baseline and ratio;
- explicit `IsWarm`; and
- shared observation/configuration/calculation lineage metadata.

The command event projector writes these values to `futures_atr_signal`.
Latest intraday and day-based reads reconstruct both the signal values and
their source lineage. The historical loader already normalizes and publishes
chronological closed-observation batches through its replay-publisher
boundary; the active publisher must route those observations through the
same observation-bearing ATR command APIs used by live bars.

## 5. Gate record

| Gate | Result |
| --- | --- |
| ATR-0 | Inventoried both existing command families, actor conventions, storage, and duplicate `MarketSignals` ownership. |
| ATR-1 | Defined authoritative intraday and day-based horizon profiles. |
| ATR-2 | Evolved commands, generated events, and read model with observation/checkpoint contracts. |
| ATR-3 | Added the immutable, bounded, replayable Wilder checkpoint and pure accumulator. |
| ATR-4 | Routed completed intraday observations through Wilder ATR for all six activation timeframes. |
| ATR-5 | Implemented cross-value-date Daily/Weekly/Monthly horizon processing from completed daily observations. |
| ATR-6 | Evolved existing Scylla schema, insert/latest queries, metadata mapping, command API, and historical warm-up contract. |
| ATR-7 | Removed the duplicate `MarketSignals` ATR state, contract, formula output, schema, write operation, and projector call. |
| ATR-8 | Added formula, gap, seed, smoothing, baseline, deduplication, horizon, serialization, command-parity, and storage round-trip tests. |
| ATR-9 | Builds and focused/full test qualification are recorded in section 6. |

## 6. Qualification

Qualification completed on 2026-08-26:

- Market Data Analytics and storage integration builds: succeeded with zero
  warnings and zero errors;
- focused ATR unit tests: 101 passed;
- complete Market Data Analytics unit suite: 910 passed;
- complete Market Data Analytics actor BDD suite: 462 passed;
- complete Market Data Analytics integration suite: 43 passed with exit code
  zero; the shared harness emitted existing cleanup `AggregateException`
  diagnostics for unrelated Market Outlook, MACD, and RSI classes; and
- live Scylla ATR projection round trip: 1 passed, including all new Wilder
  fields and source lineage.
