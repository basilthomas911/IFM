# Strategy Workflow State UI Implementation Plan v1.0

**Status:** Implemented and verified; gates SWUI-0 through SWUI-7 complete  
**Date:** 2026-09-09  
**Owner:** Strategy Operations UI / Domain.Trade Strategy Workflow  

## Implementation and verification record

All gates in this plan were completed on 2026-09-09.

| Gate | Result | Evidence |
| --- | --- | --- |
| SWUI-0 | Complete | Approved fields, stage semantics, end-state mapping, stable identities, and append-only MessagePack contract are implemented and contract-tested. |
| SWUI-1 | Complete | Each successfully projected workflow snapshot publishes the isolated `ActorType.Notify` event; projector tests prove notification failure cannot stop workflow dispatch. |
| SWUI-2 | Complete | The multi-subscriber UI consumer, workflow query/hydration service, listener lifecycle, and startup registrations are implemented. A real NATS test verified independent subscribers and stopping only one subscriber. |
| SWUI-3 | Complete | Subscribe-before-query loading, bounded eight-way hydration, monotonic revision reduction, same-revision conflict diagnostics, timeframe filtering, selection retention, 30-second recovery, and disposal fencing are implemented. |
| SWUI-4 | Complete | The six-column workflow list and progressive RD/MC/TS/OC/RM owner drawing are implemented. Tests cover every stopping actor, approval, rejection, exact bright colors, real rendered pixels, and matched/unmatched chart highlighting. |
| SWUI-5 | Complete | The read-only Details/Summary tabs, complete workflow metadata, all five persistent stage sections, typed result rendering, failures, opaque-result handling, and the exact missing-result text are implemented. Summary remains a static unavailable placeholder. |
| SWUI-6 | Complete | Reconciliation recovers a missed terminal revision, terminal snapshots use a bounded 500-entry cache, visible workflows are bounded to 500, and no latency qualification threshold was introduced. |
| SWUI-7 | Complete | Affected domain, presentation, architecture, rendering, WinForms, and live NATS checks pass; the application builds with zero warnings and errors. |

Observed development timings from the repeatable presentation fixture were **76.154 ms** for initialization plus hydration
of one persisted workflow and **1.950 ms** for a selected live revision reduction plus complete Details regeneration.
These are diagnostic observations from in-process test doubles, not production qualification limits. The real NATS
multi-subscriber acceptance test completed in **807 ms**, including a deliberate 250 ms listener-settling delay and three
published revisions.

Validation evidence:

- `TomasAI.IFM.UI.Net.Presentation.UnitTests`: 371 passed;
- `TomasAI.IFM.Domain.Trade.UnitTests`: 1,051 passed;
- `TomasAI.IFM.UI.Net.SystemTests`: 196 passed with the three unrelated MC-R08 artifact-driven observation tests excluded;
- live NATS Strategy workflow UI acceptance: 1 passed against `nats://localhost:4222`; and
- `TomasAI.IFM.UI.Net` build: succeeded with 0 warnings and 0 errors.

The three excluded MC-R08 tests require a separately generated `IFM_MC_EVIDENCE_DIR` runtime-qualification artifact.
They do not exercise this Strategy workflow list delivery or any SWUI gate.

## 1. Objective

Update the existing Strategy Operations view so an operator can follow every accepted Intrinsic Time Strategy workflow
from its triggering Futures ITI signal through the five pipeline actors.

The delivery shall:

- preserve the existing Intrinsic Time graph at the top of the Strategy view;
- replace the current signal-event list with a newest-first list of accepted strategy workflows;
- show the signal date/time, readable Futures ITI event type, trend, price, pipeline progress, and workflow end state;
- show pipeline actors as a horizontal, progressively revealed sequence of colored circles;
- update a workflow row whenever a newer authoritative workflow snapshot is committed;
- replace the bottom property grid with `Details` and `Summary` tabs;
- show the complete accumulated pipeline results in `Details`; and
- retain `Summary` as an unavailable placeholder for a later delivery.

LLM summary generation, deterministic summary generation, MAF hosting, summary persistence, summary notifications, and
summary tests are explicitly out of scope.

## 2. Approved presentation decisions

### 2.1 Workflow list columns

The workflow list shall contain these columns in order:

1. **Date/Time** — the triggering `FuturesItiSignalGeneratedEvent.CreatedOn`, formatted in Eastern Time using the
   Strategy view's existing time formatting conventions.
2. **Futures ITI Signal Event** — the readable intrinsic-time change/mode, such as `TrendDirectionChanged`.
3. **Trend Type** — the triggering signal's `IntrinsicTimeTrend`.
4. **Futures Price** — the triggering signal's `IntrinsicPrice` using the existing invariant price format.
5. **Pipeline State** — the dynamically revealed horizontal pipeline circles.
6. **Workflow End State** — an operator-facing label derived from the authoritative machine status and business outcome.

