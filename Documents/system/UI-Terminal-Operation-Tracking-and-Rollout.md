# UI Terminal-Operation Tracking and Rollout

Status: Approved UI reference pattern
Version: 1.4
Date: 2026-08-18
Scope: UI-originated commands whose accepted work finishes asynchronously through correlated complete/fail events

## 1. Purpose

This document defines the system-wide UI pattern for tracking an asynchronous actor command from submission to its
terminal result. The first reference implementation is
`TomasAI.IFM.UI.Net.ViewModels/MarketData/YieldCurveRateEditorViewModel.cs`. The same pattern now also governs the
desktop application's automatic startup reference-data imports.

The current rollout scope is UI only. Legacy scheduled tasks have not been reviewed against this pattern and are not
approved by this document. Their age, persistence model, retry behavior, and operational-status requirements require a
separate inventory and redesign before any scheduled-task rollout is claimed.

This convention is transport-neutral at submission time. A UI may use the typed client backed by REST or by NATS, but
both implementations must route the command to the same domain actor. Terminal tracking does not create a second
application-service workflow and does not bypass the domain actor, import event handler, provider-neutral application
API, or storage API.

## 2. Accepted is not completed

A successful command response means only that the domain actor accepted the request and returned a non-empty command
identifier. It does not mean the operation completed successfully.

For an event-family operation, the UI recognizes exactly two terminal outcomes for that command identifier:

- the correlated complete event means the operation completed successfully; and
- the correlated fail event means the operation failed.

Any other state, including local cancellation, timeout, listener loss, or application shutdown, means that the UI did
not observe a terminal outcome. It must not report success and must not manufacture a domain fail event. A retry is a
new operation with a new command identifier.

## 3. Authoritative UI flow

```text
initialize UI lifecycle and start terminal-event listener
  -> submit typed command through REST- or NATS-backed client
  -> receive and validate non-empty command ID (accepted)
  -> await only complete/fail carrying that exact command ID
      -> complete: query durable projection, update UI snapshot, show success
      -> fail: map typed error to UI error state, show failure
      -> cancellation/timeout/listener loss: show outcome-not-observed state
  -> clear correlation state and release the operation gate
```

The listener starts before command submission. This ordering minimizes the event-before-response race, but it does not
eliminate it: a fast terminal event can arrive before the command response supplies its identifier. The UI correlation
component must retain a small bounded set of early terminal events while a submission is active, then match the returned
identifier and discard unrelated entries.

## 4. Required invariants

Every UI implementation of this pattern must satisfy these rules:

1. Submit through a typed client API; do not address storage or an external vendor directly.
2. Start the required complete/fail listener before enabling or submitting the operation.
3. Treat command acceptance and terminal completion as separate states.
4. Reject an empty command identifier.
5. Match terminal events by the exact `CommandId`; date, subject, entity identity, or arrival order is insufficient.
6. Include both complete and fail contracts in the listener subscription.
7. Close the event-before-response race with bounded early-event correlation.
8. Make duplicate delivery harmless by completing an awaiter at most once.
9. Use asynchronous continuations, such as `TaskCreationOptions.RunContinuationsAsynchronously`, and never block the UI
   thread while awaiting a command or event.
10. On complete, re-query the durable projection before presenting the operation as reflected in current UI state.
11. On fail, preserve the typed error code and message in the UI presentation error.
12. On cancellation or timeout, report that the result was not observed; do not state that domain work failed or was
    rolled back.
13. Clear correlation state in a `finally` path and cancel pending awaits when the UI lifecycle stops or is disposed.
14. Prevent conflicting concurrent operations. A single-editor workflow should use a single-flight operation gate; a
    multi-operation UI requires a bounded command-ID-to-awaiter map with equivalent lifecycle cleanup.

A zero-record external-data import is still a successful operation when the domain complete event says storage accepted
the empty array. The UI must not reinterpret zero imported rows as failure.

## 5. Reference implementation shape

`TerminalEventCorrelation` now provides the shared exact-ID, bounded early-event, asynchronous-continuation, timeout,
and lifecycle cleanup primitive. `YieldCurveRateEditorViewModel` is the initial interactive reference because it
combines that primitive with the required pieces:

- `AsyncLifecycleCoordinator` starts and stops the event listener;
- `AsyncOperation` supplies cancellation and single-flight command execution;
- the typed command model returns the accepted command identifier;
- complete and fail events are correlated through `TerminalEventCorrelation`;
- its bounded early-terminal-event collection closes the fast-completion race;
- `TaskCompletionSource<IEvent>` uses asynchronous continuations; and
- successful completion refreshes the durable query snapshot before displaying success.

`EconomicCalendarEditorViewModel` is the second conforming implementation. It uses the same correlation semantics for
manual add/change/remove and provider imports, refreshes the bounded durable date/country projection only after complete,
exposes typed failure, and owns listener shutdown through its View and parent form. Its event-consumer registration is
transient so the editor and always-on calendar dashboard have independent listener lifecycles. The real Development UI
acceptance for G2-020 through G2-023 proves exact-ID source/terminal events, durable/UI agreement, and public-command
baseline restoration for all four operations.

`LookupTypeEditorViewModel` is the third conforming implementation. It starts its lookup terminal consumer before
loading the editor catalog, applies the shared bounded early-event correlation to add/change/remove, refreshes the full
typed lookup projection only after exact-ID completion, exposes typed failure without a false refresh, and owns awaited
listener shutdown through `LookupTypeEditorView`. G2-024 through G2-026 prove the real editor, source/terminal events,
durable partition query, visible name/short-code selectors, and public-command cleanup agree for every transition.

New UI implementations should compose the shared correlation primitive with operation-specific lifecycle, command,
terminal-event, durable-refresh, and presentation behavior. They must preserve bounded race handling, typed errors,
cancellation semantics, and exact command matching.

### 5.1 Automatic startup specialization

`IFMAppViewModel` attempts yield-curve and economic-calendar imports once during every desktop startup, independently
of live-feed trading-hours availability. Both terminal listeners start before either command is submitted. The two
commands use separate transient command Models and may run concurrently; each owns a distinct correlation attempt.

Each terminal observation is bounded to 30 seconds. A complete event records an observable completed status and is
otherwise silent. A typed fail event, command/listener failure, or unobserved timeout is written to the status console
and surfaced through the shell's presentation error. Timeout means only that the UI did not observe the outcome; it
does not manufacture a domain fail event. The shell aggregates independent failures, stops both startup-only listeners,
performs no retry, and continues normal startup. The user may later start a new import with a new command identifier
from the yield-curve or economic-calendar maintenance screen.

## 6. UI rollout process

Roll out one UI operation family at a time:

1. Confirm that its typed REST and NATS clients both reach the same domain command actor.
2. Confirm that the actor workflow emits a complete or fail event with the originating command identifier.
3. Add listener lifecycle ownership to the UI ViewModel before enabling command execution.
4. Replace submission-only success messages with correlated terminal waiting.
5. Refresh the durable projection only after complete; surface typed failure on fail.
6. Add the tests in section 7 and run the affected UI, client, domain, and serialization suites.
7. Perform a UI smoke test for success, failure, fast completion, cancellation, and retry.
8. Enable that UI operation and observe its errors and completion latency before migrating the next family.

Rollback is scoped to the UI operation: disable or revert the affected UI action while leaving the actor, application,
and storage workflow intact. A rollback must not introduce a direct provider or storage path.

## 7. Required tests

Each migrated UI operation must test:

- listener startup before command submission;
- empty command-ID rejection;
- matching complete and matching fail events;
- unrelated command IDs being ignored;
- an event arriving before the command response;
- duplicate terminal delivery completing only once;
- durable projection refresh after complete and not after fail;
- zero-record complete being reported as success;
- cancellation, timeout if configured, stop, and disposal cleanup;
- no false success when the terminal result is not observed; and
- a retry using a new command identifier.

Tests should use deterministic fakes for ordering and race cases. Transport integration tests remain responsible for
proving that the REST- and NATS-backed clients reach the same actor contract and that terminal schemas round-trip.

