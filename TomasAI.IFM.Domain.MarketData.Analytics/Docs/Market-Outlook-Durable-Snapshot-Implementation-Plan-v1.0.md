# Market Outlook Durable Snapshot Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Plan ID | `MODS` |
| Status | Implemented; automated and local-service verification complete |
| Date | 2026-09-02 |
| Scope | Persist each valid Market Outlook display snapshot, publish one realtime inserted event, and query the latest persisted snapshot at startup |
| Replaces | Process-local-only Market Outlook publication and persisted-component reconstruction for the UI startup query |
| Retains | The `MarketOutlookSnapshot.Model` live-composition layer, single-writer update channel, immutable hot cache, pure composer, component admission rules, and API-owned market-data lifecycle |
| Explicitly excluded | Market Outlook complete events, failure events, general history replay beyond the latest snapshot event, projection checkpoints, and UI-triggered component hydration |

## 1. Objective

Make the composed `MarketOutlookReadModel` a durable ScyllaDB read model. After the single Market
Outlook processor composes and commits a valid snapshot to its hot cache, it shall submit an
`InsertMarketOutlookSnapshotCommand`. Following the established `FuturesAtrSignalCommandState`
convention, command execution shall create and apply one `MarketOutlookSnapshotInsertedEvent` to
event-sourced command state. The state repository shall persist that snapshot event, project its
complete payload by ScyllaDB upsert, and publish the same event contract to realtime subscribers
only after the upsert succeeds.

The initial UI query shall read the latest persisted Market Outlook row at or before the requested
value date. It shall not reconstruct the display from separately persisted EOD, RSI, TDI, ITI,
EMA, Bollinger Band, VX, or trade-signal components.

## 2. Binding decisions

1. `InsertMarketOutlookSnapshotCommand` is the sole write entry point for the durable Market
   Outlook snapshot.
2. The command actor uses strict `_parseMap`, `_receiveMap`, and `_validationMap` dispatch. Missing
   command mappings are programming errors.
3. The command handler validates the snapshot, creates `MarketOutlookSnapshotInsertedEvent`, and
   applies it through `MarketOutlookSnapshotCommandState.Update(event, command)`.
4. The command returns its normal request/reply `ServiceResult<GuidResult>`. That response is not a
   domain completion event.
5. No `MarketOutlookSnapshotInsertedCompleteEvent` type shall be created or published.
6. No `MarketOutlookSnapshotInsertedFailEvent` type shall be created or published.
7. `MarketOutlookSnapshotStateRepository` loads state from the most recent
   `MarketOutlookSnapshotInsertedEvent`, saves newly applied events, and then denormalizes them.
8. `MarketOutlookSnapshotInsertedEvent` is the full replacement snapshot event used both for state
   restoration and, after successful ScyllaDB projection, realtime notification.
9. The Market Outlook realtime actor shall parse the inserted event and invoke an intentionally
   empty extension handler. Persistence has already completed; the actor has no second side effect.
10. Event and realtime actors use tolerant dispatch. An unknown verb is ignored during parsing,
    and a parsed event without a registered receive handler completes as a no-op. Commands and
    queries remain strict and exhaustive.
11. The UI subscribes to `MarketOutlookSnapshotInsertedEvent` before querying the latest row, then
    applies its latest-value channel to resolve the subscribe/query race.
12. A placeholder whose EOD identity exists but whose prices are zero is not persistable. Missing
    optional analytics may remain represented by `MissingInputs`, but the durable display snapshot
    must contain a valid market-price baseline.
13. The current `MarketOutlookUpdatedNotifyEvent` path and `loadPersistedBaseline` query option are
    removed after all consumers use the inserted event and durable query.
14. An upsert or publication failure is captured by the command response, structured logging, and
    operational metrics. It does not create a failure event.
15. The existing process-local hot cache remains the live composition workspace. ScyllaDB becomes
    the startup/display recovery source, not the per-component calculation workspace.
16. Existing composition code remains grouped under `MarketOutlookSnapshot/Model`; its in-process
    update pipeline remains nested under `MarketOutlookSnapshot/Model/Processing`. Command state,
    event projection, realtime transport, and query code remain separate feature responsibilities.

## 3. Target flow

