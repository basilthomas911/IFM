# Strategy Observation Details Accordion Implementation Plan v1.0

Status: Proposed

Date: 2026-09-10

## 1. Objective

Replace the strategy workflow's unstructured details text with a reusable, read-only accordion that
shows the exact ITI signal first and then the five pipeline operator results in execution order. Use
the same presentation model and accordion control in every UI surface that displays the details of
an Intrinsic Time Strategy workflow.

The ordered result sections are:

1. ITI Signal
2. Regime Discovery
3. Market Condition
4. Trade Selection
5. Order Composition
6. Risk Management

Workflow identity and overall lifecycle state remain visible in a compact header above the result
sections. They are context for all results and are not a seventh pipeline result.

## 2. Verified current state

`IntrinsicTimeStrategyWorkflowView` is already the authoritative source for this UI. It contains the
complete immutable `FuturesItiSignalGeneratedEvent` at MessagePack key 21 and the five immutable
`StrategyWorkflowStageState` values at keys 13 through 17. Each stage state contains processing
status, timing, continuation decision and reasons, failure information, parameter identity, deadline,
accepted result envelope, and terminal source-event identity.

No new API, database table, actor query, event contract, workflow transition, or cross-stream join is
required for the accordion. The exact ITI event that started the selected workflow travels inside the
workflow snapshot and must remain the only trigger rendered for that workflow.

`StrategyWorkflowPresentation.RenderDetails` currently renders one large string in this order:
workflow metadata, ITI event/signal, Regime Discovery, Market Condition, Trade Selection, Order
Composition, and Risk Management. `OperationsView` displays that string in a read-only multiline
`TextBox`. `StrategyObservationForm` has a second read-only details `TextBox` and uses
`MarketAssessmentPresenter`, including an optional projected-versus-accepted Market Condition
comparison. A text box cannot provide real section expansion and collapse.

## 3. Required behavior

### 3.1 Workflow header

Show a compact, always-visible header containing:

- workflow ID and entity identity;
- contract and timeframe;
- machine status, business outcome, current stage, and stop reason;
- workflow revision;
- started, updated, expiry, and terminal timestamps;
- trigger, correlation, and causation identities.

The header must remain readable when every result section is collapsed.

### 3.2 Accordion sections

Each section header must show its title and a concise summary without requiring expansion.

The ITI Signal header shows timeframe, signal mode, trend, and futures price. Its expanded content
shows event and command identities, entity, source, creator, created/received times, ES and VX input
prices, intrinsic-time identity, trend/mode, group and length, thresholds, deltas, levels, and trade
state. Null or compatibility fields display explicitly as unavailable; the UI must not substitute a
different historical ITI row.

Each pipeline operator header shows processing status, continuation decision, duration when known,
and the primary failure or continuation reason. Expanded operator content shows:

- processing, start, completion, failure, and expiry state;
- input workflow revision;
- continuation rule set, decision, and reason codes;
- parameter-set ID, version, and canonical payload hash;
- terminal source-event identity;
- full bounded pipeline failure information;
- result-envelope identity, type, schema, content type, payload hash and size, market-data timestamp,
  and produced timestamp;
- the typed result content when present;
- an explicit explanation when the workflow has not reached the operator or the typed result is
  unavailable.

All five operator sections remain visible. Operators that have not started display `Not started` and
stay collapsed. Failed, timed-out, or stopped stages remain expandable so their complete evidence is
available.

### 3.3 Expansion and refresh rules

- On first selection of a workflow, expand ITI Signal and collapse all operator sections.
- Opening a section collapses the previously open section.
- Clicking the open section collapses it, allowing all sections to be closed.
- Preserve the expanded section while the same workflow receives a higher revision.
- Reset expansion to ITI Signal when the selected workflow ID changes.
- Preserve the workflow-list selection and chart highlight behavior.
- Perform no automatic scrolling or expansion when a background refresh updates the selected
  workflow.
- Expose section state through names and accessible status text; color alone must never communicate
  processing or failure state.

## 4. Shared presentation model

