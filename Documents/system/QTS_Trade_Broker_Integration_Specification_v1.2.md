# QTS Trade Broker Integration Specification v1.2

**Document Type:** Codex Implementation Specification\
**Status:** Revised Architecture Baseline --- Futures, Trade Modes,
Micro Execution\
**Target:** V1 Trading System\
**Planned External Broker:** Interactive Brokers TWS API (not implemented)\
**Primary Emulator Market Source:** Databento\
**Design Priority:** Broker-neutral application contracts, deterministic
behavior, testability, realistic emulation, strict separation of
domain/application/framework concerns

## Implementation status and delivery order — 2026-09-05

Actual IBKR connectivity is not implemented. The first broker implementation
will be the **IBKR emulator**, followed later by an actual IBKR connection.
The interfaces, adapter names, and diagrams below describe the target design;
their presence in this specification is not evidence of completed integration.

1. Implement and qualify the emulator through `IOrderExecutionBroker` and
   `IAccountBroker`, using the specified `EmulatedOrderExecutionBroker` and
   `EmulatedAccountBroker` implementations and Databento market inputs.
2. Exercise order and account lifecycles, failures, recovery, and reconciliation
   through those same application-facing contracts without an IBKR connection.
3. Implement the actual IBKR adapters later and qualify the external connection
   separately. Emulator qualification does not establish actual-broker readiness.

Market Condition evaluates market and data conditions independently of an
actual broker connection. Before order submission, Order Execution must check
the selected adapter's readiness: the emulator during initial development,
and the actual IBKR adapter after it is implemented. An unavailable execution
adapter must block submission, not be represented as a healthy connection.

The currently registered `UnavailableMarketConditionBrokerReadiness` is a
placeholder, not an IBKR adapter or an emulator. Removing that execution
dependency from Market Condition remains a separate code/configuration change;
this documentation update does not implement it.

------------------------------------------------------------------------

# 1. Purpose

This specification defines the broker integration boundary for QTS.

The design MUST provide two separate application-level broker
interfaces:

1.  `IOrderExecutionBroker`
2.  `IAccountBroker`

There MUST NOT be a combined `ITradeBroker` interface in V1.

Each interface MUST have two interchangeable implementations:

``` text
IOrderExecutionBroker
├── IbkrOrderExecutionBroker
└── EmulatedOrderExecutionBroker

IAccountBroker
├── IbkrAccountBroker
└── EmulatedAccountBroker
```

The Domain and Application layers MUST depend only on application-level
interfaces and application-defined schemas.

No domain actor may depend directly on IBKR, Databento, or
emulator-specific DTOs.

------------------------------------------------------------------------

# 2. Architectural Goals

## 2.1 Order Execution

Support:

-   Place orders
-   Modify orders
-   Cancel orders
-   Order acknowledgement
-   Order rejection
-   Order status changes
-   Partial fills
-   Complete fills
-   Cancellations
-   Execution reports
-   Commission reports
-   Open-order queries
-   Broker reconnect/reconciliation

## 2.2 Account Access

Support:

-   Account summary
-   Net liquidation value
-   Cash balances
-   Available funds
-   Buying power
-   Initial margin
-   Maintenance margin
-   Excess liquidity
-   Positions
-   Average cost
-   Realized P&L
-   Unrealized P&L
-   Executions
-   Commissions
-   Open orders where appropriate
-   Account refresh and reconciliation

------------------------------------------------------------------------

# 3. Layering

``` text
Domain
    Actors
    Trading decisions
    Portfolio risk
    Position management
        ↓
Application
    IOrderExecutionBroker
    IAccountBroker
    Broker-neutral schemas
        ↓
Framework
    IBKR TWS API adapter
    Databento market adapter
    Emulator
    Level 1 execution model
    Optional Level 2 order book
```

The Application layer defines required capabilities. The Framework layer
implements technical details.

------------------------------------------------------------------------

# 4. Order Execution Interface

``` csharp
public interface IOrderExecutionBroker
{
    ValueTask<BrokerOrderSubmissionResult> PlaceOrderAsync(
        TradeOrder order,
        CancellationToken cancellationToken = default);

    ValueTask<BrokerOrderUpdateResult> UpdateOrderAsync(
        TradeOrderUpdate update,
        CancellationToken cancellationToken = default);

    ValueTask<BrokerOrderCancelResult> CancelOrderAsync(
        TradeOrderCancel cancel,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<BrokerOpenOrder>> GetOpenOrdersAsync(
        BrokerOpenOrderQuery query,
        CancellationToken cancellationToken = default);
}
```

The interface handles commands/queries only. Asynchronous broker
lifecycle events MUST be surfaced through QTS-defined application
events/channels/event publication mechanisms.

IBKR callbacks MUST be translated immediately into application-level
broker events before higher-level actor processing.

------------------------------------------------------------------------

# 5. Account Interface

