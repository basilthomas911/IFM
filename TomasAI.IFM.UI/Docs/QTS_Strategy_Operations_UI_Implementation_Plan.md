# QTS Strategy Operations UI Implementation Plan

## 1. Objective

Implement the approved QTS Operations UI incrementally in the existing WinForms application, with:

- the Operations region hosted in the left side of `operationViewSplitter`;
- Operations and Market regions each defaulting to 20% of the usable shell width;
- five selectable tabs: Strategy, Latency, Traffic, Errors, and Saturation;
- Strategy selected by default and completed first;
- safe placeholders for the other four tabs until their own implementation tranches;
- framework-neutral `net10.0` Models and ViewModels that can be reused unchanged by a future WPF view; and
- no trading, risk, market-classification, or event-correlation logic implemented in WinForms controls.

This plan implements the Strategy portion of
[`QTS_Operations_UI_Specification_v1.0.md`](QTS_Operations_UI_Specification_v1.0.md). It does not implement the four
diagnostic views beyond their selectable shell placeholders.

## 2. Required end state

```text
IFMAppView
├── operationViewSplitter.Panel1 (20% default)
│   └── OperationsView
│       ├── OperationsHeaderView
│       └── OperationsTabControl
│           ├── StrategyOperationsView (default; complete)
│           ├── LatencyOperationsView (safe placeholder)
│           ├── TrafficOperationsView (safe placeholder)
│           ├── ErrorOperationsView (safe placeholder)
│           └── SaturationOperationsView (safe placeholder)
├── center workspace (approximately 60%)
└── marketViewSplitter.Panel2 (20% default)
    └── existing Market view
```

The Strategy view must answer:

1. What is ES doing now?
2. What deterministic interpretation has the backend produced?
3. What strategy, risk, order, and position outcomes resulted?
4. What important events occurred recently?
5. Is any required input stale, missing, degraded, or failed?

## 3. Architectural boundaries

### 3.1 Framework-neutral presentation layer

New Models and ViewModels remain in the existing `net10.0` projects:

- `TomasAI.IFM.UI.Net.Models/Operations/`
- `TomasAI.IFM.UI.Net.ViewModels/Operations/`

They must not reference:

- `System.Windows.Forms`;
- WPF namespaces;
- `System.Drawing`;
- chart-control types;
- `Control`, `Form`, `Dispatcher`, or `MessageBox`; or
- database, NATS, or OpenTelemetry transport DTOs as bindable UI state.

They expose semantic records, observable properties, bounded collections, asynchronous operations, cancellation, and
asynchronous disposal. Status is represented by `OperationalStatus`, not by a color. WinForms and future WPF views map
semantic status and chart data to their own controls and palettes.

### 3.2 Backend authority

The UI never infers market regime, market condition, strategy selection, risk approval, position state, or exit intent.
Those values must come from authoritative backend contracts/read models. Missing values are displayed as
Unknown/stale with an `AsOf` timestamp and missing-input description.

### 3.3 Current state versus history

- `StrategyOperationsSnapshot` is the coherent current state.
- `StrategyOperationalEvent` is bounded/paged history.
- Price points and intrinsic-time markers are bounded chart series.

The UI must not reconstruct current business state by replaying an unbounded event stream.

### 3.4 Delivery and threading

```text
Backend query + notify/event boundaries
                    ↓
StrategyOperationsModel / event consumers
                    ↓
StrategyOperationsViewModel bounded update queues
                    ↓
WinForms UI-thread adapter
                    ↓
OperationsView controls
```

Replaceable current state uses latest-value coalescing. Ordered business outcomes use a bounded lossless FIFO with
explicit overflow behavior. No market-data, actor, NATS, or storage thread may block on UI rendering.

## 4. Existing-data inventory and required gaps

