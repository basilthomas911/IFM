# Trade Structure, Strategy, Position, and Monitor Design

> **Strategy catalog direction (2026-09-06):** Reusable strategy logic, payoff structures, variants and management-policy references are planned in ConfigurationDb. The durable executed trade/position/monitor facts described here remain Trade-domain responsibilities. Future trades must retain exact selected strategy/structure/variant/deployment versions rather than infer behavior from a legacy family enum or display label. New parameter metadata does not implement a new position, risk or exit evaluator. TradeSelection implementation is on hold. See [ConfigurationDb strategy catalog design](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

Date: 2026-08-17  
Status: Initial authoritative design baseline  
Scope: Trade-domain persistence, live position updates, strategy-specific monitoring, and Trade Blotter boundaries

Portfolio/Fund prerequisite: [Portfolio-Fund-High-Level-Design-v0.1.md](../../Documents/system/Portfolio-Fund-High-Level-Design-v0.1.md)

## Purpose

This document defines the initial target architecture for separating durable trade facts from live strategy-position calculations and immutable monitoring history.

The Portfolio/Fund HLD is authoritative for planned composition. `FundOrder` and `FundOrderTrade` are Portfolio/Fund composition records; they are not the durable broker order or filled trade described here. This document's TradeDb authority begins at the future OrderExecution boundary. OrderExecution, fills, and live-position redesign are deferred until TradeSelection, OrderComposition, and RiskManagement produce an accepted candidate.

It records the design decisions reached while reviewing the current option-trade and live option-tick flow. It intentionally stops short of specifying the complete Trade Strategy and Trade Monitor workflow pipelines. Those pipelines will extend this document after their commands, events, stages, and execution boundaries have been designed.

The primary goals are:

- Keep authoritative trade and execution facts durable.
- Remove realtime position marking from the durable trade aggregate.
- Keep position calculations specific to the actual trade strategy.
- Persist the current calculated position independently from its immutable monitoring history.
- Ensure live processing continues without a running UI.
- Turn the Trade Blotter into a view and user-intent surface rather than a workflow orchestrator.

## Decision summary

| Decision | Authoritative direction |
|---|---|
| Durable execution facts | Remain in `TradeDbContext` and durable trade actors after future OrderExecution begins. |
| Current strategy position | Stored through `TradePositionDbContext`. |
| Position persistence shape | Strategy-specific and allowed to use multiple tables for one logical position. |
| Position update actors | Realtime and strategy-specific. |
| Position update events | Strategy-specific; no flattened strategy-neutral domain event is required. |
| Monitor persistence | Stored through `TradeMonitorDbContext`. |
| Monitor table shape | One append-only table containing immutable, denormalized strategy-position snapshots. |
| Realtime delivery | Core NATS using `ActorType.Realtime`; no JetStream replay, durable projector queue, or event-source replay. |
| Realtime terminal semantics | Publish source, complete, and fail events. Failure is observed and logged; the next market update supersedes it. |
| UI delivery | External `ActorType.Notify` events published after the relevant backend update. |
| Trade Blotter | A view over backend trade, position, and monitor projections; it must not calculate or persist live positions. |
| Existing `TradePlan` implementation | Legacy candidate to be revisited after the strategy and monitor workflow pipelines are defined. |

## Core domain separation

The target design separates four related concepts that are currently coupled.

### Trade structure

The trade structure represents authoritative facts about what was created and executed. It is durable and must survive restarts and replay.

Examples include:

- Trade and order identity.
- Selected strategy identity.
- Instrument and option-contract legs.
- Leg quantities and sides.
- Orders, broker acknowledgements, fills, and commissions.
- Opening and closing facts.
- Durable lifecycle state.
- Trade limits and user-approved configuration.
- EOD or explicitly requested durable snapshots.

`OptionTradeCommandActor` currently owns a large event-sourced option-trade structure containing both durable facts and frequently changing position data. The target design keeps the durable option structure but removes live position marking from that aggregate.

### Trade strategy

Terminology update: the reusable strategy definition expresses why and when to trade; a reusable structure definition describes the legs/payoff. The existing strategy-specific calculations below consume the selected structure and policy capabilities. These are separate catalog identities even when they share a familiar name.

A trade strategy defines how instruments and legs form an economic position and how that position must be evaluated.

Position calculations cannot be assumed to be strategy-neutral. An Iron Condor has concepts that do not apply to a futures position or even to every option strategy, including:

- Four coordinated option legs.
- Put and call vertical spreads.
- Wing widths.
- Combined spread value.
- Strategy-level maximum profit and maximum loss.
- Strategy Greeks and underlying exposure.
- Iron-Condor-specific adjustment and exit thresholds.

The target architecture therefore uses strategy-specific realtime actors and concrete strategy events. Infrastructure may use a small shared interface or envelope for identifiers, timestamps, versions, and routing, but domain processing must receive the concrete strategy payload.

Example actor and contract names are:

- `IronCondorTradePositionRealtimeActor`
- `IronCondorTradePositionUpdatedEvent`
- `IronCondorTradePositionUpdatedCompleteEvent`
- `IronCondorTradePositionUpdatedFailEvent`
- `IronCondorTradeMonitorRealtimeActor`
- `IronCondorTradeMonitorUpdatedNotifyEvent`

These names are illustrative until the workflow design finalizes the contracts.

### Trade position

A trade position is the latest calculated state of an active strategy. It is derived from durable trade facts plus current market data.

Examples include:

- Current leg marks.
- Current bid and ask values.
- Current Greeks.
- Underlying price.
- Current spread and strategy values.
- Unrealized P&L.
- Current risk measures.
- Current strategy-position status.

This state changes at market-data frequency. It must not force the durable trade aggregate to load, append events, replay state, and run a durable projector for every accepted tick.

The current position is operational state. It may be persisted, but it is updated through a realtime projector without event-source or projector replay.

### Trade monitor

The Trade Monitor is an immutable historical view of complete strategy-position updates.

It answers:

- What complete strategy position was calculated at a specific update?
- Which market-data version produced it?
- What did the monitor observe or decide?
- Which thresholds or conditions were active?
- What information did the UI and downstream workflows receive?

The monitor is not a second mutable position model. Each successfully produced monitor record is appended and never updated by normal application processing.

## Persistence boundaries

The target design has three explicit database contexts.

| Context | Question answered | Mutation model |
|---|---|---|
| `TradeDbContext` | What trade, orders, fills, and lifecycle facts exist? | Durable authoritative mutations. |
| `TradePositionDbContext` | What is the latest calculated state of this strategy position? | Realtime, versioned current-state updates. |
| `TradeMonitorDbContext` | What complete position/monitor snapshots were produced over time? | Immutable append-only history. |

### TradeDbContext

`TradeDbContext` remains the source for durable business and execution facts. Live option marks, transient Greeks, and tick-by-tick unrealized P&L must not be written back through the event-sourced `OptionTradeCommandActor` merely because the current option-trade object contains position collections.

Durable commands remain appropriate for:

- Creating a trade.
- Placing an order.
- Recording fills.
- Opening or closing a position.
- Changing user-approved durable configuration.
- Manual corrections that must become authoritative facts.
- Processing EOD or explicit checkpoints.
- Performing durable order execution.

### TradePositionDbContext

`TradePositionDbContext` owns the latest calculated strategy-position state.

It must support multiple strategy-specific tables for a single logical position. This is necessary because a strategy may have several components with different query and update requirements.

An Iron Condor may eventually use tables such as:

- `iron_condor_trade_position`
- `iron_condor_trade_position_leg`
- `iron_condor_trade_position_spread`
- `iron_condor_trade_position_risk`

These are example logical names, not final CQL definitions.

The storage API should expose one strategy-level operation rather than requiring the actor or projector to coordinate individual table calls. For example:

```csharp
ValueTask UpsertIronCondorTradePositionAsync(
    IronCondorTradePositionSnapshot position,
    CancellationToken cancellationToken = default);
```

The concrete storage implementation owns the multi-table mutation.

Every component written for one logical update must carry the same identifiers:

- Order ID.
- Trade ID.
- Strategy type.
- Value date.
- Position version.
- Source market-data sequence or version.
- Calculated timestamp in UTC.

Readers must not combine components from different position versions. The implementation should use versioned component rows and a committed-position marker or equivalent storage convention. The position header/current pointer is committed only after all components for that version have been written successfully.

The exact CQL partition and clustering keys will be finalized with the storage implementation and query requirements.

### TradeMonitorDbContext

`TradeMonitorDbContext` owns one immutable history table. The recommended logical table name is:

```text
trade_monitor_snapshot
```

Each row represents one complete, denormalized strategy-position update. A monitor row must be appended only from a single internally consistent position snapshot.

The table uses a common envelope for routing and cross-strategy history queries while preserving the complete strategy-specific result in a versioned payload.

Candidate common fields are:

- Monitor snapshot ID.
- Order ID.
- Trade ID.
- Strategy type.
- Value date.
- Position version.
- Source market-data sequence.
- Source event ID and correlation ID.
- Calculated timestamp in UTC.
- Recorded timestamp in UTC.
- Position status.
- Monitor/workflow status.
- Current value and P&L summary.
- Triggered condition or result summary.
- Payload type.
- Payload schema version.
- Strategy-specific serialized payload.

The complete Iron Condor payload may contain:

- All four leg marks.
- Put-spread and call-spread state.
- Net strategy value.
- Strategy Greeks.
- Underlying price.
- Maximum profit and loss.
- Current unrealized P&L.
- Risk measurements.
- Monitor thresholds.
- Triggered conditions.
- Current monitor or workflow phase.

A future futures, vertical-spread, or calendar-spread update uses a different typed payload inside the same monitor envelope.

Using one monitor table does not require a strategy-neutral position domain event. The storage envelope is generic; the originating event and serialized snapshot remain strategy-specific.

Normal application processing must not update an existing monitor row. Deletes, retention, or migration operations are administrative concerns and are outside the live processing contract.

## Target live-update flow

The initial Iron Condor flow should evolve toward the following structure:

```text
Databento option trade/quote
  -> TickAggregationRealtimeActor
  -> FuturesOptionTickDataRealtimeActor
  -> enriched futures-option price update (Realtime)
  -> IronCondorTradePositionRealtimeActor
       - identify active Iron Condor positions using the option contract
       - ignore duplicate or stale source sequences
       - update the matching leg mark
       - recalculate both spreads and the complete strategy position
       - assign one position version
  -> IronCondorTradePositionUpdatedEvent (Realtime source)
  -> IronCondor trade-position realtime projector
       - write all TradePositionDbContext components for that version
       - commit the current-position version
  -> IronCondorTradePositionUpdatedCompleteEvent
  -> IronCondorTradeMonitorRealtimeActor
       - create one complete denormalized monitor snapshot
       - later: evaluate strategy monitor workflow stages
  -> TradeMonitor realtime projector
       - append trade_monitor_snapshot
  -> monitor complete event
  -> IronCondorTradeMonitorUpdatedNotifyEvent
  -> Trade Blotter and external consumers
```

The exact division between the position actor, monitor actor, and their projectors will be finalized with the workflow design. The persistence order is authoritative:

1. Calculate one complete strategy position in memory.
2. Persist all position components under one version.
3. Commit that position version.
4. Create and append one denormalized immutable monitor snapshot from that exact version.
5. Publish the UI notification only after the backend update reaches the required completion boundary.

## Realtime processing semantics

Live market updates use the realtime conventions defined in the system actor documentation.

- Actor subjects use `ActorType.Realtime` for backend live processing.
- Realtime actors and projectors live under `Realtime` folders where appropriate.
- The realtime projector publishes source, complete, and fail events.
- Realtime events are not stored as replayable aggregate history.
- Realtime projectors do not use JetStream work queues, a durable outbox, or replay recovery.
- Storage may still be updated once by a realtime projector.
- Duplicate or older market sequences are ignored.
- A failed update is logged and publishes its fail event.
- There is no replay retry; the next market update calculates and writes a newer position.
- `ActorType.Notify` is reserved for UI, console, and external consumers.

This behavior is appropriate because the position represents the latest market state. Missing one intermediate update is acceptable; blocking subsequent updates behind a durable backlog is not.

### Partial failure rules

The multi-table position update must not expose a partially committed position version.

If a position component write fails:

- The new version is not marked current.
- No monitor snapshot is appended for that version.
- A realtime fail event is published and logged.
- The next accepted market update produces a newer complete version.

If the position version commits but the monitor append fails:

- The current position remains valid.
- The failed monitor append is logged and publishes a fail event.
- The history has an observable gap for that version.
- The next position update continues normally and appends its own snapshot.

If complete monitor history later becomes a regulatory or business requirement, that requirement must introduce an explicit durable delivery mechanism. It must not silently turn the realtime position pipeline into the existing durable replay model.

## Strategy-specific events and shared infrastructure

The system should not introduce a single position event whose payload attempts to represent every strategy.

Concrete examples are:

- `IronCondorTradePositionUpdatedEvent`
- `VerticalSpreadTradePositionUpdatedEvent`
- `CalendarSpreadTradePositionUpdatedEvent`
- `FuturesTradePositionUpdatedEvent`

A narrow shared infrastructure contract may expose only:

- Trade identity.
- Strategy discriminator.
- Value date.
- Position version.
- Source sequence.
- Calculated timestamp.

This shared metadata can support routing, logging, metrics, and the monitor envelope. Strategy actors, projectors, and monitor workflows must deserialize and operate on the concrete strategy contract.

## Active-position discovery and actor state

The repository already contains `IOptionTradeLiveFeedMap`, which maps an option contract ID to the active option trades containing that leg. This is a useful starting point for backend routing, but it must no longer exist only to support UI-driven updates.

The final workflow design must decide whether to:

- Retain and formalize this registry as the active strategy-position registry.
- Replace it with a Trade Position application interface.
- Create one registry per strategy.

Regardless of the implementation, active-position registration must be owned by backend trade/position lifecycle processing. Opening the Trade Blotter must not register a position, and closing it must not unregister one.

Realtime position state should be seeded from:

- The durable trade definition and fills in `TradeDbContext`.
- The latest committed position in `TradePositionDbContext`, when present.
- The latest hot market-data cache.

It must track the latest accepted sequence or timestamp per instrument leg so that late or duplicate market messages cannot replace a newer mark.

## Current legacy flow

The current Iron Condor position update is UI-driven:

```text
Databento option update
  -> FuturesOptionTickDataRealtimeActor
  -> OptionTradeTickPriceDataUpdatedEvent (Notify)
  -> FuturesOptionTickDataUIEventConsumer
  -> IronCondorViewModel
       - match the option leg
       - construct OptionTradeLegDataReadModel
       - compare the option midpoint
       - invoke ChangeOptionTradeLegDataCommand
  -> REST or NATS client
  -> durable OptionTradeCommandActor
       - load/replay option-trade state
       - create/update an embedded intraday trade position
       - persist events and project the position
  -> TradePositionUpdatedEvent
  -> UI position listener
```

This flow is legacy because:

- Position updates stop when the UI is not running.
- The UI performs backend domain translation and orchestration.
- Every accepted option-price change enters a durable event-sourced aggregate.
- Frequently changing derived position data is mixed with authoritative trade facts.
- The Trade Blotter is coupled directly to backend workflow mechanics.

`ChangeOptionTradeLegDataCommand` must be removed from the automatic live tick path. It may remain temporarily for compatibility or an explicitly authorized manual correction until the replacement pipeline is validated.

The existing backend `UpdateFuturesOptionTradeLegDataAsync` helper currently has no active caller and must not be treated as the target architecture.

The current `OptionTradeLegDataChangedEvent` handler and its projector registration must also be reviewed during migration; the current option-trade projector does not register that event as a published projection source.

## OptionTrade aggregate migration direction

The present `OptionTrade` model treats the option trade and its changing position history as one structure. This was appropriate for the original whole-trade update model but is not the target live architecture.

The future split is:

```text
Durable OptionTrade
  - identity
  - strategy selection
  - instruments and option legs
  - quantities
  - orders and fills
  - lifecycle facts
  - durable limits/configuration

Strategy-specific Trade Position
  - current leg marks
  - current spreads
  - current value and P&L
  - current Greeks and risk
  - current strategy-position state

Immutable Trade Monitor snapshot
  - complete denormalized position version
  - monitor state and results
  - chronological history
```

The migration must preserve the durable facts required to seed a strategy position while removing tick-frequency mutation from the aggregate.

## TradePlan migration direction

The current `Domain.Trade/Plan` code predates the planned Trade Strategy and Trade Monitor workflow pipelines. It should be treated as a legacy candidate rather than renamed mechanically.

Its responsibilities must be reassigned only after the future workflow design determines whether each concept belongs to:

- Durable strategy configuration.
- Strategy-selection workflow state.
- Current position state.
- Monitor thresholds.
- Immutable monitor output.
- Durable order-execution intent.

No current `TradePlan` types should be made authoritative for the new persistence model merely because they already exist.

## Trade Blotter boundary

The Trade Blotter is the most important trade UI, but it must become a view into backend-owned state.

The future Trade Blotter reads:

- Trade identity, lifecycle, orders, and fills from `TradeDbContext` APIs.
- Current strategy-specific position state from `TradePositionDbContext` APIs.
- Historical position and monitor snapshots from `TradeMonitorDbContext` APIs.
- Realtime display changes from strategy-specific Notify events.

The Trade Blotter may send explicit user intentions such as open, close, cancel, or manually intervene. It must not:

- Calculate authoritative position state.
- Translate market ticks into position commands.
- Coordinate backend workflow stages.
- Control whether backend position monitoring is active.
- Require a view to remain open for a position to stay current.

The legacy Trade Blotter should be frozen except for critical fixes until the backend strategy and monitor pipelines produce the replacement read and notification contracts.

## Query responsibilities

Queries must preserve the storage boundaries.

### Current position queries

Current position queries use `TradePositionDbContext`. The application API hides the number of physical strategy tables and returns one typed strategy-position read model.

Examples are:

- Get current Iron Condor position.
- Get current position by order/trade ID.
- Get all active positions for a strategy.
- Get positions containing an instrument or option contract.

### Monitor history queries

Historical queries use `TradeMonitorDbContext` and the immutable monitor table.

Examples are:

- Get the latest monitor snapshot for a trade.
- Get monitor history for a value date.
- Get the position as observed at or before a timestamp.
- Get snapshots that triggered a monitor condition.
- Get strategy-specific history using the payload discriminator and version.

### Trade Blotter summary

A small cross-strategy summary projection may be introduced later if the blotter needs one efficient list of all active trades. Such a projection may contain common fields such as trade identity, strategy, status, latest P&L, and last update time.

That read optimization does not create a strategy-neutral position aggregate or event. Selecting a row loads the appropriate strategy-specific position and monitor models.

## Initial storage API direction

The following interfaces illustrate the responsibility split. Names and signatures will be finalized during implementation.

```csharp
public interface ITradePositionDbContext
{
    ValueTask UpsertIronCondorTradePositionAsync(
        IronCondorTradePositionSnapshot position,
        CancellationToken cancellationToken = default);

    ValueTask<IronCondorTradePositionSnapshot?> GetIronCondorTradePositionAsync(
        OptionTradeEntityId tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);
}

public interface ITradeMonitorDbContext
{
    ValueTask AppendTradeMonitorSnapshotAsync(
        TradeMonitorSnapshot snapshot,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<TradeMonitorSnapshot>> GetTradeMonitorHistoryAsync(
        OptionTradeEntityId tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);
}
```

The strategy-specific storage operation accepts one complete position snapshot even when the storage implementation writes several tables. The monitor operation accepts one immutable denormalized record.

## Validation requirements

The implementation must include unit and integration coverage proving the following behavior.

### Position calculation and routing

- An option update is routed to every active strategy position containing that contract.
- An unrelated contract does not update the position.
- Duplicate and stale market sequences are ignored.
- Different legs of the same position are serialized or synchronized correctly.
- One accepted leg update produces one complete position version.
- Iron Condor spread, strategy value, Greeks, and P&L are calculated from a consistent set of leg marks.

### Position persistence

- One position version is written consistently across every required strategy table.
- Readers never combine components from different versions.
- A failed component write does not advance the committed current version.
- A later market update can supersede an incomplete failed update.
- Restart can seed the realtime actor from durable trade facts and the latest committed position.

### Monitor persistence

- Exactly one monitor snapshot is appended for one successfully completed position version.
- The monitor snapshot contains the same position version and source sequence as the committed position.
- Existing monitor rows cannot be updated through normal APIs.
- Monitor history is ordered deterministically.
- Strategy-specific payload types and schema versions round-trip correctly.
- A failed position update does not append a monitor snapshot.
- A monitor append failure does not invalidate the already committed current position.

### Realtime delivery

- Source, complete, and fail events use `ActorType.Realtime`.
- The flow does not create event-source log entries or durable projector backlog.
- A failure does not block later market updates.
- UI Notify is published only at the selected successful completion boundary.

### UI independence

- Position and monitor updates continue when no UI process is running.
- Opening a Trade Blotter view does not start backend position calculation.
- Closing the view does not stop backend position calculation.
- The UI does not send `ChangeOptionTradeLegDataCommand` in response to market data.
- Reopening a view loads the latest backend position and can receive subsequent Notify updates.

## Deferred workflow design

This document will be extended when the Trade Strategy and Trade Monitor workflow pipelines are designed.

Deferred subjects include:

- Strategy activation and deactivation commands.
- Strategy selection and construction stages.
- Position actor lifecycle and ownership.
- Monitor workflow phases.
- Risk, adjustment, profit-taking, and exit-condition stages.
- The transition from a monitor decision to durable order execution.
- Manual intervention and approval boundaries.
- Workflow status and terminal-operation UI tracking.
- Retention policy for immutable monitor history.
- Payload evolution and migration policy.
- Exact CQL schemas and partitioning.

## Open implementation decisions

The following decisions remain intentionally open:

- Whether `IOptionTradeLiveFeedMap` is retained, replaced, or generalized.
- Whether the position actor and monitor actor are one actor per strategy type or further partitioned by trade.
- The final strategy event and actor names.
- The exact committed-version mechanism across multiple position tables.
- The serialization format for the strategy-specific monitor payload.
- The common monitor summary columns required for cross-strategy queries.
- Monitor-history retention and archival rules.
- Whether a manual correction replaces or retires `ChangeOptionTradeLegDataCommand`.
- Which existing `TradePlan` concepts survive under new names.

These questions must be answered by explicit design and tests rather than inferred from the legacy implementation.

## Related documentation

- `Domain-Actor-Implementation-Details.md`
- `Domain-Actor-Optimization-Details.md`
- `../Order/Execution/Docs/OrderExecutionWorkflowSpecification.md`
- `../../Documents/system/Actor-Implementation-Conventions.md`
- `../../Documents/system/Actor-Message-Types-and-Delivery-Conventions.md`