``` csharp
public interface IAccountBroker
{
    ValueTask<BrokerAccountSnapshot> GetAccountSnapshotAsync(
        BrokerAccountQuery query,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<BrokerPosition>> GetPositionsAsync(
        BrokerPositionQuery query,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<BrokerExecution>> GetExecutionsAsync(
        BrokerExecutionQuery query,
        CancellationToken cancellationToken = default);

    ValueTask<BrokerAccountReconciliation> ReconcileAsync(
        BrokerAccountReconciliationRequest request,
        CancellationToken cancellationToken = default);
}
```

Account changes MAY also be published as application-level events.

------------------------------------------------------------------------

# 6. Broker-Neutral Schemas

``` csharp
public enum BrokerSource : byte
{
    Unknown = 0,
    InteractiveBrokers = 1,
    Emulator = 2
}
```

Domain logic SHOULD NOT branch on `BrokerSource` except for
diagnostics/environment assertions.

------------------------------------------------------------------------

# 7. Trade Order Schema

``` csharp
public sealed record TradeOrder
{
    public required Guid OrderIntentId { get; init; }
    public required string AccountId { get; init; }
    public required string StrategyId { get; init; }
    public required TradeMode TradeMode { get; init; }
    public required string UnderlyingSymbol { get; init; }
    public required IReadOnlyList<TradeOrderLeg> Legs { get; init; }
    public required TradeOrderType OrderType { get; init; }
    public required TimeInForce TimeInForce { get; init; }
    public required decimal Quantity { get; init; }
    public decimal? LimitPrice { get; init; }
    public decimal? StopPrice { get; init; }
    public required bool Transmit { get; init; }
    public ExecutionPatternRequest? ExecutionPattern { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public string? ClientReference { get; init; }
}
```

``` csharp
public sealed record TradeOrderLeg
{
    public required string InstrumentId { get; init; }
    public required string Symbol { get; init; }
    public required InstrumentType InstrumentType { get; init; }
    public required OrderSide Side { get; init; }
    public required decimal Ratio { get; init; }
    public decimal? Strike { get; init; }
    public DateOnly? Expiration { get; init; }
    public OptionRight? OptionRight { get; init; }
    public string? Exchange { get; init; }
    public string? Currency { get; init; }
    public string? BrokerContractReference { get; init; }
}
```

------------------------------------------------------------------------

# 8. Order Enums

``` csharp
public enum TradeMode : byte
{
    Manual = 1,
    Algorithm = 2
}

public enum TradeOrderType : byte
{
    Market = 1,
    Limit = 2,
    Stop = 3,
    StopLimit = 4
}

public enum OrderSide : byte
{
    Buy = 1,
    Sell = 2
}

public enum TimeInForce : byte
{
    Day = 1,
    GoodTillCancelled = 2,
    ImmediateOrCancel = 3,
    FillOrKill = 4
}

public enum InstrumentType : byte
{
    Future = 1,
    FutureOption = 2,
    Equity = 3,
    EquityOption = 4
}

public enum OptionRight : byte
{
    Call = 1,
    Put = 2
}
```

------------------------------------------------------------------------

# 9. Update and Cancel Contracts

``` csharp
public sealed record TradeOrderUpdate
{
    public required Guid OrderIntentId { get; init; }
    public decimal? NewQuantity { get; init; }
    public decimal? NewLimitPrice { get; init; }
    public decimal? NewStopPrice { get; init; }
    public TimeInForce? NewTimeInForce { get; init; }
}

public sealed record TradeOrderCancel
{
    public required Guid OrderIntentId { get; init; }
    public required string Reason { get; init; }
}
```

------------------------------------------------------------------------

# 10. Order Result and Event Schemas

``` csharp
public sealed record BrokerOrderSubmissionResult
{
    public required Guid OrderIntentId { get; init; }
    public required BrokerSource Source { get; init; }
    public required bool AcceptedForProcessing { get; init; }
    public string? BrokerOrderId { get; init; }
    public string? Message { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
}
```

Equivalent result records should exist for update and cancel.

Required broker events:

``` text
BrokerOrderSubmitted
BrokerOrderAcknowledged
BrokerOrderRejected
BrokerOrderStatusChanged
BrokerOrderPartiallyFilled
BrokerOrderFilled
BrokerOrderCancelled
BrokerExecutionReported
BrokerCommissionReported
BrokerConnectionChanged
```

``` csharp
public abstract record BrokerOrderEvent
{
    public required Guid OrderIntentId { get; init; }
    public required BrokerSource Source { get; init; }
    public required DateTimeOffset BrokerTimestampUtc { get; init; }
    public string? BrokerOrderId { get; init; }
    public string? ExecutionId { get; init; }
}
```

------------------------------------------------------------------------

# 11. Fill Schema

``` csharp
public sealed record BrokerFill
{
    public required Guid OrderIntentId { get; init; }
    public required string InstrumentId { get; init; }
    public required OrderSide Side { get; init; }
    public required decimal FillQuantity { get; init; }
    public required decimal FillPrice { get; init; }
    public required decimal RemainingQuantity { get; init; }
    public required bool IsPartialFill { get; init; }
    public required DateTimeOffset FillTimestampUtc { get; init; }
    public string? ExecutionId { get; init; }
    public decimal? Commission { get; init; }
}
```

