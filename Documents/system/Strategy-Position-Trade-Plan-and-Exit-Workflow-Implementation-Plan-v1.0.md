# Strategy Position Trade Plan and Exit Workflow Implementation Plan

**Document type:** System-wide implementation plan
**Status:** Backend implementation and deterministic automated qualification complete through strategy exit-order execution, activity/timeline projections, query APIs, the UI query-service boundary, and retirement of the legacy root Option and generic Trade Plan runtimes; legacy UI migration, controlled infrastructure disruption, and the agreed live soak remain release gates
**Version:** 1.9
**Created:** 2026-09-13
**Primary pilot:** Futures option iron-condor position
**Subsequent strategies:** Futures option vertical spread and outright futures
**Design reference:** `Strategy-Position-Trade-Plan-Monitoring-System-Design-v1.0.md`

## 1. Purpose

This plan replaces the legacy iron-condor option algorithm and the generic Trade Plan runtime with three strategy-specific Trade Plan domains. Each domain owns its identity, event stream, complete snapshot event, calculations, material-change policy, projection, queries, UI model, and exit workflow.

Every accepted strategy-position update invokes exactly one strategy-specific event-sourced FunctionActor. The FunctionActor loads the most recent `XXXTradePlanUpdatedEvent` from the single Trade Plan stream for that position and value date, constructs the complete next plan, calculates forward trade price and exit conditions inline, and completes with another full snapshot event. The event is projected to TradePlanDb only when the plan changed materially. An exit-state snapshot starts the strategy-specific Exit Position workflow.

The common lifecycle is:

```text
market tick
  -> realtime ContractId router
  -> strategy PositionCommandActor
  -> committed strategy PositionChangedEvent
  -> strategy PositionRealtimeActor
       -> UpdateXXXTradePlanCommand
       -> strategy XXXTradePlanFunctionActor
            load latest XXXTradePlanUpdatedEvent snapshot
            calculate current plan
            calculate forward trade price inline
            calculate exit condition inline
            save and return one committed XXXTradePlanUpdatedEvent
       -> inspect the committed FunctionActor response
       -> if ExitRequired, start strategy Exit Position workflow
  -> receipt-backed material projection through the strategy Position EventProjector
       -> strategy TradePlanDb table and UI
  -> strategy Exit Position workflow, only when started by PositionRealtimeActor
       -> ExitOrderComposer FunctionActor
       -> PositionRiskManager FunctionActor
       -> Portfolio
       -> TradeOrder
       -> OrderExecution
       -> broker fills
       -> established trade and position closure
```

## 1.1 Implementation checkpoint

The following implementation is complete at this checkpoint:

- committed-event-first FunctionActor completion support;
- stable value-date Trade Plan streams for Iron Condor, Vertical Spread, and Futures;
- the three strategy calculation, forward-price, exit-condition, comparison, and validation model sets;
- the three strategy Trade Plan FunctionActors, state repositories, durable Position EventProjector mappings, and PositionRealtimeActors;
- additive TradePlanDb schema and current/history storage for all three strategies, value-date activity, and exit-workflow timelines;
- strategy-specific current/history/activity/exit-workflow query messages, dedicated QueryActors and extension handlers, paged NATS and UI service APIs, and host registration;
- cross-strategy activity and common exit-workflow support under the root `Position` hierarchy, with no redundant `Domain.Trade.Trade` namespace or folder;
- propagation of broker-neutral contract metadata into position legs;
- direct framework-base inheritance and frozen parse, validation, receive, event, and execution-policy maps on all six strategy Exit workflow FunctionActors;
- startup and dependency-injection registration;
- happy-path and edge-case unit, BDD, storage-integration, actor-registration, serialization, schema, and Function-lifecycle tests; and
- deterministic qualification of 100,000 ordered position updates with injected duplicate and out-of-order ticks, MessagePack snapshot recovery into a fresh state machine, and successful continuation at the next source sequence.

Gate 7 now uses a distinct Portfolio close-order composition operation. The existing
`EvaluatePortfolioOrderCompositionCommand` remains the opening admission operation; exit workflows call
`EvaluatePortfolioCloseOrderCompositionCommand`, which validates the identified open position and exact
remaining quantities, allocates only a new Order ID, retains the existing Trade ID, and commits the
Portfolio decision, accepted close order, close uniqueness row, financial revision, receipt, and event
in one PostgreSQL transaction.

The domain decision is now explicit. `TradeOrderPositionType` is a broker-neutral byte enum with
`Unknown = 0`, `Opening = 1`, and `Closing = 2`. It is owned by `Domain.Trade.Shared` and is persisted
on the root `TradeOrderDefinition`. The Intrinsic Time Strategy workflow always submits `Opening`;
strategy-position Exit workflows always submit `Closing`; and `Unknown` is compatibility-only and is
rejected for every newly created order.

Iron Condor, Vertical Spread, and Futures now each have a strategy-specific event-sourced Exit Position
Workflow CommandActor, RealtimeActor, Exit Order Composer FunctionActor, and Position Risk Manager
FunctionActor. A committed exit-required plan starts one deterministic workflow. Its composer produces
an exact reversing `Closing` composition, its Risk Manager obtains the atomic Portfolio decision, and an
accepted close order flows through TradeOrder and OrderExecution. A filled close produces no opposing
established trade; it closes the referenced established Trade and strategy Position. The established
Trade schema is version 3 and retains both its immutable opening fills and the validated exact reversing
closing fills with the UTC close time, so completed-trade queries retain the full execution evidence.

BenchmarkDotNet now records the strategy plan calculations at 1.147-1.480 microseconds per operation,
MessagePack plan serialization at 2.297 microseconds, and four-leg close composition at 13.474
microseconds on the development host. Normal calculations allocate 1,488-2,121 bytes, serialization
allocates 648 bytes, and the exit-only four-leg composition allocates 15,696 bytes.

Trade Plan projections now use the existing receipt-backed strategy Position EventProjectors. Each
strategy snapshot event implements `IRequireDurableProjection` and enables it only for a material snapshot.
For a material snapshot,
EventSourceDb commits the event and its initial projector state in the same PostgreSQL transaction. A
nonmaterial snapshot incurs no projector-state row or queue work. The Function state repository then submits
the committed collection to the event projector. A failed or interrupted enqueue or TradePlanDb write is
therefore discoverable by the projector's existing startup recovery scan; recovery introduces no
background database poll. Replaying a plan whose strategy row was already written also repairs its
activity row idempotently, closing the failure window between those two derived writes.

The automated checkpoint is green for 1,115 Trade unit tests, 45 Trade BDD scenarios, ten focused
actor-registration and serialization integration tests, twelve Trade-flow qualification tests, five real
TradePlanDb storage integration tests, 30 EventSourceDb appender/marker integration tests, 223 Portfolio
unit tests, and the real PostgreSQL atomic close-
composition integration test. The API Server builds with zero warnings and zero errors. The wider Trade
verification assembly still contains previously identified Regime Discovery golden-vector failures that
are outside this Trade Plan implementation; the focused Trade-flow qualification set is green.

The activity query and UI service now supply the complete selected `StrategyPositionId`, including the
Portfolio ID and position GUID, without fabricating identity from the legacy Fund screen. The final desktop
binding remains gated because the earlier trade-lifecycle refactor removed `ITradeCommandApi` and
`TradeCommandService` while the legacy Iron Condor and end-of-day view models still reference them; the
existing desktop solution therefore does not compile independently of this Trade Plan work. Restoring that
retired command path is prohibited by the agreed migration. The legacy screen must be replaced as one unit
before it can consume the completed strategy query service. Legacy Trade Plan retirement, controlled
disruption tests, and the previously agreed live trading-session soak remain release qualification work.
They do not change the frozen opening/closing contracts or the implemented backend exit path.

## 2. Final agreed decisions