| Strategy requirement | Existing source | Planned treatment |
| --- | --- | --- |
| ES reference-price series | Futures-bar query/event flow and `FuturesBarDataReadModel` | Reuse bounded 15-second bars; expose presentation-only price points |
| ITI changes and DC/TE/TR markers | `FuturesItiSignalUpdatedNotifyEvent` and `FuturesItiSignalV2ReadModel` | Reuse the committed Notify boundary and lifecycle-owned Operations consumer; map every ITI mode, not only chart-marker modes |
| Current market outlook | `MarketOutlookSnapshotReadModel` and `MarketOutlookUpdatedNotifyEvent` | Reuse as one input to the Strategy snapshot |
| Current futures trade signal | `FuturesTradeSignalV2ReadModel` and notify consumer | Reuse through a multi-subscriber boundary; do not duplicate calculations |
| Trade-placement outcome | Existing placement events/consumer | Map to semantic Strategy outcomes |
| Position state | Existing trade-position queries/events | Map active position and lifecycle state |
| Market regime | No complete named contract matching the specification was found | Backend contract/read-model decision required at Gate G0 |
| Market condition | No complete named contract matching the specification was found | Backend contract/read-model decision required at Gate G0 |
| Strategy selection and order composition | No complete unified operational contract was found | Add authoritative events/read-model fields or formally map existing domain outcomes |
| Portfolio-risk approval/denial | No complete unified operational contract was found | Add authoritative outcome and reasons; never infer in UI |
| ForwardTradePrice | No complete `ForwardTradePriceUpdated` operational contract was found | Define source, timestamp, units, and unavailable/stale behavior |
| Coherent Strategy snapshot | Not present | Add an application/domain coordinator and query/notify contract |
| Paged recent Strategy event history | Not present | Add an append-only operational projection and bounded keyset query |
| Health/staleness | Partial stream metrics exist | Add configuration-driven freshness and worst-status policy |

## 5. Contract and persistence design

The exact owning bounded context is decided at Gate G0, but the resulting public surface must provide equivalent typed
contracts for:

- `GetStrategyOperationsSnapshotQuery`;
- `GetStrategyOperationalEventsQuery` with bounded keyset paging;
- `StrategyOperationsUpdatedNotifyEvent` containing a revisioned coherent snapshot; and
- `StrategyOperationalEventRecordedNotifyEvent` or an equivalent ordered incremental-history notification.

The snapshot includes at minimum:

- strategy context and driving instrument;
- overall and component statuses;
- reference price and `AsOf` time;
- intrinsic-time summary;
- market regime and market condition;
- selected strategy;
- portfolio-risk outcome and reasons;
- order/position state;
- ForwardTradePrice;
- snapshot revision and last update;
- missing/stale input descriptions; and
- correlation identifiers required for detail lookup.

The history row includes at minimum:

- timestamp, context, horizon, actor, event name, instrument, status, and summary;
- event/stream identifiers and stream version where available;
- trace/correlation/command/workflow identifiers; and
- a typed or safely discriminated detail payload.

If persistence is required, add versioned migrations for:

- one latest snapshot per strategy context/instrument; and
- append-only operational event history ordered by context/instrument/time/sequence.

Storage selection, retention, partitioning, and backfill are evidence-based Gate G0 decisions. Schema changes must be
backward compatible. Existing MessagePack key positions are never renumbered; new optional fields are appended.

## 6. Implementation gates

Every gate is independently reviewable. Build and relevant automated tests must pass before proceeding to the next
gate.

### First milestone — Minimal Futures ITI Strategy view

This is the first executable goal while the remaining Strategy contracts are being defined:

1. Publish `FuturesItiSignalUpdatedNotifyEvent` after each successful durable or realtime ITI projection.
2. Subscribe through a multi-subscriber `ActorType.Notify` UI boundary and forward every valid ITI mode.
3. Subscribe before querying the latest Daily, Weekly, and Monthly signals so startup changes are merged without gaps.
4. Show a bounded, newest-first 500-row ITI list in the default Strategy tab, with selected authoritative detail.
5. Make Latency, Traffic, Errors, and Saturation selectable, inert placeholders.
6. Preserve the framework-neutral `net10.0` Models/ViewModels boundary and lifecycle-owned shutdown.

