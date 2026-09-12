# Trade Lifecycle Backend Implementation Plan

**Date:** 2026-09-12  
**Version:** 1.0  
**Status:** Approved for implementation  
**Design authority:** `Trade-Order-Execution-Trade-Position-Backend-Schema-Design-v1.2.md`  
**Actor convention:** `Documents/system/Actor-Implementation-Conventions.md`

## 1. Outcome

Implement the broker-neutral backend lifecycle:

```text
TradeOrder -> OrderExecution -> OptionTrade | FuturesTrade -> StrategyPosition
```

The lifecycle carries Portfolio and Fund ownership from the approved order to the live position. Position-opened, position-closed, and correction events provide the future Portfolio integration boundary. Portfolio mutation, the IBKR adapter, the IBKR emulator, and the legacy Trade Order UI are outside this increment.

## 2. Non-negotiable boundaries

- `TradeOrder` owns executable intent and approval evidence.
- `OrderExecution` owns normalized execution attempts, reports, fills, reconciliation, and the accepted-fill boundary.
- `OptionTrade` and `FuturesTrade` own immutable established-trade facts and an immutable copy of every accepted opening fill.
- Strategy Position actors own Open, MTM, EOD, Close, and Correction history.
- Realtime actors route market ticks and never own P&L or position state.
- Portfolio/Fund IDs are mandatory throughout the chain; Portfolio behavior is deferred.
- Existing immutable history is never overwritten. Compatibility adapters keep legacy records readable.
- No database access, LINQ, reflection, string formatting, or per-tick logging is permitted in the realtime tick-routing hot path.

## 3. Gate 0 - inventory and compatibility fixtures

1. Record legacy TradeOrder and OptionTrade message keys, event names, stream IDs, query results, and database shapes.
2. Inventory callers of removed TradeOrder handlers and live-position responsibilities in OptionTrade.
3. Preserve legacy MessagePack decoding and read paths while introducing versioned writers.
4. Establish build and test baselines before cutover.

**Exit:** compatibility samples exist and all affected references have an owner in the new lifecycle.

## 4. Gate 1 - shared lifecycle contracts

Implement append-only MessagePack schemas for:

- ownership and aggregate identities;
- proposed components and stable `TradeLegId` legs;
- execution groups and constraints;
- normalized reports, fills, fill allocations, and terminal outcomes;
- common established Trade, OptionTrade, FuturesTrade, and fill evidence;
- common StrategyPosition envelope and strategy-specific payloads;
- lifecycle commands, events, concrete queries, paging, and typed results.

Validation must reject default IDs, duplicate leg IDs, duplicate component IDs, mismatched ownership, unsupported topology, invalid quantities, invalid prices, unbalanced typed exposure, non-UTC timestamps, and invalid lifecycle transitions without producing incidental exceptions.

**Exit:** serialization, identity, canonicalization, hash, and validation tests pass.

## 5. Gate 2 - TradeOrder actors

Create `TradeOrderCommandActor` and `TradeOrderQueryActor` using explicit parse, validation, receive, and exception maps. Command extensions own Create, Amend, Approve, Ready, Cancel, Expire, BindExecution, ReleaseExecution, and Complete behavior. The state applies private events and the repository persists them through the standard event-source context.

**Exit:** replay reconstructs identical state; duplicate commands are idempotent; invalid transitions fail before persistence.

## 6. Gate 3 - OrderExecution actors

Create broker-neutral `OrderExecutionCommandActor` and `OrderExecutionQueryActor`. Support normalized Manual and Broker channels without an adapter. Allocate fills deterministically to stable legs, accept balanced partial exposure only under explicit policy, and emit exactly one typed trade-creation request per accepted component.

**Exit:** partial, completed, cancelled, rejected, duplicate, corrected, and reconciled executions have deterministic tests.

## 7. Gate 4 - typed Trade actors

Create `FuturesTradeCommandActor`/query support and refactor `OptionTradeCommandActor` to established-trade ownership. Original accepted fills remain immutable. Late fees and corrections append evidence amendments. Remove order execution, live leg observation, MTM, EOD, and position-history ownership from the new OptionTrade authority.

Concrete query contracts include Iron Condor, Vertical Spread, and supported Futures strategies while returning their common asset-family read model.

**Exit:** accepted one-leg Futures and supported Option executions create exactly one idempotent typed Trade; unbalanced exposure cannot be mislabeled.

## 8. Gate 5 - StrategyPosition actors

Create resident event-sourced position actors for Futures Iron Condor and Futures Vertical Spread, plus their query actors. State is an `ITradePositionCollection` keyed by stable `TradeLegId`. Replacing one leg price recalculates and appends one coherent whole-strategy version. Commands cover Open, UpdateLegMarketPrice, EndOfDay, Close, CorrectBasis, and Snapshot.