------------------------------------------------------------------------

# 12. Account Snapshot and Position Schemas

``` csharp
public sealed record BrokerAccountSnapshot
{
    public required string AccountId { get; init; }
    public required BrokerSource Source { get; init; }
    public required decimal NetLiquidationValue { get; init; }
    public required decimal CashBalance { get; init; }
    public required decimal AvailableFunds { get; init; }
    public required decimal BuyingPower { get; init; }
    public required decimal InitialMargin { get; init; }
    public required decimal MaintenanceMargin { get; init; }
    public required decimal ExcessLiquidity { get; init; }
    public required decimal RealizedPnL { get; init; }
    public required decimal UnrealizedPnL { get; init; }
    public required DateTimeOffset AsOfUtc { get; init; }
}
```

``` csharp
public sealed record BrokerPosition
{
    public required string AccountId { get; init; }
    public required string InstrumentId { get; init; }
    public required string Symbol { get; init; }
    public required InstrumentType InstrumentType { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal AverageCost { get; init; }
    public decimal? MarketPrice { get; init; }
    public decimal? MarketValue { get; init; }
    public decimal? RealizedPnL { get; init; }
    public decimal? UnrealizedPnL { get; init; }
    public required DateTimeOffset AsOfUtc { get; init; }
}
```

------------------------------------------------------------------------

# 13. IBKR Order Execution Implementation

`IbkrOrderExecutionBroker` MUST:

-   reuse the existing dedicated IBKR message-pump thread;
-   translate `TradeOrder` to TWS API order/contract/combo structures;
-   correlate QTS `OrderIntentId` with broker order IDs;
-   support place/modify/cancel;
-   support partial fills and fill ordering;
-   reconcile duplicate callbacks safely;
-   support futures and futures options in V1;
-   support multi-leg combo/spread orders;
-   translate IBKR errors to QTS error codes;
-   never expose TWS API DTOs outside Framework.

``` text
TradeOrder
    ↓
IBKR contract resolution
    ↓
IBKR order translation
    ↓
TWS place/modify/cancel
    ↓
IBKR callback
    ↓
QTS BrokerOrderEvent
```

------------------------------------------------------------------------

# 14. IBKR Account Implementation

`IbkrAccountBroker` MUST:

-   translate TWS account data to `BrokerAccountSnapshot`;
-   translate positions to `BrokerPosition`;
-   translate executions/commissions to QTS schemas;
-   consume callbacks from existing IBKR pump;
-   expose coherent latest account state;
-   support explicit reconciliation;
-   distinguish broker-authoritative state from locally cached latest
    state.

------------------------------------------------------------------------

# 15. Emulator Architecture

``` text
EmulatedOrderExecutionBroker
        ↓
IEmulatedMarketExecutionModel
        ├── Level1ExecutionModel        [MANDATORY]
        └── Level2OrderBookExecutionModel [OPTIONAL]
        ↓
Emulated fills/order state
        ↓
EmulatedAccountBroker
```

The emulator MUST simulate broker behavior only. It MUST NOT know why a
trade was selected or whether a strategy is desirable.

------------------------------------------------------------------------

# 16. Emulator Scenario Configuration

The emulator MUST load a scenario configuration at startup.

``` csharp
public sealed record BrokerEmulatorScenario
{
    public required string ScenarioName { get; init; }
    public required EmulatorMode Mode { get; init; }
    public required MarketExecutionConfiguration MarketExecution { get; init; }
    public required BrokerLatencyConfiguration Latency { get; init; }
    public required BrokerFailureConfiguration Failures { get; init; }
    public required BrokerAccountConfiguration Account { get; init; }
    public required CommissionConfiguration Commission { get; init; }
    public required RandomizationConfiguration Randomization { get; init; }
}
```

------------------------------------------------------------------------

# 17. Emulator Modes

``` csharp
public enum EmulatorMode : byte
{
    DeterministicTest = 1,
    DatabentoLiveMarket = 2,
    DatabentoHistoricalReplay = 3,
    FailureInjection = 4
}
```

## DeterministicTest

Scripted market/fills with exact reproducible outcomes.

## DatabentoLiveMarket

Live Databento market data drives simulated execution.

## DatabentoHistoricalReplay

Recorded Databento data drives deterministic historical broker
emulation.

## FailureInjection

Market-driven execution plus declarative broker failure scenarios.

------------------------------------------------------------------------

# 18. Level 1 Execution Model --- Mandatory

Required market state:

``` text
Last trade
Best bid
Best ask
Bid size
Ask size
Exchange timestamp
Sequence
```

``` csharp
public interface IEmulatedMarketExecutionModel
{
    ValueTask<MarketExecutionDecision> EvaluateAsync(
        TradeOrder order,
        MarketExecutionSnapshot market,
        CancellationToken cancellationToken = default);
}
```

Initial rules:

