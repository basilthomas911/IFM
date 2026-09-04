# QTS Operations UI Specification v1.0

**Codex implementation specification --- approved V1 design**

Target: existing WinForms trading application. Strategy is the default
Operations view. UI is read-only operational presentation; all
trading/business decisions remain outside the UI.

All Operations views must follow the normative
[Frontend Display-Only Policy](Frontend-Display-Only-Policy.md). Backend query
results and notifications are authoritative; frontend code formats and selects
data for display and must not silently reject records using duplicated business
or data-quality rules.

## 1. Operations Shell

Five views: **Strategy, Latency, Traffic, Errors, Saturation**.

``` text
SYSTEM 🟢 | STRATEGY 🟢 | LATENCY 🟢 | TRAFFIC 🟢 | ERRORS 🟡 | SATURATION 🟢
PAPER | ES | DATABENTO CONNECTED | IBKR CONNECTED
Strategy | Latency | Traffic | Errors | Saturation
```

`OperationalStatus = Unknown | Green | Yellow | Red`. Green=normal;
Yellow=degraded/stale/retrying/near threshold;
Red=failed/unsafe/critical; Unknown=uninitialized. View/global status is
the worst active status, never an average. Critical
portfolio/broker/feed conditions may force SYSTEM Red.

## 2. Strategy View --- Primary Operator Screen

Show **one strategy context at a time**: `DailyFutures`,
`WeeklyVerticalSpread`, `MonthlyIronCondor`. V1 driving asset is **ES
Futures**.

The view answers: What is ES doing? What does the deterministic system
believe it means? What strategy outcome resulted? What important events
happened recently?

``` text
┌──────────────────────────────────────────────────────────────────────────────┐
│ Monthly Iron Condor | Monthly | ES 6412.25 | 🟢 HEALTHY                    │
│ Intrinsic: Bullish | Regime: Range | Condition: Mean Reversion / Strong     │
├──────────────────────────────────────────────────────────────────────────────┤
│                         ES FUTURES PRICE GRAPH                               │
│ Price ▲                 TE                                                   │
│       │                  ●                                                    │
│       │             ╭───╯ ╰────╮                  ╭────╮                     │
│       │        ╭────╯           ╰────╮       ╭────╯    ╰──╮                  │
│       │────────●──────────────────────●───────●──────────────               │
│               DC                     TR      DC                             │
│       └──────────────────────────────────────────────────────────► Time       │
├──────────────────────────────────────────────┬───────────────────────────────┤
│ STRATEGY OUTCOME TIMELINE                    │ CURRENT OBSERVATION FLOW      │
│ Regime ●── Condition ●── Strategy ●── Risk ● │ Regime 🟢 Range              │
│                                      ── Open ●│ ↓ Condition 🟢 MeanRev       │
│                                              │ ↓ Strategy 🟢 Iron Condor     │
│                                              │ ↓ Risk 🟢 Approved            │
│                                              │ ↓ Position 🟢 Open            │
│                                              │ ↓ FTP 🟢 +$615               │
├──────────────────────────────────────────────┴───────────────────────────────┤
│ RECENT STRATEGY EVENTS                                                       │
│ Time | Horizon | Actor | Event | Instrument | Status | Summary               │
├──────────────────────────────────────────────────────────────────────────────┤
│ SELECTED EVENT DETAIL                                                        │
└──────────────────────────────────────────────────────────────────────────────┘
```

Recommended split: top 55--60%, bottom 40--45%; graph dominates top;
timeline beneath graph; Observation Flow at timeline right; splitters
resizable.

### 2.1 Intrinsic-Time Price Graph

MUST show ES reference-price series, axes, `DC` Directional Change, `TE`
Trend Extreme, `TR` Trend Reversal, current reference price, and trade
entry/exit markers when applicable. MAY show tooltip/cursor.

MUST NOT overlay RSI, ADX, Bollinger Bands, EMA lines, or conventional
indicators in V1. It is **Price + Intrinsic Time + trade lifecycle
markers**. Markers come from domain events; UI never recalculates them.

Default windows: Daily=current/recent sessions; Weekly=days to weeks;
Monthly=weeks to months. Exact windows configuration-driven. Zoom/pan
allowed and independent from timeline.

``` mermaid
flowchart LR
 A[Databento] --> B[Market Data Shard]
 B --> C[ReferencePriceChanged]
 C --> D[IntrinsicTimeActor]
 C --> E[UI Price Projection]
 D --> F[DC / TE / TR]
 E --> G[Price Graph]
 F --> G
```

UI MUST NOT subscribe to raw ticks.

### 2.2 Strategy Outcome Timeline and Observation Flow

Timeline shows important business outcomes, not every event:
`MarketRegimeDiscovered`, `MarketConditionIdentified`,
`StrategySelected`, `OrderComposed`, `OrderApproved`, `OrderDenied`,
`PositionOpened`, `PositionStateChanged`, `ExitRequested`,
`PositionClosed`. Nodes expose timestamp, label, status, tooltip,
selection. Timeline does not need pixel/time synchronization with graph.

