# Actor Implementation Conventions

**Document type:** System-wide implementation guide for all actor types  
**Status:** Evolving design convention; durable and realtime EventActor conventions documented, CommandActor and QueryActor conventions reserved for later review
**Created:** 2026-08-14  
**Last updated:** 2026-08-16
**Applies to:** Actor base classes, derived actors, actor message contracts, mapped handlers, and actor unit and integration tests

## 1. Purpose

This document is the system-wide implementation guide for IFM actors. It will define the common structure and actor-type-specific conventions for EventActors, CommandActors, QueryActors, and any additional actor roles approved later.

The initial revision documents only the EventActor convention. CommandActor and QueryActor conventions will be added after their existing implementations and recent refactoring changes have been reviewed. The absence of those sections must not be interpreted as permission to apply EventActor-specific behavior to CommandActors or QueryActors.

Across all actor types, this document will be expanded as decisions are made about base actor behavior, derived actor mapping, handler contracts, validation, logging, retries, state, persistence, queries, and testing.

## 1.1 Actor-type coverage

| Actor type | Documentation status | Notes |
| --- | --- | --- |
| EventActor | Initial convention documented | Parse mapping, receive mapping, event-family extensions, and lifecycle handlers are defined below. |
| RealtimeActor | Initial convention documented | Uses the EventActor mapping/handler structure with `ActorType.Realtime`, Core NATS delivery, a required primary actor, and optional realtime routes. |
| CommandActor | Reserved for later review | Command parsing, validation, state loading/saving, event production, projectors, and handler organization are not yet standardized by this document. |
| QueryActor | Reserved for later review | Query parsing, read-model access, response behavior, paging, and handler organization are not yet standardized by this document. |
| Additional actor roles | Not yet defined | Add only after the role and its implementation convention are explicitly approved. |

## 1.2 Current EventActor objective

The central design objective is:

> Event actors should have the same core structure. Differences between actors should primarily represent domain behavior, and that behavior should be isolated in event-family handler extension classes.

The structure must make an event actor easy to understand by inspection:

1. the derived actor validates and parses the message through a verb map;
2. the derived actor selects a handler through a receive map; and
3. an event-family extension executes the domain-specific behavior.

The EventActor sections below record the convention. They do not, by themselves, authorize or imply that every existing event actor has already been migrated.

## 2. EventActor design principles

### 2.1 Uniform actor core

Derived event actors should use a consistent implementation for:

- actor mailbox identity;
- startup and dependency initialization;
- message-subject validation;
- verb-to-parser mapping;
- event validation after deserialization;
- concrete-event-to-handler mapping;
- unsupported-event handling;
- awaiting handler completion;
- exception reporting; and
- logging that belongs to the actor execution pipeline.

These responsibilities stay in the derived actor unless and until a later design explicitly promotes them into `BaseEventActor<TActor>` or another shared framework component.

### 2.2 Domain behavior in handlers

The derived actor must not grow a large type switch containing domain operations. Domain-specific operations belong in handler extension classes, including:

- creating and sending a domain command;
- updating a cache or blackboard;
- calling a domain API;
- writing a projection;
- publishing a domain follow-up event;
- translating a domain event into another domain message; and
- applying event-family-specific failure behavior.

This division keeps the derived actor mechanically similar to other event actors while making its actual domain differences visible in small, focused handler files.

### 2.3 Explicit dispatch

Supported messages and handlers must be visible in explicit maps. Reflection-based discovery, broad catch-all handlers, and implicit service-location are not part of the current convention.

The actor's maps are its supported-event manifest. A reviewer should be able to identify every accepted verb and every executable handler without tracing a switch statement or scanning the entire domain assembly.

### 2.4 One event family per handler class

A handler extension class represents a main event and its lifecycle family. Complete and fail handlers do not receive separate extension classes.

For example:

```text
Main event:       FuturesTickTradeDataInsertedEvent
Complete event:   FuturesTickTradeDataInsertedCompleteEvent
Fail event:       FuturesTickTradeDataInsertedFailEvent
Extension class:  FuturesTickTradeDataInserted
Source file:      FuturesTickTradeDataInserted.cs
```

This co-location makes the complete lifecycle of one domain operation visible in one file.

## 3. EventActor responsibility boundaries

### 3.1 Framework base actor

`BaseEventActor<TActor>` owns framework-level actor execution behavior already provided by the actor infrastructure. The precise base contract may evolve, but domain handlers must not duplicate framework transport or mailbox mechanics.

### 3.2 Derived event actor

A derived event actor owns common functionality for that actor, including:

- its actor name and mailbox ID;
- injected dependencies shared by its handlers;
- any actor-level parameter object used to pass those dependencies;
- `_parseMap`;
- `_receiveMap`;
- the common `ParseMessage` algorithm;
- the common `ReceiveAsync` dispatch algorithm;
- actor-level startup initialization; and
- actor-level exception reporting.

The derived actor forwards the resolved event to the selected extension handler. It should not contain the handler's domain algorithm.

### 3.3 Event-family extension handler

An event-family handler owns only the behavior of its event family. At a minimum, every public handler extension receives `IEventActorContext` and the derived actor's typed logger. Actor messaging, context operations, and exception logging must remain explicit at every mapped handler boundary.