No database migration is required for this milestone. Complete historical backfill remains part of the later bounded
operational event-history projection; the first milestone seeds the latest three period snapshots and retains every
committed live change observed thereafter.

Exit criteria:

- durable and realtime completion paths publish the notification without allowing notification failure to reverse a
  persisted ITI signal;
- every `IntrinsicTimeModeType` value reaches the Strategy ViewModel;
- filtering, ordering, exact duplicate suppression, startup reconciliation, 500-row capacity, and stop/dispose are
  covered by tests;
- Strategy is selected by default and all five Operations views remain selectable; and
- affected builds, domain tests, presentation tests, and a real NATS integration path pass.

### Gate G0 — Data coverage and contract freeze

Steps:

1. Trace each Strategy field to an authoritative query, event, and owning backend component.
2. Decide the exact meanings of Regime, Condition, Strategy, Risk, Position, and ForwardTradePrice.
3. Decide Daily/Weekly/Monthly context identity and chart windows.
4. Define status/staleness thresholds in configuration.
5. Define snapshot revision ordering, event ordering, retention, pagination, and deduplication.
6. Decide storage and migration/backfill requirements.
7. Approve typed shared contracts and MessagePack compatibility rules.

Exit criteria:

- no Strategy UI field depends on an undocumented calculation or string convention;
- every mandatory field has an authoritative source or an approved Unknown/unavailable state;
- contract serialization round-trip tests pass; and
- schema migration and rollback plans are reviewed when persistence changes are required.

### Gate G1 — Operations shell and MVVM foundation

Steps:

1. Add framework-neutral `OperationView`, `OperationalStatus`, component-status, tab-state, and presentation records.
2. Add `OperationsViewModel` with selected tab and aggregated header status.
3. Add WinForms `OperationsView` to `pnlOperationView`.
4. Add all five tabs immediately and make Strategy the explicit default.
5. Add safe, clearly labelled placeholders for Latency, Traffic, Errors, and Saturation.
6. Persist only the selected tab if preference persistence is approved; never persist business state.
7. Wire startup, close, and reopen lifecycle without starting placeholder-tab data sources.

Exit criteria:

- the shell opens with Strategy selected;
- all five tabs can be selected repeatedly without exceptions or backend work from placeholders;
- the 20/60/20 splitter layout remains intact and resizable;
- Models/ViewModels compile as `net10.0` without UI-framework references; and
- shell lifecycle and tab-selection unit/component tests pass.

### Gate G2 — Backend Strategy snapshot coordinator

Steps:

1. Implement the approved Strategy snapshot contracts.
2. Implement a coordinator that owns current component state and revision ordering.
3. Consume relevant asynchronous domain changes without blocking their publishers.
4. Publish one coherent Strategy snapshot notification per meaningful semantic change, with bounded coalescing for
   replaceable price-only state.
5. Expose a query for deterministic initial/reconnect loading.
6. Add persistence/migrations if Gate G0 requires durable current state.
7. Report missing inputs and staleness explicitly rather than throwing expected availability exceptions.

Exit criteria:

- query and notify paths return the same revisioned state;
- duplicate/out-of-order input tests preserve monotonic snapshots;
- missing, stale, disconnect, and recovery scenarios have deterministic status;
- serialization, domain unit, API integration, and storage migration tests pass; and
- no UI-specific type enters backend contracts.

### Gate G3 — Operational event-history projection

Steps:

1. Normalize approved business outcomes into `StrategyOperationalEvent` rows.
2. Preserve source identifiers and correlation/trace metadata.
3. Implement idempotent append/projection behavior.
4. Add bounded keyset paging and filters for context, instrument, status, and time.
5. Add an incremental notification for newly committed rows.
6. Apply configured retention without affecting authoritative domain/event stores.

Exit criteria:

- the deterministic reference sequence is represented in correct order;
- replay/retry does not create duplicate rows;
- paging is stable while new events arrive;
- retention and query bounds are enforced; and
- projection and real-storage integration tests pass.