Observation Flow is current state, not history:

``` text
Regime 🟢 Range / Normal Volatility
 ↓
Condition 🟢 Mean Reversion / Strong
 ↓
Strategy 🟢 Monthly Iron Condor
 ↓
Portfolio Risk 🟢 Approved
 ↓
Position 🟢 Open
 ↓
ForwardTradePrice 🟢 +$615
```

Keep it semantic and compact. Detailed numerical evidence belongs in
event detail.

``` mermaid
flowchart LR
 A[Intrinsic Time] --> B[Regime Discovery] --> C[Market Condition]
 C --> D[Strategy Selector] --> E[Order Composer] --> F[Portfolio Risk]
 F --> G[Order Execution] --> H[Position Management] --> I[ForwardTradePrice]
 I --> J[Hold / Exit]
```

### 2.3 Strategy Event Grid and Detail

Virtual/bounded grid columns:
`Timestamp | Horizon | Actor | Event | Instrument | Status | Summary`.
Filter to selected strategy context. `OrderDenied` is a valid
deterministic outcome and is not automatically operational Red.

Selected detail common fields: timestamp, actor, event name/id, stream
id/version, instrument, horizon, trace/correlation IDs, status, summary.
Add schema-specific details: - MarketCondition:
condition/direction/strength/phase/confidence/evidence. -
OrderApproved/Denied: composed order, risk metadata/reasons. -
ForwardTradePrice: value, mark, Greeks, position state. -
EventProjector: last completed stage, failed stage, resume stage,
retries.

Strategy status is worst relevant state across Market Data, Intrinsic
Time, Regime Discovery, Market Condition, Strategy Selection, Order
Composition, Portfolio Risk, Position Management, and active
broker/execution.

## 3. Diagnostic Views

### Latency

Question: **Is anything taking longer than expected?** Top virtual grid,
bottom selected detail. Columns: Time, Component, Operation, Duration,
Warning, Critical, Status, Actor/Shard. Track tick→price-change, mailbox
wait, actor processing, event-store append, Scylla projection, NATS
publication, ForwardTradePrice, IBKR submission/ack, DB query.
Thresholds configuration-driven.

### Traffic

Question: **Is data/message flow normal?** Columns: Time, Source,
Destination, Message/Event, Current Rate, Queue Depth, Status. Track
Databento, ReferencePriceChanged, actor messages, NATS events,
event-store appends, projection/replay queues, IBKR, order lifecycle.
Status is contextual; zero expected ES traffic can be Red.

### Errors

Question: **What failed or requires intervention?** Columns: Time,
Component, Actor, Error, Severity, Retry Count, Status. Detail includes
error code/type/message, source event, stream/version, workflow stage,
retries, first/last occurrence, last successful stage, resume stage,
trace/correlation ID. EventProjector failures integrate here.

### Saturation

Question: **Is anything approaching capacity?** Track CPU/P-cores,
memory, NVMe utilization/queue depth, PostgreSQL pool, Scylla
storage/latency, Redis memory, JetStream storage, actor mailboxes,
market-data rings, projection/replay queues, network, disk. Columns:
Time, Resource, Current, Warning, Critical, Status.

## 4. Application/UI Contracts

``` csharp
public enum OperationView : byte { Strategy=1, Latency=2, Traffic=3, Errors=4, Saturation=5 }

public sealed record OperationalEvent
{
    public required DateTimeOffset Timestamp { get; init; }
    public required OperationView View { get; init; }
    public required string Component { get; init; }
    public string? Actor { get; init; }
    public required string EventName { get; init; }
    public required OperationalStatus Status { get; init; }
    public required string Summary { get; init; }
    public Guid? EventId { get; init; }
    public Guid? StreamId { get; init; }
    public long? StreamVersionId { get; init; }
    public string? TraceId { get; init; }
    public object? Detail { get; init; }
}
```

Do not expose database/NATS/OTEL DTOs directly to WinForms.

Strategy current state SHOULD use a `StrategyOperationsSnapshot`
containing context, status, driving instrument, reference price,
IntrinsicTime summary, Regime, MarketCondition, Strategy, PortfolioRisk,
Position, ForwardTradePrice, and AsOf timestamp.

**Snapshot = current state. Event grid = history.**

## 5. UI Structure

``` text
OperationsView
├── OperationsHeader
└── OperationsTabControl
    ├── StrategyOperationsView
    │   ├── StrategyHeaderView
    │   ├── IntrinsicTimePriceChart
    │   ├── StrategyOutcomeTimeline
    │   ├── StrategyObservationFlow
    │   ├── StrategyEventGrid
    │   └── StrategyEventDetailView
    ├── LatencyOperationsView
    ├── TrafficOperationsView
    ├── ErrorOperationsView
    └── SaturationOperationsView
```

