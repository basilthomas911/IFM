# Market Outlook Snapshot Command Architecture — MOS-0 Baseline

> Historical baseline only. The command actor, persisted working state, projector, revision DTO
> and active database route described here were superseded and removed by the implemented
> `Market-Outlook-Hot-Cache-Refactor-Implementation-Plan-v1.0`.

## Purpose

This baseline defines the incremental migration of Market Outlook accumulation from
`MarketOutlookSnapshotRealtimeActor` to an event-sourced command aggregate. It covers the
compatibility boundaries and qualification criteria for gates MOS-0 through MOS-9.

## MOS-0 baseline behavior

- RSI, TDI, ITI, and VX component updates are delivered to the Market Outlook realtime mailbox.
- The realtime actor accumulates those inputs in an in-memory dictionary keyed by
  `MarketOutlookEntityId(contractId, valueDate)`.
- An ES EOD update is the publication boundary.
- At that boundary, the actor creates `MarketOutlookSnapshotReadModel`, writes it directly to
  `market_outlook_snapshot`, and publishes `MarketOutlookUpdatedNotifyEvent`.
- The realtime actor can hydrate inputs from existing signal read models, but its working
  dictionary is not event-sourced.

## Target ownership

- `MarketOutlookSnapshotCommandActor` is the sole owner of Market Outlook working state.
- PostgreSQL's transactional event log is the authoritative state store.
- `MarketOutlookSnapshotRealtimeActor` becomes a stateless bridge between realtime events,
  commands, and projection-complete notifications.
- A command-owned event projector updates ScyllaDB.
- The `market_outlook_working_state` projection exposes the current accumulation state.
- The existing `market_outlook_snapshot` projection remains the finalized UI/query model.

## Compatibility boundaries

- Preserve `MarketOutlookEntityId` formatting: `{contractId}.{valueDate:yyyyMMdd}`.
- Preserve the existing `MarketOutlook` realtime mailbox name during the incremental migration.
- Preserve `GetMarketOutlookSnapshotQuery` and `MarketOutlookSnapshotReadModel` behavior.
- Preserve `MarketOutlookUpdatedNotifyEvent` as the UI refresh notification.
- Do not add a business Event actor. Durable projection is owned by the command projector;
  realtime completion handling remains stateless.

## Idempotency and ordering

- Every source component and EOD event is represented by a stable source event ID.
- Commands carry source sequence and timestamp metadata for deterministic ordering.
- The aggregate records a fixed last-accepted watermark for each component stream in its
  event-sourced checkpoint. This prevents idempotency metadata from growing throughout the day.
- Duplicate deliveries are acknowledged without creating another domain event or revision.
- A published snapshot event is a full aggregate checkpoint. Events after that checkpoint can
  reconstruct the current state without relying on process memory or ScyllaDB.

## Gate boundaries

- MOS-0: baseline documentation and compatibility boundaries.
- MOS-1: command, domain-event, complete-event, fail-event, and working-state contracts.
- MOS-2: immutable replayable command state.
- MOS-3: closed-generic command context, extensions, and event-source repository.
- MOS-4: executable command actor and command handlers.
- MOS-5: ScyllaDB working-state schema and storage APIs.
- MOS-6: conventional command event projector.
- MOS-7: stateless realtime bridge and routing.
- MOS-8: DI/startup and query compatibility qualification.
- MOS-9: full unit, BDD, integration, replay, and cleanup qualification.

## MOS-0–MOS-3 exit criteria

- Shared contracts serialize and retain stable entity/source identities.
- Replaying component and snapshot domain events reconstructs the same working state.
- State exposes no public mutation surface; only domain-event application changes it.
- The command context is closed over `MarketOutlookSnapshotCommandActor`.
- The repository loads from the latest published snapshot event, saves transactionally, and
  delegates denormalization to the command projector.
- During MOS-0 through MOS-3, the actor, context, and repository implementations remained
  abstract so open-generic DI could not activate an incomplete pipeline. MOS-4 through MOS-7
  activated them together as one qualified runtime unit.
- Existing Market Outlook runtime behavior remained unchanged until MOS-4 through MOS-7.

## MOS-4–MOS-9 implemented architecture