A handler may also receive dependencies supplied by the derived actor, such as:

- an event API;
- `IDbContextFactory`;
- a blackboard or cache service;
- a status-console writer;
- an actor-specific parameter object grouping shared dependencies.

The typed logger is mandatory and is passed directly to every handler overload. It must not be available only indirectly through an optional parameter object.

Dependencies should be passed explicitly. An extension handler should not resolve arbitrary services from a global container merely to avoid declaring what it uses.

## 4. EventActor parse-map convention

### 4.1 Map purpose

`_parseMap` maps an event verb to the function that deserializes the corresponding concrete event from `IActorMessage`.

A representative shape is:

```csharp
static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
{
    [SomeEvent.Verb] = message => message.AsEvent<SomeEvent>()!,
    [SomeCompleteEvent.Verb] = message => message.AsEvent<SomeCompleteEvent>()!,
    [SomeFailEvent.Verb] = message => message.AsEvent<SomeFailEvent>()!
};
```

The key is the contract's `Verb`, not the CLR type name.

### 4.2 Common parse algorithm

`ParseMessage` should perform the same ordered checks in each derived event actor:

1. reject a null actor context;
2. read the parsed `ActorSubject` from the message;
3. require `ActorType.Event`;
4. require the derived actor's exact actor name;
5. find the verb in `_parseMap`;
6. return `default` for an actor type, actor name, or verb not supported by this actor;
7. invoke the mapped deserializer;
8. reject a null deserialization result; and
9. apply common event-envelope validation, including the command-ID rule where required by the actor event contract.

Deserialization and envelope validation belong to the actor's common path. Handler extensions should receive an already resolved concrete event.

### 4.3 Parse-map completeness

Every concrete event the actor intends to receive must be registered, including main, complete, and fail events. A lifecycle event must not be silently omitted merely because its initial behavior is logging only.

## 5. EventActor receive-map convention

### 5.1 Map purpose

`_receiveMap` maps the concrete event type to a delegate that invokes the correct extension-handler overload.

The current convention uses the concrete CLR type name as the dispatch key:

```csharp
var eventName = @event.GetType().Name;
```

The exact delegate signature can include actor-specific dependencies. A representative mapping is:

```csharp
readonly Dictionary<string, Func<IEvent, IEventActorContext, ValueTask<bool>>> _receiveMap = new()
{
    [typeof(SomeEvent).Name] = (value, context) =>
        ((SomeEvent)value).ExecuteAsync(context)
};
```

If several handlers require the same services, the actor may pass an actor-specific parameter object instead of repeatedly expanding the delegate signature.

### 5.2 Common receive algorithm

`ReceiveAsync` should:

1. reject a null actor context;
2. reject a null event;
3. obtain the event's concrete type name;
4. resolve the handler from `_receiveMap`;
5. throw `InvalidOperationException` when no handler is registered;
6. invoke the mapped handler with the context and required dependencies; and
7. await the handler before completing event processing.

Handlers must not be invoked through fire-and-forget tasks. Actor completion, acknowledgement, retry, and error behavior depend on the handler's asynchronous operation being observed.

### 5.3 Unsupported events

An event that passed parsing but has no receive handler is an actor configuration error. It must produce a clear exception containing the actor identity and event subject. It must not be treated as successfully handled.

## 6. EventActor handler extension conventions

### 6.1 Naming

The extension class and source filename use the main event type with the `Event` suffix removed.

Examples:

| Main event contract | Extension class | Source file |
| --- | --- | --- |
| `FuturesEodDataInsertedEvent` | `FuturesEodDataInserted` | `FuturesEodDataInserted.cs` |
| `VixFuturesEodDataInsertedEvent` | `VixFuturesEodDataInserted` | `VixFuturesEodDataInserted.cs` |
| `FuturesTickTradeDataChangedEvent` | `FuturesTickTradeDataChanged` | `FuturesTickTradeDataChanged.cs` |
| `FuturesTickTradeDataInsertedEvent` | `FuturesTickTradeDataInserted` | `FuturesTickTradeDataInserted.cs` |

Separate classes named after `CompleteEvent` or `FailEvent` are not used when those events belong to an existing main-event family.

### 6.2 Extension method name

Handler overloads use `ExecuteAsync`. The concrete `this` parameter selects the lifecycle member:

```csharp
public static ValueTask<bool> ExecuteAsync(
    this SomeInsertedEvent @event,
    IEventActorContext context,
    SomeEventParameters parameters,
    ILogger<SomeEventActor> logger);

public static ValueTask<bool> ExecuteAsync(
    this SomeInsertedCompleteEvent @event,
    IEventActorContext context,
    SomeEventParameters parameters,
    ILogger<SomeEventActor> logger);

public static ValueTask<bool> ExecuteAsync(
    this SomeInsertedFailEvent @event,
    IEventActorContext context,
    SomeEventParameters parameters,
    ILogger<SomeEventActor> logger);
```

The return contract may be refined as the actor pattern evolves. Until then, an implementation must preserve the result semantics expected by its derived actor and must not use a successful return to conceal an exception that should trigger actor retry behavior.