```text
eligible component or ES trade
  -> MarketOutlookUpdateChannel
  -> MarketOutlookUpdateProcessor (single writer)
  -> update immutable inputs
  -> compose MarketOutlookReadModel
  -> replace process-local hot-cache snapshot
  -> validate durable snapshot eligibility
  -> InsertMarketOutlookSnapshotCommand
  -> strict MarketOutlookSnapshotCommandActor dispatch
  -> load MarketOutlookSnapshotCommandState from latest MarketOutlookSnapshotInsertedEvent
  -> command creates MarketOutlookSnapshotInsertedEvent
  -> state.Update(event, command)
  -> state repository saves the event to the event stream
  -> custom snapshot projector performs ScyllaDB INSERT upsert
  -> projector publishes the same MarketOutlookSnapshotInsertedEvent (Realtime/Core NATS)
       +-> MarketOutlookSnapshotRealtimeActor explicit empty handler
       +-> UI MarketOutlook event consumer
```

Failure ordering is deliberate:

```text
event-stream save succeeds, but ScyllaDB upsert fails
  -> command returns failure
  -> log and metrics record the failure
  -> no realtime MarketOutlookSnapshotInsertedEvent is published
  -> the saved snapshot event remains available for idempotent reprojection/retry
```

Therefore receipt of the realtime `MarketOutlookSnapshotInsertedEvent` means the same snapshot is
already queryable from ScyllaDB. The persisted event-stream record is not itself a realtime
notification.

### 3.1 Feature layout

```text
MarketOutlookSnapshot/
  Model/
    MarketOutlookComponentEligibility.cs
    MarketOutlookComposer.cs
    MarketOutlookPriceVolatilityClassifier.cs
    Processing/
      MarketOutlookUpdates.cs
  Command/
    InsertMarketOutlookSnapshot.cs
    Actor/
    EventProjector/
    State/
    Validation/
  Query/
  Realtime/
```

`Model/Processing` remains the live in-memory composition path. It must not contain the new
event-sourced command state or ScyllaDB projection logic.

## 4. Contracts

### 4.1 Insert command

Add `InsertMarketOutlookSnapshotCommand : ICommand` under the shared Analytics command contracts.
It contains:

- `ActorSubject`;
- `MarketOutlookEntityId`;
- non-empty `CommandId`;
- aggregate/source metadata required by the common command envelope;
- the complete `MarketOutlookReadModel`; and
- an operation-specific error code.

The subject is strict:

```text
Command.MarketOutlookSnapshotCommand.Insert.{contractId}.{valueDate}
```

Validation shall reject:

- an empty command ID;
- an empty contract ID or value date;
- a snapshot whose contract ID/value date differs from the entity ID;
- a snapshot with a missing ES symbol/identity;
- non-positive open, high, low, or close prices;
- `high < low`;
- an open or close outside the declared high/low range;
- negative volume;
- invalid timestamps; and
- malformed serialized size/content that exceeds the approved command envelope limit.

Optional or warming analytics do not invalidate an otherwise usable snapshot.

### 4.2 Inserted snapshot/realtime event

Add `MarketOutlookSnapshotInsertedEvent : IEvent<MarketOutlookEntityId>` under the shared Analytics
event contracts. It contains the normal event envelope and the complete `MarketOutlookReadModel`.
The event is the command state's full snapshot event. After it is saved and projected, the same
contract is also published through the realtime route.

Use:

```text
ActorType: Realtime
Actor:     MarketOutlook
Verb:      SnapshotInserted
Entity:    {contractId}.{valueDate}
```

Do not define corresponding complete or fail contracts.

### 4.3 Query

Retain the public operation name `GetMarketOutlookSnapshotAsync`, but redefine it as a durable
latest-row query:

```csharp
Task<ServiceResult<MarketOutlookReadModel>> GetMarketOutlookSnapshotAsync(
    string contractId,
    DateOnly valueDate);
```

Remove `loadPersistedBaseline`. The query actor remains strict and maps both its wire verb and CLR
query type. A missing row returns a typed not-found/unavailable result; it must not manufacture a
zero-valued `MarketOutlookReadModel`.

## 5. ScyllaDB projection

The repository already contains an unused `market_outlook_snapshot` table, upsert CQL, parameters,
and a latest-row CQL definition. Reuse and normalize those assets rather than introduce a second
table.

Required logical shape:

```sql
CREATE TABLE IF NOT EXISTS market_outlook_snapshot (
    contractId text,
    valueDate date,
    updatedOn timestamp,
    marketDataAsOf timestamp,
    refreshTrigger text,
    missingInputs text,
    snapshot blob,
    PRIMARY KEY ((contractId), valueDate)
) WITH CLUSTERING ORDER BY (valueDate DESC);
```

