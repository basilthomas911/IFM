# Trade Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.Trade` combines the event-sourced option-trade actor pipeline with trade query/command APIs, order and plan logic, position workflows, and intrinsic-time strategy orchestration.

The target separation of durable trade structure, strategy-specific realtime positions, mutable position persistence, immutable monitor history, and the legacy Trade Blotter boundary is defined in `Trade-Structure-Strategy-Position-Monitor-Design.md`. That design is authoritative for new trade-position and monitor work; this document continues to describe the currently implemented actor inventory.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.Trade/`. Every leaf includes all intermediate folders.

```text
Command/Api/
Docs/
Model/Strategy/
Option/Algorithm/CommandHandlers/
Option/Algorithm/Decorators/
Option/Algorithm/Model/LongIronCondor/
Option/Algorithm/Model/ShortIronCondor/
Option/Algorithm/ValidationRules/
Option/Command/Actor/
Option/Command/Exceptions/
Option/Command/State/
Option/Command/Validation/
Option/Event/Actor/
Option/Event/Extensions/
Option/Query/Actor/
Order/CommandHandlers/
Order/Decorators/
Order/Exceptions/
Order/Execution/BrokerOrder/
Plan/CommandHandlers/
Plan/Decorators/
Plan/ForwardLossLimit/CommandHandlers/
Plan/ForwardLossLimit/Decorators/
Plan/ForwardLossLimit/Validation/
Plan/Models/
Plan/QueryHandlers/
Plan/Validation/
Position/Monitor/Futures/Option/IronCondor/
Position/Monitor/Futures/Option/VerticalSpread/
Position/Workflow/ExitCondition/Advisor/
Position/Workflow/ForwardTradePrice/
Position/Workflow/OrderComposer/Advisor/
Position/Workflow/PortfolioRisk/
Queries/Handlers/
Queries/Model/
Query/Api/
Strategy/IntrinsicTime/Futures/Option/IronCondor/Candidate/Advisor/
Strategy/IntrinsicTime/Futures/Option/IronCondor/Candidate/Builder/
Strategy/IntrinsicTime/Futures/Option/IronCondor/Candidate/Comparison/
Strategy/IntrinsicTime/Futures/Option/SpreadEngine/OptionChain/
Strategy/IntrinsicTime/Futures/Option/VerticalSpread/
Strategy/IntrinsicTime/Workflow/Command/Actor/
Strategy/IntrinsicTime/Workflow/Event/
Strategy/IntrinsicTime/Workflow/MarketCondition/
Strategy/IntrinsicTime/Workflow/OrderComposer/Advisor/
Strategy/IntrinsicTime/Workflow/OrderComposer/Comparison/
Strategy/IntrinsicTime/Workflow/PortfolioRisk/
Strategy/IntrinsicTime/Workflow/Query/
Strategy/IntrinsicTime/Workflow/RegimeDiscovery/Advisor/
Strategy/IntrinsicTime/Workflow/TradeSelector/Advisor/
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

`bin/` and `obj/` are generated; `net8.0` leaves are legacy output. Several empty workflow leaves are explicitly preserved by the project file as future extension points.

## Folder responsibilities

- `Command/Api/` exposes actor-backed trade commands.
- `Query/Api/` exposes actor-backed/storage-backed trade reads; `Queries/` contains the general `TradeQueryActor`, handlers, and query models.
- `Model/Strategy/` contains domain-level strategy data.
- `Option/Command`, `Option/Event`, and `Option/Query` implement the option-trade actor vertical. `Option/Algorithm` contains decorators, handlers, validation rules, and iron-condor models used to compose trades.
- `Order/` contains order command handling, decoration, execution boundaries, and exceptions.
- `Plan/` contains plan mutation/query handlers, decorators, validation, models, and forward-loss-limit logic.
- `Position/` reserves and implements monitoring/workflow components for exits, pricing, order composition, and portfolio risk.
- `Strategy/IntrinsicTime/Futures/Option/` contains candidate selection and spread construction structures.
- `Strategy/IntrinsicTime/Workflow/` contains the workflow command actor and staged market-condition, regime, selection, composition, and risk folders.
- `Docs/` contains this document; `TradeActorAssembly` supports discovery.

## Implemented actors

`OptionTradeCommandActor`, `OptionTradeEventActor`, and `OptionTradeQueryActor` implement the option-trade lifecycle. `TradeQueryActor` handles broader trade queries. `ActorTradeCommandApi` and `ActorTradeQueryApi` expose domain operations. `ItiStrategyWorkflowCommandActor` exists as the intrinsic-time workflow command entry point and should be reviewed alongside its currently scaffolded workflow branches.

## Processing model

Option-trade commands use the shared event-source actor lifecycle: subject parsing, validation, state restoration, typed execution, persistence, event publication, and standardized error handling. Query actors delegate to query handlers/storage. Algorithm, plan, order, position, and strategy components are invoked behind the actor boundary so mailbox serialization protects aggregate changes while computational helpers remain independently testable.

## Extension points

Keep aggregate mutations in actor command paths and pure selection/pricing rules in algorithm or advisor folders. New workflow stages belong beneath the intrinsic-time workflow root. Populate scaffold folders only when the stage has a clear contract and actor transition.

## Solution-wide cancellation status

- The coordinated solution-wide cancellation pass now covers both Trade query actors, their handlers/query model, all 15 direct in-process IActorTradeQueryApi operations, the Trade read-storage contract, and the concrete database calls used by those paths. Canceled actor reads publish no stale reply, while direct API cancellation remains an OperationCanceledException.
- Hydrated option-trade graphs propagate one token through bounded sibling-trade and distinct-date fan-out, including positions, option legs, leg data, trade limits, type limits, fills, and fill data. The existing maximum concurrency of four remains unchanged.
- Command validation/replay already honors cancellation. Event persistence and required denormalization retain the shared non-cancelable commit boundary so a durable mutation is never reported as safely abandoned.
- The inactive `Option/Algorithm/AlgorithmBuilder` path now awaits remote inputs end to end and constructs rule delegates from the materialized results. Its registrations remain commented out because the workflow itself is not functionally complete.
- `ItiStrategyWorkflowCommandActor` and its empty workflow branches remain intentional extension scaffolding. Empty command/event actor implementations must not be removed solely because they currently do no work.
