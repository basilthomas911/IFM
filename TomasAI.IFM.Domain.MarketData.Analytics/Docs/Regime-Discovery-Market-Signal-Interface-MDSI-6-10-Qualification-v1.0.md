# MDSI-6 through MDSI-10 Qualification

Qualification record v1.0

| Item | Value |
| --- | --- |
| Date | 2026-08-25 |
| Gates | MDSI-6, MDSI-7, MDSI-8, MDSI-9, MDSI-10 |
| Status | Complete |
| Design authority | `Regime-Discovery-Market-Signal-Interface-Design-v1.0.md` |
| Implementation authority | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |

## 1. Accepted runtime shape

The existing RSI, ATR, ADX, and MACD Start/Stop surfaces now attach and detach
their identities from the shared observation stream. They no longer start an
independent hot-cache sampling timer. Their realtime actors route
`FuturesTradeSessionBarClosedRealtimeEvent` and retain the complete source
observation on the append-only compatibility sample contract.

RSI13 remains the TDI source under `rsi-13-tdi-v1`. RSI14 is isolated under
`rsi-14-regime-v1`; its slope is nullable until a prior RSI exists, so a
not-yet-warm slope cannot be mistaken for zero.

As qualified by MSR-0 through MSR-9, EMA10/20/50/200 and EMA-centered BB10/20
now have independent event-sourced Command actors. A stateless EMA Realtime
actor forwards bars, the EMA projector persists successfully generated values,
and the EMA Event actor continues the exact observation/EMA pair into the
Bollinger Command actor. ATR remains independently event sourced. The former
combined snapshot, realtime state, projector, cache, and Query actor no longer
exist.

## 2. Formula boundaries

- EMA uses multiplier `2 / (period + 1)` and a simple-mean seed. The 200th
  close seeds EMA200; the 201st supplies current, prior, and slope.
- BB10/20 use population standard deviation and EMA10/20 centerlines. A
  mismatched EMA `ObservationId` is rejected. The BB20 width baseline is the
  mean of the prior 20 completed positive widths and excludes the current
  width.
- ATR uses Wilder ATR14. True range consumes shared high, low, and prior close.
  The ATR baseline is the mean of the prior 20 completed ATR14 values and
  excludes the current value. Ratio is emitted only for a positive baseline.
- Historical and live calculation paths call the same deterministic states.

## 3. Storage

The Market Data Scylla schema now includes:

- `futures_ema_signal`;
- `futures_bollinger_band_signal`; and
- `futures_atr_volatility_signal`.

All three tables partition by formatted market-series identity, timeframe,
immutable configuration identity, and year-month. Their clustering lineage is
`marketDataAsOf DESC, observationId ASC`. Named storage commands include common
source sequence, calculation version/method, validity, and schema version.

## 4. Qualification evidence

The following commands completed successfully on 2026-08-25:

| Qualification | Result |
| --- | --- |
| API server build | succeeded, 0 warnings, 0 errors |
| Market Data Analytics unit tests | 898 passed, 0 failed |
| Market Data Analytics BDD tests | 462 passed, 0 failed |
| Market Data Analytics integration tests | 39 passed, 0 failed |
| Application Storage integration tests | 372 passed, 0 failed |
| Final-binary RSI provenance migration integration test | 1 passed, 0 failed |

The focused unit coverage proves:

- RSI13 and RSI14 configuration/state isolation;
- exact EMA200 seed/current/prior boundaries;
- live/historical EMA parity;
- mismatched EMA/Bollinger observation rejection;
- BB prior-only width baseline warm-up;
- ATR prior-only baseline and ratio boundary;
- idempotent shared-observation attachment; and
- consistent `ObservationId` across the ordered snapshot and projection
  lifecycle events.

## 5. Deferred work

This qualification does not advance MDSI-11 or later gates. The consolidated
latest cache is intentionally transitional until MDSI-15. Historical data load
orchestration remains owned by MDSI-3; these gates only ensure that historical
and live observations execute the same formulas.