``` text
Market Buy       → execute against ask
Market Sell      → execute against bid
Buy Limit >= Ask → executable
Sell Limit <= Bid→ executable
Buy Limit < Ask  → working
Sell Limit > Bid → working
```

Top-of-book size MAY drive partial fills.

Example:

``` text
Order quantity = 10
Ask size = 4
→ Fill 4
→ Remaining 6 working
```

------------------------------------------------------------------------

## 18.1 Intraday ES Futures Order Flow --- Mandatory

Single-leg futures execution MUST be a first-class broker capability.
The same `TradeOrder` contract is used with exactly one `TradeOrderLeg`
whose `InstrumentType` is `Future`.

``` mermaid
flowchart LR
    A[Manual Entry or Strategy Pipeline] --> B[Order Composer]
    B --> C[Portfolio Risk]
    C -->|OrderApproved| D[OrderExecutionActor]
    D --> E[IOrderExecutionBroker]
    E --> F[IBKR or Emulator]
    F --> G[Submitted / Ack / Working]
    G --> H[Partial Fill / Fill / Cancel / Reject]
    H --> I[Position Management]
```

Mandatory ES futures behavior:

-   market and limit entry;
-   stop / stop-limit where supported;
-   modify and cancel;
-   partial/full fill;
-   rejection;
-   execution reports;
-   account/position updates;
-   deterministic exit orders from position management.

Optional Level 2 execution may later improve fill fidelity without
changing the order contract.

------------------------------------------------------------------------

## 18.2 Manual Trade Mode and Algorithm Trade Mode

Every `TradeOrder` MUST identify its origin with `TradeMode`. Both modes
use identical broker interfaces and lifecycle events.

### Manual

``` text
Manual Trade Entry
    ↓
Order Composer
    ↓
Portfolio Risk
    ↓
OrderApproved / OrderDenied
    ↓
Order Execution
```

Manual mode MUST NOT bypass `PortfolioRiskActor`. It may select more
direct execution instructions, but those instructions must be
represented in `TradeOrder`.

### Algorithm

``` text
Regime Discovery
    ↓
Market Condition
    ↓
Strategy Selector
    ↓
Order Composer
    ↓
Portfolio Risk
    ↓
Order Execution
```

Algorithm mode MAY request a deterministic execution pattern. Execution
MUST NOT make strategy or portfolio-risk decisions.

### Common invariant

``` text
Manual or Algorithm
        ↓
Same TradeOrder schema
        ↓
Same PortfolioRisk boundary
        ↓
Same IOrderExecutionBroker
        ↓
Same broker lifecycle events
```

Trade mode MUST be persisted/logged for audit, Operations UI, replay,
and tests.

------------------------------------------------------------------------

# 19. Multi-Leg / Combo Execution

Weekly verticals and monthly iron condors MUST be supported.

For Level 1, derive a synthetic executable combo price:

``` text
Long leg  → ask
Short leg → bid
        ↓
Synthetic combo debit/credit
```

Then compare submitted combo limit to executable combo market.

Configuration SHOULD control:

-   atomic combo fill;
-   simulated legging;
-   partial combo fill behavior;
-   minimum fill delay;
-   slippage.

V1 default SHOULD favor conservative atomic combo simulation unless a
scenario explicitly requests legging behavior.

------------------------------------------------------------------------

# 20. Level 2 Order Book Model --- Optional

Level 2 is NOT mandatory for V1.

Optional capability:

-   multi-level bid/ask depth;
-   depth reconstruction;
-   queue-position estimation;
-   multi-level sweeping;
-   more realistic partial fills;
-   liquidity exhaustion;
-   market-impact approximation.

It MUST implement the same `IEmulatedMarketExecutionModel` contract.

No domain/application actor changes are permitted when switching Level 1
↔ Level 2.

------------------------------------------------------------------------

# 21. Emulator Order State Machine

Suggested states:

``` text
Created
Submitted
Acknowledged
Working
PartiallyFilled
Filled
CancelPending
Cancelled
ReplacePending
Rejected
```

``` mermaid
stateDiagram-v2
    [*] --> Submitted
    Submitted --> Acknowledged
    Acknowledged --> Working
    Working --> PartiallyFilled
    PartiallyFilled --> PartiallyFilled
    PartiallyFilled --> Filled
    Working --> Filled
    Working --> CancelPending
    CancelPending --> Cancelled
    Submitted --> Rejected
    Acknowledged --> Rejected
```

All transitions MUST be deterministic and covered by tests.

------------------------------------------------------------------------

## 21.1 Extensible Micro Execution Pattern

The broker implementations MUST support a small broker-neutral
micro-execution instruction describing **how an already approved order
is worked in the market**. It MUST NOT select strategy, modify
portfolio-risk authority, or materially redesign the trade.