### 6.3 Service identifier and log source

Every main event-family extension class owns a static `ServiceId`. Its value comes from a dedicated `LogSourceType` entry whose name is the main event name with the `Event` suffix removed.

For example:

```text
Main event:      FuturesTickTradeDataInsertedEvent
Extension class: FuturesTickTradeDataInserted
Log source:      LogSourceType.FuturesTickTradeDataInserted
ServiceId:       "FuturesTickTradeDataInserted"
```

A representative implementation is:

```csharp
static FuturesTickTradeDataInserted()
{
    ServiceId = $"{LogSourceType.FuturesTickTradeDataInserted}";
}

static string ServiceId { get; }
```

New `LogSourceType` values are appended so existing numeric enum values are not shifted. Main, complete, and fail overloads in the same extension class share the main event family's `ServiceId`.

### 6.4 Default logging behavior

Every handler receives the logger, but successful processing is quiet by default. Informational success logging will be added only where domain or operational requirements justify it.

The current default is:

- catch and log handler exceptions with `LogErrorEvent`, the family `ServiceId`, the exception, and structured event identity;
- preserve the handler's established exception propagation or return behavior after logging;
- log a received fail lifecycle event as an error because the event represents an earlier exception or failed domain operation; and
- do not emit an information log merely because a main or complete handler succeeded.

Logging must not accidentally convert a retryable exception into a successful actor acknowledgement.

### 6.5 Method documentation

Every method created in a handler extension class must have XML documentation, including private helper methods.

Documentation must describe, as applicable:

- the event being handled;
- required context and dependencies;
- commands or events published;
- state, cache, or projection changes;
- the meaning of the return value;
- exceptions deliberately propagated;
- retry implications; and
- complete or fail lifecycle behavior.

The extension class itself should also have a summary describing the event family it owns.

### 6.6 Private helpers

Event-specific conversion helpers belong beside the handler that uses them. For example, conversion from `FuturesTickTradeDataChangedEvent` to `InsertFuturesTickTradeDataCommand` belongs in `FuturesTickTradeDataChanged`, not in `TickAggregationEventActor`.

A private helper must not perform hidden asynchronous work. Sending, persistence, and other side effects should remain visible in `ExecuteAsync`.

## 7. EventActor complete and fail handlers

### 7.1 Co-location

Complete and fail handlers are overloads in the main event's extension class. This is mandatory even when their current implementation is terminal and has no additional domain work.

For example, `FuturesEodDataInserted` owns handlers for:

- `FuturesEodDataInsertedEvent`;
- `FuturesEodDataInsertedCompleteEvent`; and
- `FuturesEodDataInsertedFailEvent`.

The same rule applies to Tick Aggregation inserted-event families and other event families introduced later.

### 7.2 Default complete behavior

When no additional domain action is required, a complete handler validates its event, context, and logger and then completes without another domain action. It does not emit an informational log by default. A complete handler is terminal unless a documented workflow explicitly requires another message.

### 7.3 Default fail behavior

When no additional domain action is required, a fail handler logs the failed lifecycle event with the main event family's `ServiceId`, including:

- the concrete event name;
- entity identity;
- command or correlation identity when present;
- error code;
- error type; and
- error message.

Default failure logging uses `LogErrorEvent` and the existing typed logger conventions. A fail handler is terminal unless a documented recovery workflow explicitly requires another message.

### 7.4 No lifecycle loops

A default complete or fail handler must not:

- repeat the main database or cache operation;
- recreate the originating command;
- publish another copy of the same lifecycle event; or
- create a complete/fail processing loop.

Specialized lifecycle behavior is allowed, such as the existing VX completion workflow, but it must remain in the same main-event extension class and be explicitly tested.

### 7.5 Transactional external-data imports

An external-data import is a transactional event-family workflow, not a replayable data snapshot. Its authoritative flow is:

```text
parameter-only import command
  -> parameter-only main import event
  -> provider-neutral application API
  -> canonical domain read-model array (0..N)
  -> one bulk storage API call
  -> correlated complete or fail event
```

The command and main event carry only acquisition parameters, correlation metadata, and the duplicate policy. They do not carry provider rows. The main event handler owns acquisition through an application-layer, vendor-neutral interface, conversion to canonical domain read models, validation, and the single array-based storage call. Framework vendor implementations must not be resolved by a command actor or storage context.

The complete event is sent only after storage succeeds and may carry the canonical rows required by UI or downstream consumers. A valid zero-row provider response is a successful import: storage receives an empty array and the handler sends a complete event with zero records. An acquisition, mapping, validation, or storage exception sends a correlated fail event and must never send complete for the same attempt.

Import main events are operation markers. Command state projectors must not use their row data to rebuild durable state, and state repositories post them to the event workflow rather than treating them as completed storage projections. The complete/fail pair is the authoritative terminal result for a given command. Interactive terminal tracking is currently approved for UI consumers through the [UI Terminal-Operation Tracking and Rollout](UI-Terminal-Operation-Tracking-and-Rollout.md) convention. Legacy scheduled tasks have not been reviewed and must not be treated as compliant with that UI rollout. A failed attempt is terminal; obtaining current data requires a new import command and command ID rather than replaying the old import event.

