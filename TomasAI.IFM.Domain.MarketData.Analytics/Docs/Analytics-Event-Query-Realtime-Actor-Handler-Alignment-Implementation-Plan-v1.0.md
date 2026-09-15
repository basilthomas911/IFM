# Analytics Event, Query, and Realtime Actor Handler Alignment — Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Status | Implemented; verification complete |
| Date | 2026-09-15 |
| Scope | All deficient Event, Query, and Realtime actors in `TomasAI.IFM.Domain.MarketData.Analytics` |
| Authority | `Documents/system/Actor-Implementation-Conventions.md`, sections 2.4, 6, 9.4.1, 13.2, and 13.4.2 |
| Inventory | 35 actors: 13 Event, 11 Query, 11 Realtime. Twenty-three actors have handler deficiencies. |

## 1. Objective and invariant

Every concrete receive-map message has exactly one extension-handler class and source file directly in its actor-role folder (`Event`, `Query`, or `Realtime`). The class and file use the concrete message name without the trailing `Event`, `RealtimeEvent`, or `Query` suffix. Each class exposes one mapped `Execute` or `ExecuteAsync` extension method for that message. Lifecycle qualifiers such as `Complete` and `Fail` remain in the name. Pure calculation, shared storage helpers, and other domain-specific support may live in `Model`; transport dispatch, actor context, typed replies, and message-specific behavior stay in the dedicated handler.

The actor retains its `_parseMap`, exact-type `_receiveMap`, and framework-owned parse/receive flow. Query actors also retain `_exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys)` and typed `IQuery<TResult>` replies. Event and Realtime actors retain current routing, acknowledgement, failure logging, and retry semantics. A formatting-only or handler-placement change must not silently turn a failed event into success or add a database read, queue, timer, or per-tick allocation.

The source inventory found matched parse/receive entries in all 35 actors and exception maps in all 11 Query actors. This is a static observation to be enforced by tests, not a claim that every current handler is convention-compliant.

## 2. Gate A — freeze the current message manifest and behavior

1. Record each actor's accepted verbs, exact receive types, route registrations, and handler result behavior in an architecture test manifest. Include the 12 currently aligned actors so later edits cannot regress them.
2. Add source/architecture checks for equal parse/receive message sets; Query parse/receive/exception parity; one exact class/file per receive message in the owning role folder; one public mapped extension method with the corresponding `this <ConcreteMessage>` parameter; and receive delegates that only cast and call that extension.
3. Capture behavior tests before moving code: typed success/not-found/failure Query replies, cancellation and malformed ingress, event Complete/Fail outcomes, HistoricalDataLoader requested/completed/failed semantics, and Realtime routed-event failure behavior.
4. Establish the baseline with Analytics Unit, BDD, and Integration suites and the normal API build. Do not run the integration host beside a live IFM API/UI process. Stop the test host and verify it exited after each integration run.

**Gate exit:** The frozen manifest lists all 35 actors and every receive-map message; tests identify current handler deficiencies without changing supported messages or contracts.

## 3. Gate B — align 11 Query actors (22 mapped queries)

| Actor | Handler work |
| --- | --- |
| `MarketOutlookSnapshotQueryActor` | Move `ReceiveSnapshotAsync` into `Query/GetMarketOutlookSnapshot.cs`; retain minimum-revision and typed error behavior. |
| `FuturesAnalyticsHistoricalDataLoaderQueryActor` | Move its store lookup and typed reply into `Query/GetFuturesAnalyticsHistoricalDataLoader.cs`. |
| `FuturesAdxSignalQueryActor` | Give `GetFuturesAdxSignal` and `GetFuturesAdxDailySignal` complete message handlers; move read/reply work out of map lambdas. |
| `FuturesAtrSignalQueryActor` | Do the same for `GetFuturesAtrSignal` and `GetFuturesAtrDailySignal`. |
| `FuturesItiSignalQueryActor` | Do the same for data, latest, history, and trend-direction-changed queries. |
| `FuturesMacdSignalQueryActor` | Do the same for intraday and daily queries. |
| `FuturesRsiSignalQueryActor` | Do the same for intraday, daily, and trend-direction queries. Keep calculation/read helpers separate from the typed reply handler. |
| `FuturesTdiSignalQueryActor` | Give `GetFuturesTdiSignal` a complete read/reply handler. |
| `FuturesTradeSignalQueryActor` | Give current, last, and IDs queries their own read/reply handlers. |
| `FuturesVwapSignalQueryActor` | Give latest and history queries their own read/reply handlers; retain paging and ordering semantics. |
| `FuturesVxTermStructureSignalQueryActor` | Give latest observation query a complete handler. |

