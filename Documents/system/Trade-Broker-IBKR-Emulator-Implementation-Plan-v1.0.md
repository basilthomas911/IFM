# IBKR Trade Broker Emulator: Detailed Implementation Plan v1.0

**Status:** Planned; no implementation in this document<br>
**Sequence:** Plan 1 of 2; complete, test, review and accept before starting the [IBKR Live Adapter plan](Trade-Broker-IBKR-Live-Adapter-Implementation-Plan-v1.0.md)<br>
**Target:** .NET 10; actor messages use versioned MessagePack; internal event/financial authority is PostgreSQL<br>
**Account environment:** one active synthetic cash-backed Emulator account; no TWS connection or real/paper broker mutation

## 1. Inputs, outcome and release boundary

This plan implements the Emulator half of the [Trade Broker Architecture Design](Trade-Broker-Application-and-Framework-Architecture-Design-v1.0.md) and [BrokerOrder/Accounting Specification](Trade-Broker-BrokerOrder-Execution-and-Accounting-Specification-v1.0.md). Implementation follows [Actor Implementation Conventions](Actor-Implementation-Conventions.md) and [compile-time structured logging convention](System-Wide-Telemetry-and-Distributed-Tracing-Design.md#811-compile-time-structured-logging-convention). The emulator is an operational, selectable broker mode with a coherent synthetic order/account ledger. The [scripted broker test harness](../../TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers/Docs/ScriptedBrokerTestHarnessSpecification.md) remains a separate **test-only** facility; it cannot be registered as a runtime broker.

The completed Emulator release must take a Portfolio-approved Opening or Closing `TradeOrderDefinition` from strategy workflow, exit workflow or manual UI, start OrderExecution, create one logical BrokerOrder per executable component, commit place/update/cancel intents, dispatch through application `ITradeBroker` to the emulator framework ports, return normalized acknowledgement/fill/commission/failure/account events through actor mailboxes, and hand verified executions to Trade/Position and Portfolio/Fund accounting. It must expose the synthetic cash account, gates, order evidence, discrepancies and actor health. It must never create Fund settlement merely from an order receipt or invented favorable fill.

Shared contracts and actors built here are the **same** contracts and actors that the later Live adapter will use. The live plan may add provider-specific framework modules and account qualification but may not fork the business lifecycle, actor maps, financial book or micro-execution policy by broker mode.

The current baseline has shell `Framework.TradeBroker` projects, no Application TradeBroker project or `Trade/Order/Broker` actors, and a local-only OrderExecution state machine. Gate E0 inventories exact solution/project references, existing versions and actor routes before code. This plan makes those gaps explicit and does not treat old manual Trade Order screens as broker execution.

## 2. Project layout and dependency direction

```text
TomasAI.IFM.Application.TradeBroker/
  Contracts/ITradeBroker.cs
  Contracts/Models/{BrokerOrder,Dispatch,Observation,Account,Gate,Health,Error}.cs
  Mapping/{Order,Account,Observation}Mapper.cs
  Hosting/TradeBrokerObservationBridge.cs
  Configuration/TradeBrokerOptions.cs
  InteractiveBrokersEmulatorTradeBroker.cs
  Logging/ApplicationTradeBrokerLogging.cs

TomasAI.IFM.Framework.TradeBroker/
  Contracts/IFrameworkOrderExecutionBroker.cs
  Contracts/IFrameworkBrokerAccount.cs
  Contracts/Models/{Order,Account,Observation,Health,Error}.cs

TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator/
  Engine/{EmulatorLedger,EmulatorClock,DeterministicScheduler,Scenario,Journal}.cs
  OrderExecution/{EmulatedOrderExecutionBroker,OrderMatcher,ComboMatcher,Correlation}.cs
  BrokerAccount/{EmulatedBrokerAccount,SnapshotAssembler,ConservativeFundingModel}.cs
  Logging/{EmulatorOrderLogging,EmulatorAccountLogging}.cs

TomasAI.IFM.Domain.Trade/Order/Broker/
  Command/Actor/{BrokerOrderCommandActor,BrokerOrderCommandContext}.cs
  Command/State/{BrokerOrderCommandState,BrokerOrderStateRepository}.cs
  Command/Validation/*.cs
  Command/EventProjector/BrokerOrderEventProjector.cs
  Command/<one message handler file per command>.cs
  Event/Actor/{BrokerOrderEventActor,BrokerOrderEventContext}.cs
  Event/<one message handler file per event>.cs
  Query/Actor/{BrokerOrderQueryActor,BrokerOrderQueryContext}.cs
  Query/<one message handler file per query>.cs
  Realtime/Actor/{BrokerOrderRealtimeActor,BrokerOrderRealtimeContext}.cs
  Realtime/BrokerExecutionQuoteUpdated.cs
  Model/{MicroExecutionPolicy,Constraints,Price,DecisionState,Correlation}.cs
  Logging/{BrokerOrderCommandLogging,BrokerOrderEventLogging,
           BrokerOrderQueryLogging,BrokerOrderRealtimeLogging}.cs

TomasAI.IFM.Domain.TradeBroker/BrokerAccount/{Command,Event,Query,Model,Logging}/
TomasAI.IFM.Domain.Trade/Order/Execution/{Command,Event,Query,Model}/
TomasAI.IFM.Domain.Portfolio/{GeneralLedger,Fund,OrderComposition}/
```

`Domain` references application contracts, never concrete emulator types or `IBApi`; Application references only the two provider-neutral Framework ports/models; Emulator references Framework ports and a provider-neutral market snapshot contract. The API/host composition root selects both Emulator framework ports from **one** ledger/clock and constructs exactly one Application `ITradeBroker` plus one observation bridge. Mixed modes/accounts fail startup. The emulator assembly must have no IBKR C# API reference, credential loading or TWS socket creation. The existing `Domain.TradeBroker` stub/project name is reconciled before BrokerAccount actors are added, without creating a second `Trade` folder under Domain.Trade.

The namespace for the new broker order actors is `TomasAI.IFM.Domain.Trade.Order.Broker`, matching the requested hierarchy. BrokerAccount logging may use `BrokerAccountCommandLogging`, `BrokerAccountEventLogging` and `BrokerAccountQueryLogging` as needed; changed OrderExecution handlers use a dedicated `OrderExecutionLogging` declaration class. Source-generated logging remains declaration-only in all of these classes.

## 3. Contract and schema gate

At E1 freeze append-only MessagePack keys and union discriminators for approved TradeOrder account alias/environment, execution envelope, source Portfolio approval ID, account promotion approval reference, micro-execution profile ID/version/hash and pilot-compatible bounds. Older immutable TradeOrders remain readable but cannot enter paper/live dispatch without required fields. Define `BrokerOrderId = OrderExecutionId + ComponentId`, stable `OperationId`, normalized `ObservationId`, exact string `ContractId`, `ExecutionAttemptId`, and full `TradeEntityId`. Reuse `BrokerEnvironment { Unknown, Emulator, Paper, Live }` and `TradeOrderPositionType { Unknown=0, Opening=1, Closing=2 }`; `LivePilot` is an approval scope, not another account environment.

Implement `ITradeBroker` for submit, price-only modify, individual cancel, order/account reconciliation, immutable latest account/position reads, capability/gate reads, account resynchronization and one observation stream. Framework contracts remain **two ports**: `IFrameworkOrderExecutionBroker` and `IFrameworkBrokerAccount`. Application and Framework DTOs are separate immutable types with mapper round-trip tests for every field. Dispatch receipts distinguish `RejectedLocally`, `AcceptedForDispatch`, `OutcomeUnknown`; receipt acceptance never means broker acknowledgement. Order/status/fill/commission/account events are closed versioned unions and retain source, epoch, sequence, UTC times and correlation. `Unknown` values are invalid for approval or completion.

The canonical order-shape manifest initially supports one-leg ES futures limit orders, two-leg vertical-spread futures-option BAG-equivalent orders and four-leg iron-condor futures-option BAG-equivalent orders, Opening and Closing. Multiple executable components under one TradeOrder each have a BrokerOrder stream but share an OrderExecution aggregate; unsupported mixed/custom orders fail locally with classified detail. Exact overall combo limit sign convention, tick alignment, cost/currency rounding and available market-evidence requirements are frozen in E0/E1 fixtures before matching code is written. Emulator economic behavior is declared and labelled; it never asserts IBKR fill/margin equivalence.

## 4. Required actor inventory and mapping contracts

### 4.1 BrokerOrder CommandActor

`BrokerOrderCommandActor` inherits only `BaseEventSourceCommandActor<BrokerOrderCommandActor>`. It has frozen `_parseMap` (serialized `Verb` -> `AsCommand<T>()`), `_validationMap` (exact `typeof(T)` -> accumulated `List<ValidationError>`), and `_receiveMap` (exact `typeof(T)` -> dedicated extension). The three type sets must match exactly; unknown verb/type fails closed. `ParseMessage`, `OnValidateAsync` and `ReceiveAsync` delegate to `ParseMappedCommand`, `ValidateMappedCommand` and `ResolveMappedCommandHandler`. Typed context owns repository, projector, actor messaging, `ITradeBroker` account snapshot/gate access and pure Models; the actor class contains no business switch. One stream is keyed by `BrokerOrderId` and loads a bounded latest durable state/checkpoint, not the whole execution stream per market quote.

| Mapped concrete Command | Dedicated file/class in `Broker/Command` | Main business guard and state change |
| --- | --- | --- |
| `InitializeBrokerOrderCommand` | `InitializeBrokerOrder` | Exact approved order/component/account/profile and one attempt; no-op if identical initialization. |
| `RequestPlaceBrokerOrderCommand` | `RequestPlaceBrokerOrder` | Approved order, gate, synthetic account, shape, not-after, capital/envelope; persist PlaceRequested once per operation/hash. |
| `RequestUpdateBrokerOrderCommand` | `RequestUpdateBrokerOrder` | Known broker ack/revision, price-only within approved envelope, no in-flight mutation; persist UpdateRequested. |
| `RequestCancelBrokerOrderCommand` | `RequestCancelBrokerOrder` | Known logical order/operation, cancel-capable gate; persist CancelRequested without claiming cancelled. |
| `RecordBrokerDispatchReceiptCommand` | `RecordBrokerDispatchReceipt` | Exact operation/hash; accepted-for-dispatch, rejected-local or unknown fact; never broker acceptance. |
| `RecordBrokerOrderAcknowledgementCommand` | `RecordBrokerOrderAcknowledgement` | Source/epoch/account/order echo/revision match; persist ack or classified conflict. |
| `RecordBrokerExecutionObservationCommand` | `RecordBrokerExecutionObservation` | Exact external exec ID/leg/contract/quantity/price; same ID/hash no-op, conflict quarantined. |
| `RecordBrokerCommissionObservationCommand` | `RecordBrokerCommissionObservation` | Join exact exec ID or hold pending; corrections append, no invented zero fee. |
| `RecordBrokerOrderFailureCommand` | `RecordBrokerOrderFailure` | Typed definitive versus ambiguous failure and current exposure; persist detail/next status. |
| `RecordBrokerReconciliationCommand` | `RecordBrokerReconciliation` | Current generation/completeness, order/fill/fee truth, no blind resend. |
| `EvaluateMicroExecutionCommand` | `EvaluateMicroExecution` | Material decision fingerprint, allowed-action mask and exact approved limit; no event for unchanged Wait. |

The command handler method is `Execute` or awaited `ExecuteAsync`; its class and file omit only `Command`. XML documentation covers the class and **all** created methods, including private helpers. Each handler evaluates explicit ordered state-dependent business rules, returns `UpdateFailed`/typed `ServiceFailed<GuidResult>` for rejected rules, constructs its own private event, then calls `state.Update(event, command)` through `UpdatedOk` only if the concrete state's `Apply` returns `true`. A routine duplicate, stale observation or unchanged calculation returns a successful no-op **before** event construction. Do not use a generic transition delegate, transient dedupe set or partial handler class to hide these rules.

### 4.2 BrokerOrder EventActor and RealtimeActor

`BrokerOrderEventActor` derives directly from `BaseEventActor<BrokerOrderEventActor>`, declares exact `_parseMap` and `_receiveMap`, and calls `ParseMappedEvent`/`ResolveMappedEventHandler`. Each event handler returns an awaited `ValueTask<bool>`, receives typed `IEventActorContext` and logger, sends actor Commands and logs every real handler failure before preserving exception/failure behavior. One EventActor can own committed mutation dispatch and inbound normalized broker observations; it cannot write BrokerOrder state by a direct method call. The EventProjector's durable receipt is complete only after the correlated actor/dispatch handoff is durably recorded.

| Event message | Dedicated file/class in `Broker/Event` | Actor message/action |
| --- | --- | --- |
| `OrderExecutionChangedEvent` | `OrderExecutionChanged` | From committed OrderExecution notification, initialize/request only the specifically authorized transition; ignore fill-generated revisions as new Place. |
| `BrokerOrderPlaceRequestedEvent` | `BrokerOrderPlaceRequested` | Map to `ITradeBroker.SubmitOrder` and send receipt Command; no socket assumption. |
| `BrokerOrderUpdateRequestedEvent` | `BrokerOrderUpdateRequested` | Map to `ModifyOrderLimit`, record receipt. |
| `BrokerOrderCancelRequestedEvent` | `BrokerOrderCancelRequested` | Map to individual Cancel, record receipt. |
| `BrokerOrderAcknowledgedEvent` | `BrokerOrderAcknowledged` | Normalize exact echo, send acknowledgement Command. |
| `BrokerExecutionReportedEvent` | `BrokerExecutionReported` | Send execution-observation Command with exact exec/leg ID. |
| `BrokerCommissionReportedEvent` | `BrokerCommissionReported` | Send commission-observation Command. |
| `BrokerOrderFailureReportedEvent` | `BrokerOrderFailureReported` | Send classified failure Command with source/operation/detail. |
| `BrokerOrderReconciledEvent` | `BrokerOrderReconciled` | Send reconciliation Command with completeness. |
| `BrokerAccountGateChangedEvent` | `BrokerAccountGateChanged` | Material gate transition; evaluate/hold current working orders, do not create a fresh Place from this event alone. |

One hosted `TradeBrokerObservationBridge` consumes the application observation stream and publishes these concrete Event messages losslessly for critical order/fill/fee/gate facts. It validates source/epoch/account, redacts private IDs, and closes readiness on queue overflow/gap; it does not call OrderExecution directly or write PostgreSQL on an emulator callback thread. BrokerOrder's committed evidence projector then sends stable-ID `ConfirmOrderExecutionSubmissionCommand`, existing `AddOrderExecutionFillCommand`, new fee correction/failure Commands, or `AcceptOrderExecutionCommand` **only when** verified acceptable individual leg fills prove completion. Cancel/failure status alone cannot release exposure. Same-source EventId receipts prevent a BrokerOrder ↔ OrderExecution event cycle.

`BrokerOrderRealtimeActor` is the primary Core realtime destination for a provider-neutral, coalesced `BrokerExecutionQuoteUpdatedRealtimeEvent` published only for currently working approved contract routes. Its `_parseMap` and `_receiveMap` have exact parity, use `ParseMappedRealtimeEvent` and `ResolveMappedEventHandler`, and route to the dedicated `Broker/Realtime/BrokerExecutionQuoteUpdated.cs` extension. That handler reads an immutable hot working-order index, checks contract, source sequence, freshness, minimum cadence and material price change, then sends **at most one** `EvaluateMicroExecutionCommand` per eligible decision fingerprint. It ignores other ticks without logging or queuing them; it never reads EventSourceDb, calls the broker or writes a log for each ignored tick. A bounded one-shot busy/free guard prevents overlapping command generation. The hot index is rebuilt from committed working-order events on startup and updated at status changes; it is never the sole authority for durable order state. Failure paths use source-generated Error logs.

### 4.3 BrokerOrder QueryActor

`BrokerOrderQueryActor` derives directly from `BaseQueryActor<BrokerOrderQueryActor>`. `_parseMap` maps verbs, `_receiveMap` maps exact query types to dedicated files, and `_exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys)` has identical type coverage. `ParseMappedQuery`, `ResolveMappedQueryHandler` and `ExceptionMappedQueryAsync` own malformed ingress, typed replies and failures; no query executes a mutation or repeats command-domain validation.

| Query | Dedicated file/class in `Broker/Query` | Result |
| --- | --- | --- |
| `GetBrokerOrderQuery` | `GetBrokerOrder` | Versioned current logical broker order, account/environment, evidence quality. |
| `GetBrokerOrderOperationsQuery` | `GetBrokerOrderOperations` | Paged Place/Update/Cancel intents and dispatch/ack receipts. |
| `GetBrokerOrderEvidenceQuery` | `GetBrokerOrderEvidence` | Exact leg fills/fees/reconciliation and source IDs by date range. |
| `GetBrokerOrderMicroExecutionQuery` | `GetBrokerOrderMicroExecution` | Paged decision history/profile/version/action mask/reasons. |

### 4.4 BrokerAccount actors

Correct the existing `Domain.TradeBroker` stub/project boundary and add **Command, Event and Query actors**, with typed contexts and Models. No BrokerAccount Realtime or Function actor is required: account callbacks are material normalized Event facts, approval is a durable Command, and account reads are Query/immutable snapshot operations. `BrokerAccountCommandActor` has the same three-map parity/validation conventions as BrokerOrder; `BrokerAccountEventActor` has exact parse/receive maps; `BrokerAccountQueryActor` has parse/receive/exception maps. Every mapped message gets its own role-folder file/class:

| Actor role | Required message -> dedicated handler file (suffix removed) |
| --- | --- |
| Command | `RecordBrokerAccountSnapshotCommand` -> `RecordBrokerAccountSnapshot`; `RecordBrokerAccountGateCommand` -> `RecordBrokerAccountGate`; `RecordBrokerAccountDiscrepancyCommand` -> `RecordBrokerAccountDiscrepancy`; `RequestBrokerAccountResynchronizationCommand` -> `RequestBrokerAccountResynchronization`; `SetManualTradingHoldCommand` -> `SetManualTradingHold`; `ReleaseManualTradingHoldCommand` -> `ReleaseManualTradingHold`; `SubmitAccountQualificationEvidenceCommand` -> `SubmitAccountQualificationEvidence`; `AcceptAccountQualificationCommand` -> `AcceptAccountQualification`; `RevokeAccountQualificationCommand` -> `RevokeAccountQualification`; `IssuePaperQualificationAuthorizationCommand` -> `IssuePaperQualificationAuthorization`. |
| Event | `BrokerAccountSnapshotObservedEvent` -> `BrokerAccountSnapshotObserved`; `BrokerAccountGateObservedEvent` -> `BrokerAccountGateObserved`; `BrokerAccountDiscrepancyObservedEvent` -> `BrokerAccountDiscrepancyObserved`; `BrokerConnectionChangedEvent` -> `BrokerConnectionChanged`; `AccountQualificationEvidenceSubmittedEvent` -> `AccountQualificationEvidenceSubmitted`; `AccountQualificationAcceptedEvent` -> `AccountQualificationAccepted`; `AccountQualificationRevokedEvent` -> `AccountQualificationRevoked`. |
| Query | `GetBrokerAccountSnapshotQuery` -> `GetBrokerAccountSnapshot`; `GetBrokerAccountGateQuery` -> `GetBrokerAccountGate`; `GetBrokerAccountDiscrepanciesQuery` -> `GetBrokerAccountDiscrepancies`; `GetAccountQualificationQuery` -> `GetAccountQualification`; `GetConfiguredBrokerAccountQuery` -> `GetConfiguredBrokerAccount`. |

Qualification evidence is submitted by a runner but **accepted only by an authorized human-facing command** after review. Emulator qualification can use isolated synthetic tests before operational Emulator acceptance. Paper test authorization is implemented as a shared contract/actor gate here, but it cannot be exercised until Emulator acceptance and the later Live plan. The account gate is distinct from adapter readiness and per-order Portfolio risk approval; an approval record is version/account/profile bound, revocable and persisted. Missing/invalid approval, mismatch or critical account gap closes new-risk without blocking possible known-order cancel/reconcile.

### 4.5 Existing actors to change

Update `TradeOrderCommandActor`/projector only to append frozen account/envelope/approval fields and to start execution from the accepted order as before. Update `OrderExecutionCommandActor` and its dedicated per-message handler files: introduce requested/locally dispatched/broker acknowledged/Partial/CancelPending/Cancelled/Rejected/OutcomeUnknown states; add `ConfirmOrderExecutionSubmissionCommand`, `UpdateOrderExecutionFillCostCommand` and `RecordOrderExecutionFailureCommand`; retain existing `AddOrderExecutionFillCommand`, `AcceptOrderExecutionCommand`, `CancelOrderExecutionCommand`, and `RejectOrderExecutionCommand` with corrected evidence guards. Replace its combined handler class for any touched receive entries with one class/file per concrete Command. A late correlated execution after cancel acknowledgement is accepted and reconciled, not thrown away. Exact duplicate fills are successful no-ops with no new pending event; same ID/different payload is a classified conflict. Established Trade/Position and Fund handoffs occur only after balanced approved fill evidence, never on emulator status text alone.

## 5. Command validation, state guards and error handling gate

For **every** BrokerOrder/BrokerAccount/changed OrderExecution Command `_validationMap` entry, build one visible `List<ValidationError>` in convention order: `ValidateCommandId`; intrinsic `EntityId` validation; scalar/enum/UTC/version/payload property checks; cross-check repeated IDs, account aliases, hashes, leg/component identities and exact subject/entity relation only where the base has not already checked routing. Use domain `Command/Validation` list extensions for simple checks and FluentValidation with `BaseValidationRules` for structured reference payloads. Null payloads append errors; ordinary invalid input does not throw or stop on first failure. `ValidateMappedCommand` throws **one** aggregate `CommandValidationException` after all deterministic errors; this occurs after command-audit reservation and before state load. No validation-map delegate opens a DB, reads account state, performs price policy or calls the emulator. Actor `OnExceptionAsync` uses the common boundary and a generated structured Error log for unexpected exceptions; it never manufactures a broker rejection from a timeout.

State-dependent rules live in the dedicated command handler. Required small guard methods include `CanInitialize`, `CanRequestPlace`, `CanRequestUpdate`, `CanRequestCancel`, `IsSameOperation`, `CanRecordAck`, `CanApplyExecution`, `CanApplyCommission`, `CanApplyReconciliation` and `HasMaterialMicroExecutionChange`; they are pure checks over reconstructed state plus immutable authoritative account/gate facts. Use a method only where it clarifies a repeated or nontrivial rule; simple single-command checks stay inline. Guards distinguish: no-op exact replay, classified business decline, and unexpected invariant conflict. Durable OperationId/hash, observation ID/hash and external exec ID/hash are rehydrated; transient collections cannot prove deduplication.

The concrete `BrokerOrderCommandState.Apply(IEvent)` returns `true` **only** after it has validated and applied an actual state transition, and checks all reject conditions before mutating fields. `false` adds no pending event in the base class. Each handler tests `state.Update` and returns `UpdatedOk` only on `true`. An expected duplicate/unchanged result avoids `Update` entirely. A `false` after successful preflight is an invariant failure (`UpdateFailed`/logged) rather than a success; if a command creates several events, preflight all rules and abort the entire command on any later rejection. Unit tests assert pending-event counts and replay equality for every accepted, ignored and failed path. Repository loading uses the latest necessary snapshot/checkpoint and bounded correlated evidence, not full-stream replay per quote.

Error data is typed and complete: stage, severity, definitive/unknown outcome, `BrokerOrderId`, full TradeOrder/attempt, OperationId, account alias/environment, source epoch/observation, relevant ContractId, safe category/message, UTC time and exposure completeness. The BrokerOrder evidence handoff preserves this in `RecordOrderExecutionFailureCommand` and subsequent UI/workflow projections. Expected missing quote, closed gate, foreign callback, duplicate or unsupported shape yields a result/diagnostic, **not** a thrown exception. Real Event/Realtime handler failures are logged at their owning extension boundary before the base failure path handles them. Retry is bounded by durable intent receipts; there is no 2-second projector poll or blind external resend.

## 6. Emulator framework engine and application facade gate

Build a shared versioned `EmulatorScenario`: starting synthetic cash by currency, Fund allocation references, conservative funding/margin assumptions, contract/reference catalog, commissions/fees, market execution profile, acknowledgement/fill/modify/cancel/account latency, partial-fill rules, seeded random option when enabled, deterministic UTC/manual clock, and declared faults. The engine's append-only journal/checkpoint contains orders, operations, executions, fees, positions, cash balances, account generation/epoch, scenario version and ledger hash. One ordered scheduler emits callbacks after a durable ledger mutation. Same scenario + starting ledger + market input + ordered operations + clock/seed reproduces event IDs, normalized observation order and final hash. Restart recovers journal/checkpoint without calling an external broker.

`EmulatedOrderExecutionBroker` validates exact broker-neutral order shape and account/environment, persists an OperationId/hash correlation before emulated dispatch, returns **local** receipt and later emits broker acknowledgement/status/exec/commission/failure observations. `EmulatedBrokerAccount` reads the same engine to publish immutable account snapshot versions and typed cash/available funds/positions/P&L, with complete-versus-incomplete evidence markers. Every synthetic fill and fee changes the same ledger before the next coherent snapshot; there is no order-only fake broker and no cross-account cash transfer on mode switch. Funding/margin figures are labelled conservative synthetic approximations; absent data is `Unknown`/`NotApplicable` only under explicit rules.

Level-1 execution requires exact ContractId, last/bid/ask/size/time/sequence/freshness evidence from provider-neutral MarketData. One-leg futures matching uses a documented crossing and bounded available size. Vertical/iron-condor combos require a coherent multi-leg quote snapshot and one versioned overall combo price convention; unrelated single-leg quotes cannot be combined into a guaranteed fill. No quote, stale quote, conflicting contract fingerprint, insufficient synthetic cash/collateral or unapproved limit results in Working/Rejected/Unavailable according to declared facts, **never** favorable default fills. Fill reports carry per-leg exec IDs and signed quantities and must reconcile to approved combo ratios. Scenario faults cover delayed/duplicate/out-of-order callbacks, fee delay/correction, cancel/fill race, unknown dispatch, account gap, disconnect/reconnect, partial/unbalanced fill and crash at every intent/correlation/ledger/actor boundary.

The Application `InteractiveBrokersEmulatorTradeBroker` maps to both framework ports, checks their identical emulator instance/account/generation, merges order/account observation streams once and offers lock-free immutable latest account reads. If either framework stream loses a critical fact, readiness/gate becomes degraded and BrokerAccount/BrokerOrder receive an incident; telemetry may be coalesced only with an explicit gap marker. `ITradeBroker` does not submit automatically from market ticks; only a committed BrokerOrder mutation event invokes Submit/Modify/Cancel.

## 7. Micro-execution and Portfolio/Fund accounting gate

Implement the pure, generic `IMicroExecutionPolicy`, `IMicroExecutionConstraintEvaluator`, `IMicroExecutionPriceCalculator` Models and reference-data `MicroExecutionProfile` for Opening and Closing. The immutable decision input includes approved envelope and profile versions, broker acknowledgement/working revision, actual remaining balanced units, latest material quote source/freshness, account gate, deadline and prior actions. Constraints build an allowed-action mask first; policy chooses Wait/Place/UpdateLimit/Cancel/Reconcile/Escalate; price model computes an exact tick-aligned limit within frozen bounds. Closing can have a different urgent profile but not unlimited aggression. No model calls a broker, DB, clock or random source. `EvaluateMicroExecutionCommand` appends only material decision/action events; market quote filtering occurs in Realtime. Timers have stable IDs and fire once at an actual deadline/transition, not by database polling. A future shadow policy uses the same input but cannot mutate.

Add `IPortfolioTradeAccountingApi` or equivalent typed Portfolio boundary for accepted Opening/Closing execution and fee corrections. It maps exact full TradeEntityId, FundId, source OrderExecution event/exec IDs, signed leg quantities, prices, currencies, approved authority and account environment to existing PostgreSQL Portfolio financial/position/capacity commands. Portfolio alone determines TradeSettlement/Commission/RealizedPnl timing and double-entry rules. A fill can create exposure or an obligation before confirmed settlement; do not post settled cash on order receipt. Stable accounting OperationId/source hash returns the original receipt under redelivery and rejects changed content. Emulator, Paper and Live Fund books/positions are segregated by environment/account alias; synthetic executions cannot seed Paper/Live cash or positions. Pending/failed accounting is visible and replays the **same** idempotent handoff after a crash. Batch size, manifest, financial `LedgerPost` access and rule/authority fences follow existing Portfolio validation.

## 8. Storage, DI, UI and generated observability gate

Use EventSourceDb streams for BrokerOrder/OrderExecution/BrokerAccount durable events; provider correlation and observation inbox PostgreSQL tables enforce OperationId and external exec ID uniqueness; PortfolioDb owns accepted Fund/ledger/approval accounting, and an explicit BrokerAccount qualification/approval record persists the reviewed manifest. Scylla/read projections are rebuildable UI history only. Intent plus correlation either shares an explicitly proven enlisted PostgreSQL transaction or uses post-commit durable ordering so correlation commits **before** emulator mutation. Durable projector receipts remain pending only for real incomplete handoffs; recovery is event-triggered by startup, reconnect, incident or explicit command, without perpetual 2-second polling.

API composition registers one Emulator ledger, one order framework port, one account framework port, one Application `ITradeBroker`, one observation bridge, the BrokerOrder/Account actors and routes, and no live provider. Startup validates scenario/contract/reference versions, reconstructs journal and hot working-order index, synchronizes current account/open orders/executions, reconciles unknown attempts, publishes coherent capability/gate snapshots, then allows operational synthetic orders only under an accepted Emulator manifest. Qualification tests may run in isolated test scope before acceptance. Manual UI intended for emulator uses Portfolio-approved `ExecutionChannel.Broker`; `ExecutionChannel.Manual` remains separate human-entered evidence, never a simulated IBKR callback. UI shows synthetic mode, account alias, available synthetic cash, working orders/fills/fees, Fund accounting status and exact failure/gate reasons.

Create **dedicated logging-only** `internal static partial` classes under each component's `Logging` folder and `[LoggerMessage]` methods with audited unique EventIds, constant templates, explicit levels and scalar structured IDs. The actor/provider/handler classes remain **non-partial** and decide when to log; only the generated logging declaration class is partial. Provide generated entries for initialization, durable intent, local receipt, authoritative ack/fill/fee, unknown outcome, reconciliation, account gate/manifest acceptance, accounting handoff and real handler failure. No normal ignored tick, duplicate/no-op, per-fill raw payload, account ID, credential or unrestricted error text is logged. Error declarations accept `Exception` and safe category/OperationId/epoch details. Measure enabled-level allocation and callback routing; counters/Actor Health show mailbox backlog by thread, durable queue receipts, unknown mutations, fill/cancel race and Fund accounting lag. XML documentation covers all new/modified public methods and actor handler helpers.

## 9. Sequential implementation gates and required evidence

| Gate | Implement and verify before proceeding | Required evidence |
| --- | --- | --- |
| **E0 Baseline/freeze** | Inventory solution/routes, old TradeOrder/OrderExecution contracts, exact order shapes, combo price/tick convention, cash/fee/settlement rules, market quote feed, EventId namespace and test-host isolation. | Reviewed contract manifest, no ambiguous financial/price assumptions. |
| **E1 Shared contracts** | Application `ITradeBroker`, two Framework ports, neutral models/enums/MessagePack migrations and exact mappers. | Build, old-payload reader compatibility, field coverage, invalid/Unknown mode tests. |
| **E2 Actor skeletons** | BrokerOrder Command/Event/Query/Realtime and BrokerAccount Command/Event/Query base classes, maps/contexts, one-message files, validation extensions and states. | Parse/validate/receive parity, actor-convention scripts, malformed/unknown type and aggregate ValidationError tests. |
| **E3 Durable handoffs** | TradeOrder/OrderExecution revisions, committed execution-to-Broker notification, Broker mutation projector, observation bridge, evidence-to-OrderExecution messaging and provider correlation. | NATS/PostgreSQL integration, stable IDs, no self-trigger loop, crash-after-intent/receipt tests. |
| **E4 Emulator ledger** | Shared journal/checkpoint, clock/scheduler, order/account ports, deterministic account snapshots and local receipts. | Port contract tests, restart/ledger-hash replay, no IBApi/credential reference, account/order coherence. |
| **E5 Fill behavior** | Futures Level-1 and coherent vertical/iron-condor combo matching, individual leg exec IDs, fees, partial fills and declared faults. | Happy/edge BDD, ratio/currency/cash invariants, no-quote/no-fill, cancel/late-fill and callback ordering tests. |
| **E6 Micro-execution** | Pure profile/constraints/policy/price Models, coalesced quote Realtime trigger, bounded deadlines and approved mutation guard. | Pure deterministic unit tests, real quote route integration, no per-tick DB/allocating queue, opening/closing price bounds. |
| **E7 Fund accounting** | Typed Portfolio trade-accounting API, Opening/Closing/correction handoff, exact Fund posting/position/capacity segregation. | PostgreSQL financial integration, idempotent receipts, no fabricated settlement, accounting-failed visibility. |
| **E8 Account qualification** | BrokerAccount snapshot/gate and immutable evidence/accept/revoke contracts/storage; operational Emulator gate. | Human acceptance cannot be forged by tests/config, changed version/revocation/expired gate tests. |
| **E9 API/UI/health** | Single active synthetic account, BrokerOrder/order evidence/account/Fund views and Actor Health metrics; manual UI converges on Portfolio approval. | API/UI build, source/fund/date-range queries, redaction/gate/status acceptance. |
| **E10 Full verification** | BDD, unit, integration, verification, replay, performance/soak suites below. | Clean test-host exit, linked results/trace manifest, no recurring expected-exception loop. |
| **E11 Review/accept** | Produce version-bound Emulator qualification manifest and present it for explicit human acceptance. | Accepted manifest/approval ID or gate remains closed; Live plan cannot start implementation before acceptance. |

## 10. Test, verification, soak and benchmark matrix

Tests are behavior-oriented and meaningful, not mirrors of handler code. Include: (a) accepted Opening futures, vertical and iron-condor orders from strategy and UI; (b) accepted Closing order from exit workflow, original and closing fills, Portfolio Fund ledger/position receipts; (c) submit/modify/cancel local receipt followed by authoritative callback; (d) exact duplicate CommandId/OperationId/execId no-op and changed-content conflict; (e) missing/stale quote and insufficient synthetic funding result in no favorable fill; (f) partial balanced versus unbalanced leg exposure; (g) cancel acknowledgement followed by late fill; (h) unknown dispatch/ambiguous callback and bounded reconciliation; (i) fee before fill, fee correction, foreign execution; (j) account snapshot incomplete versus complete empty position set; (k) synthetic account mismatch/cross-Fund/cross-environment isolation; (l) approval missing/revoked/expired/version mismatch; (m) restart at each intent/correlation/ledger/callback/actor/Portfolio boundary; (n) handler parse/validation/mapping parity and single extension class; and (o) safe structured log/Actor Health fields without credentials.

Run convention checks `scripts/Test-CommandActorConventions.ps1`, `Test-EventActorConventions.ps1`, `Test-QueryActorConventions.ps1` and `Test-RealtimeActorConventions.ps1` for the new actor tree. Use Domain.Trade/Portfolio BDD and unit projects, Application.Api and Storage integration projects, and new framework port contract tests (same suite later runs against live fixtures). Verification tests prove MessagePack old/new round trips, state `Apply(false)` no pending event, pending-event/replay equality, durable projector receipts, and exactly one observation bridge. Integration starts an **isolated** API/emulator host with PostgreSQL/NATS/PortfolioDb and exits it after the run; never overlap it with a user's live IFM host. Run normal API/UI builds after integration.

BenchmarkDotNet benchmarks report benchmark host, framework version, scenario, payload, account generation, allocations/op and throughput for order DTO mapping, dispatch receipt/correlation lookup, callback normalization/bridge routing, immutable account snapshot reads, hot quote filter and pure micro-execution decision. The allocation budget is measured on the relevant hot paths; no blanket "zero allocation" claim for durable orders/SQL is made. Soak an isolated deterministic market/replay session long enough to observe working orders, partial fills, timer/deadline transitions and restarts, compare final ledger/event/Portfolio hashes across repeat runs, and capture GC/LOH/queue/latency trend. Synthetic TPS is **not** a forecast of live IBKR fill throughput.

## 11. Acceptance package and handoff to Plan 2

The Emulator qualification package contains exact code/build/contract/profile/scenario hashes, order shapes, test/benchmark/soak links, deterministic replay ledger hashes, account/fund reconciliation, failure and degraded-operation findings, open discrepancies, source-generated log and actor-map checks, and a proposed allowed synthetic account/capability scope. A reviewer explicitly accepts or declines it via the durable BrokerAccount approval Command; tests do not set Accepted. Operational Emulator new-risk trading remains gated until accepted. The [IBKR Live Adapter plan](Trade-Broker-IBKR-Live-Adapter-Implementation-Plan-v1.0.md) may be read/reviewed now, but its implementation begins only after this acceptance and a material-version compatibility check.