These decisions govern implementation and supersede conflicting generic-monitoring or separate-stage proposals in the design reference:

1. All Trade Plan behavior is organized by concrete trade-position strategy.
2. The first three strategies are Iron Condor, Vertical Spread, and outright Futures.
3. Each strategy has its own Trade Plan FunctionActor, state, update command, completed snapshot event, models, projector, database table, queries, and Exit Position workflow.
4. A Trade Plan stream is identified by the complete strategy-position identity and one `ValueDate`.
5. There is exactly one event stream for that Trade Plan for that value date.
6. Every invocation of `UpdateXXXTradePlanCommand` loads state from the most recent `XXXTradePlanUpdatedEvent` snapshot for that stream.
7. Every successful invocation completes once and saves one `XXXTradePlanUpdatedEvent` containing the complete current plan.
8. The incoming command is idempotent by command ID and request fingerprint. A matching duplicate returns its previously completed snapshot.
9. A later position update has a new command ID and source position sequence, executes against the loaded plan, and appends the next snapshot to the same stream.
10. Forward Trade Price and Exit Condition are pure models invoked inline inside the Trade Plan FunctionActor handler. They are not separate actors.
11. The FunctionActor determines `Normal`, `Hold`, `Warning`, `Breached`, `ExitRequired`, `CalculationFailed`, or `Closed` before it persists the completed event.
12. TradePlanDb and UI projection occurs only when the completed event reports a material change.
13. A maximum-loss breach is always material and always produces `ExitRequired` with an immediate market-exit policy.
14. Only a committed Trade Plan snapshot whose state is `ExitRequired` can start an Exit Position workflow.
15. The Exit Position workflow contains strategy-specific Exit Order Composer and Position Risk Manager pipeline FunctionActors.
16. Exit composition reverses each remaining open leg using explicit close/reduce intent. It does not create a new opposing position.
17. Portfolio remains the financial authority and creates accepted Trade Orders. Position Risk Manager does not allocate Portfolio/Fund business identities.
18. Trade Order, OrderExecution, fills, established Trade, and final Position closure retain their existing event-sourced lifecycles.
19. TradePlanDb is a derived monitoring and query store. EventSourceDb remains the durable source for Trade Plan state.
20. There is no polling loop. All processing is caused by committed position, plan, workflow, execution, or fill events.
21. Each strategy owns a PositionRealtimeActor whose dedicated position-event handler invokes the Trade Plan FunctionActor, inspects its committed completion response, and starts the strategy Exit Position workflow only for `ExitRequired`.
22. TradePlanDb projection is never the exit trigger and cannot delay or suppress an exit decision.
23. Every Trade Order has one order-level `TradeOrderPositionType`; entry composition produces only `Opening` and position Exit workflows produce only `Closing`.
24. A `Closing` order allocates a new Order ID, retains the existing Trade ID, identifies the existing `StrategyPositionId`, and can only reduce that position's remaining signed leg quantities.
25. TradeOrder and OrderExecution preserve position type unchanged; an Opening fill establishes a trade, while a Closing fill closes the referenced established trade and position without creating an opposing trade.
26. A committed material Trade Plan event is projected by the strategy Position EventProjector under its normal durable receipt and startup-recovery rules; the FunctionActor does not own a separate direct Scylla projector.
27. Strategy Plan behavior remains in each concrete strategy's `Plan` folder; cross-strategy activity queries and reusable exit-workflow support reside under the root `Position` hierarchy.

## 3. Verified current baseline

The following repository behavior exists before this plan is implemented:

1. `FuturesOptionRealtimeActor` receives `FuturesTickTradeDataChangedEvent` and uses an in-memory Contract ID route index.
2. One incoming option tick may map to multiple `PortfolioFundTradeLeg` routes and therefore multiple strategy positions.
3. The realtime handler creates a deterministic `ChangeTradeLegDataCommand` for each route.
4. `ChangeTradeLegDataCommand` contains the complete `StrategyPositionId`, leg ID, broker-neutral Contract ID, price, source sequence, route generation, UTC effective time, and `TradeStrategyKind`.
5. Iron-condor and vertical-spread position actors inherit `BaseInMemoryEventSourceCommandActor` and retain resident state for leg-data updates.
6. The position actors commit complete `IronCondorPositionChangedEvent`, `VerticalSpreadPositionChangedEvent`, or `FuturesPositionChangedEvent` snapshots.
7. Position projectors currently write `StrategyPositionSnapshot` to TradeDb and update open-position routes on position boundaries. They do not invoke a strategy Trade Plan FunctionActor.
8. Empty strategy `Plan` folders already exist beneath Iron Condor, Vertical Spread, and Futures.
9. An empty `Application.Storage/TradePlanDb` folder already exists, but `IDbContextFactory` does not expose TradePlanDb or TradePlanSchema.
10. The legacy option algorithm supports long and short iron condors only.
11. The legacy builder performs service and database calls in the calculation path and uses mutable Blackboard state.
12. Legacy `HasTradePlanChanged` compares only `TradePlanReadModel.AssetPrice`.
13. Legacy `AssetPrice` is populated from futures EOD close data, not the incoming option-leg tick.
14. A volatility or option-premium change can therefore change position P&L without changing the legacy comparison value.
15. The legacy algorithm contains unreachable rules, broad default-producing catches, ignored required MessagePack data, and unimplemented `ExecuteAlgorithm` methods.
16. The generic `Domain.Trade/Plan` implementation writes a wide Scylla TradeDb model and then publishes an event. It is not the target durability model.
17. `BaseEventSourceActorRepository` already supports loading state from the most recent event of a designated snapshot type.
18. `BaseEventSourceFunctionActor` loads state for every invocation, performs exact mapped dispatch, projects successful completion when configured, saves the completed event, and replays a matching completion.
19. Existing completed-only pipeline Function states use one stream per invocation. Trade Plan state must make completion relative to the currently loaded command while preserving the preceding plan snapshot in the stable value-date stream.
20. The existing Intrinsic Time Risk Manager and Order Composition actors provide the five-map FunctionActor convention and event-sourced completion pattern.
21. The existing Risk Manager request and identities are coupled to the Intrinsic Time entry workflow. Position exit requires strategy-position-specific requests and state.
22. TradeOrderCommandActor and OrderExecutionCommandActor are event-sourced and already hand off an executing Trade Order to OrderExecution.

## 4. Scope

### 4.1 Included

- Strongly typed strategy Trade Plan identities containing full trade ownership, position identity, and value date.
- Strategy-specific MessagePack commands, completed events, results, queries, and read models.
- Strategy-specific full Trade Plan snapshots.
- Snapshot-aware FunctionActor state and repository behavior on a stable value-date stream.
- Inline forward-trade-price and exit-condition models.
- Configurable material-change policies.
- TradePlanDb context, schema context, CQL, projectors, current/history queries, and activity queries.
- UI-facing current plan, history, calculations, decision explanation, and live material-update notification.
- Strategy-specific Exit Position workflows.
- Closing Order Composer and Position Risk Manager FunctionActors.
- Portfolio close-order handoff and downstream Trade Order/OrderExecution integration.
- Actor registration, API clients, service registration, startup schema creation, and Actor Health metrics.
- Removal of migrated legacy algorithm and generic Trade Plan runtime code after compatibility gates pass.
- Unit, BDD, integration, verification, recovery, benchmark, and soak tests.

### 4.2 Excluded

- Broker-specific IBKR contract translation and broker adapter implementation.
- Refactoring the generic Trade Order entry UI.
- Exercise, assignment, expiration settlement, rolling, and hedge workflows beyond recording an explicit future action.
- Reinforcement learning and automated parameter tuning.
- Replacing the established Portfolio financial-authority model.
- Changing raw market-tick storage ownership.
- Polling TradePlanDb or EventSourceDb for new work.