The trigger event GUID, workflow ID, correlation ID, timestamps, revisions, and technical provenance belong in
`Details`, not in the compact workflow list.

### 2.2 Pipeline actor order and labels

Circles always appear from left to right in workflow execution order:

| Position | Short label | Pipeline actor |
| --- | --- | --- |
| 1 | `RD` | Regime Discovery |
| 2 | `MC` | Market Condition |
| 3 | `TS` | Trade Selection |
| 4 | `OC` | Order Composition |
| 5 | `RM` | Risk Management |

A circle is not rendered until its actor has started. Whichever actor fails, times out, is cancelled, or commits a stop
decision becomes the final red circle; completed prior actors retain their committed colors and later actors remain
hidden. For example, a workflow that fails in Regime Discovery shows one red circle, while a workflow currently
processing Order Composition shows three green circles followed by one yellow circle.

### 2.3 Circle state mapping

The UI shall map the authoritative `StrategyWorkflowStageState` without reproducing business validation:

| Authoritative stage state | Display |
| --- | --- |
| `NotStarted` with no start timestamp | Circle hidden |
| `Processing` | Bright yellow |
| `Completed` with continuation `None` | Bright yellow; the workflow has not committed its continuation yet |
| `Completed` with continuation `Proceed` | Bright green |
| `Completed` with continuation `Stop` | Bright red |
| `Failed`, `TimedOut`, or `Cancelled` | Bright red |

Approved examples:

| Workflow condition | Visible circles |
| --- | --- |
| Regime Discovery processing | yellow `RD` |
| Regime Discovery failure | red `RD` |
| Market Condition processing | green `RD`, yellow `MC` |
| Market Condition stops with No Trade | green `RD`, red `MC` |
| Trade Selection failure | green `RD`, green `MC`, red `TS` |
| Order Composition processing | green `RD`, green `MC`, green `TS`, yellow `OC` |
| Risk Management processing or awaiting final continuation | green `RD`, green `MC`, green `TS`, green `OC`, yellow `RM` |
| Approved workflow | five green circles |
| Risk rejection | four green circles and red `RM` |

Color is not the only carrier of meaning. Each circle shall expose its short actor label and an accessible description
containing actor name, processing status, continuation decision, timestamps, and failure/stop reason when available.

### 2.4 Workflow end-state mapping

The row must combine `WorkflowStrategyMachineStatus` and `StrategyWorkflowOutcome`; machine status alone is
insufficient because a technically completed workflow can have the business outcome `NoTrade`.

| Authoritative state | Display label |
| --- | --- |
| Machine status `Started` | `In Progress` |
| Outcome `Completed` after successful Risk authorization | `Approved` |
| Outcome `NoTrade` | `No Trade` |
| Outcome `PipelineFailed` | `Pipeline Failed` |
| Outcome `InvalidResult` | `Invalid Result` |
| Outcome `TimedOut` | `Timed Out` |
| Outcome `Cancelled` | `Cancelled` |
| Outcome `ConsistencyFault` | `Consistency Fault` |
| Empty or unknown terminal combination | explicit `Unknown` plus technical state in Details |

The UI shall not reinterpret a `NoTrade` outcome as a technical failure. Its active stopping actor still receives a red
circle because the approved circle semantics define every halt or stop as red.

## 3. Required screen layout

```text
Strategy tab
├── Existing header and timeframe selector
├── Existing Intrinsic Time graph (unchanged)
├── Strategy workflow list
│   ├── Date/Time
│   ├── Futures ITI Signal Event
│   ├── Trend Type
│   ├── Futures Price
│   ├── Pipeline State: RD MC TS OC RM, revealed as actors start
│   └── Workflow End State
└── Workflow information tabs
    ├── Details
    │   ├── Workflow and trigger metadata
    │   ├── Regime Discovery Result
    │   ├── Market Condition Result
    │   ├── Trade Selection Result
    │   ├── Order Composition Result
    │   └── Risk Management Result
    └── Summary
        └── "Summary is not available."
```

The current horizontal split ratios and minimum panel sizes remain responsive. The graph remains linked to the selected
timeframe and continues to show the existing ITI history independently of whether every charted signal started an
accepted workflow.

Selecting a workflow row shall highlight its triggering chart point when the stable signal identity is present in the
current chart. Selecting a chart point shall select the corresponding workflow when one exists. A signal that did not
start an accepted workflow does not create a workflow row.

## 4. Details tab behavior

The `Details` tab shall render the selected workflow's authoritative snapshot. It must preserve all earlier results as
later actors complete; it must not replace the previous actor's result with the current actor's result.