``` csharp
public enum ExecutionPatternType : byte
{
    Direct = 1,
    PassiveLimit = 2,
    AdaptiveLimit = 3
}

public sealed record ExecutionPatternRequest
{
    public required ExecutionPatternType Pattern { get; init; }
    public int? MaximumAttempts { get; init; }
    public TimeSpan? MaximumDuration { get; init; }
    public decimal? MaximumPriceMovement { get; init; }
    public TimeSpan? RepriceInterval { get; init; }
    public bool AllowCrossSpread { get; init; }
}
```

### Direct

Submit exactly as composed. No automatic repricing. SHOULD be the V1
default.

### PassiveLimit

Submit the approved limit and allow it to rest. No automatic repricing
unless explicitly configured by a future extension.

### AdaptiveLimit

A bounded deterministic extension that may perform controlled
cancel/replace/reprice within explicit limits already authorized for
execution.

``` mermaid
stateDiagram-v2
    [*] --> Submit
    Submit --> Observe
    Observe --> Filled: filled
    Observe --> Partial: partial fill
    Partial --> Observe
    Observe --> Reprice: deterministic rule permits
    Reprice --> Observe
    Observe --> Cancel: duration/attempt/price bound reached
    Cancel --> [*]
    Filled --> [*]
```

Rules:

-   Starts only after `OrderApproved`.
-   May change execution mechanics only within supplied bounds.
-   MUST NOT change strikes, expiry, leg ratios, strategy, or
    risk-approved economic exposure.
-   A material trade change terminates the pattern and requires new
    composition plus Portfolio Risk evaluation.
-   IBKR and emulator interpret the same request.
-   Emulator behavior is deterministic in deterministic scenarios.
-   Every pattern step and reason is observable/logged.

The pattern contract is intentionally small so new execution patterns
can be added without changing `IOrderExecutionBroker`.

------------------------------------------------------------------------

# 22. Failure Injection

Scenario configuration MUST support:

``` text
Order rejection
Delayed acknowledgement
Dropped acknowledgement
Partial fill
Multiple partial fills
Delayed fill
Cancel delay
Cancel rejection
Replace rejection
Fill-before-cancel race
Broker disconnect
Broker reconnect
Duplicate callback
Out-of-order callback
Account update delay
Execution report delay
Commission delay
Margin rejection
Insufficient buying power
Invalid contract
Market-data stale
Market-data disconnect
```

Failure rules MUST be declarative/configuration-driven.

------------------------------------------------------------------------

# 23. Scenario Example

``` json
{
  "scenarioName": "partial-fill-cancel-race",
  "mode": "FailureInjection",
  "marketExecution": {
    "model": "Level1",
    "partialFillEnabled": true
  },
  "latency": {
    "acknowledgementMs": 25,
    "fillMs": 50,
    "cancelMs": 100
  },
  "failures": {
    "orderRejectProbability": 0.0,
    "duplicateCallbackProbability": 0.0,
    "disconnectAfterSubmission": false,
    "cancelAfterPartialFill": true
  },
  "randomization": {
    "enabled": false,
    "seed": 12345
  }
}
```

Deterministic tests SHOULD use fixed seeds or no randomization.

------------------------------------------------------------------------

# 24. Emulator Account Engine

`EmulatedAccountBroker` MUST track:

``` text
Cash
Net liquidation
Buying power
Available funds
Initial margin
Maintenance margin
Excess liquidity
Positions
Average cost
Realized P&L
Unrealized P&L
Executions
Commissions
Open orders
```

Every simulated fill MUST update account state.

``` text
Fill
    ↓
Execution record
    ↓
Position quantity / average cost
    ↓
Cash
    ↓
Commission
    ↓
Margin
    ↓
Realized / Unrealized P&L
    ↓
AccountUpdated
```

------------------------------------------------------------------------

# 25. Initial Account Configuration

``` csharp
public sealed record BrokerAccountConfiguration
{
    public required string AccountId { get; init; }
    public required decimal StartingCash { get; init; }
    public required decimal StartingNetLiquidation { get; init; }
    public required decimal BuyingPowerMultiplier { get; init; }
    public required MarginModelConfiguration MarginModel { get; init; }
}
```

The V1 margin model may be simplified but MUST be conservative. It MUST
NOT claim exact replication of proprietary IBKR margin behavior unless
explicitly implemented.

------------------------------------------------------------------------

# 26. Commission Model

``` csharp
public interface ICommissionModel
{
    decimal Calculate(BrokerExecution execution);
}
```

IBKR uses broker-reported commission. Emulator uses configurable rules.
Both produce the same application schema.

------------------------------------------------------------------------

# 27. Latency Model

Scenario-controlled latency SHOULD support:

``` text
Acknowledgement
Modify
Cancel
Fill
Execution report
Account update
Commission
```

``` csharp
public sealed record BrokerLatencyConfiguration
{
    public int AcknowledgementMs { get; init; }
    public int ModifyMs { get; init; }
    public int CancelMs { get; init; }
    public int FillMs { get; init; }
    public int AccountUpdateMs { get; init; }
}
```

Deterministic scenarios use fixed latency. Future stochastic modes MAY
use bounded distributions.

------------------------------------------------------------------------

# 28. Market Data Integration