## 5. Target domain hierarchy

### 5.1 Iron Condor

```text
TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/
  Realtime/
    Actor/
      IronCondorTradePositionRealtimeActor.cs
      IronCondorTradePositionRealtimeContext.cs
    IronCondorPositionChanged.cs
  Plan/
    Function/
      Actor/
        IronCondorTradePlanFunctionActor.cs
        IronCondorTradePlanFunctionContext.cs
      State/
        IronCondorTradePlanFunctionState.cs
        IronCondorTradePlanFunctionStateRepository.cs
      Projector/
        IronCondorTradePlanProjector.cs
      CompleteIronCondorTradePlan.cs
      ExecuteUpdateIronCondorTradePlan.cs
      FailIronCondorTradePlan.cs
      ResolveIronCondorTradePlanExecutionPolicy.cs
    Model/
      IronCondorTradePlan.cs
      IronCondorTradePlanAlgorithm.cs
      IronCondorForwardTradePriceModel.cs
      IronCondorExitConditionModel.cs
      IronCondorTradePlanComparisonModel.cs
      IronCondorTradePlanParameters.cs
      IronCondorTradePlanValidation.cs
    Query/
      Actor/
        IronCondorTradePlanQueryActor.cs
        IronCondorTradePlanQueryContext.cs
      GetCurrentIronCondorTradePlan.cs
      GetIronCondorTradePlanHistory.cs
  Workflow/
    Command/
    Realtime/
    OrderComposer/
      Function/
      Model/
    RiskManager/
      Function/
      Model/
```

### 5.2 Vertical Spread

```text
TomasAI.IFM.Domain.Trade/Futures/Option/Position/VerticalSpread/
  Realtime/
    Actor/
      VerticalSpreadTradePositionRealtimeActor.cs
      VerticalSpreadTradePositionRealtimeContext.cs
    VerticalSpreadPositionChanged.cs
  Plan/
    Function/
    Model/
      VerticalSpreadTradePlan.cs
      VerticalSpreadTradePlanAlgorithm.cs
      VerticalSpreadForwardTradePriceModel.cs
      VerticalSpreadExitConditionModel.cs
      VerticalSpreadTradePlanComparisonModel.cs
    Query/
  Workflow/
    Command/
    Realtime/
    OrderComposer/
    RiskManager/
```

### 5.3 Futures

```text
TomasAI.IFM.Domain.Trade/Futures/Position/
  Realtime/
    Actor/
      FuturesTradePositionRealtimeActor.cs
      FuturesTradePositionRealtimeContext.cs
    FuturesPositionChanged.cs
  Plan/
    Function/
    Model/
      FuturesTradePlan.cs
      FuturesTradePlanAlgorithm.cs
      FuturesForwardTradePriceModel.cs
      FuturesExitConditionModel.cs
      FuturesTradePlanComparisonModel.cs
    Query/
  Workflow/
    Command/
    Realtime/
    OrderComposer/
    RiskManager/
```

Shared code is limited to transport primitives, base interfaces, stable enums, and infrastructure mechanics. A shared calculation model must not contain strategy branching.

## 6. Identities and stream ownership

### 6.1 Strategy-specific identifiers

Replace the legacy order/trade-only `IronCondorTradePlanId` with identifiers equivalent to:

```text
IronCondorTradePlanId(StrategyPositionId Position, DateOnly ValueDate)
VerticalSpreadTradePlanId(StrategyPositionId Position, DateOnly ValueDate)
FuturesTradePlanId(StrategyPositionId Position, DateOnly ValueDate)
```

`StrategyPositionId` already contains:

```text
TradeEntityId(PortfolioId, FundId, OrderId, TradeId)
PositionId
```

Trade date, expiry, option type, direction, and maturity remain snapshot fields. They are not used to replace the globally unique position identity.

### 6.2 Stream invariant

For one strongly typed Trade Plan ID:

- there is one EventSourceDb stream;
- every successful update appends one full `XXXTradePlanUpdatedEvent`;
- the stream continues until the value date ends;
- the next value date creates a new stream;
- the preceding value-date stream remains immutable;
- the first event of a new value date explicitly carries forward allowed cross-day state such as an active trailing stop or pending exit.

### 6.3 Command identity

The update command ID is deterministic from:

```text
source PositionChangedEvent ID
+ strategy position ID
+ value date
+ update cause
```

Repeated delivery of the same position event therefore reaches the same stream with the same command ID and fingerprint.

### 6.4 Exit workflow identity

An Exit Position workflow ID is deterministic from:

```text
Trade Plan ID
+ exit-triggering plan event ID
+ exit condition
```

This identity is used by Exit Order Composer, Position Risk Manager, Portfolio handoff, and exit-status projection. It prevents two workflows from producing duplicate close orders for the same exit decision.

## 7. Shared contracts

### 7.1 Enums

Add stable MessagePack enums under the strategy Trade Plan shared contract hierarchy:

```text
TradePlanState
  Unknown
  Normal
  Hold
  Warning
  Breached
  ExitRequired
  ExitPending
  ExitSubmitted
  ExitPartiallyFilled
  ExitFilled
  ExitFailed
  CalculationFailed
  Closed

TradePlanCompletionState
  Updated
  ExitRequired

TradePlanUpdateCause
  PositionChanged
  ValueDateOpened
  ForwardCalculationChanged
  ExitDecisionChanged
  RiskManagementChanged
  ExecutionChanged
  PositionClosed
  Correction

TradePlanExitAction
  None
  ExitAtMid
  ExitAtLimit
  ExitAtMarket

TradePlanExitCondition
  None
  MaximumLoss
  ForwardLoss
  TrailingStop
  ProfitTarget
  Expiry
  MarketReversal
  GammaRisk
  DeltaRisk
  Manual
  PortfolioEmergency

TradePlanDataQuality
  Complete
  Degraded
  Stale
  Invalid
```

Numeric values are explicit and never reordered. Additive values are appended.

### 7.2 Commands

Each command contains:

- schema version;
- command ID;
- Function actor subject and strongly typed Trade Plan ID;
- complete `StrategyPositionSnapshot`;
- source position event ID and event version;
- source position sequence and route generation;
- value date and UTC evaluation time;
- immutable established-trade evidence;
- current underlying observation where required;
- strategy-specific market inputs;
- resolved parameter-set identity, version, payload hash, and immutable payload;
- update cause; and
- canonical request fingerprint.

Commands:

```text
UpdateIronCondorTradePlanCommand
UpdateVerticalSpreadTradePlanCommand
UpdateFuturesTradePlanCommand
```

No required input may use `[IgnoreMember]`.

### 7.3 Completed snapshot events

Use one completed snapshot event type per strategy:

```text
IronCondorTradePlanUpdatedEvent
VerticalSpreadTradePlanUpdatedEvent
FuturesTradePlanUpdatedEvent
```

Every event contains:

- full event and command lineage;
- strongly typed Trade Plan ID;
- complete strategy-specific plan snapshot;
- source position sequence;
- event stream version/revision;
- request fingerprint;
- completion state: `Updated` or `ExitRequired`;
- material-change result and changed-field flags;
- current material-policy identity/version/hash;
- whether an exit workflow must be dispatched; and
- complete calculation explanation and data-quality outcome.

`ExitRequired` is a state in the same snapshot event type. A separate event type must not become the newest state while snapshot loading searches only for `XXXTradePlanUpdatedEvent`.

### 7.4 Failed results

Each actor returns its strategy-specific failed event for invalid messages or operational failures:

```text
IronCondorTradePlanFailedEvent
VerticalSpreadTradePlanFailedEvent
FuturesTradePlanFailedEvent
```

Expected missing-data outcomes that produce a valid degraded or calculation-failed plan are completed snapshots. Unexpected parsing, validation, state-load, calculation, projection-policy, persistence, or reply failures use the typed failure event and comprehensive error detail.