**Exit:** Open/MTM/EOD/Close/Correction replay and latest/date-range projections pass for one, two, four, and bounded custom leg counts.

## 9. Gate 6 - optimized realtime routing

Create `FuturesRealtimeActor` and `FuturesOptionRealtimeActor` around a shared in-memory reverse index:

```text
MarketInstrumentId -> RouteBucket[PortfolioId, FundId, TradeId,
                                  StrategyPositionId, TradeLegId,
                                  ActorType, ActorThreadId, Generation]
```

The dictionary is pre-sized and owned by the actor mailbox. Each value is a stable route array replaced only on position Open, Close, Correction, or recovery. Tick processing is one dictionary lookup plus a linear walk over matching routes. A route generation fences close/correction races. Ticks are partitioned by canonical instrument identity to preserve per-contract ordering while allowing unrelated contracts to run concurrently.

Lookup outcomes are `Routed`, `NoOpenPosition`, `StaleRoute`, `UnknownInstrument`, `InvalidTick`, and `DuplicateOrOutOfOrder`. Expected outcomes are acknowledged and ignored; they never throw, retry, dead-letter, or replay. The hot path increments actor-local counters. It emits rate-limited first-occurrence diagnostics and periodic aggregate summaries instead of one log allocation per ignored tick.

The recovery projection supplies a complete snapshot of open positions and active legs. The actor atomically replaces its index before accepting live routing. No PostgreSQL call is allowed per tick.

**Performance acceptance:**

- zero managed allocation for an unrouted valid tick;
- no route-collection allocation for a routed tick;
- deterministic fan-out for multiple positions sharing one contract;
- no exception for closed, unknown, duplicate, or not-yet-defined trades;
- benchmark median, P95/P99, throughput, bytes/op, and mailbox-depth behavior for zero, one, and many routes.

## 10. Gate 7 - storage and projections

Add versioned, additive schemas for orders/components/legs, executions/fills, typed trades/fill evidence, current positions, history, and open-position routing recovery. Use deterministic keys and expected stream revisions. Apply migrations idempotently and leave old tables/events untouched.

Projection handlers run from committed lifecycle events. Every handoff has a durable receipt or an idempotent destination identity so replay cannot duplicate a Trade or position.

**Exit:** clean install, upgrade, rollback-read, replay, and projection-rebuild integration tests pass.

## 11. Gate 8 - end-to-end cutover

Wire actor registration and lifecycle handoffs. Stop legacy writers only after the new path has passed build, unit, BDD, actor-map, serialization, replay, NATS, PostgreSQL, failure, and benchmark qualification. Keep compatibility readers until production history is migrated under a separately approved retirement gate.

**Exit:** `TradeOrder -> OrderExecution -> typed Trade -> StrategyPosition` completes exactly once and preserves ownership, fills, correlation, and causation.

## 12. Required test matrix

### Unit

- identity, canonical key, equality, hash, MessagePack keys, and round trips;
- every validator and every state transition;
- map parity and exact-type rejection for every actor;
- deterministic fill allocation and typed-trade classification;
- per-leg replacement and whole-strategy calculations;
- route add/remove/replace/generation behavior;
- ignored-tick outcomes without exceptions or retry signals.

### BDD

- approved order becomes a completed execution, typed trade, and open position;
- partial execution remains pending;
- explicitly accepted balanced partial exposure establishes the correct trade;
- unbalanced exposure cannot become an Iron Condor or Vertical Spread;
- the same contract routes to positions in multiple Funds/Portfolios;
- ticks before Open and after Close are observed and ignored;
- a close racing a tick is fenced by route generation;
- restart rebuilds routing before live processing.

### Integration

- event log append/replay and command deduplication;
- PostgreSQL migrations, constraints, projections, paging, and date ranges;
- actor request/reply and NATS serialization;
- handoff idempotency at every boundary;
- complete lifecycle with Manual normalized fill evidence;
- failure injection for persistence, projection, duplicate delivery, and restart.

### Verification and benchmarks

- repository-wide actor convention checks and no legacy-writer references;
- compatibility decoding of frozen legacy samples;
- BenchmarkDotNet route lookup/fan-out/allocation cases;
- resident position update throughput and durability-window behavior;
- sustained mailbox test proving bounded depth and ordered per-instrument delivery.

## 13. Deferred work

- IBKR connectivity, translation, callbacks, session recovery, and reconciliation adapter;
- IBKR emulator;
- Trade Order entry UI;
- Portfolio/Fund aggregate mutation, accounting, capital, and risk behavior;
- custom mixed-asset Trade actors beyond the extensibility contracts;
- production soak acceptance, which requires an approved live trading window.