### Gate G4 — Strategy Models, ViewModel, and event lifecycle

Steps:

1. Add presentation records such as `StrategyPricePoint`, `IntrinsicTimeMarker`, `StrategyObservationStep`,
   `StrategyTimelineNode`, `StrategyEventRow`, and typed detail models.
2. Implement `StrategyOperationsModel` for typed queries and independently owned event consumers.
3. Implement `StrategyOperationsViewModel` as `ObservableObject` and `IAsyncDisposable`.
4. Load the snapshot, bounded price/marker window, and first event page before starting live updates.
5. Reconcile query/listener races using revision, sequence, and event identifiers.
6. Use latest-value channels for snapshot/price updates and ordered bounded channels for event history.
7. Aggregate overall status as the worst active component status.
8. Surface coded presentation errors and stale state without exception-driven normal control flow.

Exit criteria:

- ViewModel tests cover mapping, status aggregation, filtering, ordering, deduplication, bounds, cancellation, and
  disposal;
- reconnect produces one coherent current state without duplicate history;
- no public ViewModel member exposes WinForms, WPF, drawing, chart, NATS, or database types; and
- architecture tests enforce the framework-neutral boundary.

### Gate G5 — Strategy header and current Observation Flow

Steps:

1. Add a compact Strategy header with context, ES price, overall status, and freshness.
2. Add a Daily/Weekly/Monthly context selector using backend-defined context identity.
3. Add the vertical current Observation Flow: Regime, Condition, Strategy, Risk, Position, ForwardTradePrice.
4. Map semantic statuses to the existing WinForms presentation palette.
5. Show Unknown/stale/missing states and last-success timestamps explicitly.
6. Make the narrow 20% panel DPI-safe, keyboard accessible, and vertically scrollable where required.

Exit criteria:

- every displayed value is traceable to the Strategy snapshot;
- context switching cannot mix revisions or histories;
- color is never the only status indicator;
- supported DPI/font scaling does not clip labels or values; and
- component/UI Automation tests pass.

### Gate G6 — ES price graph and intrinsic-time markers

Steps:

1. Reuse the existing WinForms DataVisualization package only in the Views project.
2. Render bounded ES reference-price points from the ViewModel.
3. Render DC, TE, and TR markers from authoritative intrinsic-time events.
4. Add current price, axes, tooltips, and configured Daily/Weekly/Monthly windows.
5. Add optional trade entry/exit markers only when backed by authoritative domain events.
6. Coalesce rendering to a configured maximum of 5–10 Hz.
7. Preserve chart data as framework-neutral point/marker records so WPF can use a different chart control unchanged.

Exit criteria:

- the chart never subscribes to or renders raw ticks;
- marker time/price/type mapping is deterministic;
- series are bounded and discontinuities/stale data are visible;
- update bursts do not block backend or UI threads; and
- chart component and burst-performance tests pass.

### Gate G7 — Outcome timeline, event grid, and selected detail

Steps:

1. Render the ordered Strategy outcome timeline from normalized operational events.
2. Add the bounded/virtual recent-event grid.
3. Add selection and typed detail providers with a safe generic fallback.
4. Add incremental loading of older pages on demand.
5. Add filter/context behavior without losing selection correctness.
6. Use responsive columns and an expandable detail area for the 20% pane; retain every specified field even when a
   narrow layout initially hides optional columns.

Exit criteria:

- timeline, grid, and detail agree on event identity and ordering;
- selection remains correct while new events arrive;
- the in-memory collection remains within its configured 500–2,000 row bound;
- unavailable typed details fall back safely; and
- component, paging, and accessibility tests pass.

### Gate G8 — Resilience, performance, and failure behavior

Steps:

1. Exercise Databento/feed disconnect, stale price, NATS reconnect, query failure, projection replay, event burst,
   malformed payload, and shutdown during updates.
2. Display Yellow/stale or Red/unavailable according to configured policy.
3. Ensure normal missing-data states do not throw first-chance exceptions.
4. Measure queue depth, dropped/coalesced updates, dispatcher delay, and render duration.
5. Verify chart refresh at 5–10 Hz maximum, grids at 2–5 Hz batched, and status visibility within 250 ms where
   practical.