The primary key intentionally provides one current snapshot per contract/value date. ScyllaDB
`INSERT` semantics upsert that row as new live snapshots arrive. The single Market Outlook
processor and command mailbox preserve write order for a contract/value date.

The canonical latest query is:

```sql
SELECT snapshot
FROM market_outlook_snapshot
WHERE contractId = :contractId
  AND valueDate <= :valueDate
ORDER BY valueDate DESC
LIMIT 1;
```

The mapper deserializes the complete `snapshot` blob and verifies that the payload identity matches
the row identity. Duplicate EOD/trade-signal blobs are unnecessary once the complete snapshot blob
is authoritative; retain old columns only when required for a non-destructive schema transition.

## 6. Command state, repository, and persistence ordering

Follow `FuturesAtrSignalCommandState` rather than treating the command actor as a direct database
writer.

### 6.1 Command state convention

Add `MarketOutlookSnapshotCommandState` that:

- derives from `BaseEventSourceActorState<MarketOutlookSnapshotCommandState>`;
- implements `IEventSourceActorState<MarketOutlookSnapshotCommandState>`;
- overrides `ActorThreadId Id`;
- owns the current complete snapshot in private backing state;
- changes that state only in `protected override bool Apply(IEvent domainEvent)`;
- applies `MarketOutlookSnapshotInsertedEvent` as a full replacement; and
- returns `false` for unsupported events.

The command execution extension creates the inserted snapshot event and calls
`state.Update(insertedEvent, command)`. It returns the normal `ServiceResult<GuidResult>` based on
whether the event was applied. It does not write ScyllaDB or publish NATS directly.

### 6.2 State repository convention

Add `MarketOutlookSnapshotStateRepository` using the same event-source repository conventions as
the ATR implementation:

```csharp
LoadStateFromSnapshotAsync<
    MarketOutlookSnapshotCommandState,
    MarketOutlookSnapshotInsertedEvent>(command, cancellationToken)
```

No last-N range is required because every inserted event contains the complete replacement
snapshot. `SaveStateAsync` delegates to `SaveStateAndDenormalizeEventsAsync`, which saves the newly
applied event to the event stream before calling the Market Outlook snapshot projector.

### 6.3 Custom snapshot projector

The custom projector handles only `MarketOutlookSnapshotInsertedEvent` and performs these awaited
steps in order:

1. upsert the event's complete snapshot into `market_outlook_snapshot`;
2. after the upsert succeeds, publish that same event contract on the realtime/Core NATS route; and
3. return without generating a complete or failed event.

Do not use `ConventionalEventProjector` or `BaseRealtimeProjector` for this path because their
lifecycle-event conventions conflict with the approved single-event flow. If projection fails after
the event-stream save, the command reports failure and no realtime event is published. The saved
full snapshot event supports idempotent reprojection; the ScyllaDB primary key makes retry an
upsert.

The command is idempotent by entity row and command audit identity. Repeating the same accepted
snapshot produces the same table contents. The implementation shall document whether a duplicate
audited command suppresses a second realtime notification; the preferred behavior is suppression.

## 7. Realtime actor dispatch policy

Normalize `MarketOutlookSnapshotRealtimeActor` to the `_parseMap` and `_receiveMap` naming and
structure while retaining tolerant event behavior.

`_parseMap` includes known materializable contracts:

- `MarketOutlookComponentChangedRealtimeEvent`;
- `MarketOutlookEodUpdatedRealtimeEvent`;
- `FuturesMarketPriceUpdatedRealtimeEvent`; and
- `MarketOutlookSnapshotInsertedEvent`.

`_receiveMap` includes the three existing update submissions and:

```csharp
[typeof(MarketOutlookSnapshotInsertedEvent)] =
    static (@event, context) =>
        ((MarketOutlookSnapshotInsertedEvent)@event).ExecuteAsync(context)
```

The extension is deliberately empty:

```csharp
internal static ValueTask ExecuteAsync(
    this MarketOutlookSnapshotInsertedEvent @event,
    IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    => ValueTask.CompletedTask;
```

Receive dispatch uses `TryGetValue`; a missing event handler returns `ValueTask.CompletedTask`.
It must not call the strict `ResolveMappedEventHandler` fallback for an unregistered event.

Framework characterization tests shall preserve these distinct policies:

| Message kind | Unknown verb | Parsed type missing from receive map |
| --- | --- | --- |
| Event | No-op | No-op |
| Realtime event | No-op | No-op |
| Command | Error | Error |
| Query | Error | Error |

## 8. Processor integration

Replace `IMarketOutlookSnapshotPublisher`/`ActorMarketOutlookSnapshotPublisher` with a command
submission boundary such as `IMarketOutlookSnapshotCommandWriter`.

After each hot-cache write:

1. classify the composed snapshot as persistable or unavailable;
2. skip persistence/publication for zero-OHLC placeholders;
3. create a deterministic command identity from the source update identity where possible;
4. await command acceptance/processing according to the actor API contract;
5. record persistence and publication latency separately; and
6. continue processing later updates after a logged failure.

The channel remains latest-value/coalescing where already approved. Persistence must not introduce
parallel writes for the same entity that could regress the Scylla row.

## 9. Query and startup integration

### API query path

Change both direct and actor query implementations to call
`IMarketOutlookSnapshotStore.GetLatestAsync(contractId, valueDate)`. Remove hot-cache fallback and
component hydration from the public query.

### UI startup path

`IFMAppViewModel.StartMarketOutlookEventConsumer` shall:

1. create its latest-value UI channel;
2. subscribe to `MarketOutlookSnapshotInsertedEvent`;
3. query the latest persisted Market Outlook row;
4. enqueue the query result into the same latest-value channel;
5. allow a newer realtime inserted event to replace the queried value; and
6. show `Market Outlook: no persisted snapshot` if the query returns no row.

The UI must not render a synthetic all-zero EOD object. Until a valid current-session snapshot is
persisted, the latest prior value-date row remains visible with its actual value date and freshness
status.

### API startup ordering

Before starting the live market-data feed, API startup shall query the latest persisted Market
Outlook snapshot for the selected ES contract and operational value date and seed the display cache
when present. Absence is a typed unavailable/degraded display condition, not permission to persist
a placeholder. Historical analytics warmup and live component processing retain their existing
ownership.

## 10. UI event consumer migration

Update `MarketOutlookUIEventConsumer` to consume the realtime inserted contract instead of
`MarketOutlookUpdatedNotifyEvent`:

- subscribe to `ActorType.Realtime`, actor `MarketOutlook`, verb `SnapshotInserted`;
- deserialize `MarketOutlookSnapshotInsertedEvent`;
- reject invalid envelope/payload identity;
- fan out the event to registered UI site callbacks; and
- retain existing multi-site start/stop synchronization.

Update `MarketDataAnalyticsCommandService`/UI service callback types and the app ViewModel callback
to read `notification.MarketOutlook` from the inserted event.

After migration, remove `MarketOutlookUpdatedNotifyEvent` and its publisher registrations.

## 11. Implementation gates

### MODS-00 - Failing-first characterization

- Prove current startup can return a synthetic valid-identity snapshot with zero OHLC.
- Prove the current public query reconstructs components rather than reading
  `market_outlook_snapshot`.
- Prove current Market Outlook publication can occur without durable snapshot persistence.
- Characterize event no-op versus strict command/query dispatch.

Exit: all four tests fail for the intended new behavior and document the regression being fixed.

### MODS-01 - Shared contracts

- Add `InsertMarketOutlookSnapshotCommand`.
- Add `MarketOutlookSnapshotInsertedEvent`.
- Update MessagePack round-trip and stable-key tests.
- Do not add complete/fail event contracts.

Exit: contract tests pass and architecture tests confirm the forbidden lifecycle types do not
exist.

### MODS-02 - Storage projection and latest query

- Normalize the existing table migration.
- Implement typed upsert and latest-read methods on the Market Data DB interfaces/context.
- Serialize and deserialize the complete snapshot blob.
- Add identity and corruption checks.
- Add a custom inserted-event projector that upserts first and publishes the same event contract
  through realtime only after success.

Exit: Scylla integration tests prove same-day upsert replacement, prior-day lookup, cutoff-date
behavior, empty-table behavior, and exact snapshot round trip.

### MODS-03 - Event-sourced command state and strict command actor

- Add the dedicated command actor/context and DI registration.
- Implement strict parse/receive/validation maps.
- Add `MarketOutlookSnapshotCommandState` with event-only mutation through `Apply`.
- Have command execution create the snapshot event and call `state.Update(event, command)`.
- Add `MarketOutlookSnapshotStateRepository` loading from the latest
  `MarketOutlookSnapshotInsertedEvent`.