## 8. Snapshot FunctionActor lifecycle

### 8.1 Required behavior

Each `XXXTradePlanFunctionActor` follows all standard FunctionActor maps:

1. `_parseMap` maps the one exact update verb.
2. `_validationMap` validates the concrete command.
3. `_receiveMap` calls the dedicated `ExecuteUpdateXXXTradePlan` extension.
4. `_executionPolicyMap` calls the strategy-specific policy extension.
5. `_eventMap` maps completed and failed outcomes through dedicated Complete and Fail extensions.

The actor class contains mapping and lifecycle code only. Business rules, calculation, event construction, and state mutation reside in the update extension and `Plan/Model` classes.

### 8.2 State load

For every invocation, the state repository calls the existing snapshot-aware event-store loader with:

```text
TState         = XXXTradePlanFunctionState
TSnapshotEvent = XXXTradePlanUpdatedEvent
StreamId       = command.EntityId.Format()
```

Because every saved event in the stream is the same full snapshot type, loading begins at the latest event and does not replay the older daily history.

Loaded state contains:

- current complete plan;
- last command ID and request fingerprint;
- last source position sequence;
- last route generation;
- last event ID and stream version;
- last material projection revision and comparison values;
- parameter-set identity/version/hash; and
- exit workflow identity/status when applicable.

### 8.3 Invocation-relative completion

Function completion is relative to the current request:

- same command ID and same fingerprint: return the previous completed event;
- same command ID and different fingerprint: fail with `CommandPayloadConflict`;
- older position sequence: return a classified stale completion or failure according to contract, without changing business state;
- equal sequence and equal position hash: return the existing completion;
- equal sequence and conflicting position hash: fail with `PositionSequenceConflict`;
- newer position sequence: execute and append the next snapshot.

The state repository prepares the loaded state for the incoming request so `IsCompleted` means that the loaded event completed this command, rather than merely indicating that the stream has any historical event.

### 8.4 Save

The repository saves the completed snapshot at the loaded expected stream version. An optimistic concurrency conflict causes state reload and duplicate/conflict classification; it must not blindly retry a calculation against stale state.

The save result supplies the committed event identity and version used by projection and Exit Position dispatch.

### 8.5 Function persistence/projection ordering gate

The standard completed-only Function path currently projects before persisting. Trade Plan requires committed-event-first behavior because:

- EventSourceDb is authoritative;
- TradePlanDb must never become the only record of an uncommitted plan;
- an Exit Position workflow must start only from a committed snapshot; and
- projection failure must not erase a committed exit decision.

Implement one narrow, reusable Function completion policy equivalent to `EventThenProjection`:

1. append the completed event at the expected stream version;
2. expose the committed event with assigned event metadata;
3. return the original committed completion to the calling strategy PositionRealtimeActor;
4. let that RealtimeActor inspect the completion and start Exit Position only when it is `ExitRequired`; and
5. publish the committed event independently to the idempotent durable strategy projector.

Existing Function actors retain their current policies. Update the actor convention document and architecture tests for this explicit policy before using it in production.

## 9. Strategy calculation models

### 9.1 Common execution order

Every strategy algorithm performs these steps synchronously and deterministically:

1. Validate identity, strategy kind, open/closing status, position sequence, route generation, legs, quantities, prices, and UTC timestamps.
2. Restore allowed durable plan state such as trailing stops and pending exit identity.
3. Build current leg and whole-position valuation from the supplied position snapshot.
4. Calculate current P&L and strategy risk.
5. Run the strategy-specific Forward Trade Price model.
6. Evaluate maximum loss before lower-priority exit rules.
7. Run the strategy-specific Exit Condition model.
8. Resolve exactly one final state, action, severity, reason, and explanation.
9. Compare the complete candidate with the previous snapshot under the assigned material-change policy.
10. Construct one full completed snapshot event.

The models perform no database, actor, NATS, Blackboard, logging, wall-clock, or service-locator calls.

### 9.2 IronCondor models

`IronCondorTradePlanAlgorithm` validates and calculates:

- exactly four distinct legs;
- one long put, one short put, one short call, and one long call;
- common underlying and expiry;
- ordered strikes and signed remaining quantities;
- per-leg current value and P&L;
- current net debit/credit and strategy market value;
- maximum profit and loss;
- break-even prices and wing widths;
- aggregate delta, gamma, theta, and vega when supplied;
- put/call OTM probability;
- forward strategy value, forward P&L, and forward loss;
- trailing stop and profit targets; and
- final normal, warning, breach, or exit state.

`IronCondorForwardTradePriceModel`, `IronCondorExitConditionModel`, and `IronCondorTradePlanComparisonModel` reside in `IronCondor/Plan/Model`.

### 9.3 VerticalSpread models

The Vertical Spread algorithm validates and calculates:

- exactly two compatible option legs;
- common underlying, expiry, and option type;
- one long and one short leg;
- debit/credit direction;
- spread width, break-even, maximum profit, and maximum loss;
- per-leg and whole-spread value and P&L;
- threatened short-leg probability and Greeks;
- forward spread value and forward loss; and
- final normal, warning, breach, or exit state.

All Vertical Spread calculations reside in `VerticalSpread/Plan/Model`.

### 9.4 Futures models

The Futures algorithm validates and calculates:

- exactly one futures leg;
- signed remaining quantity and direction;
- contract multiplier, tick size, and tick value;
- opening/current price and notional;
- current and forward P&L;
- configured target, stop, and trailing stop;
- margin evidence when supplied;
- roll/expiry proximity; and
- final normal, warning, breach, or exit state.

All Futures calculations reside in `Futures/Position/Plan/Model`.

## 10. Material-change policy

### 10.1 Purpose

Every invocation is durably represented by its completed snapshot event. Materiality controls TradePlanDb/UI projection and whether a new position-monitor evaluation result is visible as a material revision. It does not control whether the FunctionActor validates and calculates the plan.

### 10.2 Mandatory material changes

These changes are always material and cannot be disabled:

- first plan for the value date;
- position open, closing, closed, or correction boundary;
- `Normal`, `Hold`, `Warning`, `Breached`, `ExitRequired`, or failure-state transition;
- exit action or winning exit condition;
- maximum-loss or emergency breach;
- trailing-stop activation, increase, clear, or breach;
- required-input data-quality degradation or recovery;
- parameter-set identity/version/hash change;
- route-generation correction; and
- conflict or calculation status that changes operator action.

### 10.3 Configurable numeric materiality

Reference Data parameter sets define strategy-specific comparisons such as:

- any underlying asset-price change or a configured tick/percentage threshold;
- any P&L change or configured absolute/percentage threshold;
- forward-price and forward-loss thresholds;
- Greek and probability thresholds;
- minimum projection interval when the state remains otherwise unchanged; and
- decimal/floating-point comparison tolerance.

Raw option-leg tick change alone need not be material. It must still update the position, enter the Trade Plan calculation, and participate in current P&L and breach detection.

If a threshold compares cumulative movement since the last projected plan, the full snapshot carries the last-material revision and the comparison values required to restore that baseline from only the newest snapshot event.

### 10.4 Legacy compatibility starting policy

The Iron Condor pilot begins with:

- current underlying asset-price change as a material trigger;
- any whole-position P&L change as an additional material trigger;
- all mandatory state/risk transitions above; and
- no comparison to raw tick receipt time alone.

This retains the useful intent of the legacy asset-price check while closing the option-premium/P&L blind spot.

## 11. TradePlanDb

### 11.1 Provider and authority

TradePlanDb uses the repository's ScyllaDB storage conventions for derived, append-oriented monitoring history. It is not part of Portfolio financial transactions and is never used to rehydrate authoritative Trade Plan state.

### 11.2 Context layout