6. Verify all listeners/processors are owned, stopped, and asynchronously disposed.

Exit criteria:

- simulated production bursts do not freeze the shell;
- no unbounded queue or collection exists;
- no dedicated backend worker blocks on UI rendering;
- failures remain visible and recovery clears stale state correctly; and
- performance/failure tests meet approved thresholds.

### Gate G9 — End-to-end and operator acceptance

Steps:

1. Run the deterministic sequence:
   `ReferencePriceChanged → TrendDirectionChanged → MarketRegimeDiscovered → MarketConditionIdentified →
   StrategySelected → OrderComposed → OrderApproved/Denied → PositionOpened → ForwardTradePriceUpdated →
   ExitRequested → PositionClosed`.
2. Verify snapshot, graph markers, Observation Flow, timeline, event grid, detail, and status at every transition.
3. Verify all five tabs remain selectable and Strategy remains the startup default.
4. Run full presentation, domain, API, storage, integration, and UI-system-test suites affected by the change.
5. Perform user-driven Development acceptance against the real backend.
6. Update implementation details, schema notes, operational runbook, and test evidence.

Exit criteria:

- all automated gates pass with no unexplained exceptions or log noise;
- the operator confirms the Strategy view is readable and responsive at the default 20% width;
- startup, reconnect, context switching, splitter movement, and shutdown pass; and
- rollback/feature-disable behavior is proven.

## 7. Test matrix

| Layer | Required coverage |
| --- | --- |
| Contract | serialization compatibility, validation, versioning, optional fields |
| Domain/coordinator | revision ordering, missing inputs, staleness, status, deduplication |
| Storage | migrations, idempotent projection, paging, retention, rollback |
| API/NATS | initial query, notification routing, reconnect, multi-subscriber ownership |
| Model/ViewModel | mapping, context switching, bounds, coalescing, errors, cancellation, disposal |
| Architecture | `net10.0`; no WinForms/WPF/drawing/chart/database types in Models/ViewModels |
| WinForms component | default tab, five selectable tabs, binding, DPI, accessibility, selection/detail |
| Performance | burst responsiveness, refresh limits, queue bounds, dispatch/render timing |
| System | real shell/backend deterministic sequence, failure/recovery, clean shutdown |

## 8. Rollout and rollback

1. Add an `Operations:Enabled` configuration switch while implementation is incomplete.
2. Keep incomplete diagnostic tabs visible but inert and clearly labelled; do not start their listeners.
3. Enable Strategy only after G0–G7 pass in Development.
4. Run G8 soak/failure evidence before making Strategy generally active.
5. Rollback disables the Operations host/listeners without changing existing Market or trading workflows.
6. Database migrations, if required, must have an approved backward-compatible rollback or forward-fix procedure.

## 9. Definition of Strategy complete

Strategy is complete only when:

- it is the default Operations tab and the other four tabs remain selectable;
- it displays coherent current state, bounded price history, DC/TE/TR markers, outcome timeline, Observation Flow,
  recent event history, and selected detail;
- every business value is backend-authored and every missing/stale value is explicit;
- Models/ViewModels are reusable by WPF unchanged;
- performance, failure, lifecycle, accessibility, and end-to-end gates pass; and
- the operator accepts the view at the default 20% width.

## 10. Planned delivery order

Deliver one reviewable change per gate in this order:

1. G0 contract/data decision record.
2. G1 Operations shell and five tabs.
3. G2 Strategy snapshot backend.
4. G3 operational event-history projection.
5. G4 framework-neutral Strategy Models/ViewModel.
6. G5 header and Observation Flow.
7. G6 graph and intrinsic-time markers.
8. G7 timeline, grid, and detail.
9. G8 resilience/performance hardening.
10. G9 system and operator acceptance evidence.

No later gate may compensate for missing earlier backend semantics by adding calculations to the UI.