Tests for every external import family must cover parameter propagation, provider-to-domain mapping, the 0-row and N-row cases, one bulk storage invocation, storage-before-complete ordering where observable, acquisition failure, storage failure, and MessagePack round trips for request, complete, and fail schemas.

## 8. EventActor error and retry behavior

### 8.1 Actor-level failures

Failures in common parsing, dispatch, or actor execution flow through the derived actor's normal exception path. The derived actor remains responsible for producing the framework-level event exception notification required by `BaseEventActor<TActor>`.

### 8.2 Handler-level failures

Handler behavior must preserve the domain workflow's delivery and retry guarantees.

For example, a replay-durable inserted-event projection handler may:

1. await the database write;
2. publish the typed complete event after success;
3. publish the typed fail event after an exception; and
4. rethrow the original exception so durable actor retry remains active.

Moving this logic into an extension class must not change whether an exception propagates. Refactoring dispatch is not authorization to weaken durability, acknowledgement, or retry behavior.

Transactional external-data import events follow section 7.5 instead: their typed fail event terminates that attempt, and an authorized caller starts any retry by submitting a new command. Scheduled-task retry policy remains deferred until the legacy scheduler design is reviewed.

### 8.3 Context use

At least `IEventActorContext` is passed to every extension handler. Context operations must be awaited. Cancellation support will follow the actor context contracts available at implementation time and may be expanded by later revisions of this document.

## 9. Initial application of the EventActor convention

### 9.1 Tick Aggregation

Phase 1 replaces the replay-durable `TickAggregationEventActor`,
`TickAggregationCommandActor`, command state/repository, and durable projector with
`TickAggregation/Realtime/Actor/TickAggregationRealtimeActor` and
`TickAggregation/Realtime/Projector/TickAggregationRealtimeProjector`.

`TickAggregationService` publishes normalized trade and quote changed events through Core NATS to
the primary `Realtime.TickAggregationRealtime` mailbox. The realtime actor converts each accepted
changed event to its existing typed inserted contract and gives it to the realtime projector. The
projector publishes inserted source, writes the normalized Scylla row once, and publishes the typed
complete or fail result. None of these messages enters the event-source log, JetStream process
queue, replay queue, outbox, checkpoint, or recovery worker.

Production code for this family resides under `TickAggregation/Realtime`; the obsolete
`TickAggregation/Event` implementation is removed. The shared legacy insert-command contracts may
remain only as event-store compatibility types for historical storage tests and are not registered
or consumed by a production actor.

### 9.2 Futures EOD data

Manual/API-initiated EOD commands retain the established durable Event path:

| Extension class | Event contracts handled |
| --- | --- |
| `FuturesEodDataInserted` | Futures EOD inserted, complete, and fail |
| `VixFuturesEodDataInserted` | VX futures EOD inserted, complete, and fail; the type name is retained for wire compatibility |

The live feed path is separate. `FuturesEodDataRealtimeActor` routes realtime TickAggregation trade
insertions, computes the rolling futures or VX EOD model, and passes it to
`FuturesEodDataRealtimeProjector`. The projector uses the same EOD source/complete/fail payload
schemas with `ActorType.Realtime`, writes storage once, and never replays. Its query helpers live in
`FuturesEodData/Realtime/Extensions`; they do not remain under the durable `FuturesTickData/Event`
folder.

`FuturesEodDataInsertedCompleteEvent` is the documented exception to the otherwise terminal
complete-handler default. The actor that owns the active projection path—durable for an explicit
command, realtime for a live tick—publishes the distinct best-effort
`FuturesEodDataUpdatedNotifyEvent` after storage succeeds. The notification carries the stored
`FuturesEodDataV2ReadModel`, preserves command correlation, uses a new message identity, and is sent
on `ActorType.Notify`. Publication failure does not reverse or retry the completed storage write.
No backend actor subscribes to this Notify contract.

The main-shell Market Outlook queries the currently traded ES contract for its startup snapshot and accepts live notifications only when both the symbol and full contract ID match that same ES contract. A single UI notification consumer fans out to registrations keyed by `siteId`; secondary EOD screens cannot replace or stop the main-shell registration.

`FuturesTradeSignalUpdatedCompleteEvent` is the corresponding exception for the lower Market
Outlook fields. During Phase 1, a realtime ITI completion queries the required EOD, daily RSI-14,
15-second TDI, timeframe-specific ITI, and VX close inputs, computes the retained legacy trade
signal, and passes its update event to the same ITI realtime projector. Storage and
source/complete/fail publication remain one-attempt realtime operations. Only completion publishes
`FuturesTradeSignalUpdatedNotifyEvent`; the Notify remains best effort and cannot change the stored
result. This compatibility branch remains until UI optimization replaces Futures Trade Signal.

The Futures Trade Signal UI consumer listens only on `ActorType.Notify`, fans out by `siteId`, and accepts live values only for the exact currently traded ES contract selected during startup. Backend actors do not subscribe to this notification.

### 9.3 Realtime tick consumers and durable stream ownership