For each Query actor, `_receiveMap` becomes an exact-type cast followed by `query.ExecuteAsync(typedContext, cancellationToken)`. The dedicated handler performs the existing read and sends the same `ServiceResult<TResult>` through `ReplyAsync`; exceptions still reach the actor's existing typed exception path. Do not add a Command-style validation map or change Query contracts. Existing read-only helper methods may remain as Model/storage helpers only when they are actually called by a dedicated handler and do not process another mapped message.

**Gate exit:** All 22 Query entries call one dedicated handler; success, null/not-found, paging where relevant, cancellation, storage exceptions, and `ServiceFailed<TResult>` preserve their observed behavior. Query map parity passes.

## 4. Gate C — align 8 Event actors (14 handler corrections)

| Actor | Handler work |
| --- | --- |
| `FuturesBbSignalEventActor` | Add `Event/FuturesBbSignalGeneratedComplete.cs`; move Market Outlook publication out of the receive delegate. |
| `FuturesRsiSignalEventActor` | Add `Event/FuturesRsiSignalGeneratedComplete.cs`; move its warm/valid component gate and publication from the actor map. |
| `FuturesTradeSignalEventActor` | Move Market Outlook publication for `FuturesTradeSignalUpdatedCompleteEvent` into its existing dedicated handler; leave the receive delegate as a cast/call. |
| `FuturesAdxSignalEventActor` | Rename the Started and Stopped classes to exactly `FuturesAdxSignalStarted` and `FuturesAdxSignalStopped`; retain their attachment and failure behavior. |
| `FuturesMacdSignalEventActor` | Rename the two completed-event handler classes/files to `FuturesMacdSignalGeneratedComplete` and `FuturesMacdDailySignalGeneratedComplete`; retain timer and publication behavior. |
| `FuturesVxTermStructureSignalEventActor` | Split combined `FuturesVxTermStructureSignalEvents` into `Event/FuturesVxTermStructureSignalUpdatedComplete.cs` and `...UpdatedFail.cs`. |
| `FuturesVwapSignalEventActor` | Split combined `FuturesVwapSignalEvents` into `Event/FuturesVwapSignalUpdatedComplete.cs` and `...UpdatedFail.cs`. |
| `FuturesAnalyticsHistoricalDataLoaderEventActor` | Move requested-event processing out of `ReceiveRequestedAsync` into `Event/FuturesAnalyticsHistoricalDataLoaderRequested.cs`; add dedicated Completed and Failed handlers even where the current receive action is terminal/no-op. |

Pass actor context and typed logger explicitly to each Event handler. Keep retry-triggering exceptions observable and preserve the current event actor return/acknowledgement behavior. Lifecycle handlers may share logging identifiers, but not a mapped handler class. Avoid placing Market Outlook business decisions in receive delegates.

**Gate exit:** All deficient Event entries call dedicated single-message handlers; no Event actor contains message-specific processing in `_receiveMap` or an actor-local receive method. The five already aligned Event actors remain aligned.

## 5. Gate D — align 4 Realtime actors (10 handler corrections)

| Actor | Handler work |
| --- | --- |
| `MarketOutlookSnapshotRealtimeActor` | Create direct `Realtime` handlers for component changed, EOD updated, market price updated, session statistics updated, and snapshot inserted. Move `SubmitComponent`, `SubmitEod`, `SubmitMarketPriceAsync`, and `SubmitVxSessionStatisticsAsync` message-specific work out of the actor; keep pure shared calculation in `Model`. |
| `FuturesTdiSignalRealtimeActor` | Keep `FuturesRsiSignalsGenerated` as its dedicated handler. Create separate direct handlers for TDI Generated, GeneratedComplete, and GeneratedFail; move projection, Market Outlook publication, failure logging, and terminal no-op decisions out of the map. |
| `FuturesVwapSignalRealtimeActor` | Move the existing one-message `FuturesMarketPriceUpdated` handler from `Realtime/Extensions` to `Realtime/FuturesMarketPriceUpdated.cs` without changing its hot-path behavior. |
| `FuturesVxTermStructureSignalRealtimeActor` | Make the same handler-placement move. |

