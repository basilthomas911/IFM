# Market Data Feed Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.MarketData.Feed` implements ingestion and distribution actors for the market-data feed itself plus futures bars, closing prices, end-of-day data, option quotes, option ticks, and futures ticks.

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
FuturesOptionQuoteData/Command/Actor/
FuturesOptionQuoteData/Command/Exceptions/
FuturesOptionQuoteData/Command/Model/
FuturesOptionQuoteData/Command/State/
FuturesOptionQuoteData/Command/Validation/
FuturesOptionQuoteData/Event/Actor/
FuturesOptionQuoteData/Event/Extensions/
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

Feed-wide command, event, and query actors coordinate the subsystem. Futures Bar, EOD, Option Tick, and Tick Data implement command/event/query triplets. Futures Closing Price and Futures Option Quote Data implement command/event pairs. Actor API classes provide feed command, event, and query operations to callers without exposing mailbox mechanics.

## Processing model

Feed commands arrive on typed NATS subjects, are validated, applied to reconstructed state, and persisted as events. Event actors handle storage, fan-out, analytics/trade integration, or follow-up messages. Query actors read materialized feed data through storage contexts. Context extensions keep multi-actor workflows close to the relevant event or query feature.

## Extension points

Add a new data stream as a vertical branch. Define shared messages first, then add the minimum command/event/query actors needed, state persistence, validation, and any context extensions. Keep feed-wide coordination in the root branches.