- Save state with `SaveStateAndDenormalizeEventsAsync` and denormalize through the custom projector.
- Return failures without publishing fail events.

Exit: state/repository/actor tests prove snapshot-event restoration, event-only mutation,
save/project/publish ordering, duplicate behavior, strict unknown-command handling, and zero-OHLC
rejection.

### MODS-04 - Tolerant realtime event actor

- Convert the Market Outlook actor to `_parseMap`/`_receiveMap` structure.
- Add the inserted event parser and explicit empty extension handler.
- Use tolerant receive dispatch.
- Add framework/actor tests for undefined event no-op behavior.

Exit: known events execute once, the inserted event performs no actor-side work, and unknown event
verbs/types do not call the exception path.

### MODS-05 - Processor command integration

- In `MarketOutlookSnapshot/Model/Processing`, replace direct Notify publication with
  insert-command submission.
- Add durable eligibility validation before command creation.
- Preserve single-entity ordering and processor liveness after failures.
- Extend processor metrics for command, persistence, and realtime publication outcomes.

Exit: a valid composition results in one saved snapshot event, one upsert, and one realtime
publication; an invalid placeholder produces none. A failed upsert may leave the saved snapshot
event for retry but produces neither a realtime publication nor a newer durable row.

### MODS-06 - Durable query migration

- Change the direct query extension, query actor, REST endpoint, REST client, NATS client, and UI
  query service to the two-argument durable query.
- Remove `loadPersistedBaseline` from parameters and contracts.
- Remove public query dependence on
  `MarketOutlookSnapshot.Model.Processing.MarketOutlookSnapshotHydrator` and the hot cache.

Exit: every query transport returns the same latest persisted row and never manufactures a zero
snapshot.

### MODS-07 - UI subscription migration

- Move the UI consumer from Notify Updated to Realtime SnapshotInserted.
- Preserve subscribe-before-query startup ordering.
- Route query and realtime values through the same latest-value channel.
- Preserve prior persisted data until a newer valid snapshot arrives.

Exit: presentation and system tests show non-zero persisted OHLC immediately at startup and live
replacement without duplicate rendering.

### MODS-08 - Remove superseded path and complete verification

- Remove `ActorMarketOutlookSnapshotPublisher` and `MarketOutlookUpdatedNotifyEvent`.
- Remove `Model/Processing/MarketOutlookSnapshotHydrator` if it has no remaining internal startup
  responsibility.
- Remove obsolete DI registrations and tests.
- Update Market Outlook and application-startup architecture documents.

Exit: solution build, targeted unit/integration/system tests, architecture contract tests, and a
real Scylla/API/UI startup acceptance test all pass.

## 12. Required tests

### Unit tests

- persistability validation accepts complete EOD with optional missing analytics;
- persistability validation rejects ID mismatch and every invalid OHLC invariant;
- command and query dispatch remain strict;
- event and realtime dispatch ignore unsupported verbs/types;
- inserted event extension has no observable side effect;
- serialization round trips preserve every Market Outlook field; and
- UI formatting never receives an accepted zero-price snapshot.

### Storage integration tests

- first insert creates one row;
- second insert for the same contract/date replaces that row;
- another value date creates a second clustered row;
- cutoff query returns the newest row at or before the requested date;
- no row returns typed absence;
- corrupt blob does not become a synthetic snapshot; and
- concurrent/out-of-order test proves the single-writer path cannot regress the durable row.

### Actor and pipeline integration tests

- command state restores from the latest `MarketOutlookSnapshotInsertedEvent`;
- only `Apply` mutates the restored snapshot and unsupported state events return `false`;
- event-stream save completes before ScyllaDB projection;
- upsert completes before inserted-event publication;
- upsert failure publishes no realtime event and leaves an idempotently reprojectable snapshot event;
- no complete/fail event subjects are emitted;
- command duplicate handling is deterministic;
- an unknown realtime event is a no-op;
- a missing command/query mapping fails visibly; and
- processor continues after one command failure.

### UI/system tests

- startup with the feed stopped shows the latest persisted Market Outlook;
- startup with the feed already running resolves the subscribe/query race to the newest snapshot;
- previous value-date data remains visible until a valid current snapshot is inserted;
- an empty table shows an unavailable status instead of zeros; and
- live inserted events update OHLC and analytics without a Notify event.

