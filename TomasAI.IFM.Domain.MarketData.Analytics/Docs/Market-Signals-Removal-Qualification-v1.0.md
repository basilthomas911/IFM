# Market Signals Removal Qualification v1.0

Date: 2026-08-26
Gates: MSR-0 through MSR-9
Status: Complete

## Result

The broad `MarketSignals` runtime and shared-contract folders have been
removed. No production or test C# namespace contains `MarketSignals`.
Reusable contracts now live with their concrete business capability:

- market-series identity and signal lineage: `Shared/Common`;
- closed OHLCV observations: `Shared/FuturesTradeSessionBarPublisher`;
- historical load contracts: `Shared/HistoricalDataLoader`;
- RSI configurations and Wilder state: `FuturesRsiSignal`;
- EMA contracts and event-sourced state: `FuturesEmaSignal`;
- Bollinger contracts and event-sourced state: `FuturesBbSignal`.

## Runtime ownership

`FuturesTradeSessionBarClosedRealtimeEvent` is routed to a stateless
`FuturesEmaSignalRealtimeActor`. It sends a command to the event-sourced EMA
command actor. The EMA projector writes `futures_ema_signal`; its completed
event is handled by the EMA event actor, which sends the exact observation and
same-observation EMA result to the event-sourced Bollinger command actor. The
Bollinger projector writes `futures_bollinger_band_signal`.

RSI13 and RSI14 now use the existing event-sourced `FuturesRsiSignal` actor.
Each generated event carries the immutable Wilder checkpoint needed to resume
after replay. RSI13 remains the TDI source and RSI14 retains an independent
Regime Discovery configuration identity.

Realtime and Event actors own no calculation state. Calculation state exists
only in Command actor state and is persisted in the ACID event log.

## Formula and replay invariants

- Wilder RSI seeds after exactly `period` price changes.
- EMA uses an SMA seed and multiplier `2 / (period + 1)`.
- EMA200 seeds on close 200; close 201 supplies current, prior, and slope.
- Bollinger uses EMA10/EMA20 centerlines and population deviation.
- BB20 baseline contains only the prior 20 completed positive widths.
- Duplicate and stale observations are rejected.
- Bollinger rejects an EMA whose `ObservationId` differs from its source bar.
- EMA and Bollinger checkpoints are bounded and event serialized.

## Storage compatibility

The existing development tables remain unchanged. Version suffixes were
removed from the C# CQL command and parameter names; no `_vX` table was added.
The two dedicated command projectors remain the sole writers.

## Qualification

MSR-9 covers focused formula boundaries, checkpoint resume parity, identity
isolation, lifecycle event checkpoint preservation, stateless realtime actor
shape, runtime EMA-to-Bollinger continuation, the complete Analytics unit and
BDD suites, and the complete Analytics integration suite.

Final qualification results:

- Analytics unit tests: 910 passed;
- Analytics BDD tests: 462 passed;
- Analytics integration tests: 44 passed;
- serialized whole-solution build: succeeded with 0 warnings and 0 errors;
- `git diff --check`: no whitespace errors.