## 8. Current conformance and next UI migration

| UI area | Status | Notes |
| --- | --- | --- |
| Yield-curve editor | Reference implementation | Correlates complete/fail by command ID, handles an early event, and refreshes durable state after complete. |
| Economic-calendar editor | Accepted implementation | Correlates manual and imported complete/fail by command ID, handles early and duplicate delivery, refreshes the bounded durable date/country projection after complete, and owns awaited listener shutdown; G2-020 through G2-023 passed in the real Development UI. |
| Lookup editor | Accepted implementation | Correlates add/change/remove complete/fail by command ID, handles early and unrelated delivery, refreshes the full durable lookup catalog only after complete, and owns awaited listener shutdown; G2-024 through G2-026 passed in the real Development UI. |
| Market economic-calendar view | Partial supporting behavior | Owns calendar listeners, refreshes on events, and exposes import failure, but it is not the submitting command's correlation owner. |
| Application-startup automatic imports | Implemented | Attempts both imports once before the live-feed trading-hours gate, observes exact-ID complete/fail events for up to 30 seconds, reports only failed/unobserved outcomes, performs no retry, and continues startup. |
| Legacy scheduled tasks | Explicitly excluded | Unreviewed and not approved as terminal-operation tracking implementations. |

The yield-curve, economic-calendar, and lookup maintenance editors plus the application shell establish the reusable interactive
and automatic-startup forms of the pattern. The broader market-calendar view may continue to refresh observational
state. Any additional automatic workflow must make its timeout, failure-presentation, retry, and startup-continuation
policy explicit rather than inheriting the startup specialization implicitly.

## 9. Deferred scheduled-task design

Do not copy the UI lifecycle directly into a background scheduler. Before scheduled imports are modernized, review and
document at least:

- task definitions, enablement, ownership, and concurrency;
- durable execution and terminal-status persistence across process restarts;
- command correlation for single commands and bounded date-range fan-out;
- retry/backoff policy and the distinction between retrying observation and starting a new import;
- timeout, cancellation, shutdown, and recovery semantics;
- task history, logs, metrics, alerts, and user-visible status; and
- duplicate scheduling and idempotency guarantees.

The existing `FmpMarketDataImportHostedService` submits scheduled commands and logs aggregate submission/rejection
counts, but it does not correlate and persist each command's terminal outcome. It is therefore not the system-wide
reference for terminal-operation tracking. Its presence or ability to submit a command must not be interpreted as a
completed scheduled-task rollout.

## 10. Related documents

- [Actor Implementation Conventions](Actor-Implementation-Conventions.md)
- [Actor Message Types and Delivery Conventions](Actor-Message-Types-and-Delivery-Conventions.md)
- [FMP Market Data Architecture](../../TomasAI.IFM.Framework.MarketData.FinancialModelingPrep/Docs/Financial-Modeling-Prep-Architecture.md)

## 11. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 1.4 | 2026-08-18 | Migrated lookup add/change/remove to the shared listener-first exact-ID pattern and accepted G2-024 through G2-026 with durable partition/UI selector agreement and public Reference-command cleanup. |
| 1.3 | 2026-08-18 | Extended the economic-calendar editor pattern to manual add/change/remove terminal events and accepted G2-020 through G2-023 with exact-ID correlation, bounded durable/UI refresh, production FMP import, and public-command baseline restoration. |
| 1.2 | 2026-08-16 | Extracted shared terminal-event correlation and migrated automatic startup yield/calendar imports to independent listener-first, exact-ID, 30-second bounded observation with failure-only reporting, no retry, cleanup, and degraded startup continuation. |
| 1.1 | 2026-08-16 | Migrated the economic-calendar editor to exact command-ID complete/fail correlation, durable refresh after complete, bounded early-event handling, typed failure, and awaited listener shutdown; recorded independent listener instances and identified automatic startup imports as a separate UI review. |
| 1.0 | 2026-08-16 | Established the UI-only terminal-operation tracking and rollout convention, named the yield-curve editor as the reference implementation, identified the economic-calendar UI as the next migration, and explicitly deferred legacy scheduled-task review. |
