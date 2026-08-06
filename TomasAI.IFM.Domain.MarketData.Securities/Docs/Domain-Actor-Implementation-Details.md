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

## Performance implementation notes

The August 2026 root-to-leaf optimization pass is recorded in
[`Domain-Actor-Optimization-Details.md`](Domain-Actor-Optimization-Details.md). It covers non-blocking command auditing, typed bulk snapshot recovery, actor-state memory, option-contract bulk reads and writes, bounded enrichment concurrency, validator reuse, contract-ID allocation, and async-path simplification.

Event histories remain immutable and unbounded. A command may succeed without changing state, and the empty event actors remain intentional default publication targets. State reconstruction continues to return the best available, possibly empty, state when a requested snapshot or event type is absent; genuine storage exceptions continue through the actor processing pipeline.

## Graceful cancellation status

The futures-contract and futures-option query actors propagate their supervisor worker token through all seven handlers and the complete Securities read interface. Token-aware storage paths cover direct reads, projection-state fences, bulk ID reads, and streamed projection selection. Canceled actor work publishes no reply.

Projection fallback has an explicit durability boundary. Cancellation is honored while deciding whether a projection is current and while beginning its repair journal. Once the fallback starts mutating the durable projection, the delete, canonical scan, repopulation, verification, and journal completion run with `CancellationToken.None`. This prevents graceful shutdown from reporting cancellation while leaving a partly rebuilt projection or an ambiguous repair outcome.

The direct in-process `IActorMarketDataQueryApi` also has cancellation-aware overloads for all 18 operations. Its aggregate Iron Condor query starts the Securities and MarketData reads concurrently with the same token. `OperationCanceledException` is rethrown rather than converted to a normal `ServiceFailed` result. Existing no-token entry points remain compatible.