### 4.1 Workflow header

Show at least:

- workflow ID and workflow entity ID;
- trigger event ID;
- correlation and causation IDs;
- workflow definition and version;
- workflow revision;
- started, updated, expiry, and terminal times;
- machine status, business outcome, current stage, and workflow stop reason;
- contract, value-date/timeframe identity, signal event type, trend, and price.

### 4.2 Repeating stage sections

Always render the five stage headings in execution order. Each stage section shall show:

- processing status;
- continuation decision and reason codes;
- started, completed, and failed timestamps;
- input workflow revision;
- parameter-set identity, version, and payload hash;
- source event ID and expiry;
- failure type, code, message, and timestamp when present;
- result envelope identity, type, schema, content type, hash, market-data time, and produced time; and
- every field of the typed stage result using a stage-specific read-only presenter.

When no result exists, display `None — workflow did not reach this pipeline result yet.` A failure remains visible even
when the result is `None`. Unknown or unsupported result schemas shall remain visible through their envelope metadata
and an explicit `Result details unavailable` message; the UI must not discard the workflow.

The stage sections may use collapsible read-only panels to keep large results navigable. All populated prior sections
remain available after a live update, and the current scroll/selection state should be retained where practical.

## 5. Summary tab behavior

The `Summary` tab remains present so the layout does not need to change when a later approved LLM delivery begins.

For this delivery it shall contain only the neutral read-only message:

`Summary is not available.`

It shall not:

- call an LLM or model provider;
- generate a deterministic narrative;
- publish or subscribe to summary messages;
- persist summary state;
- interpret pipeline results; or
- affect workflow loading, selection, errors, or readiness.

## 6. Verified backend foundation

The current backend already provides the authoritative information required by this UI:

- `IntrinsicTimeStrategyWorkflowView` holds the trigger, status, outcome, five stage states, workflow revision, and
  terminal reason.
- `StrategyWorkflowStageState` holds processing status, continuation decision, timing, result envelope, parameter
  provenance, source event identity, and failure.
- typed stage-result envelopes exist for Regime Discovery, Market Condition, Trade Selection, Order Composition, and
  Risk Management.
- `GetRecentAsync` returns bounded workflow history for one stable workflow entity.
- `GetByIdAsync` returns the complete persisted workflow snapshot.
- `GetTimelineAsync` remains available for later diagnostics but is not required to reconstruct current UI state.
- every committed `WorkflowStrategyStateUpdatedEvent` is projected before it is published into realtime workflow
  processing.

The current Strategy Operations UI listens only for Futures ITI signal notifications. It has no UI-owned workflow
subscription and its history row lacks the five stage states. The implementation therefore needs a stable notification
boundary plus startup hydration from the existing workflow queries.

## 7. Data flow and reliability

### 7.1 Stable UI notification boundary

Add a dedicated `ActorType.Notify` contract for a successfully projected workflow snapshot. The notification shall
contain, at minimum:

- notification ID and source snapshot/event ID;
- workflow ID and entity ID;
- workflow revision;
- update time; and
- the complete authoritative `IntrinsicTimeStrategyWorkflowView`.

The notification is observational. Publishing or consuming it must not start, continue, retry, stop, authorize, or
otherwise influence the trading workflow.

Publish it after the authoritative workflow projection succeeds. UI notification failure must not roll back persisted
workflow state or prevent the committed workflow from continuing. The internal realtime dispatch contract remains
separate from the public UI notify contract.

### 7.2 Subscribe before query

On Strategy view initialization:

1. start the workflow notification listener;
2. start the existing ITI notification listener used by the graph;
3. query recent workflows for each supported display timeframe entity;
4. hydrate each returned workflow with `GetByIdAsync` using bounded concurrency;
5. merge query and notification results by `WorkflowId`; and
6. publish the selected timeframe's immutable workflow rows to the view.

Subscribing first closes the startup gap. Notifications received during history loading are retained and win when their
workflow revision is newer than the queried revision.

### 7.3 Monotonic update reducer

The ViewModel shall keep one replaceable row per `WorkflowId` and apply these rules:

- accept a workflow not currently retained;
- replace it only when the incoming `WorkflowRevision` is greater;
- treat the same revision and same state identity as a duplicate;
- retain and surface a same-revision conflict as a presentation diagnostic rather than inventing a winner;
- never regress the selected workflow or Details tab to an older revision; and
- preserve newest-first ordering by trigger time, then workflow ID as a stable tie-breaker.

The backend remains responsible for validating workflow state. The frontend admits successfully transported snapshots
and maps their declared state to presentation records in accordance with `Frontend-Display-Only-Policy.md`.

### 7.4 Recovery and bounds

