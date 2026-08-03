# Fund Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.Fund` implements command, event, and query actors for funds and fund transactions. It uses event-source storage for command state, EventProjector for projections, and shared Fund, Analytics, and Trade contracts.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.Fund/`. Every leaf path includes its parent folders.

```text
Command/Actor/
Command/EventProjector/
Command/Exceptions/
Command/Model/
Command/State/
Command/Validation/
Docs/
Event/Actor/
Event/Api/
Query/Actor/
Query/Api/
Transaction/Command/Actor/
Transaction/Command/Exceptions/
Transaction/Command/Model/
Transaction/Command/State/
Transaction/Command/Validation/
Transaction/Event/Actor/
Transaction/Query/Actor/
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

`bin/` and `obj/` are generated output/intermediate trees. The `net8.0` leaves are stale artifacts; the project currently targets `net10.0`.

## Folder responsibilities

- `Command/` contains the fund write side: `Actor` routes commands, `Validation` checks input, `Model` carries write-side data, `State` restores and persists the aggregate, `Exceptions` defines command-specific failures, and `EventProjector` updates the fund read model.
- `Event/` contains the fund event mailbox and the actor-scoped event API used by handlers to publish or coordinate events.
- `Query/` contains the fund query mailbox and storage-backed query API.
- `Transaction/` is the nested bounded context for fund transactions. Its `Command`, `Event`, and `Query` branches follow the same actor split; command state, models, validation, and exceptions are local to transaction processing.
- `Docs/` contains this document.
- The root contains `FundActorAssembly` for assembly discovery and the .NET project definition.

## Implemented actors

- `FundCommandActor` is an event-sourced command actor for the `FundCommand` mailbox.
- `FundEventActor` consumes `FundEvent` messages.
- `FundQueryActor` handles fund read requests through the query API.
- `FundTransactionCommandActor`, `FundTransactionEventActor`, and `FundTransactionQueryActor` implement the equivalent transaction pipeline.
- `ActorFundEventApi` and `ActorFundQueryApi` expose actor-safe event and query operations to other services.

## Processing model

Command actors parse NATS subjects and payloads, validate command data, load aggregate snapshots, dispatch to typed command logic, persist uncommitted events, and denormalize those events. Event actors route published events to side-effect handlers. Query actors delegate reads to database contexts through the API layer. Actor exceptions are converted to standardized error events/results by the shared actor base classes.

## Extension points

Keep new behavior inside the appropriate Fund or Transaction branch. A new event-sourced command normally requires a shared contract, parser/receiver/validator registration, state transition, repository persistence/denormalization, and matching event handling or projection.