The emulator consumes normalized market state from QTS market-data
infrastructure.

Mandatory:

``` text
Databento Level 1 trades
Databento Level 1 quotes
```

Optional:

``` text
Databento Level 2 depth/order book
```

The emulator MUST NOT own the primary Databento network connection.

------------------------------------------------------------------------

# 29. Historical Replay

``` text
Recorded Databento data
        ↓
Market-data replay
        ↓
Level 1 / Level 2 execution model
        ↓
Emulated broker
        ↓
Normal broker application events
        ↓
Normal OrderExecutionActor / Account / Position workflows
```

Given the same:

``` text
market data
initial account
submitted orders
scenario configuration
random seed
```

results SHOULD be reproducible.

------------------------------------------------------------------------

# 30. Live Databento Simulation

The emulator MUST support:

``` text
Live Databento
    +
Emulated broker
    =
Controlled live-market paper trading
```

This mode lets QTS test against a live market independently of IBKR
paper behavior.

------------------------------------------------------------------------

# 31. Reconciliation

Both real and emulator implementations MUST support explicit
reconciliation.

The application must be able to answer:

``` text
What orders are open?
What positions exist?
What executions occurred?
What account values are authoritative now?
```

After reconnect/restart, normal actors query the same interfaces
regardless of broker implementation.

------------------------------------------------------------------------

# 32. Dependency Injection

``` csharp
if (settings.BrokerMode == BrokerMode.InteractiveBrokers)
{
    services.AddSingleton<IOrderExecutionBroker, IbkrOrderExecutionBroker>();
    services.AddSingleton<IAccountBroker, IbkrAccountBroker>();
}
else
{
    services.AddSingleton<IOrderExecutionBroker, EmulatedOrderExecutionBroker>();
    services.AddSingleton<IAccountBroker, EmulatedAccountBroker>();
}
```

Domain actors MUST NOT branch on `BrokerMode`.

------------------------------------------------------------------------

# 33. Observability

Both implementations MUST expose common operational telemetry:

``` text
order submissions
ack latency
fill latency
partial fills
rejections
cancel latency
modify latency
disconnect/reconnect
account refresh latency
reconciliation results
emulator scenario name
emulator mode
```

These metrics/events should feed existing Strategy, Latency, Traffic,
Errors, and Saturation Operations views.

------------------------------------------------------------------------

# 34. Testing Strategy

## 34.1 Shared Contract Tests

Create one broker contract-test suite and run it against emulator
implementations and against IBKR adapters where practical.

## 34.2 Deterministic Emulator Tests

Must test:

``` text
market fill
limit fill
working limit
modify
cancel
full fill
partial fill
multiple partial fills
combo fill
order reject
margin reject
disconnect
reconnect
duplicate callback
out-of-order callback
cancel/fill race
account update
commission
reconciliation
```

## 34.3 BDD Example

``` text
Given an ES weekly vertical is submitted
And Level 1 Databento market is active
And only 40% of top-of-book size is available
When the order becomes executable
Then a partial fill is emitted
And the remaining quantity remains working
And the account reflects only the filled quantity
```

------------------------------------------------------------------------

# 35. Acceptance Criteria

1.  `IOrderExecutionBroker` exists in Application.
2.  `IAccountBroker` exists in Application.
3.  No required combined broker interface exists.
4.  IBKR and Emulator implement the same two contracts.
5.  Domain/Application schemas are independent of TWS API schemas.
6.  Futures and futures options are supported in V1.
7.  Place/update/cancel work.
8.  Ack/reject/status/partial-fill/fill/cancel/execution/commission
    events work.
9.  Account snapshot/positions/executions/reconciliation work.
10. Emulator reads scenario configuration.
11. DeterministicTest mode works.
12. Live Databento mode works.
13. Historical Databento replay works.
14. FailureInjection mode works.
15. Level 1 execution is mandatory and complete.
16. Level 2 is optional behind the same execution-model interface.
17. Vertical and iron-condor combo orders are supported.
18. Emulator account updates from simulated fills.
19. Shared broker contract tests exist.
20. Fixed-seed replay produces reproducible outcomes.
21. Single-leg ES futures market/limit/modify/cancel/fill flow is
    explicitly supported.
22. Every order identifies `Manual` or `Algorithm` trade mode.
23. Manual orders pass through the same Portfolio Risk approval/denial
    boundary.
24. Manual and Algorithm modes use the same broker interfaces and event
    schemas.
25. `ExecutionPatternRequest` supports at least Direct and PassiveLimit
    with AdaptiveLimit extensibility.
26. Micro execution cannot materially alter a risk-approved trade
    without recomposition and renewed risk approval.

------------------------------------------------------------------------

# 36. Codex Implementation Sequence

## Stage 1 --- Application Contracts

Create interfaces, order/account schemas, broker-neutral enums/events,
`TradeMode`, and `ExecutionPatternRequest`. Build and test.

## Stage 2 --- ES Futures + Trade Mode Baseline

