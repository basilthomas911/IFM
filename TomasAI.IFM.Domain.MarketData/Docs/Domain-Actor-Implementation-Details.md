# Market Data Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.MarketData` provides general market-data queries and the event-sourced yield-curve-rate actor pipeline. It targets .NET 10 and uses the Storage, Shared Domain, and MarketData Shared projects.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.MarketData/`.

```text
Docs/
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

`MarketDataQueryActor` and `YieldCurveRateQueryActor` inherit the shared query actor pipeline. `YieldCurveRateCommandActor` owns the `YieldCurveRateCommand` mailbox and uses event-sourced state/repository persistence. `YieldCurveRateEventActor` consumes the resulting `YieldCurveRateEvent` messages.

## Processing model

Writes travel from a NATS command subject through parsing, validation, state reconstruction, typed dispatch, event persistence, and event publication. Event actors perform downstream processing, while query actors execute read-only operations through the market-data database API. All actor mailbox identities combine actor type, actor name, verb, and entity identifier.

## Extension points

Add general reads beneath `Query`. Add a full command/event/query feature beneath its own market-data entity folder, mirroring `YieldCurveRate` and keeping state, validation, and models with the write side.
