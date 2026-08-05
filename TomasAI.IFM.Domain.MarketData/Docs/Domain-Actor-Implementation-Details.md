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

`MarketDataQueryActor` and `YieldCurveRateQueryActor` inherit the shared query actor pipeline. `YieldCurveRateCommandActor` owns the `YieldCurveRateCommand` mailbox and uses event-sourced state/repository persistence. `YieldCurveRateEventActor` intentionally remains a no-op sink: command actors default to publishing events to an event actor in the same domain, and not every domain event currently requires downstream work.

## Processing model

Writes travel from a NATS command subject through parsing, validation, state reconstruction, typed dispatch, event persistence, and event publication. Event actors perform downstream processing when required, while query actors execute read-only operations through the market-data database API. All actor mailbox identities combine actor type, actor name, verb, and entity identifier.

The boolean returned by a command state's `Update` operation means that state changed. It is not the command's success result: a valid command may succeed without producing a state change. Actor handlers must preserve that distinction.

## Extension points

Add general reads beneath `Query`. Add a full command/event/query feature beneath its own market-data entity folder, mirroring `YieldCurveRate` and keeping state, validation, and models with the write side.

## TODO: solution-wide graceful cancellation

Cancellation propagation is intentionally deferred until optimization of all root domain projects is complete. Implement it as a dedicated solution-wide change so the supervisor can stop actors gracefully and cancellation semantics remain consistent from the highest orchestration layer to the lowest storage or network operation.

The solution-wide design must define:

- supervisor shutdown deadlines and whether queued messages drain or are abandoned;
- cancellation of in-flight actor handlers without violating per-entity ordering;
- token propagation through actor contracts, base actors, repositories, storage contexts, providers, and network clients;
- the distinction between a caller cancelling its wait and cancelling server-side execution; and
- idempotency and consistency when cancellation occurs during event persistence, projection updates, or publication.

Until that work begins, domain optimizations must keep operations properly awaited, remove sync-over-async calls, and avoid introducing fire-and-forget work so cancellation can be added without another behavioral rewrite.