Diagnostic views may reuse `OperationalGridView` +
`OperationalDetailView`. Strategy remains specialized.

Use `IOperationalDetailProvider` (or equivalent) for event-specific
detail rendering with a safe generic fallback.

Data sources: Strategy graph=ReferencePrice projection;
markers=Intrinsic Time events; Strategy grid=domain operational read
model; Latency=OTEL/internal timings; Traffic=counters/queues/NATS;
Errors=structured logs/workflow failures;
Saturation=host/runtime/database/queue/ring metrics. Controls use
application query/read services, never direct production DB access.

## 6. Performance, Threading, Retention

-   Virtualize every grid.
-   Keep configurable recent rows (suggested 500--2,000) in UI memory.
-   Query older data on demand.
-   Never render every market tick.
-   Coalesce transient/high-frequency updates.
-   All control mutations occur on WinForms UI thread.
-   Dedicated market-data/actor threads MUST NOT block on UI.

``` mermaid
flowchart LR
 A[NATS / Application Listener] --> B[ViewModel Update Queue]
 B --> C[UI Coalescer]
 C --> D[WinForms UI Thread]
 D --> E[Controls]
```

Suggested maximum refresh: graph 5--10 Hz; grids 2--5 Hz batched; status
immediate where practical or \<=250 ms.

Centralize status thresholds/policies in application configuration; do
not hard-code thresholds in controls.

## 7. Interaction and Resilience

Strategy supports context switch, graph zoom/pan, timeline selection,
grid selection, detail inspection, splitter resizing, optional
auto-scroll pause.

V1 excludes strategy/risk editing, order entry from Operations, chart
drawing, parameter mutation, AI commentary, and arbitrary dashboards.

Operational query/read failures MUST NOT crash UI. Show Yellow/stale
with last successful timestamp or Red/unavailable.

Persist only UI preferences: selected tab/context, grid widths/sort,
splitter positions, optional chart range. Never persist business state
in UI settings.

## 8. Testing

Unit: status aggregation, strategy filtering, threshold policy, detail
provider selection, Observation Flow mapping, bounded collections,
ViewModel coalescing.

Component: strategy switching, graph updates, DC/TE/TR rendering,
timeline rendering/selection, event selection/detail, status changes.

Integration deterministic sequence:

``` text
ReferencePriceChanged → TrendDirectionChanged → MarketRegimeDiscovered
→ MarketConditionIdentified → StrategySelected → OrderComposed
→ OrderApproved → PositionOpened → ForwardTradePriceUpdated
→ ExitRequested → PositionClosed
```

Verify graph markers, timeline, Observation Flow, event rows, detail,
status.

Failure tests: Databento/IBKR disconnect, stale price, projection replay
active, queue/ring saturation, EventProjector failure, DB latency
breach, UI query failure.

Performance tests: event bursts do not freeze UI; virtualization
responsive; graph bounded; batching works; no dedicated worker blocks on
UI.

## 9. Acceptance Criteria

1.  Operations is default main application tab.
2.  Five sub-views exist and expose Green/Yellow/Red.
3.  Strategy is default sub-view and shows one context at a time.
4.  Large ES price graph renders reference price plus DC/TE/TR.
5.  Outcome timeline and current Observation Flow are visible
    beneath/beside graph.
6.  Recent Strategy grid is virtualized and selection renders structured
    detail.
7.  Latency/Traffic/Errors/Saturation use top-grid/bottom-detail layout.
8.  UI refresh is decoupled from high-frequency processing.
9.  No view contains trading business logic.
10. Application-level read models/query services are used.
11. UI remains responsive under simulated production load.
12. Operational failures propagate visibly to status.

## 10. Codex Implementation Sequence

Implement incrementally and require build/tests to pass after every
stage:

1.  Operations shell + tabs + status model.
2.  Shared virtual operational grid/detail controls.
3.  Latency/Traffic/Errors/Saturation.
4.  Strategy header/context selector.
5.  ES reference-price graph.
6.  DC/TE/TR markers.
7.  Strategy Outcome Timeline.
8.  Current Observation Flow.
9.  Strategy event grid + detail providers.
10. UI coalescing, performance tests, failure tests.

Do not implement the entire specification in one change.

## 11. Non-Goals

No WPF/Avalonia rewrite, Level-2 visualization, LLM control/commentary,
strategy optimization UI, Monte Carlo research UI, risk/strategy
parameter editing, arbitrary dashboard builder, or manual chart drawing
in V1.

## 12. Final Operator Principle

``` text
NORMAL OPERATION:
Strategy View
    Price first
        ↓
    Current interpretation
        ↓
    Strategy outcome
        ↓
    Recent business events
        ↓
    Detailed diagnostics

WHEN YELLOW/RED:
Latency | Traffic | Errors | Saturation
```

The operator should normally remain on Strategy and switch to a
diagnostic view only when status indicates investigation is required.