```text
TomasAI.IFM.Application.Storage/TradePlanDb/
  ITradePlanDbReadContext.cs
  ITradePlanDbWriteContext.cs
  TradePlanDbContext.cs
  TradePlanDbCql.cs
  Schema/
    TradePlanSchemaDb.cs
    TradePlanSchemaCql.cs
```

Use only the standard read and write context-interface files. All runtime CQL constants reside in `TradePlanDbCql.cs`; schema CQL resides in `TradePlanSchemaCql.cs`.

Extend `IDbContextFactory` with:

```text
ITradePlanDbReadContext/ITradePlanDbWriteContext or combined TradePlanDbContext
TradePlanSchemaDb TradePlanSchema
```

Register the context and schema in API, desktop, tests, development-data, and schema-installation hosts.

### 11.3 Tables

Create additive versioned tables:

```text
iron_condor_trade_plan_v1
vertical_spread_trade_plan_v1
futures_trade_plan_v1
position_trade_plan_activity_by_date_v1
position_exit_workflow_v1
```

Each strategy table stores immutable material revisions. Partition by complete trade and position identity plus value date; cluster by material revision descending.

Common columns include:

- Portfolio, Fund, Order, Trade, Position, and ValueDate identity;
- plan/event/material revisions;
- source position/event/command/route sequences;
- strategy kind and position phase;
- trigger Contract ID, leg ID, price, sequence, and UTC time;
- complete position valuation and P&L;
- forward price, forward P&L, forward loss, probability, and calculation version;
- state, action, severity, condition, reason, and explanation;
- data quality and missing/stale-input reasons;
- parameter and material-policy identities, versions, and hashes;
- changed-field flags and canonical payload hash; and
- versioned MessagePack trade, position, and strategy-plan payloads.

Strategy-specific columns follow the complete schemas in the system design document.

### 11.4 Projection rules

For each committed completed event:

1. validate event identity, plan identity, value date, strategy kind, payload hash, and schema version;
2. return success without writing when `MaterialChange` is false;
3. insert the immutable material revision when true;
4. treat same key/same hash as idempotent success;
5. treat same key/different hash as a projection conflict;
6. update the activity-by-date table for bounded operational queries;
7. publish the UI material-update notification after successful projection; and
8. perform no Exit Position dispatch because the strategy PositionRealtimeActor owns that decision from the direct committed FunctionActor response.

No background database polling is introduced.

## 12. UI and query integration

### 12.1 Queries

Create strategy-specific QueryActors and queries:

```text
GetCurrentIronCondorTradePlanQuery
GetIronCondorTradePlanHistoryQuery
GetCurrentVerticalSpreadTradePlanQuery
GetVerticalSpreadTradePlanHistoryQuery
GetCurrentFuturesTradePlanQuery
GetFuturesTradePlanHistoryQuery
GetPositionExitWorkflowQuery
GetPositionExitWorkflowTimelineQuery
```

History queries require complete identity, bounded value-date/UTC range, page size, and paging token.

### 12.2 Trade-position monitoring UI

The UI consumes the strategy-specific current plan and material history. It displays:

- full ownership and position identity;
- strategy and leg structure;
- current leg and strategy values;
- current, realized, unrealized, and total P&L;
- forward price, forward P&L, forward loss, probability, and score;
- limits, targets, trailing-stop values, and expiry state;
- plan state, severity, exit condition, action, and explanation;
- market-data, calculation, parameter, and material-policy versions;
- last position sequence, plan event revision, material revision, and update UTC; and
- exit workflow, accepted close orders, execution, fills, and failure details when present.

Normal nonmaterial events remain in EventSourceDb and do not produce UI projection churn. UI freshness is calculated from the last material revision and the current position feed status rather than fabricating per-tick Trade Plan rows.

Migrate the existing Trade Plan query/event services and UI consumers to the new strategy-specific contracts before removing legacy services.

## 13. Exit Position workflow

### 13.1 Trigger

The dedicated extension handler of the strategy PositionRealtimeActor calls `UpdateXXXTradePlanCommand`, awaits the Trade Plan FunctionActor result, and examines the returned committed `XXXTradePlanUpdatedEvent`. It starts the strategy Exit Position workflow when all conditions hold:

- `CompletionState == ExitRequired`;
- `Plan.State == ExitRequired`;
- `RequiresExit == true`;
- the position remains open with nonzero remaining quantity;
- no equal or newer exit workflow is already active; and
- the exit workflow ID matches the deterministic identity.

A maximum-loss result bypasses all lower-priority rules inside the Trade Plan algorithm and sets `ExitAtMarket` before the snapshot is committed.

This is a direct request/result continuation. The PositionRealtimeActor does not wait for, query, or subscribe to the TradePlanDb projection. The update command ID is deterministic from the committed position event, and the exit workflow ID is deterministic from the committed exit-plan event, making duplicate delivery idempotent.

### 13.2 Strategy-specific workflow actors

Each strategy owns concrete actors and messages:

```text
IronCondorExitPositionWorkflowCommandActor
IronCondorExitPositionWorkflowRealtimeActor
IronCondorExitOrderCompositionFunctionActor
IronCondorPositionRiskManagementFunctionActor

VerticalSpreadExitPositionWorkflowCommandActor
VerticalSpreadExitPositionWorkflowRealtimeActor
VerticalSpreadExitOrderCompositionFunctionActor
VerticalSpreadPositionRiskManagementFunctionActor

FuturesExitPositionWorkflowCommandActor
FuturesExitPositionWorkflowRealtimeActor
FuturesExitOrderCompositionFunctionActor
FuturesPositionRiskManagementFunctionActor
```

The workflow root follows the strategy-workflow transition pattern but has only two calculation stages: Exit Order Composition and Position Risk Management. These actors reside in the strategy position's `Workflow/OrderComposer` and `Workflow/RiskManager` folders. FunctionActor completed events are persisted under the standard Function convention. The root records dispatch, accepted completion, failure, timeout, and final handoff state so an exit cannot disappear between stages.

### 13.3 Exit Order Composer

The composer receives the complete exit-state Trade Plan and position snapshot. It:

- verifies remaining quantity for every leg;
- creates one closing component per remaining leg;
- maps long quantity to sell-to-close and short quantity to buy-to-close;
- uses explicit close/reduce-only intent;
- preserves broker-neutral Contract IDs;
- preserves the originating Trade and StrategyPosition identity;
- assigns no Portfolio/Fund/Order/Trade business IDs;
- calculates limit or market instructions from the committed exit action;
- creates a deterministic composition hash; and
- completes with either one exact exit composition or a classified failure.

An iron condor normally produces four closing legs, a vertical spread two, and an outright futures position one. Partial fills and prior reductions use remaining quantities rather than original quantities.

### 13.4 Position Risk Manager

The Position Risk Manager is event-sourced and strategy-specific. It:

- validates Trade Plan revision, exit condition, composition hash, ownership, quantities, and Contract IDs;
- validates that every component reduces or closes existing exposure;
- prevents duplicate or overlapping close requests;
- records maximum-loss and emergency priority;
- creates the immutable Portfolio close-order request;
- sends it to Portfolio;
- validates the committed Portfolio completion; and
- records accepted close Trade Order identities or a comprehensive failure.

A valid maximum-loss close cannot become `NoTrade` because of ordinary entry-risk or capacity limits. Its allowed outcomes are `ExecuteExitOrders` or `Failed`. A failure is critical, visible in Actor Health and the position UI, and leaves the position open until execution evidence proves otherwise.

### 13.5 Downstream lifecycle

Portfolio remains responsible for atomic financial acceptance and Trade Order identity allocation. The existing common actors then perform:

```text
PortfolioOrderCompositionFunctionActor
  -> TradeOrderCommandActor
  -> TradeOrderEventProjector
  -> OrderExecutionCommandActor
  -> broker adapter
  -> AddOrderExecutionFillCommand
  -> established Trade close transition
  -> strategy Position close transition
  -> open-position route removal
```