`TickAggregationRealtimeProjector` is the one-attempt persistence boundary for normalized futures
and futures-option feed ticks. Downstream actors do not create tick insert commands. They consume
the realtime `FuturesTickTradeDataInsertedEvent` source publication. Its delivery is deliberately
independent of storage completion; consumers needing a confirmed row must consume the matching
complete contract instead.

The downstream routing convention is:

| Source event | FuturesEodDataRealtimeActor | FuturesOptionTickDataRealtimeActor |
| --- | --- | --- |
| `FuturesTickTradeDataInsertedEvent` with `AssetTypeId.Futures` | Process when `IsTickDataStreamActive` confirms runtime activity; compute and project rolling EOD. | Ignore. |
| `FuturesTickTradeDataInsertedEvent` with `AssetTypeId.FuturesOption` | Ignore. | Process when `IsTickDataStreamActive` confirms runtime activity; combine the exact trade with the hot quote and publish the UI Notify contract. |
| `FuturesTickQuoteDataInsertedEvent` | Do not consume downstream. | Do not consume downstream. |

Quote insertion events remain inside the realtime TickAggregation projection lifecycle. A later UI
notification contract must copy only explicitly approved fields and must not expose the pooled
quote buffer. Realtime UI and limit-order-book contracts remain a separate future design.

The downstream realtime actors register an explicit actor-supervisor route for the TickAggregation
trade-inserted verb during startup and remove it during shutdown. Core fan-out gives each actor an
independent bounded mailbox branch without republishing or dual-writing the event. The durable
`FuturesTickDataEventActor` and `FuturesOptionTickDataEventActor` retain only start/stop command
lifecycle responsibilities and do not register live-tick routes.

Streaming start handlers register a stable `TickerStreamOwner` through the asset-specific `IMarketDataApi.StartStreaming...` method and store the contract carried by the started event in actor-local state. Streaming stop handlers call the matching stop method and remove that state. `ITickerDataReader`, reader leases, lease IDs, and stream generations are not part of the application contract.

TickAggregation owns the canonical per-contract state:

- provider and domain contract identity;
- latest decimal trade snapshot;
- latest decimal quote snapshot;
- optional option Greeks aligned with the available price observation;
- active workflow owners; and
- first-owner/final-owner route activation.

Registration is idempotent for the tuple `(contract ID, workflow type, workflow ID, leg ID)`. Distinct owners may overlap on the same contract. The first owner activates transient routing once, intermediate owner removals leave it active, and the final owner removal deactivates it. The owner set, rather than a raw counter, prevents duplicate event delivery from inflating ownership.

The futures handler derives the rolling EOD workflow input from the exact realtime trade payload and
the provider-neutral contract API. The futures-option handler combines that exact realtime trade
with the latest lease-independent option hot-cache quote and optional Greeks, then publishes the
established option trade-price Notify event. Raw DataBento integer-scaled values remain limited to
ingestion and tick storage; actor-domain ticker snapshots use decimal prices.

### 9.4 Futures market-price realtime actor

`FuturesMarketPriceRealtimeActor` is the first realtime application of the EventActor structural convention. It derives from `BaseEventActor<TActor>` but owns the mailbox `Realtime.FuturesMarketPrice`, so startup selects the Core NATS producer and the Core consumer delivers its non-durable messages. `BaseEventActor<TActor>` records validation, execution, and failure metrics using the derived mailbox actor type; realtime work must not be reported as durable `Event` work.

`FuturesMarketPriceUpdatedRealtimeEvent` has the subject convention:

```text
Realtime.FuturesMarketPrice.Updated.{tickDataEntityId}
```

The contract carries a provider-neutral, decimal-based `FuturesMarketPriceSnapshot` containing contract and instrument identity plus optional latest quote and trade snapshots. Raw provider prices and pooled quote buffers are not part of this realtime actor contract.

The primary actor has exactly one parse-map entry and one receive-map entry, both for `FuturesMarketPriceUpdatedRealtimeEvent`. Its `FuturesMarketPriceUpdated` extension handler receives `IEventActorContext` and `ILogger<FuturesMarketPriceRealtimeActor>`. The primary handler intentionally performs no domain workflow; it establishes the required primary destination while routed actors own their domain response. No complete or fail contracts or handlers exist for this realtime family because Core NATS has no durable lifecycle, acknowledgement, or replay.

The primary actor does not register a route to itself. Signal realtime actors register and remove their own routes for `(Realtime, FuturesMarketPrice, Updated)` during their lifecycle. Core fan-out includes the registered primary exactly once and gives every routed realtime actor an independent mailbox branch. `Notify`, durable `Event`, `Command`, and `Query` actors cannot register as realtime route destinations.

`FuturesItiSignalRealtimeActor` is the first routed signal actor. It owns
`Realtime.FuturesItiSignal`, accepts the routed market-price update plus its own ITI and temporary
trade-signal source/complete/fail contracts, and uses the same explicit parse-map and receive-map
structure. Its market-price handler:

1. accepts only the startup-validated current ES contract;
2. lazily acquires explicit ES and VX stream registrations owned by `FuturesItiSignal/CurrentContracts/ES` and `FuturesItiSignal/CurrentContracts/VX`;
3. requires both owned workflow streams to be active;
4. obtains a fresh VX price through the hot-cache-backed market-data API; and
5. evaluates independent Daily, Weekly, and Monthly hot states; and
6. passes a `FuturesItiSignalGeneratedEvent` to its realtime projector only for each timeframe that
   has a publishable transition.

Expected timing gaps, including an inactive required stream or no fresh VX trade yet, suppress
projection without treating the realtime message as a failure. Contract-identity mismatches and
missing startup rollover state remain errors. The legacy `FuturesEodDataInsertedCompleteEvent`
trigger is no longer registered by `FuturesItiSignalEventActor`, preventing the same ITI generation
workflow from being triggered by both EOD and realtime paths.

Actor construction deliberately precedes hosted market-data startup, so stream acquisition cannot occur in `OnStartup`. The first eligible routed update after rollover and market-data initialization performs idempotent acquisition. Subsequent updates reuse the registrations without incrementing ownership. A rollover replacement acquires the new ES/VX contracts before releasing retired contracts. Actor shutdown removes the realtime route first and then releases both registrations; a stopped or replaced market-data epoch is already clean and is treated as successful release.

The ITI period contract is:

```text
Core Realtime ES update
  -> evaluate Daily hot state
  -> evaluate Weekly hot state
  -> evaluate Monthly hot state
  -> for each publishable transition only:
       realtime Generated source
       -> canonical/query and current-timeframe storage update
       -> realtime Generated complete/fail
       -> temporary legacy Futures Trade Signal realtime projection
       -> UI Notify after trade-signal completion
```

Daily, Weekly, and Monthly use default trading-day counts of 1, 5, and 20 respectively. Each period owns its own actor entity identified by contract, period, and first observed trading value date in the timeframe. Daily resets on each new feed value date. Weekly and Monthly reset on the first observed value date in a new ISO-week bucket or calendar month, so a Monday holiday naturally makes Tuesday the weekly frame start. On restart, `futures_iti_timeframe_state` restores the persisted frame start and last durable signal; bounded legacy history is only a migration fallback.

Direction changes remain immediate comparisons against `UpTrendTrigger` and `DownTrendTrigger`, and
only direction changes increment `IntrinsicTimeGroupId`. Start-of-timeframe is group zero.
Trending, extreme, and reversal changes are projected only after price moves at least 10% of the
calculated ITI threshold from the last successfully stored anchor. Ticks inside the band update
actor-owned hot observation state but create no source event, completion, or storage row.

The generated and completed contracts retain the source VX price. Their existing
`DeriveLongerPeriods` field remains at MessagePack key 12 for wire compatibility but is deprecated
and always `false` for new generated events. No generated-complete handler creates another ITI
period. In Phase 1, completion may create the retained legacy Futures Trade Signal as a second
one-attempt realtime projection for Market Outlook compatibility.

#### 9.4.1 Normalized last-price cache

`TickAggregationService` deliberately separates runtime stream ownership from hot-cache access. The application-level APIs are:

- `IsTickDataStreamActive(contractId)` checks whether the running aggregation service has at least one registered workflow owner.
- `TryGetLastTickPrice(contractId, out FuturesMarketPriceSnapshot)` reads the normalized futures/underlying hot cache.
- `TryGetLastOptionTickPrice(contractId, out OptionTickerPriceSnapshot)` reads the normalized option hot cache and sequence-aligned optional Greeks.

Neither price method checks stream ownership, activates a route, extends stream lifetime, queries a provider, or accesses durable storage. Clients that require live data check stream activity first; this is client policy and is not enforced by the cache.

Every accepted quote atomically replaces the quote portion of the contract's cached `FuturesMarketPriceSnapshot` while retaining a same-value-date trade. Every accepted trade atomically replaces the trade portion while retaining a same-value-date quote. The per-contract cache uses a single-writer versioned snapshot cell: updates allocate no cache wrapper, and readers retry if they overlap a write rather than taking the stream-owner lock. Duplicate and older quote or trade observations do not supersede the normalized snapshot. The accepted trade update then publishes `FuturesMarketPriceUpdatedRealtimeEvent` with that exact combined snapshot. Quotes do not publish this realtime event; quote-oriented UI notification contracts remain a separate future design.

The hot-cache read returns `false` when the contract is unknown or no quote or trade has yet been observed. Per-contract stream stop does not erase the cache, so an inactive contract can still return its last observation; clients requiring live data must first check `IsTickDataStreamActive` and should also inspect the snapshot timestamps. Stream activity becoming true does not imply that the first price has arrived. A value-date transition discards the previous combined snapshot before accepting data for the new date. Core publication failure increments TickAggregation publication-failure metrics but does not stop ingestion or invalidate the newer cached value; loss and recovery remain consistent with the explicitly non-durable realtime contract.

Realtime ITI processing consumes the snapshot carried by `FuturesMarketPriceUpdatedRealtimeEvent`. Timer-derived RSI, ATR, ADX, and MACD processing checks stream activity and samples `TryGetLastTickPrice` only when its time event fires, then sends a durable generation command. TDI and trade-signal workflows consume their upstream durable signal events rather than reading the raw feed or subscribing directly to market-price updates.

