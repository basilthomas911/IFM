# Market Data Feed Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.MarketData.Feed` implements ingestion and distribution actors for the market-data feed itself plus futures bars, closing prices, end-of-day data, option ticks, and futures ticks.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.MarketData.Feed/`. Each leaf includes all intermediate folders.

```text
Command/Actor/
Command/Api/
Command/Exceptions/
Command/State/
Command/Validation/
Docs/
Event/Actor/
Event/Api/
Event/Extensions/
FuturesBarData/Command/Actor/
FuturesBarData/Command/Model/
FuturesBarData/Command/State/
FuturesBarData/Command/Validation/
FuturesBarData/Event/Actor/
FuturesBarData/Event/Extensions/
FuturesBarData/Query/Actor/
FuturesClosingPrice/Command/Actor/
FuturesClosingPrice/Command/Exceptions/
FuturesClosingPrice/Command/Model/
FuturesClosingPrice/Command/State/
FuturesClosingPrice/Command/Validation/
FuturesClosingPrice/Event/Actor/
FuturesEodData/Command/Actor/
FuturesEodData/Command/Exceptions/
FuturesEodData/Command/Model/
FuturesEodData/Command/State/
FuturesEodData/Command/Validation/
FuturesEodData/Event/Actor/
FuturesEodData/Event/Extensions/
FuturesEodData/Query/Actor/
FuturesEodData/Query/Extensions/
FuturesOptionTickData/Command/Actor/
FuturesOptionTickData/Command/Model/
FuturesOptionTickData/Command/State/
FuturesOptionTickData/Command/Validation/
FuturesOptionTickData/Event/Actor/
FuturesOptionTickData/Event/Extensions/
FuturesOptionTickData/Query/Actor/
FuturesTickData/Command/Actor/
FuturesTickData/Command/Model/
FuturesTickData/Command/State/
FuturesTickData/Command/Validation/
FuturesTickData/Event/Actor/
FuturesTickData/Event/Extensions/
FuturesTickData/Query/Actor/
Query/Actor/
Query/Api/
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

`bin/` and `obj/` are generated; `net8.0` is legacy output for this .NET 10 project.

## Folder responsibilities

- Root `Command`, `Event`, and `Query` branches manage feed-wide operations. Their `Api` folders expose actor-safe adapters; `State`, `Validation`, and `Exceptions` support the write side; `Extensions` provides event-context helpers.
- Each `Futures*` branch is a vertical feed type. `Command/Actor` ingests or changes data, `Command/State` persists event-sourced state, `Model`, `Validation`, and `Exceptions` support command execution, `Event/Actor` handles emitted events, and `Query/Actor` serves reads when that feed type exposes queries.
- `Event/Extensions` and `Query/Extensions` contain feature-specific orchestration or result helpers.
- `Docs/` contains this document; `MarketDataFeedActorAssembly` marks the assembly for registration.

## Implemented actor groups

Feed-wide command, event, and query actors coordinate the subsystem. Futures Bar, EOD, Option Tick, and Tick Data implement command/event/query triplets. Futures Closing Price implements a command/event pair. Actor API classes provide feed command, event, and query operations to callers without exposing mailbox mechanics.

## Processing model

Feed commands arrive on typed NATS subjects, are validated, applied to reconstructed state, and persisted as events. Event actors handle storage, fan-out, analytics/trade integration, or follow-up messages. Query actors read materialized feed data through storage contexts. Context extensions keep multi-actor workflows close to the relevant event or query feature.

## Extension points

Add a new data stream as a vertical branch. Define shared messages first, then add the minimum command/event/query actors needed, state persistence, validation, and any context extensions. Keep feed-wide coordination in the root branches.

## Performance implementation notes

The August 2026 root-to-leaf optimization pass is recorded in
[`Domain-Actor-Optimization-Details.md`](Domain-Actor-Optimization-Details.md). It covers the actor lifecycle, command audit path, replay boundaries, per-stream timers, feed identifiers, query fan-out, hot-path logging, option pricing concurrency, and EOD calculations. Empty event actor implementations remain intentional default publication targets.

Command success and state change remain separate concepts. A successful command is not required to emit an event or mutate state. State reconstruction continues to return the best state available; missing snapshots or selected event types are normal empty results, while genuine storage exceptions continue through the actor processing pipeline.

## Deferred solution-wide cancellation TODO

Cancellation is deliberately not introduced by this project-local pass. A later, dedicated solution-wide change must propagate one coherent cancellation contract from supervisors through actor dispatch, command/event/query APIs, repositories, storage queries, broker calls, timers, and external I/O. The supervisor must be able to request graceful actor shutdown without leaving partial work or silently converting cancellation into command failure. This is a cross-cutting compatibility change and should be designed, implemented, and tested only after the root domain optimization passes are complete.