The Trade Plan is updated to `ExitPending`, `ExitSubmitted`, `ExitPartiallyFilled`, `ExitFilled`, `ExitFailed`, and `Closed` through strategy-specific update commands caused by committed downstream events.

## 14. Actor mapping and implementation conventions

All actors must follow `Documents/system/Actor-Implementation-Conventions.md`:

- exact frozen parse, validation, receive, execution-policy, event, and exception maps as applicable;
- one extension-handler class per mapped command, query, event, or realtime message;
- extension filename equals the message name without `Command`, `Query`, or `Event` suffix;
- actor classes contain lifecycle and mapping only;
- business rules are explicit in deterministic order in the handler/model;
- unsupported exact CLR types fail closed;
- no assignable-type fallback or string-type switching;
- no fire-and-forget mapped task;
- complete XML documentation for public/internal actor extension methods;
- source-generated compile-time logging for hot-path logs; and
- all handled exceptions reported to `SupervisorRuntimeContext` with complete stage and identity detail.

Expected duplicate, stale, no-route, closed-position, and unchanged-plan outcomes are classified results rather than thrown exceptions.

## 15. Parameter sets

Create or extend Reference Data assignments:

```text
Parameter family: PositionTradePlan
Operator:
  IronCondor
  VerticalSpread
  Futures
Horizon/value-date policy: strategy-specific
```

Parameters include:

- material underlying-price and P&L policies;
- calculation tolerances;
- maximum loss and warning thresholds;
- profit targets;
- trailing-stop activation and increments;
- forward-loss limits;
- probability and Greek thresholds;
- expiry and roll thresholds;
- data freshness and minimum evidence;
- exit action selection; and
- calculation-version compatibility.

The FunctionActor resolves the immutable assignment when the value-date stream is created or when an explicit parameter-version update occurs. It persists the exact ID, version, and payload hash in every snapshot. It does not query Reference Data for every market tick.

## 16. Failure and recovery behavior

### 16.1 State recovery

- Restart loads the latest `XXXTradePlanUpdatedEvent` snapshot for the value-date stream.
- The loaded event restores complete plan, material baseline, parameter version, and exit identity.
- Later position updates converge prices and P&L from complete observations.
- No TradePlanDb read is used for recovery.

### 16.2 Projection recovery

- Projection receipts use source event ID, target table, material revision, and payload hash.
- Each strategy Position EventProjector declares its position event and strategy Trade Plan updated event as projected event types.
- Each strategy Trade Plan updated event implements `IRequireDurableProjection` with the exact owning actor and projector names, enables it only for material snapshots, and starts at `ApplyProjection`.
- EventSourceDb commits a material plan event and initial projector-state marker atomically in PostgreSQL; nonmaterial events do not create markers.
- The Function state repository submits the committed event collection to that existing durable projector immediately after EventSourceDb append.
- A failed projection leaves its receipt absent and the existing startup scan resubmits it from EventSourceDb.
- Replay writes the same immutable key and is idempotent.
- Replay also repairs a missing activity row when the immutable strategy plan row already exists with the same payload hash.
- Replays are event-driven and operator/supervisor observable; no two-second poller is created.

### 16.3 Exit recovery

- A committed `ExitRequired` snapshot is sufficient to recreate the deterministic exit workflow identity.
- Duplicate dispatch returns the existing workflow state.
- A completed Exit Order Composer result is replayed without recomposition.
- A completed Position Risk Manager result is replayed without a second Portfolio request.
- Portfolio and Trade Order identities prevent duplicate financial orders.
- A process crash before the exit snapshot commit produces no reported durable exit; the next position update recalculates.
- A crash after the exit snapshot commit is recovered from the committed event and durable workflow handoff.

### 16.4 Errors

Errors include:

- actor, message, command, and stream identity;
- strategy, Portfolio, Fund, Order, Trade, Position, and ValueDate;
- source position and plan revisions;
- update cause and current workflow stage;
- stable error code, type, reason code, message, and complete safe detail;
- exception type, inner exception chain, and stack trace in logs;
- parameter and calculation versions; and
- projection/replay or Portfolio handoff status.

No calculation exception is converted into a valid zero value.

## 17. Observability

Expose through `SupervisorRuntimeContext` and Actor Health:

- update commands received by strategy;
- snapshot loads, empty loads, and load latency;
- duplicate replay and conflict count;
- calculation duration and allocation;
- material versus nonmaterial completion count;
- plan append duration and failures;
- TradePlanDb projection duration, queue depth, oldest age, replay count, and conflicts;
- plan states by strategy;
- maximum-loss and exit-required count;
- exit workflow stage duration and failures;
- close compositions produced;
- Risk Manager and Portfolio handoff duration;
- accepted close orders and execution status; and
- end-to-end tick-to-exit-request latency.

Hot-path metrics use counters/histograms and source-generated structured logging. Normal per-tick success is not written as an information log.

## 18. Implementation gates

### Gate 0 - Freeze contracts and add failing architecture tests

1. Record this plan as the implementation authority.
2. Inventory legacy consumers and new empty scaffolds.
3. Add failing tests for folder ownership, actor maps, strategy-specific contracts, and forbidden generic Trade Plan dependencies.
4. Freeze new MessagePack numeric keys and enum values.
   `TradeOrderPositionType` is frozen as `Unknown = 0`, `Opening = 1`, `Closing = 2`; `TradeOrderDefinition.PositionType` is appended at MessagePack key 12 and schema version 3.
5. Record a compatibility manifest for legacy messages that remain readable during migration.

**Exit:** reviewers can identify every new contract, owner, stream, table, actor, and legacy retirement target.

### Gate 1 - Function snapshot-stream support

1. Add tests proving one stable Function stream accepts sequential commands.
2. Implement invocation-relative completed-state matching.
3. Use `LoadStateFromSnapshotAsync<TState, XXXTradePlanUpdatedEvent>` for every invocation.
4. Append at the loaded expected stream version.
5. Add the explicit committed-event-first projection policy without changing existing Function policies.
6. Update actor conventions and architecture verification.
7. Test duplicate, conflict, stale, concurrency, persistence failure, projection failure, and restart behavior.

**Exit:** a generic test fixture executes at least three distinct commands on one stream, loads only the latest full snapshot for each next command, and replays a duplicate without recalculation.

### Gate 2 - Shared strategy Trade Plan contracts

1. Add new full-ownership IDs.
2. Add enums, update commands, snapshot completed events, failed events, results, queries, and notifications.
3. Register MessagePack unions and numeric-key contracts.
4. Add canonical fingerprints and payload hashes.
5. Add contract round-trip and compatibility tests.

**Exit:** all contracts serialize, deserialize, validate, format, parse where required, and preserve complete identity and calculation evidence.

### Gate 3 - TradePlanDb schema and context

**Automated implementation complete.** The five additive schemas, idempotent/conflict behavior,
current/history/activity/timeline paging, equal-timestamp exit-stage ordering, and idempotent activity-row
repair pass against real ScyllaDB.

1. Implement read/write interfaces, context, CQL, schema context, and additive DDL.
2. Add the three strategy tables, activity table, and exit-workflow query table.
3. Register factory and host dependencies.
4. Add idempotent insert and conflict detection.
5. Add bounded/paged current and history reads.

**Exit:** empty-schema creation, repeated creation, insert, idempotent replay, conflict, latest, history, and paging integration tests pass against real ScyllaDB.

### Gate 4 - Iron Condor models

1. Port useful legacy rules into explicit ordered models.
2. Implement four-leg validation and valuation.
3. Implement inline Forward Trade Price.
4. Implement inline Exit Condition with maximum-loss priority.
5. Implement material comparison.
6. Replace hard-coded thresholds with immutable parameters.
7. Add golden payoff and legacy-intent comparison tests.