Implement single-leg ES futures order flow plus Manual/Algorithm mode
through the same risk and broker contracts. Implement Direct execution
first. Build and test.

## Stage 3 --- Emulator Core

Create emulator order/account implementations, scenario model,
deterministic order state machine. Build and test.

## Stage 4 --- Level 1 Market Execution

Integrate normalized Databento Level 1 state. Implement
market/limit/partial-fill behavior. Build and test.

## Stage 5 --- Multi-Leg Execution

Implement vertical and iron-condor combo execution. Build and test.

## Stage 6 --- Failure Injection

Implement rejection, latency, disconnect, duplicate, race, and account
failure scenarios. Build and test.

## Stage 7 --- Micro Execution Patterns

Implement PassiveLimit after Direct is stable. Add bounded AdaptiveLimit
only after deterministic tests prove the first two patterns. Build and
test.

## Stage 8 --- IBKR Order Adapter

Implement TWS order translation and callback mapping. Build and test.

## Stage 9 --- IBKR Account Adapter

Implement account/position/execution translation and reconciliation.
Build and test.

## Stage 10 --- Historical Replay

Wire Databento replay to emulator and verify deterministic end-to-end
behavior.

## Stage 11 --- Live Databento Simulation

Run emulator against live Databento market data.

## Stage 12 --- Optional Level 2

Only after Level 1 is stable, add order-book/depth execution behind the
same interface.

------------------------------------------------------------------------

# 37. Non-Goals for V1

Do not require:

``` text
Full exchange matching-engine simulation
Exact exchange queue position
Exact proprietary IBKR margin replication
HFT-grade market impact
Level 2 order book
Equities/equity options
Smart-routing emulation
ML fill models
LLM broker decisions
```

------------------------------------------------------------------------

# 38. Final Architecture

``` mermaid
flowchart TD
    OE[OrderExecutionActor] --> IO[IOrderExecutionBroker]
    AC[Account / Position Actors] --> IA[IAccountBroker]

    IO --> IBO[IbkrOrderExecutionBroker]
    IO --> EOE[EmulatedOrderExecutionBroker]

    IA --> IBA[IbkrAccountBroker]
    IA --> EAB[EmulatedAccountBroker]

    IBO --> TWS[IBKR TWS API]
    IBA --> TWS

    EOE --> MEX[IEmulatedMarketExecutionModel]
    MEX --> L1[Level1ExecutionModel]
    MEX --> L2[Level2OrderBookExecutionModel - Optional]

    L1 --> DB[Databento Normalized Market Data]
    L2 --> DB

    EOE --> EAB
```

The central invariant is:

> QTS Domain/Application code sees one order-execution contract and one
> account contract. Whether the implementation is IBKR or the emulator
> is purely a Framework/configuration concern.

The emulator exists to make the broker boundary reproducible,
failure-injectable, realistic, and fully testable before production
capital is exposed.

------------------------------------------------------------------------

# V1.2 Execution Policy Addendum

This section is normative and supersedes any earlier V1 micro-execution
guidance that conflicts with it.

## Default Order Policy

`Limit` is the default broker order type for V1.

Market orders are NOT a normal strategy-selected order type. They are a
protected execution escalation used only for deterministic defensive
exits when remaining exposed is considered more dangerous than accepting
execution slippage.

``` text
Entry
    → Passive/Aggressive Limit only

Profit Exit
    → Limit only

Normal Exit
    → Aggressive Limit

Defensive Loss Exit
    → Aggressive Limit
    → bounded wait/reprice
    → Market escalation only when explicitly permitted
```

## Trade Intent

Add the following application-level contract:

``` csharp
public enum TradeIntent : byte
{
    Entry = 1,
    ProfitExit = 2,
    NormalExit = 3,
    DefensiveExit = 4
}
```

`TradeOrder` MUST carry `TradeIntent`.

The intent is part of the approved order semantics and MUST NOT be
inferred from broker callbacks.

## Micro Execution Modes

The V1 execution modes are:

``` csharp
public enum MicroExecutionMode : byte
{
    PassiveLimit = 1,
    AggressiveLimit = 2,
    DefensiveExit = 3
}
```

Remove or deprecate any vague `Direct` execution mode from the V1
design.

### PassiveLimit

-   Submit a Limit order.
-   Rest at the approved price.
-   Permit normal fill/partial-fill lifecycle.
-   Never convert to Market.

### AggressiveLimit

-   Submit a marketable or near-marketable Limit order within approved
    bounds.
-   Permit bounded cancel/reprice/replace behavior.
-   Never convert to Market.
-   Must not change strategy structure, contracts, expirations, strikes,
    leg ratios, or risk-approved economic exposure.

### DefensiveExit

-   Used only for an already-open position being exited for
    deterministic capital-protection reasons.
-   Start with an Aggressive Limit order.
-   Permit bounded repricing/retry.
-   Permit Limit → Market escalation only when the configured instrument
    policy explicitly allows it and deterministic escalation conditions
    are satisfied.
-   Market escalation is an execution decision, not a strategy-selection
    decision.

## Market Order Safety Invariant