### 9.5 Realtime read-model projection

No-replay domain projectors reside in `Application.EventProjector/Realtime` and derive from
`BaseRealtimeProjector<TActor>`, where `TActor` is the `ActorType.Realtime` actor that owns and
starts the projector. This deliberately mirrors the descriptor-driven semantics of
`BaseEventProjector<TActor>` without sharing its durability machinery.

Each immutable `RealtimeProjectionDescriptor` binds one typed source event to one update action and
its conventional complete/fail conversions. `ProcessRealtimeEventAsync` performs exactly one
ordered attempt:

1. publish the source event through Core NATS as `ActorType.Realtime`;
2. await the descriptor's storage or cache update;
3. publish the typed complete event through Core NATS when the update succeeds; or
4. publish the typed fail event through Core NATS when source publication, update, conversion, or
   completion publication fails.

All three lifecycle contracts retain their domain actor name, verb, entity, payload, and command
correlation; only their actor type is normalized to `Realtime`. A failure-event publication that is
itself impossible is logged. Cancellation from the owning actor propagates normally. Every other
failure returns `false`, is logged, and does not prevent the next realtime observation.

The realtime projector contract exposes no event store, JetStream process/replay queue, outbox,
checkpoint, retry, recovery worker, operator retry/skip control, or startup replay. The owning
actor mailbox remains the bounded admission, ordering, and backpressure boundary. Writing to a
storage read model does not make the realtime message replay durable.

The legacy `UpdateReadModelAsync` helpers on `BaseDenormalizerActor` and
`BaseEventSourceActorRepository` are not this contract. The former is an older Event workflow; the
latter runs after event-source persistence. Neither may be used for no-replay realtime projection.

## 10. EventActor testing convention

Each migrated event actor should have tests covering the common actor core and the domain handlers.

### 10.1 Parse-map tests

- every registered verb deserializes to the correct concrete type;
- the parsed event preserves its envelope and representative payload fields;
- the wrong actor type is rejected;
- the wrong actor name is rejected;
- an unknown verb is rejected;
- corrupt and empty payloads fail predictably;
- null context is rejected; and
- common event-envelope rules are enforced.

### 10.2 Receive-map tests

- every supported concrete event resolves to its expected handler;
- null context and null event are rejected;
- an unsupported event throws a clear configuration exception; and
- handler tasks are awaited.

### 10.3 Domain-handler tests

- commands preserve required IDs, metadata, and payloads;
- projection and cache operations call the intended dependency;
- successful operations publish the correct complete event;
- failed operations publish the correct fail event;
- exceptions propagate when required for retry;
- caught handler exceptions use the event-family `ServiceId` and are logged without changing retry behavior;
- complete handlers remain quiet unless informational logging is explicitly required;
- fail handlers produce the required `LogErrorEvent`; and
- terminal handlers do not create workflow loops.

### 10.4 Validation scope

After a migration, validation should include:

1. the actor's focused unit tests;
2. the containing domain unit-test project;
3. relevant BDD and integration tests;
4. contract or serialization tests for every mapped concrete event; and
5. a full solution build.

## 11. EventActor review checklist

Use this checklist when creating or refactoring an EventActor. Future CommandActor and QueryActor sections will define their own checklists rather than inheriting this one without review.

- [ ] The actor derives from the appropriate event actor base.
- [ ] The actor has one stable actor name and mailbox identity.
- [ ] `_parseMap` lists every supported verb.
- [ ] `_receiveMap` lists every supported concrete event.
- [ ] `ParseMessage` follows the common validation order.
- [ ] `ReceiveAsync` performs validation and dispatch only.
- [ ] Domain work is in event-family extension classes.
- [ ] Extension class and filename omit the main event's `Event` suffix.
- [ ] Main, complete, and fail overloads are co-located.
- [ ] Every handler receives at least `IEventActorContext` and the typed derived-actor logger.
- [ ] Handler dependencies are explicit.
- [ ] The extension family has a `ServiceId` named from its main event without the `Event` suffix.
- [ ] `LogSourceType` contains the corresponding main event-family name.
- [ ] Every handler and private helper has XML documentation.
- [ ] Caught handler exceptions and fail lifecycle events use `LogErrorEvent` and the family `ServiceId`.
- [ ] Successful main and complete handlers do not log informational messages by default.
- [ ] Async sends, requests, and writes are awaited.
- [ ] Existing exception and retry semantics are preserved.
- [ ] Unsupported events fail clearly.
- [ ] Unit, BDD, integration, serialization, and solution-build validation is proportionate to the change.

## 12. Deferred EventActor decisions

The following details remain open for future revisions:

- whether the common parse and receive algorithms should move from derived actors into `BaseEventActor<TActor>`;
- whether maps should use `Dictionary`, `FrozenDictionary`, or generated static dispatch after performance measurement;
- whether all handlers should return `ValueTask`, `ValueTask<bool>`, or a richer common result;
- whether actor-specific parameter objects should implement a common interface;
- the standard logging event IDs and structured field names for complete and fail handlers;
- cancellation-token propagation through `IEventActorContext`;
- metrics for parse failures, unsupported events, handler duration, lifecycle completion, and lifecycle failure; and
- automated conformance tests or analyzers for map and handler conventions.