Replace the details string as the primary presentation contract with immutable, framework-neutral
records in `TomasAI.IFM.UI.Net.ViewModels`. The proposed shape is:

```text
StrategyWorkflowDetails
  Header
  Sections[]

StrategyWorkflowDetailHeader
  workflow identity and lifecycle summary fields

StrategyWorkflowDetailSection
  stable section key
  ordered section kind
  title
  summary
  semantic display state
  accessible status
  field groups
  optional formatted result content
```

Stable section keys are `iti`, `regime-discovery`, `market-condition`, `trade-selection`,
`order-composition`, and `risk-management`. UI expansion state uses these keys and never uses a
display label or list index.

`StrategyWorkflowPresentation` becomes the single builder for this model. It must accept the
authoritative workflow snapshot and may accept the separately queried Market Condition projection
as optional comparison evidence. It must retain the accepted workflow result as authority. The
optional projection can report `matching`, `missing`, or `mismatched`; it cannot replace an accepted
result.

Keep a plain-text formatter over the structured model for logs, clipboard/export, error fallback,
and tests that need a transportable textual representation. The text formatter is no longer the
interactive UI control contract.

## 5. Reusable WinForms control

Add one reusable read-only strategy-details control under `TomasAI.IFM.UI.Net.Views/Strategy`. It
owns:

- an always-visible workflow header;
- an auto-scrolling vertical result area;
- six accordion section headers and their collapsible content panels;
- dark-theme, typography, keyboard, focus, and accessibility behavior;
- expansion state keyed by workflow ID and stable section key;
- incremental rebinding without rebuilding unchanged content or moving the scroll position.

The control exposes a small binding API for `StrategyWorkflowDetails` and does not query services,
deserialize workflow state, or own listener lifecycle. The containing view remains responsible for
selection and data retrieval.

The section header is a real button or equivalently keyboard-operable control. Enter and Space
toggle it. The accessible name combines section title and summary, and the accessible description
states whether it is expanded or collapsed. Use text labels such as `Completed`, `Failed`, `Stop`,
and `Not started` in addition to semantic colors.

## 6. UI integrations

### 6.1 Operations strategy viewer

Replace `txtWorkflowDetails` in the Details tab of `OperationsView` with the reusable accordion.
`StrategyOperationsViewModel` exposes `StrategyWorkflowDetails? SelectedWorkflowDetails` instead of
using a rendered string as its interactive contract. Workflow selection still comes from the
existing workflow list, and selection continues to highlight the exact trigger point on the ITI
chart.

When a selected workflow revision advances, rebuild the immutable details model and bind it while
preserving the expanded section. A stale or conflicting revision must continue to be rejected by the
existing view-model reconciliation logic.

### 6.2 Standalone Strategy Observation form

Replace the form's `_details` text box with the same reusable accordion. Continue loading the
workflow snapshot through `IIntrinsicTimeStrategyWorkflowQueryApi`. Continue the optional
`IMarketConditionAssessmentQueryApi` lookup and pass the projected assessment into the shared
details builder as comparison evidence.

History loading, workflow-ID entry, Risk Details navigation, cancellation, and revision fencing stay
owned by the form. Errors that occur before a workflow can be bound use a small error state in the
details host; they do not manufacture an accordion result.

### 6.3 Additional reuse boundary

Use the control only where a complete `IntrinsicTimeStrategyWorkflowView` is being inspected.
Risk-only history, portfolio administration, and reference screens keep their specialized details
unless they open a complete workflow. This prevents a general-purpose UI container from acquiring
strategy queries or stage-specific business rules.

## 7. Delivery sequence

1. Introduce immutable details/header/section presentation records and stable section keys.
2. Refactor `StrategyWorkflowPresentation` to build the structured model from the workflow snapshot,
   including ITI first and all five ordered stage sections.
3. Incorporate the accepted-versus-projected Market Condition comparison currently owned by
   `MarketAssessmentPresenter` without changing result authority.
