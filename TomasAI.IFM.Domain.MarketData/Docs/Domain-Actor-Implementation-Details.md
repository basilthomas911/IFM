# Market Data Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.MarketData` provides general market-data queries plus the event-sourced Economic Calendar and Yield Curve Rate actor pipelines. It targets .NET 10 and uses the Storage, Shared Domain, and MarketData Shared projects.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.MarketData/`.

```text
Docs/
EconomicCalendar/Command/Actor/
EconomicCalendar/Command/Exceptions/
EconomicCalendar/Command/State/
EconomicCalendar/Command/Validation/
EconomicCalendar/Event/Actor/
EconomicCalendar/Query/Actor/
Query/Actor/
Query/Api/
YieldCurveRate/Command/Actor/
YieldCurveRate/Command/Model/
YieldCurveRate/Command/State/
YieldCurveRate/Command/Validation/
YieldCurveRate/Event/Actor/
YieldCurveRate/Query/Actor/
bin/Debug/net10.0/runtimes/win-x64/native/
bin/Debug/net8.0/
bin/Release/net10.0/runtimes/win-x64/native/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
obj/Debug/net8.0/ref/
obj/Debug/net8.0/refint/
obj/Release/net10.0/ref/
obj/Release/net10.0/refint/
```

Every leaf path includes all parent folders. `bin/` and `obj/` are generated; `net8.0` is legacy output because the project now targets `net10.0`.

## Folder responsibilities

- `EconomicCalendar/` owns calendar commands, queries, state, validation, and event publication. Its read projections are stored in the MarketData keyspace beside Yield Curve tables.
- `Query/Actor/` contains `MarketDataQueryActor`, the general market-data query mailbox.
- `Query/Api/` contains `ActorMarketDataQueryApi`, which performs the storage-backed reads used by query actors and clients.
- `YieldCurveRate/Command/Actor/` routes yield-curve write commands.
- `YieldCurveRate/Command/Model/` contains command-side data structures.
- `YieldCurveRate/Command/State/` contains event-sourced state and its repository.
- `YieldCurveRate/Command/Validation/` contains command validation rules.
- `YieldCurveRate/Event/Actor/` consumes yield-curve domain events.
- `YieldCurveRate/Query/Actor/` serves yield-curve reads.
- `Docs/` contains this document.
- The root assembly marker supports actor registration and scanning.

## Implemented actors

`MarketDataQueryActor`, `EconomicCalendarQueryActor`, and `YieldCurveRateQueryActor` inherit the shared query actor pipeline. The Economic Calendar and Yield Curve Rate command actors own their respective mailboxes and use event-sourced state/repository persistence. Their event actors provide the same-domain publication targets used by command processing.

## Processing model

Writes travel from a NATS command subject through parsing, validation, state reconstruction, typed dispatch, event persistence, and event publication. Event actors perform downstream processing when required, while query actors execute read-only operations through the market-data database API. All actor mailbox identities combine actor type, actor name, verb, and entity identifier.

The boolean returned by a command state's `Update` operation means that state changed. It is not the command's success result: a valid command may succeed without producing a state change. Actor handlers must preserve that distinction.

## Extension points

Add general reads beneath `Query`. Add a full command/event/query feature beneath its own market-data entity folder, mirroring `EconomicCalendar` or `YieldCurveRate` and keeping state, validation, and models with the write side.

## Solution-wide graceful cancellation

The solution-wide cancellation phase is now in progress. Yield-curve command validation, state replay, repository calls, event-source storage, PostgreSQL/ScyllaDB operations, and NATS operations accept the actor token. Accepted mailbox work drains before actors and their producers stop. Event persistence and required publication become non-cancelable at the commit boundary to avoid ambiguous durable outcomes.

The active `MarketDataQueryActor`, `EconomicCalendarQueryActor`, and `YieldCurveRateQueryActor` paths propagate the worker token through query handlers, MarketData reads, month-bucket calendar fan-out, trading-calendar database access and date loops, and external calendar/yield-curve parsing. A canceled read does not publish a stale query reply. Existing no-token methods remain compatibility entry points.

The direct in-process `IActorMarketDataQueryApi` composes both MarketData and Securities stores. Its public cancellation overloads are intentionally scheduled with the Securities query/read-model tranche so the token reaches every composed leaf in one change rather than merely canceling the caller's wait.

The existing Interactive Brokers feed is excluded because Databento will replace it. Future IBKR work should mirror the completed Databento lifecycle and backpressure design rather than extend the legacy implementation.
