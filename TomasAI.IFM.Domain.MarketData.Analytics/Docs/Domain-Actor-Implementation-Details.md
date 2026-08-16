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

The query is one statement, so a concurrent append is either visible to that statement's PostgreSQL snapshot or is recovered on the next load; it cannot be partially observed between separate end-position and range queries. RSI intraday commands select `FuturesRsiSignalGeneratedEvent` after `FuturesRsiSignalStartedEvent`. Persisted history remains immutable and unbounded.

### Typed last-N recovery without a separate snapshot event

The daily RSI/MACD/ADX/ATR streams have period-length windows but no lifecycle snapshot event. They use the optimized `LoadStateAsync<TState, TEvent>(command, periodLength)` path. Its PostgreSQL query filters by stream and exact event type, scans descending through the existing key, applies `LIMIT` in the database, and restores ascending event-version order before replay. Nonpositive ranges and missing event types return empty state without a code-generated exception.

The repository mapping is:

| Feed | Intraday range event | Daily range event |
|---|---|---|
| RSI | `FuturesRsiSignalGeneratedEvent` after `FuturesRsiSignalStartedEvent` | `FuturesRsiDailySignalGeneratedEvent` |
| MACD | `FuturesMacdSignalGeneratedEvent` after `FuturesMacdSignalStartedEvent` | `FuturesMacdDailySignalGeneratedEvent` |
| ADX | `FuturesAdxSignalGeneratedEvent` after `FuturesAdxSignalStartedEvent` | `FuturesAdxDailySignalGeneratedEvent` |
| ATR | `FuturesAtrSignalGeneratedEvent` after `FuturesAtrSignalStartedEvent` | `FuturesAtrDailySignalGeneratedEvent` |

Daily MACD is now accepted by its command parse/receive maps and replay state. Daily MACD, ADX, and ATR generated events are denormalized through their typed read-model paths, and all four daily completion variants are accepted by their same-domain event actors. The ATR and RSI daily completion handlers intentionally do no work after parsing.

### Realtime ITI timeframe lifecycle

The ITI realtime actor evaluates Daily, Weekly, and Monthly state independently for every accepted ES trade. Each in-memory stream is hydrated once from `futures_iti_timeframe_state`; canonical ITI history is a bounded compatibility fallback. A stream starts at group zero on the first actually observed trading value date in its day, ISO-week bucket, or calendar month. Its durable entity ID uses that timeframe-start value date while the signal retains the current observation value date.

The shared compute model is used by both the realtime pre-filter and durable command actor. Direction trigger crossings are immediate. Trending, extreme, and reversal publications require movement of 10% of the calculated ITI threshold from the durable band anchor. Inside-band observations remain hot-only. The legacy Daily-completion fan-out has been removed; Daily, Weekly, and Monthly completions cannot create other ITI commands.

### Intraday start/stop lifecycle

MACD, ADX, and ATR now implement the same event-driven lifecycle as RSI for intraday entity IDs only. Public HTTP and NATS command APIs publish typed Start/Stop commands; command actors persist Started/Stopped domain events; repositories publish those events to the same-domain event actor; and event actors register or remove the entity's recurring generation loop.

The shared timer registry guarantees one loop per entity, makes duplicate Start events idempotent, serializes callbacks within a loop, waits for an in-flight callback during Stop, and drains all loops during actor shutdown. The registry cancellation source is local lifecycle control. Daily signal entity types have no Start/Stop contracts or timer registrations: they remain one-shot commands intended to be scheduled once after market close.

### Graceful cancellation status

All seven Analytics query actors now receive the supervisor worker token and propagate it through their query handlers to 17 MarketData read-model operations. The storage overloads pass the token through PostgreSQL command execution, scalar reads, and result-set materialization. Cancellation is checked before an actor reply, so a canceled request cannot publish a stale response after its read completes.

The composite ITI signal-data query starts its independent trend-direction, trend-extreme, and trend-reversal reads together and awaits them as one cancellable group. This removes avoidable serial database latency without changing result construction or event ordering.

The direct in-process `IActorMarketDataAnalyticsQueryApi` now exposes cancellation-aware overloads for all 21 operations. Tokens flow through the API adapter and every underlying Analytics storage read, including symbol-to-contract lookup and the multi-step ITI MDI queries. `OperationCanceledException` remains cancellation and is never converted to a failed service result.

The token-aware RSI trend-direction storage path starts its independent up/down count queries together. ITI signal-data and up/down MDI composition preserve their existing concurrent fan-out while passing the same token to every branch. No-token overloads remain compatibility entry points for existing callers and tests. The local indicator timer cancellation source continues to own only recurring-loop lifecycle and drain behavior.

## Preserved actor semantics

- Command success and state mutation remain separate concepts. An operation may succeed when `Update` returns `false`; that value reports whether state changed.
- Empty event actors remain valid same-domain sinks for command-produced events.
- Actor parse dispatch remains dictionary/delegate based in this domain. No switch or generated jump table was introduced without production evidence that the extra maintenance cost is justified.