Retain periodic authoritative reconciliation using the existing 30-second cadence. Reconciliation shall reload the most
recent workflow history page and hydrate snapshots so a missed best-effort notification heals automatically.

Initial implementation bounds:

- retain at most 500 workflow rows across the Strategy view, matching the existing Strategy history design;
- request recent history in bounded pages;
- hydrate details with a small fixed concurrency limit; and
- cache already loaded immutable terminal snapshots by workflow ID and revision.

If measurement shows that initial hydration produces unacceptable query fan-out, introduce a dedicated flattened
workflow-list projection as a separately reviewed optimization. It is not required before measurement because the
existing query contracts are sufficient for correctness.

## 8. Framework-neutral presentation models

Add UI-owned immutable records under `TomasAI.IFM.UI.Net.ViewModels/Operations` or the approved presentation-model
location, including equivalents of:

- `StrategyWorkflowRow`;
- `PipelineActorIndicator`;
- `PipelineActorDisplayState`;
- `StrategyWorkflowDetails`;
- `StrategyWorkflowStageDetails`; and
- typed stage-result presentation records where raw domain types are unsuitable for binding.

`StrategyWorkflowRow` shall include the stable workflow identity, trigger stable identity, display columns, workflow
revision, end-state semantic value, and the ordered visible actor indicators. It shall not contain WinForms colors or
controls.

`PipelineActorIndicator` shall expose actor/stage, semantic state, accessible status text, and relevant timestamps. The
WinForms view alone maps semantic states to the approved bright yellow, bright green, and bright red palette.

Services own workflow API calls and event-consumer subscriptions. ViewModels own selection, filtering, bounded state,
revision reduction, lifecycle, and presentation errors. WinForms owns layout, painting, keyboard interaction, and
tooltips.

## 9. WinForms implementation

### 9.1 Workflow list

Update the existing `OperationsView` Strategy list to use the six approved columns. Preserve:

- dark-theme list headers and surfaces;
- native blue full-row selection;
- keyboard navigation and focus;
- selection across row refreshes;
- double buffering; and
- responsive column sizing.

Render the Pipeline State subitem using bounded owner drawing or an equivalent reusable control that can draw a
variable number of horizontal circles. Painting shall:

- render only started actors;
- keep fixed RD/MC/TS/OC/RM order;
- use consistent circle diameter and spacing;
- retain sufficient selected-row contrast;
- avoid repaint flicker; and
- expose the complete text equivalent for accessibility and testing.

### 9.2 Details and Summary tabs

Replace `itiPropertyGrid` with a dark `DarkTabControl` containing `Details` and `Summary`.

The Details surface shall use a resizable TreeView on the left and a read-only PropertyGrid on the right.
Workflow, ITI Signal and Workflow Pipeline roots expose nested objects, including typed Trade Selection, Order
Composition, and Risk Management results. Expanded branches and selection survive newer workflow revisions. Reuse common dark-theme controls rather than adding
screen-specific font or palette helpers.

The Summary page contains only the approved unavailable message during this delivery.

### 9.3 Selection synchronization

When the selected workflow receives a newer revision:

- update its row in place;
- repaint its visible circles and end state;
- refresh Details from the same snapshot revision;
- preserve the selected workflow ID; and
- prevent a late async load for a previously selected workflow from replacing the current selection.

## 10. Implementation gates

Each gate is reviewable and must satisfy its exit criteria before the next dependent gate is considered complete.

### Gate SWUI-0 — Contract and semantic lock

Tasks:

1. Record the approved columns, circle visibility, color mapping, actor order, end-state mapping, and Summary exclusion.
2. Confirm the trigger date/time and signal event fields against the stored trigger contract.
3. Define the notification identity, revision, and source-event semantics.
4. Define the UI row and stage-detail presentation contracts.

Exit criteria:

- every displayed value maps to an authoritative workflow or trigger field;
- no UI business inference is required; and
- MessagePack keys for any new shared contract are append-only and covered by round-trip tests.

### Gate SWUI-1 — Projected workflow notification

Tasks:

1. Add the dedicated workflow-updated Notify contract.
2. Publish one notification after each successfully projected authoritative snapshot.
3. Keep UI notification publication observational and isolated from workflow continuation.
4. Register serialization and routing metadata.

Exit criteria:

- processing, completion, continuation, failure, timeout, cancellation, and terminal snapshots can notify the UI;
- notification failure cannot change committed workflow state or pipeline progress;
- duplicate delivery is safe; and
- contract, serialization, and projector-ordering tests pass.

### Gate SWUI-2 — UI consumer and service boundary

Tasks:

1. Add a multi-subscriber workflow UI event consumer following the existing Futures ITI consumer lifecycle.
2. Add recent-history and by-ID workflow methods to `StrategyOperationsService`.
3. Add start/stop workflow listener methods.
4. Register the consumer and service dependencies in UI startup.

