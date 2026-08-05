# Market Data Analytics Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.MarketData.Analytics` implements event-sourced command actors, event actors, and query actors for futures ADX, ATR, intrinsic-time (ITI), MACD, RSI, TDI, and composite trade signals. It also exposes actor-facing analytics command and query APIs.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.MarketData.Analytics/`. Each leaf includes all intermediate parent folders.

```text
Command/Api/
Docs/
FuturesAdxSignal/Command/Actor/
FuturesAdxSignal/Command/Model/
FuturesAdxSignal/Command/State/
FuturesAdxSignal/Command/Validation/
FuturesAdxSignal/Event/Actor/
FuturesAdxSignal/Event/Extensions/
FuturesAdxSignal/Query/Actor/
FuturesAtrSignal/Command/Actor/
FuturesAtrSignal/Command/Model/
FuturesAtrSignal/Command/State/
FuturesAtrSignal/Event/Actor/
FuturesAtrSignal/Query/Actor/
FuturesItiSignal/Command/Actor/
FuturesItiSignal/Command/Model/
FuturesItiSignal/Command/State/
FuturesItiSignal/Command/Validation/
FuturesItiSignal/Event/Actor/
FuturesItiSignal/Event/Extensions/
FuturesItiSignal/Query/Actor/
FuturesMacdSignal/Command/Actor/
FuturesMacdSignal/Command/Model/
FuturesMacdSignal/Command/State/
FuturesMacdSignal/Command/Validation/
FuturesMacdSignal/Event/Actor/
FuturesMacdSignal/Query/Actor/
FuturesRsiSignal/Command/Actor/
FuturesRsiSignal/Command/Model/
FuturesRsiSignal/Command/State/
FuturesRsiSignal/Command/Validation/
FuturesRsiSignal/Event/Actor/
FuturesRsiSignal/Event/Extensions/
FuturesRsiSignal/Event/Model/
FuturesRsiSignal/Query/Actor/
FuturesTdiSignal/Command/Actor/
FuturesTdiSignal/Command/Model/
FuturesTdiSignal/Command/State/
FuturesTdiSignal/Command/Validation/
FuturesTdiSignal/Event/Actor/
FuturesTdiSignal/Query/Actor/
FuturesTradeSignal/Command/Actor/
FuturesTradeSignal/Command/Model/
FuturesTradeSignal/Command/State/
FuturesTradeSignal/Command/Validation/
FuturesTradeSignal/Event/Actor/
FuturesTradeSignal/Query/Actor/
MarketEvaluationSnapshot/
Query/Api/
VixVolatility/Command/
VixVolatility/Event/
VixVolatility/Query/
bin/Debug/net10.0/runtimes/win-x64/native/
bin/Release/net10.0/runtimes/win-x64/native/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
obj/Release/net10.0/ref/
obj/Release/net10.0/refint/
```

`bin/` and `obj/` are generated. `MarketEvaluationSnapshot/` and the three `VixVolatility/` leaves are current scaffolds retained by the project definition. `FuturesAtrSignal/Command/GenerateFuturesAtrDailySignal.cs` is included as a non-compiled project item.

## Folder responsibilities

- `Command/Api/` publishes analytics commands from an actor context and provides its factory.
- `Query/Api/` performs storage-backed analytics reads.
- Each `Futures*Signal/Command/Actor/` owns the signal's command mailbox.
- Each `Command/State/` contains event-sourced state and repository behavior; `Command/Model/` and `Command/Validation/` hold write-side structures and rules when the feature needs them.
- Each `Event/Actor/` consumes that signal's events. `Event/Extensions/` holds actor-context helpers and `Event/Model/` holds event-processing data where present.
- Each `Query/Actor/` exposes the signal's read side.
- `MarketEvaluationSnapshot/` and `VixVolatility/` reserve future feature structure.
- `Docs/` contains this document; the root assembly marker supports scanning and registration.

## Implemented actor groups

ADX, ATR, ITI, MACD, RSI, TDI, and Trade Signal each have command, event, and query actors. Command actors inherit the shared event-source command base, event actors inherit the supervised event base, and query actors inherit the query base. `ActorMarketDataAnalyticsCommandApi` and `ActorMarketDataAnalyticsQueryApi` are the public adapters used by other domain services.

## Processing model

Incoming NATS subjects select an actor mailbox and verb. Command actors deserialize and validate the contract, load state from snapshot/event storage, execute typed signal-generation logic, persist emitted events, and publish them for denormalization or downstream coordination. Event actors dispatch published signal events and may invoke other actor APIs through context extensions. Query actors retrieve the persisted analytics read models.

## Extension points

New indicators should receive an isolated feature root with command, event, and query branches. Keep indicator calculations/models local, expose cross-domain contracts from a Shared project, and register every new verb in parse, validation, and receive maps.

## State-history and recovery contract

Analytics event streams and their domain history are intentionally unbounded. Indicator actors must not discard historical events as a memory optimization. Normal recovery starts from the latest snapshot and replays the events required after that snapshot.

### Snapshot plus last typed range recovery

`LoadStateFromSnapshotLastNRangeAsync<TState, TSnapshotEvent, TRangeEvent>` now provides bounded recovery without changing unbounded event retention. A single PostgreSQL statement:

1. locates the latest event of `TSnapshotEvent` in the stream;
2. scans backward through the existing `(EventStreamId, EventNameId, EventVersion)` key for only `TRangeEvent` rows after that snapshot;
3. limits the descending scan to `NRange`; and
4. returns the snapshot and selected range events in ascending event-version order for replay.

`NRange <= 0` returns the snapshot only. No snapshot returns an empty state, including when other event types exist. A snapshot with no matching range events returns snapshot state. Missing historical data therefore remains a valid empty or partial reconstruction; only an actual database, mapping, or replay failure propagates through the existing storage/actor exception path.

The query is one statement, so a concurrent append is either visible to that statement's PostgreSQL snapshot or is recovered on the next load; it cannot be partially observed between separate end-position and range queries. RSI intraday commands select `FuturesRsiSignalGeneratedEvent`; daily commands select `FuturesRsiDailySignalGeneratedEvent`. Persisted history remains immutable and unbounded.

### TODO: solution-wide graceful cancellation

Cancellation must eventually flow from the supervisor through actor requests, repositories, and the lowest storage operations so shutdown can stop actors gracefully. Do not introduce partial cancellation contracts in one domain; implement and test the propagation as a dedicated solution-wide change after root-domain optimization is complete. The local RSI timer uses an internal cancellation source only to drain its own lifecycle during stop/shutdown and does not change that cross-solution contract.

## Preserved actor semantics

- Command success and state mutation remain separate concepts. An operation may succeed when `Update` returns `false`; that value reports whether state changed.
- Empty event actors remain valid same-domain sinks for command-produced events.
- Actor parse dispatch remains dictionary/delegate based in this domain. No switch or generated jump table was introduced without production evidence that the extra maintenance cost is justified.