The implementation MUST enforce:

> Market orders are permitted only for `TradeIntent.DefensiveExit`.

Conceptual validation:

``` csharp
if (order.OrderType == TradeOrderType.Market &&
    order.TradeIntent != TradeIntent.DefensiveExit)
{
    throw new InvalidExecutionInstructionException(
        "Market orders are permitted only for defensive exits.");
}
```

Normally `OrderComposer` SHOULD still produce an initial Limit order for
a defensive exit. The Micro Execution component owns any later
conversion to Market.

## Instrument-Specific Defensive Escalation

V1 defaults:

``` text
ES Futures
    Defensive Market escalation: ALLOWED

Weekly Vertical Spread
    Defensive Market escalation: DISABLED by default

Monthly Iron Condor
    Defensive Market escalation: DISABLED by default
```

Options spreads should normally use increasingly aggressive combo Limit
execution because uncontrolled Market execution across multiple option
legs may create unacceptable slippage.

All instrument policies MUST be configuration-driven so later versions
can evolve without changing the application contracts.

## Defensive Exit Policy

Suggested framework configuration:

``` csharp
public sealed record DefensiveExitExecutionPolicy
{
    public required TimeSpan InitialLimitTimeout { get; init; }

    public required int MaxRepriceAttempts { get; init; }

    public required decimal MaxPriceChaseTicks { get; init; }

    public required bool AllowMarketEscalation { get; init; }

    public required TimeSpan MarketEscalationTimeout { get; init; }
}
```

Additional bounds MAY be introduced later, but V1 should remain small,
deterministic, and conservative.

## Responsibility Boundary

The V1 responsibility chain is:

``` text
Position Management
    determines WHY an open position must exit
        ↓
Order Composer
    creates WHAT exact trade order expresses the exit
        ↓
Portfolio Risk
    APPROVES or DENIES the complete order
        ↓
Micro Execution
    determines HOW the approved order is worked
        ↓
IOrderExecutionBroker
    performs the broker operation
```

Micro Execution MUST NOT:

-   select another strategy;
-   change option strikes;
-   change expiration;
-   change leg ratios;
-   add/remove hedge legs;
-   materially alter approved exposure;
-   bypass Portfolio Risk.

If an execution change would alter the risk-approved economic trade, the
existing order must be cancelled/abandoned and a newly composed order
must pass through `PortfolioRiskActor` again.

## Manual vs Algorithm Trading

Both `TradeMode.Manual` and `TradeMode.Algorithm` MUST use the same
execution safety policy.

Manual trading MUST NOT provide a route around:

-   Order Composer validation;
-   Portfolio Risk approval;
-   Micro Execution constraints;
-   Market-order restrictions;
-   broker lifecycle handling.

The origin of the trade may differ, but execution safety is shared.

## Emulator Requirements for Micro Execution

The emulator MUST support deterministic testing of:

-   Passive Limit entry.
-   Aggressive Limit entry where permitted.
-   Passive profit-taking exit.
-   Aggressive normal exit.
-   Defensive ES futures exit that fills as Limit.
-   Defensive ES futures exit that requires one or more reprices.
-   Defensive ES futures Limit → Market escalation.
-   Rejection of Market entry.
-   Rejection of Market profit exit.
-   Options-spread defensive exit with Market escalation disabled.
-   Partial fill during defensive exit.
-   Fill-before-cancel race during repricing.
-   Cancel/replace failure.
-   Market-data movement during defensive execution.

The same scenario configuration mechanism defined elsewhere in this
specification MUST control these cases.

## Backtesting Scope Boundary

The broker emulator MUST be designed so it can later be reused by a
backtesting/replay environment, but a backtesting framework is
explicitly OUT OF SCOPE for this specification.

Do not introduce backtester-specific abstractions into the broker
contracts during V1 implementation.

## Additional Acceptance Criteria

The following acceptance criteria are added:

21. `Limit` is the default V1 order type.
22. `TradeIntent` is represented explicitly on application-level trade
    orders.
23. V1 supports `PassiveLimit`, `AggressiveLimit`, and `DefensiveExit`
    micro-execution modes.
24. Market orders cannot be used for Entry, ProfitExit, or NormalExit.
25. Defensive exits begin with Limit execution by default.
26. ES Futures may escalate from Limit to Market under deterministic
    configured defensive-exit rules.
27. Weekly vertical and monthly iron-condor Market escalation is
    disabled by default.
28. Micro Execution cannot alter the risk-approved economic trade.
29. Manual and Algorithm trade modes use identical execution/risk safety
    constraints.
30. Emulator scenario tests cover Limit → Market defensive escalation
    and prohibited Market-order cases.

## Codex Implementation Note

Implement these V1.2 rules as extensions of the existing broker-neutral
application contracts. Do not add IBKR-specific fields to the Domain or
Application layers.

The initial implementation should favor correctness and deterministic
tests over sophisticated execution optimization. More advanced
micro-execution algorithms can be added later behind the same bounded
execution-policy contracts.