Exit criteria:

- the service owns all query and NATS dependencies;
- independently owned UI subscribers cannot stop one another;
- malformed transport data becomes an explicit service/presentation error; and
- listener lifecycle tests pass.

### Gate SWUI-3 — Workflow ViewModel reducer and loading

Tasks:

1. Add framework-neutral workflow row, circle-state, selected-details, loading, and error state.
2. Subscribe before loading history.
3. Hydrate recent history with bounded concurrency.
4. Merge query and notify snapshots monotonically by workflow ID and revision.
5. Filter rows by selected Daily, Weekly, or Monthly timeframe.
6. Reconcile periodically and preserve selection.

Exit criteria:

- query/notification overlap creates no duplicates;
- stale and out-of-order updates cannot regress a row;
- all five stage-state mappings produce the approved circle sequence;
- timeframe changes show only the selected workflow entity; and
- stop/dispose cancels work and prevents later UI mutation.

### Gate SWUI-4 — Workflow list and circle rendering

Tasks:

1. Replace the ITI list columns with the approved workflow columns.
2. Add dynamic horizontal circle rendering and accessible text/tooltips.
3. Preserve dark theme, selection, keyboard use, and responsive sizing.
4. Keep chart rendering unchanged and synchronize matching trigger selection.

Exit criteria:

- a workflow stopped at any stage renders that actor as the final red circle and reveals no later actors;
- each next actor appears yellow only after it starts;
- earlier actors retain green/red terminal colors;
- an approved workflow renders five green circles;
- risk rejection renders four green circles and one red circle; and
- rendered checks pass at the supported display scale.

### Gate SWUI-5 — Details and Summary tab layout

Tasks:

1. Replace the property grid with the two approved tabs.
2. Implement the workflow metadata header.
3. Implement five persistent stage-result sections and stage-specific typed presenters.
4. Display `None — workflow did not reach this pipeline result yet.` for absent results.
5. Show failures even when result content is absent.
6. Add the Summary unavailable placeholder without summary integration.

Exit criteria:

- every populated typed result field is reachable in Details;
- previous stage results remain visible after each update;
- unknown schemas and absent results are explicit;
- live selected-row updates cannot show mixed revisions; and
- Summary performs no backend or AI work.

### Gate SWUI-6 — Recovery, performance, and interaction verification

Tasks:

1. Verify missed-notification recovery through the periodic query path.
2. Measure initial workflow hydration and live-update-to-paint latency.
3. Verify the 500-row bound and bounded detail caching.
4. Verify chart/list selection with matched and unmatched triggers.
5. Verify rapid timeframe and workflow selection changes.

Exit criteria:

- reconnect/reconciliation converges to the highest persisted workflow revision;
- live UI work does not block workflow actors or NATS listeners;
- stale asynchronous results cannot replace the current selection; and
- measured startup and update timings are recorded without introducing a production qualification limit.

### Gate SWUI-7 — End-to-end acceptance

Tasks:

1. Run focused unit, presentation, rendering, architecture, and real-NATS integration tests.
2. Exercise representative happy, no-trade, failure, timeout, cancellation, and out-of-order paths.
3. Run the affected UI and Domain.Trade builds.
4. Review the final Strategy view render and interaction behavior.

Exit criteria:

- all mandatory affected tests pass;
- the full five-stage happy path ends with five green circles and `Approved`;
- representative stops or failures at each pipeline stage end with that actor as the final red circle, preserve prior
  actor colors, reveal no later actors, and show the authoritative end state;
- Details agrees with the selected row's exact workflow revision;
- the top graph remains behaviorally unchanged; and
- no LLM or summary infrastructure is introduced.

## 11. Test matrix

### 11.1 ViewModel and reducer tests

- first processing snapshot produces one yellow `RD` indicator;
- each successful continuation retains green prior stages and adds one yellow current stage;
- `Completed + None` remains yellow;
- `Completed + Stop` becomes red and reveals no later stage;
- failed, timed-out, and cancelled active stages become red;
- risk authorization produces five green circles and `Approved`;
- risk rejection produces four green circles, one red circle, and `No Trade`;
- duplicate notification is idempotent;
- lower revision is ignored without regression;
- query/notify startup overlap selects the highest revision;
- reconciliation restores a missed update;
- selection survives collection replacement and row revision updates;
- Daily, Weekly, and Monthly filtering uses the authoritative workflow entity; and
- disposal rejects subsequent notifications.

### 11.2 Details tests

