# Trade Lifecycle Backend Implementation Plan

**Date:** 2026-09-12  
**Version:** 1.0  
**Status:** Implemented and qualified; external-service soak deferred
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
- Established lifecycle history is append-only. The legacy OptionTrade event stream requires no compatibility reader because no historical trade events exist.
- No database access, LINQ, reflection, string formatting, or per-tick logging is permitted in the realtime tick-routing hot path.

## 3. Gate 0 - inventory and clean cutover

1. Inventory callers of removed TradeOrder handlers and live-position responsibilities in OptionTrade.
2. Map each required responsibility to TradeOrder, OrderExecution, established Trade, StrategyPosition, or analytics ownership.
3. Remove the legacy OptionTrade command actor, command contracts, projector, state, repositories, endpoints, and clients.
4. Establish build and test baselines before cutover.

**Exit:** all affected references have an owner in the new lifecycle and no legacy OptionTrade writer remains discoverable.

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

Wire actor registration and lifecycle handoffs. Remove legacy writers after the new path has passed build, unit, BDD, actor-map, serialization, replay, NATS, PostgreSQL, failure, and benchmark qualification. No historical migration or compatibility-event reader is required because the legacy trade event store is empty.

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

## 14. Implementation evidence (2026-09-12)

The coding and automated qualification gates for the backend path are complete. Production code is organized under the owning `Trade/Order`, `Trade/Order/Execution`, `Trade/Futures`, `Trade/Futures/Option`, `Trade/Futures/Position`, and `Trade/Futures/Option/Position` hierarchies. No catch-all `Lifecycle` code namespace remains. The legacy `Domain.Trade.Option` command actor, command state, projector, shared command messages, HTTP/NATS clients, and server endpoints have been removed. The new aggregate uses `TomasAI.IFM.Domain.Trade.Shared.Model.TradeOrderId`, containing Portfolio, Fund, and Order IDs.

| Gate | Implemented result | Verification |
|---|---|---|
| Contracts | Common ownership IDs, generic orders/components/legs, normalized fill evidence, established Futures/Option trades, strategy positions, commands, events, and concrete queries | MessagePack-shape, round-trip, validation, canonical identity, and compatibility checks |
| TradeOrder | Event-sourced command/query actors with Create, Amend, Approve, Ready, Bind/Release Execution, Complete, Cancel, and Expire transitions. Release requires the exact bound attempt and proven zero exposure. | Unit transition, identity, release-fence, and replay tests |
| OrderExecution | Manual/Broker-neutral execution state, deterministic fill allocation, balanced-partial policy, immutable original fill evidence | Unit and BDD happy/edge paths |
| Established Trade | FuturesOptionTrade and FuturesTrade actors with concrete Iron Condor/Vertical Spread/Futures queries; the legacy OptionTrade command authority is removed | Serialization, classification, query-contract, and legacy-absence tests |
| StrategyPosition | Resident event-sourced Futures outright, Iron Condor, and Vertical Spread actors; Open, per-leg MTM, EOD, Close, basis correction, and snapshot operations | Replay, stale-route, duplicate-sequence, and lifecycle tests |
| Realtime routing | Separate Futures and Futures Option realtime actors over an actor-local `MarketInstrumentId` reverse index, complete startup recovery snapshot, tick fan-out registration, generation fence, and expected ignore outcomes | Direct handler tests, BDD pre-open/post-close/restart tests, allocation verification |
| Durability | Additive CQL tables and projections for orders, executions/fills, trades/history, positions/history, route lookup, and route recovery. Established-trade history v2 includes `EvidenceRevision` in its immutable primary key and leaves any v1 table untouched. | Schema qualification plus a real Scylla round-trip integration test; no destructive migration statements |
| Handoffs | Durable event projectors own all projections and `TradeOrder -> OrderExecution -> Trade -> StrategyPosition` handoffs. Deterministic destination command IDs make replay idempotent. Zero-fill cancelled/rejected executions release their exact bound order attempt; any exposure prevents automatic release. Portfolio Open/Close/Correction boundary events are emitted without mutating the deferred Portfolio aggregate. | Projector/repository registration, handoff identity, and execution-release fence tests |
| Actor conventions | Every command, query, and realtime actor derives directly from its one standard framework base. Actors contain explicit parse/validation/receive maps and infrastructure lifecycle; domain work is in `Extensions` handlers or `Model` state machines. | Reflection qualification of all direct bases plus repository scan proving no trade `Lifecycle` code namespace |

Focused automated results:

- Trade-flow unit: 20 passed.
- Trade-flow BDD: 5 passed.
- Trade-flow integration: 5 passed across serialization, recovery, and runtime registration.
- Trade-flow verification: 7 passed.
- Complete Domain.Trade unit suite: 1,092 passed.
- Complete Domain.Trade BDD suite: 41 passed.
- The broader integration and verification suites still require their local NATS/PostgreSQL/Scylla hosts; the trade-flow-filtered suites above are self-contained and passed.
- Real Scylla lifecycle storage integration: implemented and compiled; its 2026-09-12 execution is blocked because no host on `localhost:9042` responded within the driver timeout.
- Full repository build with Visual Studio 2026 Community native tooling: zero warnings and zero errors.

BenchmarkDotNet results on .NET 10, AMD Ryzen Threadripper 1950X:

| Operation | Mean | Managed allocation |
|---|---:|---:|
| Known instrument with no open position | 17.22 ns | 0 B |
| One open-position route | 16.78 ns | 0 B |
| Lookup plus complete walk of 64 routes | 81.16 ns | 0 B |
| Four-leg position MTM calculation and immutable snapshot | 274.7 ns | 280 B |

The four-leg calculation was reduced from 480.7 ns and 744 B by retaining canonical leg order in resident state and eliminating per-tick sorting and LINQ enumeration. The remaining allocation is the immutable leg array, changed leg, and whole-position snapshot that become durable event evidence.

The live NATS/PostgreSQL/Scylla lifecycle soak and production cutover remain scheduled for the approved trading window. IBKR adapters/emulator, order-entry UI, and Portfolio/Fund accounting mutations remain the explicitly deferred boundaries in section 13.