These items must not be inferred as implemented constraints until this document is updated and the corresponding implementation is approved.

## 13. Reserved actor-type sections

### 13.1 CommandActor convention

Reserved. This section will be written after reviewing representative CommandActor implementations and the changes made during recent actor and event-projector refactoring. No CommandActor mapping, handler, state, persistence, or projector convention is established here yet.

### 13.2 QueryActor convention

Reserved. This section will be written after reviewing representative QueryActor implementations and recent query and storage refactoring. No QueryActor mapping, handler, response, read-model, or paging convention is established here yet.

### 13.3 Cross-actor conventions

Reserved. Once the EventActor, CommandActor, and QueryActor sections are mature, this section will identify behavior that is genuinely common across actor types and determine whether it belongs in shared base classes, reusable helpers, or conformance tests.

## 14. Related documents

- [Actor Message Types and Delivery Conventions](Actor-Message-Types-and-Delivery-Conventions.md)
- [Actor Event Streaming and Paged Query Contracts](Actor-Event-Streaming-and-Paged-Query-Contracts.md)
- [Event Sourcing Projection Split-Brain Controls](Event-Sourcing-Projection-Split-Brain-Controls.md)
- [UI Terminal-Operation Tracking and Rollout](UI-Terminal-Operation-Tracking-and-Rollout.md)

## 15. Revision history

| Date | Revision |
| --- | --- |
| 2026-08-17 | Completed Phase 1 realtime cutover: removed the production TickAggregation command/Event chain, moved normalized tick, rolling EOD, futures-option display, ITI, and temporary Futures Trade Signal work under Realtime actors/projectors, retained durable actors only for explicit start/stop or manual command flows, and documented Event-to-Realtime folder ownership. |
| 2026-08-17 | Added the descriptor-driven `Application.EventProjector/Realtime/BaseRealtimeProjector` convention: Core NATS source/complete/fail publication, realtime-only ownership, single-attempt failure continuation, and an explicitly replay-free contract separated from legacy denormalizer and durable repository helpers. |
| 2026-08-17 | Added the ITI-to-Futures Trade Signal orchestration and durable-complete-to-Notify Market Outlook path, comprehensive semantic change detection, canonical stored sequence/time precision, exact active-ES filtering, and UI site-keyed fan-out. |
| 2026-08-17 | Added the Futures EOD complete-to-Notify Market Outlook boundary, active-ES filtering, UI site-keyed listener fan-out, and actor-owned Core publication for Notify contracts without a backend Notify actor. |
| 2026-08-16 | Scoped interactive terminal-operation tracking to the approved UI convention and explicitly deferred legacy scheduled-task retry and rollout decisions. |
| 2026-08-14 | Created the initial system-wide event actor implementation convention. Recorded derived-actor parse/receive maps, event-family extension naming, main/complete/fail co-location, default lifecycle logging, responsibility boundaries, and initial Tick Aggregation and Futures EOD application. |
| 2026-08-14 | Promoted the document to the system-wide Actor Implementation Conventions guide. Retained EventActor as the only currently defined convention and reserved CommandActor, QueryActor, and cross-actor sections for later implementation review. |
| 2026-08-14 | Required every EventActor handler to receive the typed actor logger, added a main-event `LogSourceType` and shared family `ServiceId` convention, and limited default logging to caught exceptions and fail lifecycle events. |
| 2026-08-14 | Added the TickAggregation-to-domain actor convention: durable trade-only downstream routing, actor-owned reference-counted stream registrations, decimal domain snapshots, and the prohibition on forwarding pooled quote buffers. |
| 2026-08-14 | Added the first RealtimeActor convention using `FuturesMarketPriceRealtimeActor`: required primary Core NATS destination, realtime-only route fan-out, provider-neutral decimal snapshot contract, one main handler with no complete/fail lifecycle, and actor-type-correct runtime metrics. |
| 2026-08-14 | Defined the TickAggregation normalized last-price cache: stream-independent tick/option snapshot reads, explicit stream-activity checks, allocation-free versioned snapshot reads, quote-side cache refresh, trade-triggered Core realtime publication, stale-update rejection, and timer-derived signal sampling. |
| 2026-08-14 | Added `FuturesItiSignalRealtimeActor` as the first routed signal actor, including ES/VX rollover identity checks, active-stream policy, fresh VX hot-price sampling, the realtime-to-durable command boundary, and retirement of the duplicate EOD ITI trigger. |
| 2026-08-14 | Completed the realtime ITI period and ownership contract: actor-owned lazy ES/VX registrations, Daily-only realtime entry, deterministic durable Daily-to-Weekly/Monthly derivation, recursion guards, stable derived command IDs, and source-VX preservation across generated/completed events. |
| 2026-08-16 | Replaced Daily completion fan-out with independent Daily/Weekly/Monthly realtime evaluators, 10%-of-ITI-threshold durable publication bands, timeframe-start entity identity, group-zero frame resets, holiday-safe first-observed weekly starts, and versioned restart state projection. The legacy derivation field remains wire-compatible but is no longer active. |