**Exit:** pure model tests cover normal, hold, warning, maximum loss, forward loss, trailing stop, profit, expiry, market reversal, gamma/delta risk, missing data, and invalid topology.

### Gate 5 - Iron Condor Trade Plan FunctionActor

1. Implement actor, context, state, repository, extensions, and durable Position EventProjector mapping.
2. Implement `IronCondorTradePositionRealtimeActor` with an exact receive map for committed `IronCondorPositionChangedEvent`.
3. Implement the dedicated `IronCondorPositionChanged` extension handler to create and call deterministic `UpdateIronCondorTradePlanCommand`.
4. Load the last snapshot on every FunctionActor invocation.
5. Calculate the complete plan inline.
6. Save and return one committed snapshot event per successful invocation.
7. Have the Realtime extension handler inspect that returned event and start the exit workflow only for committed `ExitRequired` state.
8. Submit committed material completions through the receipt-backed Position EventProjector independently of exit dispatch.
9. Emit UI notification only after material projection.

**Exit:** a real actor/NATS/PostgreSQL/Scylla integration test processes ordered, duplicate, stale, and conflicting position updates, restores the exact latest plan after actor restart, starts one deterministic exit workflow for `ExitRequired`, and proves a delayed or failed TradePlanDb projection cannot suppress the exit start.

### Gate 6 - Iron Condor UI and queries

**Query and UI-service boundary complete; desktop replacement blocked by the pre-existing legacy UI compile
break recorded in section 1.1.** No retired trade-command API will be restored to mask that migration issue.

1. Implement current/history QueryActor maps and handlers.
2. Update API/NATS clients and UI services.
3. Replace legacy iron-condor Trade Plan UI binding.
4. Display complete calculation and decision evidence.
5. Display stale/projection/exit status accurately.

**Exit:** a material plan update appears once in the UI with all calculated fields; a nonmaterial update creates no projection/UI churn; restart returns the same current material plan.

### Gate 7 - Iron Condor Exit Position workflow

1. Implement the strategy `Workflow` folder and workflow root command/event/realtime maps.
2. Implement event-sourced Exit Order Composer FunctionActor and pure close-composition model.
3. Implement event-sourced Position Risk Manager FunctionActor and Portfolio mapper.
4. Integrate Portfolio completion, TradeOrder creation, OrderExecution start, fills, and final position close.
5. Feed downstream material states back through `UpdateIronCondorTradePlanCommand`.
6. Add duplicate exit and partial-fill protection.

**Exit:** maximum-loss and normal exit scenarios produce one exact reduce-only close composition, one Portfolio decision, one set of Trade Orders, and one OrderExecution attempt per accepted order.

### Gate 8 - Vertical Spread implementation

Repeat Gates 4-7 with `VerticalSpreadTradePositionRealtimeActor`, its dedicated `VerticalSpreadPositionChanged` extension handler, Vertical Spread models, FunctionActor, table, projector, queries, UI, and strategy `Workflow` folder. Reuse infrastructure only; no Iron Condor calculation or schema type may appear in the Vertical Spread domain.

**Exit:** all two-leg debit/credit call/put scenarios and exits pass strategy-specific gates.

### Gate 9 - Futures implementation

Repeat Gates 4-7 for one-leg outright Futures. Integrate the raw-feed `FuturesRealtimeActor`, `FuturesTradePositionCommandActor`, and the distinct `FuturesTradePositionRealtimeActor` that invokes the Trade Plan FunctionActor and routes its committed result. Include multiplier, tick value, notional, margin evidence, roll, and expiry behavior.

**Exit:** long and short futures positions produce correct current/forward P&L and exact reduce-only exits.

### Gate 10 - Legacy migration and removal

1. Run Iron Condor shadow comparison without enabling exit dispatch.
2. Review material differences against legacy outputs.
3. Enable new Iron Condor UI projection.
4. Enable controlled exit workflow after acceptance.
5. Migrate every API/UI/service consumer.
6. Remove legacy `Domain.Trade/Option/Algorithm` runtime and tests that assert defective behavior.
7. Remove generic `Domain.Trade/Plan` runtime, forward-loss-limit bounded contexts, hosted service, and obsolete contracts only after caller inventory is empty.
8. Preserve historical schemas/readers through explicit compatibility or archival tooling.

**Exit:** no production route invokes the legacy algorithm or generic Trade Plan actors, and no live UI depends on legacy tables or messages.

### Gate 11 - Full qualification and release

1. Run unit, BDD, integration, verification, architecture, serialization, storage, and API tests.
2. Run BenchmarkDotNet before/after comparisons.
3. Run deterministic 100,000-update replay with duplicates and out-of-order messages. **Complete:**
   the verification test accepts all ordered updates, classifies injected stale inputs without exceptions,
   restores the final MessagePack snapshot into a fresh state machine, and continues with the next sequence.
4. Interrupt and restart FunctionActor, EventSourceDb connection, TradePlanDb projector, Exit workflow, Portfolio handoff, and OrderExecution at controlled boundaries.
5. Confirm all test hosts and brokers exit after tests.
6. Run a development live-feed soak, then a full trading-session qualification after owner acceptance.

**Exit:** all automated gates pass, no unexplained exception or projection backlog remains, and operational evidence demonstrates one exit order set per exit decision.

## 19. Test specification

### 19.1 Unit tests

- Strategy identity formatting, validity, and value-date partitioning.
- Command/event MessagePack round-trip and numeric keys.
- Snapshot state application and latest-event restoration.
- Invocation-relative duplicate and conflict behavior.
- Position and route sequence ordering.
- Iron Condor, Vertical Spread, and Futures topology validation.
- Current and forward valuation.
- P&L, maximum profit/loss, probability, and Greek rules.
- Maximum-loss priority over all lower rules.
- Trailing-stop state transitions.
- Material comparison and mandatory override fields.
- Exit composition signs and remaining quantities.
- Canonical fingerprints and deterministic IDs.

### 19.2 BDD scenarios

- Given the first open position update of a value date, a full material plan is saved and projected.
- Given a new normal update, the Function saves a snapshot and projects only when material policy matches.
- Given the same command twice, the second request returns the prior completion without a second event.
- Given the same command ID with different content, the request fails visibly.
- Given an option-leg move with unchanged underlying price but materially changed P&L, the plan is material.
- Given cumulative movement under a configured threshold, the last-material baseline eventually detects the threshold crossing.
- Given maximum loss, the completed plan is `ExitRequired/ExitAtMarket`.
- Given maximum loss, Exit Condition does not run lower-priority rules after the terminal decision.
- Given a normal or warning plan, no Exit Position workflow starts.
- Given an exit plan, exactly one strategy-specific Exit Position workflow starts.
- Given a partial prior reduction, Order Composer closes only remaining quantity.
- Given a duplicate exit trigger, Portfolio receives no second financial request.
- Given accepted fills, the plan and position progress to closed.
- Given failed exit execution, the plan remains open and displays complete critical failure detail.

### 19.3 Integration tests

- Real EventSourceDb snapshot load and expected-version append.
- Real Scylla schema/context/current/history/activity operations.
- Real actor supervisor, mailbox, NATS request/reply, event routing, and restart.
- Contract ID route fan-out to multiple positions.
- Position event to strategy update command.
- Committed plan event to material projection and UI notification.
- Committed exit plan to Exit Order Composer and Position Risk Manager.
- Position Risk Manager to PostgreSQL Portfolio atomic acceptance.
- TradeOrder to OrderExecution and fill-to-close lifecycle.
- Durable projector queue replay without polling.

### 19.4 Verification tests

