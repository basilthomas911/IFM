# Market Data Securities Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.MarketData.Securities` implements command, event, and query actors for futures contracts and futures option contracts.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.MarketData.Securities/`.

```text
Docs/
FuturesContract/Command/Actor/
FuturesContract/Command/Exceptions/
FuturesContract/Command/Model/
FuturesContract/Command/State/
FuturesContract/Command/Validation/
FuturesContract/Event/Actor/
FuturesContract/Query/Actor/
FuturesOptionContract/Command/Actor/
FuturesOptionContract/Command/Exceptions/
FuturesOptionContract/Command/Model/
FuturesOptionContract/Command/State/
FuturesOptionContract/Command/Validation/
FuturesOptionContract/Event/Actor/
FuturesOptionContract/Query/Actor/
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

Every leaf path includes all parent folders. `bin/` and `obj/` are generated; `net8.0` leaves are legacy artifacts for this .NET 10 project. The futures-option validation folder is also explicitly retained by the project file.

## Folder responsibilities

Both `FuturesContract/` and `FuturesOptionContract/` use the same vertical design:

- `Command/Actor/` hosts the event-sourced write mailbox.
- `Command/Exceptions/` contains feature-specific failures.
- `Command/Model/` contains command-side structures.
- `Command/State/` restores snapshots and persists state changes/events.
- `Command/Validation/` contains input and business-rule validation.
- `Event/Actor/` consumes published domain events.
- `Query/Actor/` performs read-side retrieval.

`Docs/` contains this record. `SecuritiesActorAssembly` at the root identifies the assembly for registration.

## Implemented actors

The futures-contract pipeline consists of `FuturesContractCommandActor`, `FuturesContractEventActor`, and `FuturesContractQueryActor`. The futures-option-contract pipeline consists of `FuturesOptionContractCommandActor`, `FuturesOptionContractEventActor`, and `FuturesOptionContractQueryActor`.

## Processing model

Command actors validate typed NATS messages, rebuild event-sourced state, apply changes, save new events, and publish them. Event actors route those events to side effects. Query actors use storage contexts to return contract read models. The shared domain actor framework supplies mailbox execution, supervision, standardized results, and exception events.

## Extension points

Extend the matching security branch rather than sharing mutable state between contract types. New security types should reproduce the command/state/event/query split and add their contracts to a Shared project.