### Acceptance test

Against real ScyllaDB, API Server, NATS, and UI:

1. persist a known non-zero snapshot;
2. restart API and UI with the live feed initially stopped;
3. verify the known snapshot is displayed;
4. start the live feed;
5. verify a valid current snapshot upserts the same-day row;
6. restart the UI and verify it reads that row immediately; and
7. inspect NATS traffic to confirm no Market Outlook complete/fail events were emitted.

## 13. Principal code areas

| Project | Planned change |
| --- | --- |
| `TomasAI.IFM.Domain.MarketData.Analytics.Shared` | Insert command, inserted realtime event, query contract cleanup |
| `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Model` | Retained composer, eligibility, volatility classifier, and nested live update processing |
| `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Command` | Insert execution, strict command actor/context, event-sourced state/repository, validation, custom projector |
| `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Realtime` | Tolerant event dispatch and empty inserted-event extension |
| `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Query` | Strict latest durable snapshot query |
| `TomasAI.IFM.Application.Storage` | Snapshot schema normalization, upsert/latest CQL, parameters, mapping, read/write interfaces |
| `TomasAI.IFM.Application.Api.Server` | Actor/context/service registration and durable latest-snapshot query wiring; no component-based startup cache seeding |
| `TomasAI.IFM.Application.Api.Client` | Durable REST query signature |
| `TomasAI.IFM.Application.Api.Nats.Client` | Durable NATS query and insert-command APIs |
| `TomasAI.IFM.UI.EventConsumer` | Realtime inserted-event subscription |
| `TomasAI.IFM.UI.Net.Services` | Updated query/event service contracts |
| `TomasAI.IFM.UI.Net.ViewModels` | Subscribe-before-query startup and no-zero presentation behavior |
| Unit/integration/system test projects | Contract, storage, dispatch, ordering, startup, and UI coverage |

## 14. Definition of done

The change is complete only when all of the following are true:

1. A valid composed Market Outlook is stored through `InsertMarketOutlookSnapshotCommand`.
2. ScyllaDB contains one upserted row per contract/value date.
3. `MarketOutlookSnapshotCommandState` is restored from the latest
   `MarketOutlookSnapshotInsertedEvent` and is mutated only by applying events.
4. A newly applied `MarketOutlookSnapshotInsertedEvent` is saved before its ScyllaDB projection.
5. The same event contract is published through realtime only after its ScyllaDB upsert succeeds.
6. No Market Outlook complete or fail event exists or is emitted.
7. The inserted event's actor extension is intentionally empty.
8. Undefined event/realtime messages are guaranteed no-ops.
9. Undefined commands and queries remain errors.
10. The latest query reads exactly one row from `market_outlook_snapshot` at or before the requested
   value date.
11. UI startup displays the last durable non-zero snapshot before applying live updates.
12. A zero-OHLC placeholder can neither overwrite the table nor appear as a valid UI snapshot.
13. The old Notify publication and component-hydration query path are removed.
14. Targeted tests and the real startup acceptance scenario pass.

## 15. Completion record

All implementation gates `MODS-00` through `MODS-08` were processed on 2026-09-02. The final
implementation uses the insert command as its only durable entry point, restores command state
from `MarketOutlookSnapshotInsertedEvent`, awaits ScyllaDB before realtime publication, keeps the
inserted-event actor extension empty, and retains strict command/query versus tolerant
event/realtime dispatch.

Verification completed:

- full solution build: zero warnings and zero errors;
- Analytics unit suite: 1,020 passed;
- Shared unit suite: 180 passed;
- Fund unit suite: 254 passed;
- Market Data Feed unit suite: 503 passed;
- Market Outlook BDD suite: 16 passed;
- Analytics Market Outlook pipeline integration: 2 passed;
- Market Outlook presentation/architecture selection: 28 passed;
- Market Outlook UI system selection: 2 passed;
- real local ScyllaDB upsert/latest/corruption integration selection: 2 passed; and
- development API host with local ScyllaDB, PostgreSQL, Redis, and NATS: the new command, query,
  and realtime actors started, synthetic feed updates created durable insert commands, command
  state restored from the inserted snapshot event, and the event stream recorded the snapshots.

The external Databento-live variant could not complete in the restricted development environment
because the vendor endpoint was unreachable. The deterministic synthetic-feed host was used for
the local-service acceptance run; Databento availability is an environment/deployment check and
does not change the completed durable snapshot design.