- every stage heading is present;
- unreached stages show the approved `None` message;
- a failure is shown when no result exists;
- Regime, Market Condition, Trade Selection, Order Composition, and Risk result presenters expose every typed field;
- continuation reason codes and stop reason remain visible;
- unknown result schema remains observable;
- prior stage results remain after later stage completion; and
- a late detail query cannot replace a newly selected workflow.

### 11.3 Consumer and integration tests

- a projected workflow snapshot produces the typed Notify event;
- real NATS transport delivers processing and terminal revisions;
- two UI subscribers receive the same notification independently;
- stopping one subscriber leaves the other active;
- a missed notification is recovered through `GetRecentAsync` and `GetByIdAsync`;
- duplicate and reordered messages converge on the highest revision; and
- notification unavailability does not prevent workflow continuation or terminal persistence.

### 11.4 WinForms and rendering tests

- six columns render with readable headers;
- representative stops or failures at every stage, mixed progress, full approval, and risk rejection render correctly;
- selected rows retain visible circle and text contrast;
- actor labels/status are exposed to accessibility and tooltip paths;
- Details and Summary tabs use the dark theme;
- long typed results scroll without clipping the page;
- resizing preserves the graph, list, and detail minimum sizes; and
- existing Intrinsic Time chart behavior and timeframe windows remain unchanged.

## 12. Expected file impact

The implementation is expected to affect these areas:

- `TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Events/` — stable UI Notify contract;
- `TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/Command/EventProjector/` — post-projection notification;
- `TomasAI.IFM.UI.EventConsumer/` — workflow listener interface and implementation;
- `TomasAI.IFM.UI.Net.Services/Analytics/StrategyOperationsService.cs` — workflow query/subscription boundary;
- `TomasAI.IFM.UI.Net.ViewModels/Operations/` — workflow rows, reducer, detail state, and lifecycle;
- `TomasAI.IFM.UI.Net.Views/App/OperationsView.cs` — workflow selection and circle painting;
- `TomasAI.IFM.UI.Net.Views/App/OperationsView.Designer.cs` — list columns and Details/Summary tabs;
- `TomasAI.IFM.UI.Net/Startup.cs` — dependency registration;
- `TomasAI.IFM.UI.Net.Presentation.UnitTests/` — presentation and lifecycle tests;
- `TomasAI.IFM.UI.Net.SystemTests/` — layout, rendering, and interaction tests; and
- Domain.Trade integration/unit test projects — notification and projection-order tests.

No MAF, model-provider, LLM Advisor, or workflow-summary projects are part of this file impact.

## 13. Risks and controls

| Risk | Control |
| --- | --- |
| UI notification accidentally participates in orchestration | Use a separate Notify contract; prohibit command publication and isolate notification failure |
| Out-of-order state updates regress colors/details | Apply only higher workflow revisions and reconcile from persisted state |
| Initial history hydration creates query fan-out | Bound the page and concurrency, cache terminal snapshots, measure before adding a new projection |
| Owner drawing harms dark selection or accessibility | Preserve native row selection, expose text equivalents, and add rendered/accessibility tests |
| `Completed` is mistaken for an approved trade | Derive the end-state label from both machine status and business outcome |
| A No Trade stop is hidden as a neutral state | Show `No Trade` in the end-state column and red on the actor that committed the stop |
| Details show a different revision from the row | Reduce row and details from the same immutable snapshot and fence late async responses |
| Summary placeholder acquires accidental dependencies | Keep it static and test that no summary API, listener, or provider is resolved |

## 14. Definition of done

This delivery is complete when:

1. the top Intrinsic Time graph remains functionally unchanged;
2. every accepted workflow in the retained window appears once in newest-first order;
3. the workflow list displays all six approved columns;
4. actor circles appear horizontally only after their actor starts;
5. every newer committed workflow revision updates the corresponding row and selected Details;
6. all approved green, yellow, and red mappings are correct for the five actors;
7. workflow end state reflects the authoritative machine status and business outcome;
8. Details exposes all five accumulated pipeline results, failures, reasons, and provenance;
9. unreached results show the approved `None` text;
10. missed notifications recover through bounded authoritative queries;
11. UI, architecture, serialization, integration, rendering, and affected build checks pass; and
12. Summary remains a dependency-free unavailable placeholder with all LLM work deferred.


## Strategy Details object browser ? 2026-10-08

The Details tab uses a resizable left-hand TreeView and a right-hand read-only
PropertyGrid. Root nodes are Workflow, ITI Signal, and Workflow Pipeline. Pipeline
branches show Regime Discovery, Market Condition, Trade Selection, Order Composition,
and Risk Management, retaining status colors and status tooltips. Each object node
selects its own properties; nested input, result, status and failure objects can be
expanded and inspected. Missing objects are explicitly unavailable.

The presentation retains the immutable typed workflow view alongside the existing
text representation. Both the Strategy tab and Strategy observation dialog use the
same browser. No additional subscriptions or backend queries are introduced.