Realtime actors retain route registration, stream ownership/lease setup, startup/shutdown cleanup, exact-type parsing, and mailbox boundaries. A Realtime source or terminal message that currently does nothing still has its own handler. No new persistence path or polling is introduced. Preserve structured failure logging and avoid constructing a new object or capturing a new closure for each market tick.

**Gate exit:** All 10 affected Realtime entries call a direct one-message handler; the seven already aligned Realtime actors remain aligned; route registration and hot-path behavior are unchanged.

## 6. Gate E — test and verification matrix

| Test layer | Required coverage |
| --- | --- |
| Architecture/unit | All 35 actor manifests; exact type, parse/receive parity, Query exception parity, handler name/file/location, one mapped method per class, receive-map delegation. Unsupported verb/concrete type and malformed ingress fail closed. |
| Query unit | Happy read, not-found/null, storage exception with typed failure, cancellation, and history/paging boundaries for every affected Query family. |
| Event unit | Complete and Fail delivery, logger/error details, retry/acknowledgement behavior, attachment/timer outcomes, warm/valid Market Outlook gate, and HistoricalDataLoader requested/completed/failed edges. |
| Realtime unit | Eligible and ineligible source events, market contract rollover and missing upstream data where applicable, terminal notification/no-op behavior, exception logging, and no duplicate route registration. |
| BDD | Representative end-to-end user/domain scenarios: valid analytics signal reaches Market Outlook, invalid or cold signal stays excluded, typed query succeeds or returns not-found, HistoricalDataLoader lifecycle completes/fails, and TDI/VX/VWAP source failures degrade without duplicate processing. |
| Integration | Existing Analytics suite plus targeted Market Outlook, historical loader, query API, TDI, VX, VWAP, and routed market-price tests. Confirm NATS routing, Scylla read-model updates, typed replies, and preserved Event projector lifecycle. |
| Verification | Full Analytics Unit/BDD/Integration; normal API build; relevant UI build only if project references are changed; `git diff --check`; test host exited; no live API/UI concurrently with integration host. |

Tests must verify observable behavior, not just mirror the implementation. Use source architecture checks to enforce file/class conventions, and behavioral tests to prove refactoring preserves responses and side effects. Add a focused hot-path allocation check or benchmark only if a moved Realtime handler changes the amount or shape of work per tick; do not impose a new benchmark on formatting-only moves.

## 7. Completion and review gate

The migration is complete when no deficient actor remains, the source architecture manifest passes for all 35 actors, every mapped concrete message has exactly one correctly named direct-role handler, full affected tests pass, and no query reply type, event lifecycle, realtime route, or durable projection behavior has changed unintentionally. Review the final diff for residual combined `*Events`/generic `Extensions` receive handlers and actor-local message processing before considering the gate complete. Record any genuine domain-behavior discrepancy as a separate issue rather than hiding it inside this convention refactor.

## 8. Implementation and verification record

All 35 Event, Query, and Realtime actor message manifests are frozen by the Analytics architecture test. The 23 deficient actors now dispatch each accepted concrete message to a dedicated class in the direct actor-role folder. Query read/reply logic, Event lifecycle handling, and Realtime source handling moved out of receive-map delegates. The HistoricalDataLoader requested handler has success, failure, and stale-request tests; the moved HistoricalDataLoader, VX, and VWAP Query handlers have typed no-data and cancellation tests. Existing BDD and integration scenarios cover routed signal and projection behavior.

The final Analytics Unit suite passed **1,071/1,071** tests. Analytics BDD passed **472/472** and Analytics Integration passed **45/45** after the production handler moves. The normal API build passed with **zero warnings and zero errors**. The integration host was run with no live IFM API or UI and exited afterward. No Realtime tick-path work shape changed, so this alignment did not add a benchmark gate.
