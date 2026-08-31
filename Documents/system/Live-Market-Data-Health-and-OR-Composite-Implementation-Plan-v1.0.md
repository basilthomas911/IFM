# Live Market Data Health and OR-Composite Implementation Plan v1.0

**Status:** Implemented and qualified  
**Date:** 2026-08-31

## Binding behavior

Databento is the authoritative realtime input. A record is observable downstream only after it
advances the canonical hot cache. Duplicate, older, and otherwise rejected records are counted and
ignored without throwing an expected runtime exception.

Every active route is evaluated independently:

- green: an accepted cache update whose effective age is no more than five minutes;
- yellow: older than five minutes and no more than fifteen minutes;
- red: older than fifteen minutes;
- inactive: the route has no current owner and is not evaluated as failed.

Effective age is the worse of cache-arrival freshness and Databento source-event freshness. This
prevents delayed backlog from falsely restoring a route to green.

Composite analytics use OR admission. Each valid component is accepted, persisted, projected, and
shown independently. Missing or invalid siblings do not stop it. Calculations continue to require
their actual mathematical operands: OR admission never substitutes RSI for TDI, VX for ES, or any
other unrelated signal.

## Runtime flow

```text
Databento record
  -> contract/route validation
  -> canonical hot-cache ordering admission
       rejected -> counter only
       accepted -> accepted timestamps/counter
  -> realtime market-price event
       -> independent ES/VX analytics consumers
       -> UI input-health monitor
  -> each valid analytics component
  -> Market Outlook command state
  -> PostgreSQL event + ScyllaDB working snapshot
  -> NATS frontend notification
  -> partial UI refresh; unavailable fields show N/A
```

## Completed gates

- LMO-01: baseline paths and failure modes mapped.
- LMO-02: shared accepted-input health contract and exact thresholds.
- LMO-03: futures quote/trade admission tightened.
- LMO-04: option quote/trade admission tightened.
- LMO-05: accepted/rejected counters, timestamps, API readiness details, and active-route health.
- LMO-06: realtime cache usability aligned to the fifteen-minute red boundary.
- LMO-07: OR-composite contract documented and implemented.
- LMO-08: Futures Trade Signal optional enrichments no longer form an all-input prerequisite.
- LMO-09: calculations retain operand-specific requirements and explicit partial output.
- LMO-10: Market Outlook advances on every accepted independent component; stale fallback removed.
- LMO-11: UI consumes accepted-cache events and renders unavailable values as `N/A`.
- LMO-12: BDD, unit, health-boundary, duplicate/out-of-order, and UI partial-state tests.
- LMO-13: NATS/Scylla/PostgreSQL Market Outlook and tick-aggregation integration qualification.
- LMO-14: all 127 non-empty seven-component availability masks verified.
- LMO-15: API build, documentation, and targeted regression qualification.

## Qualification evidence

- Databento unit/native: 132 passed.
- Analytics unit and exhaustive availability verification: 1,080 passed.
- Analytics BDD: 466 passed.
- UI health, service wiring, and partial Market Outlook: 32 targeted passed.
- UI system/layout color qualification: 7 passed.
- Feed tick-aggregation integration: 8 passed.
- Market Outlook NATS/storage/query integration: 1 passed.
- API server build: succeeded with zero warnings and zero errors.

The exhaustive availability matrix verifies input presence and independent progression. It does
not claim that 127 distinct market interpretations exist or that incomplete operands yield a fully
actionable trading decision.

## Unrelated baseline finding

The complete presentation-unit project reports 248 passed and six failures, all in the legacy
`TradeOrderEditorViewModelTests` fixture. That fixture does not register the now-required
`IPortfolioQueryApi`, so it dereferences a null test substitute before exercising trade-order
behavior. The Market Outlook/feed-health/UI target filters pass and none of the six failures enter
the live-market-data or Market Outlook paths.