4. Retain a deterministic plain-text formatter over the structured model for fallback and export.
5. Implement the reusable dark-themed accordion control with keyboard, accessibility, expansion,
   scroll, and incremental-refresh behavior.
6. Replace the Operations Details text box and bind selected workflow details.
7. Replace the standalone Strategy Observation form details text box and bind the same model with
   optional Market Condition projection evidence.
8. Remove duplicate detail-formatting paths after both surfaces produce equivalent information.
9. Run focused presentation, control, workflow reconciliation, and UI integration tests, followed by
   the affected UI and strategy-workflow suites.

## 8. Verification plan

### 8.1 Presentation tests

- ITI Signal is section zero and contains the exact `TriggerEvent` identity and nested signal.
- Operator sections always occur in RD, MC, TS, OC, RM order.
- Not-started operators remain visible with explicit state.
- Processing, proceed, stop, failed, timed-out, cancelled, and completed states produce accurate
  summaries.
- Failures and result-envelope metadata remain available in expanded content.
- Typed results are selected from the correct envelope member.
- Missing or opaque result content is explained without throwing.
- Accepted Market Condition remains authoritative when projection evidence is missing or mismatched.
- Plain-text fallback contains all six section headings and critical identities.

### 8.2 Accordion control tests

- ITI Signal starts expanded for a newly selected workflow.
- At most one section is expanded.
- The active section can be collapsed.
- Keyboard toggling works and accessible state changes with expansion.
- A higher revision of the same workflow preserves expansion and scroll position.
- Selecting a different workflow resets expansion to ITI Signal.
- Binding a terminal failure leaves the failed section accessible and expandable.

### 8.3 Integrated UI tests

- Selecting a workflow in Operations binds the matching trigger and results and retains chart
  highlighting.
- Realtime workflow notifications update section summaries without losing selection or expansion.
- Timeframe changes clear an out-of-scope selection and its accordion state.
- The standalone form displays the same workflow and operator details as Operations.
- The standalone form displays matching, missing, and mismatched Market Condition projection states.
- Risk Details navigation remains enabled only under its existing eligibility rules.
- Empty history, invalid workflow ID, query failure, cancellation, and form disposal remain safe.

## 9. Acceptance criteria

The work is complete when:

1. Both strategy-detail surfaces use the same structured presentation builder and reusable accordion
   control.
2. ITI Signal is the first result section and is expanded on initial workflow selection.
3. RD, MC, TS, OC, and RM follow in fixed execution order and remain visible even when not started.
4. Every section can be expanded and collapsed by mouse and keyboard, with one section open at most.
5. Live updates preserve workflow selection, expanded section, chart highlight, and scroll position.
6. The UI displays exact workflow-owned trigger and result evidence and never joins a different ITI
   history row by time alone.
7. Accepted-versus-projected Market Condition diagnostics are retained in the shared presentation.
8. No API, persistence, event, actor, or workflow behavior changes are introduced.
9. Focused and affected integration tests pass with no new build warnings.

## 10. Files expected to change

| Area | Expected change |
| --- | --- |
| `TomasAI.IFM.UI.Net.ViewModels/Operations/StrategyWorkflowPresentation.cs` | Build structured details and retain text fallback |
| `TomasAI.IFM.UI.Net.ViewModels/Operations/StrategyOperationsViewModel.cs` | Expose selected structured details and preserve revision behavior |
| `TomasAI.IFM.UI.Net.ViewModels/Strategy/MarketAssessmentPresenter.cs` | Fold unique projection-comparison evidence into the shared builder, then remove duplication |
| `TomasAI.IFM.UI.Net.Views/Strategy` | Add reusable strategy workflow details accordion control |
| `TomasAI.IFM.UI.Net.Views/App/OperationsView.cs` and designer | Replace details text box and bind accordion state |
| `TomasAI.IFM.UI.Net.Views/Strategy/StrategyObservationForm.cs` | Reuse accordion and shared presentation model |
| UI presentation/unit/system test projects | Add ordering, detail, state-preservation, accessibility, and cross-surface equivalence coverage |