Nested nodes are populated on expansion, with ancestor-cycle protection, a maximum
of 16 levels and 200 displayed collection items per branch. These are display limits;
they do not alter workflow data. The property grid exposes read-only string descriptors,
so setters and nested collection editors cannot mutate business objects.

A same-workflow revision refresh restores expanded paths and selected object paths,
then inspects the new object instance. Identical revisions retain existing controls.
A different workflow resets navigation; clearing or failing a selection clears stale
properties. Rendering tests cover the three roots, five stages, read-only properties,
revision refresh, navigation retention and clearing.


### Details responsiveness verification ? 2026-10-08

Property-grid binding is deferred until its native handle exists and the grid is
visible. During a workflow revision rebuild, intermediate tree selections do not
bind the inspector; the final selected node is bound once. Read-only descriptors are
materialized once per inspection rather than reevaluating DTO getters whenever the
grid asks for its properties. Node generation identifies an inspector refresh even
when a stage object is reused across workflow revisions.

The FlaUI regression hosts the actual browser in an STA form with Summary/Details
tabs and representative immutable workflow objects. It binds while Details is hidden,
selects all roots and pipeline stages, expands/collapses nodes, selects nested reason
code leaves, verifies read-only grid values, switches tabs and applies new snapshots.
The fixture does not submit trades or use a live backend; its revisions simulate the
existing workflow notification binding. Observed run: 84 root/stage selections,
12 nested leaf selections, 24 revision refreshes and 24 tab switches in 3.11 seconds.
All 20 affected rendering and FlaUI tests passed; the view build had zero warnings
and errors. Evidence: `.artifacts/strategy-details-flaui.log`.

### Exhaustive branch verification - 2026-10-08

The live FlaUI check navigated the running development UI across 20 strategy
workflow rows. Three recursive traversals per row verified 2,100 expandable
branches, asserting the native UI Automation Expanded and Collapsed states.
Every expand/collapse operation completed within its five-second timeout; the
complete test passed in 93 seconds. No hang was reproduced in this run.

The representative browser regression recursively checks nested branches before
exercising selections, revision refreshes and tab switches. The affected test run
passed 20 tests; the live test was skipped unless explicitly enabled with
`IFM_TEST_LIVE_STRATEGY_UI=1`, preventing normal test runs from interacting with
a user's running application.

Evidence: `.artifacts/strategy-details-live-stress.log`, the per-node audit
`.artifacts/strategy-details-live-branches.log`, and
`.artifacts/strategy-details-regression.log`. These checks verify the displayed
objects and rows exercised; they do not prove an intermittent hang can never
occur. This verification added tests without changing the browser implementation.

### Unique object navigation - 2026-10-08

Workflow expansion excludes the five pipeline stage objects and TriggerEvent.
Stages are navigated only under Workflow Pipeline; the trigger is navigated
under ITI Signal. Other workflow objects remain available under Workflow.
The rendering regression asserts that dedicated branches are not duplicated.

### Background display cache - 2026-10-08

Each workflow revision prepares its complete bounded display hierarchy on a
background task: child descriptions, formatted property values and read-only
property descriptors. No TreeNode or Control is constructed on that task.
Expand only attaches cached immediate children; selection binds the cached
property descriptor. Reflection and DTO property reads no longer run in clicks.

The UI shows Preparing details while a revision is built. For the same workflow,
its previous display stays usable until the replacement is ready. Navigation is
captured when the replacement is applied, preserving clicks made during preparation.
Different workflow selection clears the old display. Revision generations reject
stale results; cancellation occurs on replacement, clearing and disposal. Hidden
views retain a prepared result until a native handle is created.

Limits are 16 levels, 200 items per collection and 10,000 nodes per revision,
with ancestor-cycle detection and visible markers for omitted objects. Only the
current revision cache is retained. Preparation failures produce an unavailable
message. UI control creation, property-grid binding and painting remain on the UI
thread; this cache does not guarantee those native operations cannot be slow.

Verification covers cached recursive expansion, 84 selections, 24 revision
updates, hidden tab binding, navigation retention, rapid superseding revisions,
clearing during preparation and repeated disposal. Build and test evidence is in
`.artifacts/strategy-cache-build.log` and `.artifacts/strategy-cache-tests.log`.

### Intermittent ITI Signal pause investigation - 2026-10-08

The user reported a pause exceeding five seconds on the 18:40 workflow.
The live UI process started after the cache assembly build; the deployed Views
assembly matched the built assembly. Twenty targeted live checks on that workflow
measured ITI selection at 8-20 ms and expansion at 15-57 ms. The reported pause
was not reproduced, and its cause remains unconfirmed. Evidence:
`.artifacts/strategy-iti-live-test.log` and `.artifacts/strategy-iti-live-latency.log`.
A 30-second sampling trace was collected in `.artifacts/strategy-iti-freeze.nettrace`.