- `MarketOutlookSnapshotCommandActor` handles component observation and EOD publication
  commands. It is the only actor that owns and mutates the immutable working checkpoint.
- Component acceptance is guarded by bounded, per-component sequence/timestamp watermarks.
  Duplicate and stale inputs succeed without appending an event or advancing the revision.
- The event-sourced repository persists full checkpoint events in PostgreSQL and reconstructs
  state through replay without reading ScyllaDB.
- `MarketOutlookSnapshotEventProjector` durably projects every accepted checkpoint to
  `market_outlook_working_state`. Published checkpoints also update
  `market_outlook_snapshot`.
- Projection completion is explicitly published to the `MarketOutlook` realtime mailbox only
  after the ScyllaDB mutation succeeds. No Market Outlook business Event actor is introduced.
- `MarketOutlookSnapshotRealtimeActor` is stateless. It translates component/EOD realtime
  inputs into commands and translates projection-complete events into the existing
  `MarketOutlookUpdatedNotifyEvent` UI notification.
- EOD is the calculation base for fields that mathematically require EOD, but it is no longer a
  publication barrier for independent components. Every accepted RSI, TDI, ITI, VX, or EOD
  component advances the persisted snapshot and frontend notification.
- Composite admission uses OR semantics. An invalid or unavailable sibling is removed without
  suppressing valid siblings in the same message. A formula may still require its own documented
  operands; OR admission does not authorize substituting unrelated indicators.
- `MarketOutlookSnapshotReadModel` carries the independently available component values and an
  explicit `MissingInputs` description. The UI renders missing fields as `N/A` and never falls
  back to a prior trade-signal composite.
- Existing open-generic command-context registration activates the closed
  `ICommandActorContext<MarketOutlookSnapshotCommandActor>` context; no concrete context
  registration is required.
- The existing `GetMarketOutlookSnapshotQuery` contract and client API continue reading the
  finalized `market_outlook_snapshot` projection without a compatibility change.

## MOS-4–MOS-9 exit criteria

- Command-handler tests cover accepted input, duplicate/stale idempotency, combined ITI/VX
  watermarks, publication, and immutable replay state.
- Realtime actor tests prove that handlers only bridge messages and retain no actor-owned
  accumulation dictionary or direct storage mutation.
- Storage integration tests round-trip the immutable working checkpoint through the ScyllaDB
  blob projection.
- The end-to-end integration test sends real Observe and Publish commands, verifies the
  PostgreSQL event stream, both ScyllaDB projections, the existing query API, and the final UI
  notification.
- Domain unit, BDD, integration, storage integration, and API-host build suites provide the
  final regression qualification recorded with the MOS-9 implementation change.

## Live OR-composite qualification addendum (2026-08-31)

- All 127 non-empty availability combinations across EOD, RSI, TDI, ITI direction, ITI extreme,
  ITI reversal, and VX are executable verification cases.
- EOD alone can produce an explicitly partial `FuturesTradeSignalV2ReadModel`; RSI, TDI, ITI,
  and VX are optional enrichments.
- A component-only snapshot is valid and queryable before EOD. It does not claim that an EOD-based
  calculation exists.
- The working-state blob is the preferred query projection because it retains the expanded
  component contract. The legacy snapshot columns remain a rollout fallback.

## Daily EMA/Bollinger authority addendum (2026-08-31)

- Market Outlook now carries typed `FuturesEmaSignalReadModel` and
  `FuturesBbSignalReadModel` components. Their MessagePack keys are append-only in
  commands, working state, events, and snapshots.
- The UI's `50 EMA` and `200 EMA` fields come from typed EMA50/EMA200. The four
  Bollinger fields come from BB20 population standard deviation, upper band,
  EMA20 centerline, and lower band. The six legacy EOD/trade-composite values are
  no longer read by the snapshot UI path.
- Daily Analytics are admitted independently under OR semantics. One missing or
  delayed family never suppresses the other family or any intraday component.
- A prior completed Daily observation can be reconciled into the active value-date
  stream while its original market-data timestamp and value date remain intact.
  This supports closed-market startup without pretending that the value is an
  intraday calculation.
- After historical replay, one explicit active-value-date Observe command updates
  an existing EOD snapshot. Market Outlook therefore does not depend on a future
  live EOD tick to display warmed values.
