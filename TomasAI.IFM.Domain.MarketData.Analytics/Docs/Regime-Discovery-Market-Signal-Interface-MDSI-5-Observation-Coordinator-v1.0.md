# Regime Discovery Market Signal Interface MDSI-5 Observation Coordinator

Shared Futures Analytics Observation Coordinator v1.0

| Item | Value |
| --- | --- |
| Gate | `MDSI-5 - Shared observation coordinator` |
| Status | Complete |
| Date | 2026-08-25 |
| Design authority | `Regime-Discovery-Market-Signal-Interface-Design-v1.0.md` |
| Implementation authority | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |

## 1. Gate conclusion

MDSI-5 introduces one server-owned realtime actor that turns accepted market
price trades into the shared immutable OHLCV observations consumed by later
bar-derived signal actors. It owns the interval schedule, state, source-lineage
rules, storage-first projection, and route lifecycle.

## 2. Scheduling and aggregation

The state maintains 5-second, 15-second, 30-second, 1-minute, 5-minute,
15-minute, and Daily accumulators. CME session-calendar boundaries determine
the Daily value date and session close. Each bar retains OHLC, exact trade-size
volume, trade count, price-volume sum, accepted sequence range, and first/last
market-event timestamps.

Observation identities are deterministic over series, timeframe, interval end,
and last accepted source sequence. Duplicate or stale deliveries cannot close a
second logical bar. A source-ordinal gap invalidates the current epoch and all
remaining events in that epoch; a new epoch or contract roll starts clean
state. A contract roll closes the old contract before accepting the new one.

## 3. Projection and lifecycle

The realtime projector writes the immutable Scylla observation before
publishing `FuturesAnalyticsObservationClosedRealtimeEvent`. Daily closes also
write the raw EOD row. Failed persistence cannot publish or confirm an
observation.

Startup installs the market-price route and starts a one-second private clock
loop. Clock barriers pass through the actor mailbox so interval closure is
serialized with trades. Shutdown removes the route, cancels and awaits the
clock, and then lets the actor infrastructure stop. The private clock event is
a public transport type, as required by MessagePack, while its verb and fixed
identity remain internal implementation details.

## 4. Accepted qualification

| Suite | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| Analytics unit | 889 | 0 | 0 |
| Analytics BDD | 462 | 0 | 0 |
| Analytics integration | 39 | 0 | 0 |
| Feed integration | 46 | 4 | 0 |

Focused tests cover every schedule, calendar close, duplicate, out-of-order
trade, missing ordinal, epoch recovery, contract roll, deterministic identity,
storage-before-publish, startup routing, and shutdown. The complete integration
host now starts and stops without actor-shutdown errors.

## 5. Exit decision

Each logical closed interval produces one immutable identity and publishes it
once after storage. Server lifecycle and source-integrity behavior are
qualified. MDSI-5 is complete.