Slow-operation warnings (event 61002) now distinguish Populate (native child
insertion), RefreshInspector (property-grid assignment), ApplyPendingView (revision
replacement) and UiMessageLoop (a delay elsewhere on the UI thread). Operation
warnings are emitted only at 100 ms or more; the visible view checks message-loop
responsiveness every 250 ms and reports gaps exceeding one second. Fields include
Method, NodePath, WorkflowId, WorkflowRevision and ElapsedMs. No DTO payload is
serialized. Child insertion is bracketed in BeginUpdate/EndUpdate to suppress
repeated redraws. These diagnostics require restarting the UI to load the assembly.
The affected rendering, FlaUI and exception logging tests passed (23 tests, two
explicit live tests skipped). Build and verification evidence:
`.artifacts/strategy-iti-diagnostics-build.log` and
`.artifacts/strategy-iti-diagnostics-tests.log`.

### Confirmed native hover stall and workaround - 2026-10-08

The user's reproducing clicks were captured after cache deployment. The UI warning
recorded 22.7 seconds initially and approximately 28 seconds on subsequent clicks.
The user-click trace showed three long intervals (27.4, 27.6 and 28.1 seconds) in
TreeView.WndProc -> Control.WmMouseMove -> native Control.DefWndProc. The minidump
placed the native instruction in TextShaping.dll, with GDI, User32 and ComCtl32
return addresses. Cache building, Populate and RefreshInspector were absent from
the blocked managed stack. These observations locate the stall in Windows native
mouse/hover text processing rather than workflow DTO traversal.

Disabling native tooltips on the running tree stopped the pause in the user's
manual expand/collapse and label-hover retest. The permanent workaround is a local
TreeView subclass whose CreateParams sets TVS_NOTOOLTIPS (0x0080) and clears
TVS_INFOTIP (0x0800), with ShowNodeToolTips false. This disables automatic label
hover tips as well as custom node tips. Stage summaries remain available as the
read-only StageSummary property instead. The exact underlying Windows text-shaping
bug is not established; the observed failing path is avoided.

The FlaUI regression now performs real mouse selection and label hover before
checking expansion and collapse. Rendering asserts both native style bits and
availability of StageSummary. The view built with zero warnings/errors; 23 affected
tests passed and two opt-in live tests were skipped. The tooltip-off live mouse
loop had a missed-glyph timeout while the UI remained responsive; it is not counted
as a successful full loop. The user's manual no-pause confirmation provides the
live A/B result. Current running UI was patched diagnostically; the subclass makes
the workaround persistent on subsequent builds/restarts.

Evidence: `.artifacts/strategy-iti-user-click.nettrace`,
`.artifacts/strategy-iti-hang-stacks.log`, `.artifacts/strategy-iti-hang.dmp`,
`.artifacts/strategy-iti-tooltip-fix-build.log`, and
`.artifacts/strategy-iti-tooltip-fix-tests.log`. Diagnostic dumps stay in ignored
local artifacts and must not be published as normal repository content.

### Single inspector pane - 2026-10-08

The right side contains only the read-only property list. PropertyGrid.HelpVisible
is false, hiding the built-in description/help pane; its toolbar remains hidden.

### Stacked Details layout - 2026-10-08

The resizable split now places the object tree above the read-only property grid,
with a horizontal divider. The workflow identity/revision banner is removed;
identity remains inspectable through Workflow properties. A status line is shown
only for preparation, selection guidance or an error, and hidden when ready.

### Strategy workspace tabs - 2026-10-08

The Strategy view now uses one horizontal splitter below its existing header.
The graph occupies approximately one-third of the available height, and the
workspace below occupies approximately two-thirds, subject to minimum sizes.
The divider remains user-resizable; window resizing reapplies the default ratio.

The lower workspace contains tabs in this order:
1. Strategy Updates: the existing workflow list, with its paging and selection handlers.
2. Details: the existing cached tree above its read-only property grid.
3. Summary: the existing summary content.

Strategy Updates is selected initially. Switching tabs retains the selected
workflow and controls; live updates continue through the existing binding path.
The separate graph/list splitter is removed. Layout regression checks the three
pages, list ownership, default height ratio after resize and selection retention.
Evidence: `.artifacts/strategy-three-tabs-build.log` and
`.artifacts/strategy-three-tabs-tests.log`.

### Model properties only - 2026-10-08

The synthetic StageSummary property is removed from pipeline inspectors. Status,
continuation, timing and failure information remain available as individual model
properties. Native hover tooltips remain disabled.