- Every mapped message has one dedicated extension class.
- Map key/type parity and exact dispatch.
- No Forward Trade Price or Exit Condition actor exists.
- All strategy calculation types reside under the strategy `Plan/Model` folder.
- No strategy calculation model performs storage, actor, clock, or service calls.
- Trade Plan state never rehydrates from TradePlanDb.
- Every Trade Plan updated event is owned by the corresponding strategy Position EventProjector and has no direct Function projector.
- All plan IDs contain complete Trade/Position identity and value date.
- Exit composition never opens or increases exposure.
- No Databento or IBKR numeric instrument ID enters Trade Plan, Portfolio, or Trade Order contracts.
- No polling service is introduced.
- No empty catch or exception-to-zero behavior exists.
- No test host remains running after qualification.

### 19.5 Benchmarks

BenchmarkDotNet suites measure:

- latest-snapshot state load;
- Iron Condor four-leg calculation including inline forward and exit models;
- Vertical Spread calculation;
- Futures calculation;
- normal, material, and maximum-loss branches;
- plan comparison;
- MessagePack snapshot serialization with configured EventSourceDb compression;
- snapshot append;
- material Scylla projection;
- tick-to-position-to-plan actor latency; and
- exit-plan-to-RiskManager handoff latency.

Report mean, median, P95, P99, operations/second, bytes allocated/op, Gen0/1/2 collections, serialized size, and storage throughput. Compare against the legacy Iron Condor calculation and generic Trade Plan write where runnable.

## 20. Rollout and rollback

1. Install additive schemas and contracts with all new dispatch disabled.
2. Enable Iron Condor calculation in shadow mode and suppress exit dispatch.
3. Compare new snapshots, materiality, allocations, latency, and error classifications.
4. Enable Iron Condor TradePlanDb/UI projection.
5. Enable exit dispatch for a controlled development Portfolio.
6. Qualify Vertical Spread and Futures independently.
7. Remove legacy routes only after all consumers and operational gates pass.

Rollback disables new position-to-plan dispatch and exit dispatch while retaining additive events/tables for diagnosis. It must not delete committed financial orders, executions, fills, or Trade Plan history.

## 21. Acceptance criteria

Implementation is complete when:

1. Every accepted position update routes to exactly one matching strategy Trade Plan FunctionActor.
2. Every Function invocation loads its value-date stream from the latest `XXXTradePlanUpdatedEvent` snapshot.
3. A matching duplicate replays its completion, while a later command executes on the same stream.
4. Every successful invocation saves one complete strategy snapshot event.
5. Forward Trade Price and Exit Condition execute inline from pure strategy `Plan/Model` classes.
6. TradePlanDb and UI update only for material completions.
7. P&L and mandatory risk transitions cannot be hidden by an unchanged underlying asset price.
8. A maximum-loss breach produces a committed `ExitRequired/ExitAtMarket` snapshot before exit dispatch.
9. Normal, Hold, Warning, and Breached plans do not start an Exit Position workflow unless `RequiresExit` is true.
10. Each strategy has its own PositionRealtimeActor plus a `Workflow` folder containing its Exit workflow, Exit Order Composer, and Position Risk Manager pipeline actors.
11. Exit composition uses exact remaining quantities and only close/reduce actions.
12. Portfolio creates accepted Trade Orders atomically and downstream OrderExecution remains event-sourced.
13. The UI exposes complete material plan calculations, explanations, lineage, and exit progress.
14. Restart recovery uses EventSourceDb snapshots and never TradePlanDb state.
15. Projection and workflow recovery are event-driven and visible through Actor Health.
16. Legacy option algorithm and generic Trade Plan runtime have no remaining production consumers.
17. All required automated, benchmark, recovery, and soak gates pass with no unexplained exceptions or orphaned test processes.

## 22. Final actor trace

### 22.1 Legacy Option hierarchy migration

The former `TomasAI.IFM.Domain.Trade/Option` hierarchy is retired. IFM currently supports futures
options, so its remaining live behavior is owned by `TomasAI.IFM.Domain.Trade/Futures/Option`:

- `FuturesOptionTradeQueryActor` owns the established Iron Condor and Vertical Spread queries and the
  compatibility queries still used by the legacy UI and API clients;
- every supported query maps to one dedicated extension-handler class in `Futures/Option/Query`;
- `FuturesOptionTradeEventActor` owns the end-of-day Fund continuation and the compatibility
  spread-distribution continuation;
- both event messages map to dedicated extension-handler classes in `Futures/Option/Event`;
- compatibility query and event contracts now route to the futures-option mailbox names;
- the unregistered legacy Iron Condor algorithm and its bounded context are removed because the
  strategy-specific Plan FunctionActors now own plan calculation; and
- a Trade domain hierarchy convention test rejects compiled production types under either the
  redundant `Domain.Trade.Trade` namespace or the retired `Domain.Trade.Option` namespace.

The compatibility contracts remain until their UI and API consumers move to the strategy-specific
established-trade and Trade Plan queries. They no longer require a separate root Option runtime.

### Iron Condor

```text
FuturesTickTradeDataChangedEvent
  -> FuturesOptionRealtimeActor
  -> FuturesIronCondorTradePositionCommandActor
  -> committed IronCondorPositionChangedEvent
  -> IronCondorTradePositionRealtimeActor
       -> IronCondorPositionChanged extension handler
       -> IronCondorTradePlanFunctionActor
            IronCondorTradePlanAlgorithm
            IronCondorForwardTradePriceModel
            IronCondorExitConditionModel
            IronCondorTradePlanComparisonModel
       -> inspect returned committed IronCondorTradePlanUpdatedEvent
       -> IronCondorExitPositionWorkflowCommandActor, only for ExitRequired
  -> IronCondorTradePlanProjector independently for material UI state
  -> IronCondorExitOrderCompositionFunctionActor
  -> IronCondorPositionRiskManagementFunctionActor
  -> PortfolioOrderCompositionFunctionActor
  -> TradeOrderCommandActor
  -> OrderExecutionCommandActor
  -> FuturesOptionTradeCommandActor and FuturesIronCondorTradePositionCommandActor on fills/close
```

### Vertical Spread

```text
FuturesTickTradeDataChangedEvent
  -> FuturesOptionRealtimeActor
  -> FuturesVerticalSpreadTradePositionCommandActor
  -> committed VerticalSpreadPositionChangedEvent
  -> VerticalSpreadTradePositionRealtimeActor
       -> VerticalSpreadPositionChanged extension handler
       -> VerticalSpreadTradePlanFunctionActor
       -> inspect returned committed VerticalSpreadTradePlanUpdatedEvent
       -> VerticalSpreadExitPositionWorkflowCommandActor, only for ExitRequired
  -> VerticalSpreadTradePlanProjector independently for material UI state
  -> VerticalSpreadExitOrderCompositionFunctionActor
  -> VerticalSpreadPositionRiskManagementFunctionActor
  -> common Portfolio, TradeOrder, and OrderExecution actors
```

### Futures

```text
FuturesTickTradeDataChangedEvent
  -> FuturesRealtimeActor
  -> FuturesTradePositionCommandActor
  -> committed FuturesPositionChangedEvent
  -> FuturesTradePositionRealtimeActor
       -> FuturesPositionChanged extension handler
       -> FuturesTradePlanFunctionActor
       -> inspect returned committed FuturesTradePlanUpdatedEvent
       -> FuturesExitPositionWorkflowCommandActor, only for ExitRequired
  -> FuturesTradePlanProjector independently for material UI state
  -> FuturesExitOrderCompositionFunctionActor
  -> FuturesPositionRiskManagementFunctionActor
  -> common Portfolio, TradeOrder, and OrderExecution actors
```

This plan keeps the high-frequency normal path inside one strategy Trade Plan FunctionActor calculation, creates one recoverable full-state event per invocation, limits TradePlanDb/UI writes to material changes, and enters the heavier exit pipeline only when the completed plan requires an exit.
