# Application Startup and Databento Recovery Two-Stage Implementation Plan v0.1

> **Strategy catalog direction (2026-09-06):** References below to the Trade Strategy Family bootstrap/catalog describe the existing ReferenceDb compatibility records and their historical verification. The proposed reusable strategy/structure/variant catalog belongs to ConfigurationDb; it does not replace product downloads or current bootstrap behavior in this change. TradeSelection implementation is on hold. See [ConfigurationDb strategy catalog design](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

| Item | Value |
| --- | --- |
| Plan ID | `ASDR` |
| Status | Stage 1 complete (ASR-01 through ASR-11); Stage 2 not started |
| Date | 2026-09-02 |
| Design authority | `Documents/system/Application-Startup-and-Databento-Recovery-Actor-System-Design-v0.1.md` |
| Stage 1 | Actor-owned `StartApplicationAsync` refactor |
| Stage 2 | Databento reset lifecycle |
| Current deployment | API Server hosted |
| Deferred | Production shutdown orchestration, scheduled 17:01 close implementation, security, Aspire extraction |

## 1. Objective

Implement the approved application-startup and Databento recovery architecture in two independently
qualified stages.

Stage 1 makes `StartApplicationAsync` the authoritative operational-startup entry point after API
bootstrap health. It moves backend startup out of the UI, makes startup idempotent and queryable,
and routes unattended operational messages to the System Console instead of modal dialogs.

Stage 2 adds the actor-owned Databento health and reset lifecycle. It evaluates health every minute,
performs a complete reset after Failed, and repeats non-overlapping resets every five minutes without
an attempt limit until Healthy or API process cancellation during this development increment.

## 2. Binding implementation rules

1. API host bootstrap and actor-owned application startup remain separate phases.
2. `StartApplicationCommand` is posted only after bootstrap health is Healthy and actor command
   intake is ready.
3. Databento operational health cannot be a prerequisite of bootstrap health.
4. The API boot dispatcher posts a typed NATS command; it contains no domain startup operations.
5. `StartApplicationAsync` reconciles desired versus actual participant state and is safe to repeat.
6. The Application lifecycle actor owns startup ordering, correlation, aggregation, and final status.
7. Operational services may be constructed by dependency injection, but must remain idle until
   activated through their actor-owned startup participant.
8. The UI may subscribe and query, but cannot automatically start, stop, reset, roll, or supervise a
   backend service.
9. Closing the UI cannot stop Databento or any other backend operational participant.
10. Startup and recovery paths never display modal dialogs or wait for operator interaction.
11. Lifecycle progress and failures go to `IStatusConsoleWriter`, structured logs, and typed status
    state/events.
12. One Market Data lifecycle actor and its serialized runtime executor are the only Databento
    lifecycle owner.
13. The one-minute health loop and five-minute reset retry loop use `TimeProvider` and cancellable
    delays.
14. Reset attempts cannot overlap, recurse, or create more than one native generation.
15. Recovery has no attempt limit in this plan.
16. C++ and Rust remain behaviorally equivalent. Any native or ABI change is implemented and tested
    in both backends in the same gate.
17. The existing Market Outlook channel/cache design is unchanged. Reset never clears valid Market
    Outlook state or makes feed generation a cache-admission rule.
18. Production `ShutdownApplicationAsync` orchestration and scheduled close are not implemented by
    this plan. API process cancellation must still stop development tasks and dispose resources.
19. Security and authorization expansion are deferred; existing command paths and identities are
    preserved so later authorization can be added at the boundary.
20. Every changed behavior receives failing-first characterization and unit, BDD, integration,
    verification, and UI/system coverage proportional to its scope.
21. Startup activities execute strictly sequentially; Stage 1 contains no parallel startup groups.
22. Every activity owns a non-cancellation exception boundary, logs structured error detail, writes
    the bounded error to `IStatusConsoleWriter`, and returns a typed result.
23. Activity failure does not terminate the sequence. Dependent activities return
    `SkippedDependency`, while later independent activities are still attempted.
24. The aggregate terminal event is emitted only after every declared activity has returned a
    result. Any required `Failed` or `SkippedDependency` result makes the aggregate Failed; optional
    failures make it Degraded.

## 3. Execution controls

- Stage 1 must be complete before Stage 2 production code begins.
- Only one gate is `In progress` at a time in the execution record.
- A gate is complete only when its deliverables, focused tests, and exit evidence are recorded.
- New runtime behavior remains disabled behind configuration until its activation gate passes.
- Existing UI-owned behavior is removed only after the replacement actor path passes integration
  tests.
- No test may depend on Toronto wall-clock time, real five-minute delays, or a live Databento
  account unless explicitly classified as a live verification/soak test.
- `FakeTimeProvider`, deterministic native doubles, and fault injection are required for timing and
  recovery qualification.
- Unrelated baseline failures must be listed separately and cannot count as successful evidence.
- Production shutdown, security, and Aspire work cannot be pulled into either stage without a new
  approved plan revision.

## 4. Verified starting baseline

This section is the historical baseline captured before Stage 1. The completed implementation and
qualification record is in Section 9.

The starting system has the following relevant behavior:

- API `Program.cs` configures services, creates selected schemas, starts actors, and enters
  `RunAsync`, but does not post `StartApplicationCommand` after bootstrap.
- `ActorRuntimeHealthCheck` reports actor supervisor readiness.
- `/health/ready` also includes `MarketDataRuntimeHealthCheck`; using the combined endpoint as the
  startup prerequisite would create a Databento/startup dependency cycle.
- `ApplicationCommandApi.StartApplicationAsync` already posts the typed command through NATS.
- `ApplicationEventActor` currently handles `ApplicationStartupEvent` as a no-op.
- `FuturesContractRolloverStartupService` independently starts/stops the market-data runtime as an
  API-hosted side effect.
- `IFMAppViewModel.StartApplicationCoreAsync` loads startup data, initiates warmup, starts feed and
  analytics operations, and owns health/session monitoring.
- UI disposal can issue feed stop commands.
- UI `LastError` may become a modal `ShowErrorMessage` dialog.
- `IStatusConsoleWriter` already publishes `StatusConsoleLoggedEvent` notifications.
- `DatabentoMarketDataApi` exposes the bounded synchronous `IsDatabentoFeedUp` probe.
- Feed start, stop, reset, tick-stream, and bar-stream actor paths already exist, but lifecycle
  ownership is distributed.
- Framework recovery utilities exist but are not the production coordinator and do not implement
  this plan's unlimited five-minute policy.

## 5. Stage 1 - Actor-owned StartApplication refactor

### Stage 1 outcome

After Stage 1, a healthy API boot automatically submits `StartApplicationCommand`. The Application
lifecycle actor reconciles every registered operational startup participant and publishes an
accurate terminal result. The same command remains safe for the scheduled 17:59 invocation. The UI
only subscribes, queries, and renders; UI startup or shutdown cannot control backend lifecycle.

Databento can be started through its actor-owned startup participant in Stage 1, but automatic
Failed-to-Resetting recovery remains disabled until Stage 2.

### ASR-01 - Baseline characterization and startup inventory

**Deliverables**

- Inventory every API hosted service and UI method with operational startup or shutdown side
  effects.
- Classify each item as host bootstrap, actor startup participant, presentation-only subscription,
  query/hydration, or deferred shutdown.
- Capture the current application-command, event, completion, and UI-listener behavior.
- Add failing-first architecture tests for current UI feed ownership and Application actor no-op
  behavior.
- Record focused build/test baselines for Application, MarketData Feed, API, and UI projects.

**Exit verification**

- No startup side effect remains unclassified.
- The inventory explicitly identifies direct `IMarketDataApi.StartAsync`, `StopAsync`, and reset
  callers.
- Tests demonstrate the current command does not establish participant readiness.
- Existing unrelated failures are recorded separately.

### ASR-02 - Bootstrap health split

**Deliverables**

- Tag health checks as `bootstrap`, `application`, or combined `ready`.
- Define bootstrap health from host configuration, required schema compatibility, NATS, actor
  supervisor, and command intake.
- Exclude Databento runtime/feed state from the bootstrap gate.
- Expose bootstrap status for verification without a self-referential HTTP call.
- Preserve operator-facing application/market-data detail in `/health/ready`.

**Exit verification**

- API bootstrap can become Healthy while Databento is not yet started.
- Actor intake cannot be reported bootstrap Healthy before consumers are ready.
- Databento degradation still appears in application readiness after startup begins.
- Unit and API integration tests prove there is no readiness dependency cycle.

### ASR-03 - Post-bootstrap StartApplication dispatcher

**Deliverables**

- Add a minimal `ApplicationStartupCommandDispatcher` hosted bridge.
- Wait for `ApplicationStarted`, bootstrap Healthy, and actor command readiness.
- Resolve the authoritative operational value date.
- Submit `StartApplicationAsync` through the typed NATS API exactly once per API process boot.
- Add configuration `ApplicationStartup:AutoStartAfterBootstrap`, defaulting to `true` in
  Development.
- Publish acceptance/rejection to System Console and structured logs.
- Do not retry independently after accepted delivery; actor reconciliation owns repeated commands.

**Exit verification**

- No command is posted before bootstrap readiness.
- Exactly one automatic command is posted per process boot.
- Dispatcher contains no direct participant or Databento API call.
- Disabled configuration prevents automatic dispatch without affecting manual/scheduled commands.
- Cancellation during bootstrap exits without an operational exception or partial command.

### ASR-04 - Application lifecycle state and actor contracts

**Deliverables**

- Evolve the Application actor from its startup no-op into a lifecycle coordinator.
- Add startup states `Bootstrapped`, `Starting`, `Running`, `Degraded`, `Failed`, and
  `ScheduledStopped`.
- Store the current process-boot value date, boot ID, command/correlation IDs, timestamps, and
  activity outcomes.
- Add a Starting snapshot, typed activity-terminal results, and startup-complete/degraded/fail
  event contracts.
- Add a typed current-startup status query; the activity collection is the participant-detail view.
- Preserve command acceptance as distinct from workflow completion.

**Exit verification**

- `ApplicationStartupCompleteEvent` cannot occur before all required participants are terminal.
- Required failure produces Failed; optional failure produces Degraded.
- Missed notifications can be reconstructed by query.
- MessagePack/NATS contract compatibility and serialization round trips pass.
- Command/event correlation is stable while queued commands are serialized by the actor mailbox.

### ASR-05 - Participant orchestration and idempotency

**Deliverables**

- Define the participant registry, criticality, dependencies, timeouts, and one explicit sequential
  activity order.
- Implement dependency-ordered sequential execution from authority through final qualification.
- Implement `Started`, `AlreadySatisfied`, `ScheduledStopped`, `Degraded`, and `Failed` outcomes.
- Coalesce or observe duplicate commands while reconciliation is active.
- Probe actual participant state before performing a side effect.
- Fence previous value-date resources before starting a new value date.
- Ensure actor redelivery cannot duplicate workers, subscriptions, or native generations.

**Exit verification**

- Deterministic tests prove exact ordering and prohibit parallel activity execution.
- Repeated same-date commands create no duplicate side effects.
- Concurrent commands converge to one coherent result.
- A new value date cannot mix contracts or runtime state from the previous date.
- Participant timeout is isolated, reported, and reflected in aggregate state.
- A failed activity does not prevent every later activity from returning a result.
- A blocked dependent result is `SkippedDependency`, while later independent work is attempted.

### ASR-06 - Market Data startup participant and lifecycle ownership cutover

**Deliverables**

- Add the Market Data startup participant controlled by `MarketDataLifecycleActor`.
- Move rollover reconciliation and current-contract qualification behind that participant.
- Start Databento, required ES/VX feeds, aggregation workers, tick routes, bar routes, and publishers
  through the existing actor command/event paths.
- Convert `FuturesContractRolloverStartupService` from an independent lifecycle owner into an idle
  executor/adapter or remove it after replacement.
- Establish one serialized lifecycle operation permit.
- Add architecture enforcement that prohibits other production callers from directly mutating
  Databento lifecycle.
- Stage 1 reports startup failure truthfully but does not yet enable automatic reset.

**Exit verification**

- API startup can establish all configured feeds without the UI process.
- One startup command produces no more than one native generation.
- Required contracts and routes are qualified before participant success.
- Existing direct API-hosted and UI lifecycle ownership is removed.
- Market Outlook remains writable/queryable and is not cleared by lifecycle activation.

### ASR-07 - Remaining operational startup participant migration

**Deliverables**

- Move automatic reference imports that are operational prerequisites into an actor participant.
- Move historical analytics warmup/hydration into an Analytics startup participant.
- Move realtime RSI, TDI, ITI, EMA, Bollinger, MDI, EOD, and composite signal activation behind
  actor-owned participants where activation is required.
- Classify Market Outlook processor, FMP import, Trade Position, Trade Plan, and other registered
  hosted services as bootstrap infrastructure or actor-activated operational participants.
- Make operational hosted components idle until their participant activation when applicable.
- Keep high-throughput local channel processing local; actor messages control lifecycle rather than
  carrying high-frequency updates.

**Exit verification**

- Every operational startup item from ASR-01 has a tested target owner.
- Required analytics do not start before their market-data prerequisites.
- Historical warmup failure is visible and cannot silently report startup complete.
- Repeated reconciliation does not duplicate timers, listeners, consumers, or processors.
- Existing Market Outlook producer/channel/cache semantics remain unchanged.

### ASR-08 - UI startup and disposal simplification

**Deliverables**

- Remove automatic reference import, feed start/stop/reset, rollover, warmup, realtime signal
  startup, backend health decisions, and operational session transitions from `IFMAppViewModel`.
- Retain UI-only event subscriptions, queries, view-model initialization, and rendering.
- Change UI startup to subscribe first, query application lifecycle status, then hydrate current
  display snapshots.
- Remove feed stop and application shutdown commands from main-form disposal.
- Preserve authorized explicit operator commands through actor APIs where applicable.
- Ensure closed/degraded/recovering market data does not prevent read-only menu access.

**Exit verification**

- Starting UI with an already-running API does not change native generation/feed state.
- Closing and reopening UI does not change backend lifecycle.
- UI can connect before, during, or after application reconciliation.
- Missed startup events are recovered from the status query.
- UI system tests prove presentation subscriptions still update Market Outlook and status views.

### ASR-09 - System Console and no-dialog lifecycle policy

**Deliverables**

- Route every startup phase, participant transition, warning, and failure through
  `IStatusConsoleWriter`.
- Include source, value date, command/correlation IDs, participant, outcome, reason, and next action.
- Write equivalent structured server logs.
- Remove `LastError`/`ShowErrorMessage` publication from startup and feed-lifecycle paths.
- Prevent healthy one-minute status from flooding the console; emit transitions and actionable
  outcomes.
- Query lifecycle state after the UI listener subscribes so late UI startup receives current status.

**Exit verification**

- Startup success, degradation, and failure appear in System Console tests.
- No startup/lifecycle path calls `MessageBox`, `ShowDialog`, or waits for acknowledgement.
- System Console publication failure cannot fail a participant or startup workflow.
- Structured logs contain the same workflow correlation and bounded reason.
- User-initiated business confirmation dialogs outside lifecycle scope remain unchanged.

### ASR-10 - Stage 1 BDD, integration, verification, and UI qualification

**Deliverables**

- Add BDD scenarios for healthy ordering, optional degradation, required failure/dependency skip,
  and scheduled-stop aggregation. Cover duplicate commands and continuation after failure at the
  workflow unit boundary, and UI-independent ownership with architecture/system tests.
- Add actor/NATS integration tests for command-to-participant-to-terminal-result flow.
- Add API process tests for bootstrap-health handoff.
- Add UI system tests for observer-only initialization, late attachment, and close/reopen behavior.
- Add architecture tests for sole lifecycle ownership and prohibited lifecycle dialogs.
- Run all affected unit, BDD, integration, presentation, and system-test projects.

**Exit verification**

- All new tests pass deterministically.
- Full affected-project suites pass or unrelated baselines are documented with evidence.
- An API-only composition can execute terminal startup without any UI dependency.
- A UI launched afterward can retrieve the current Application status and hydrate Market Outlook.
- No duplicate native generation, subscription, or hosted operation is observed.

### ASR-11 - Stage 1 acceptance boundary

Stage 1 is complete only when:

1. bootstrap health is independent of Databento application health;
2. one typed `StartApplicationCommand` is posted after healthy API bootstrap;
3. Application actor participant orchestration is real, correlated, idempotent, and queryable;
4. all operational startup identified in ASR-01 has moved out of the UI;
5. all Databento lifecycle mutation is behind the Market Data lifecycle actor;
6. UI startup and shutdown do not alter backend lifecycle;
7. lifecycle messages use System Console/structured logs without modal dialogs;
8. required BDD, unit, integration, verification, and UI tests pass; and
9. Stage 1 evidence is recorded before Stage 2 begins.

## 6. Stage 2 - Databento reset lifecycle

### Stage 2 entry criteria

- ASR-01 through ASR-11 are complete.
- The Market Data startup participant is the sole Databento lifecycle owner.
- API-only startup establishes and queries current market-data state.
- No UI code starts, stops, resets, or makes recovery decisions.
- Automatic recovery remains disabled until DRC-07 activation.

### Stage 2 outcome

After Stage 2, the API-hosted actor-owned lifecycle evaluates Databento health every minute. A core
Failed result enters Orange/Resetting, performs a complete teardown and restart, and retries every
five minutes without overlap or an attempt limit until Healthy or API process cancellation. The UI
observes state and System Console progress but does not participate in recovery.

### DRC-01 - Recovery baseline and fault model

**Deliverables**

- Inventory current health policies, runtime probes, native terminal facts, managed worker state,
  route freshness, and existing reset paths.
- Define recoverable core failures, optional degradation, quiet-feed freshness, cleanup failures,
  and fatal host cancellation.
- Add failing-first tests for completed native readers/aggregation workers with an epoch still
  present.
- Capture current reset completion timing and generation/resource counts.

**Exit verification**

- Every Failed input maps to a named reason and recovery decision.
- Connection/runtime failure and market-data freshness failure remain distinguishable.
- Existing false-positive/false-negative cases are represented by deterministic fixtures.
- Baseline resource and reset evidence is recorded for comparison.

### DRC-02 - Serialized lifecycle executor and state machine

**Deliverables**

- Implement states `ScheduledStopped`, `Starting`, `Healthy`, `Degraded`, `Failed`, and `Resetting`.
- Add one serialized operation queue/permit for start, probe, and reset.
- Register the process-local recovery runtime idle and activate it only from the Market Data actor.
- Store current generation, value date, state revision, reason, correlation, attempt count, and next
  retry time.
- Make state transitions atomic and queryable.

**Exit verification**

- Concurrent operations cannot create overlapping native generations.
- Invalid transitions are ignored/reconciled without operational exceptions.
- State snapshots are internally coherent under concurrent queries.
- Repeated transitions do not recurse or grow the stack.

### DRC-03 - One-minute health evaluation

**Deliverables**

- Run one cancellable health evaluation every minute while lifecycle is enabled.
- Use the bounded synchronous Databento probe, native terminal state, managed aggregation state,
  required route state, last accepted update, and processing/publication failures.
- Apply existing Green (within five minutes), Yellow (five to fifteen minutes), and Failed/Red
  (beyond fifteen minutes or confirmed core failure) policy.
- Keep optional feed failure Degraded/Orange without resetting healthy core feeds unless policy
  explicitly marks it core.
- Record health counters and reasons without blocking the data path.

**Exit verification**

- `FakeTimeProvider` tests prove exact boundaries.
- A quiet provider and a broken runtime produce different reasons.
- Confirmed core runtime failure need not wait fifteen minutes.
- Health polling cannot overlap reset or mutate Market Outlook.
- Probe timeout becomes a health fact rather than an escaping expected exception.

### DRC-04 - Failed-to-Resetting transition

**Deliverables**

- Atomically transition core Failed to Resetting once per state revision.
- Assign one recovery correlation ID and monotonic attempt number.
- Cancel/suppress redundant watchdog-triggered resets while Resetting.
- Publish transition, reason, and next action to System Console and structured logs.
- Preserve last valid market-data and Market Outlook snapshots with explicit stale health.

**Exit verification**

- Multiple simultaneous failure observations initiate exactly one reset.
- Resetting is Orange and queryable before teardown begins.
- No cache clear, version fence, or input rejection is introduced.
- Optional-only degradation does not start the core reset cycle.

### DRC-05 - Complete Databento reset operation

**Deliverables**

- Stop/detach feed publishers and routes.
- Stop managed aggregation workers.
- capture native terminal diagnostics before handle destruction.
- Stop and dispose all native feeds and the complete epoch.
- Continue best-effort teardown after individual cleanup failures while recording each failure.
- Reload authoritative value date/contracts and rerun rollover reconciliation.
- Create exactly one new native generation and restart required feeds, workers, routes, and
  publishers.
- Perform bounded post-start qualification.

**Exit verification**

- No prior native handle, worker, route, or publisher remains active after teardown.
- Partial stop failure cannot prevent the next clean attempt or create a second generation.
- Reset restores ES quarterly, VX front, and VX second required roles.
- Post-start qualification verifies runtime and routes rather than command acceptance alone.
- Market Outlook resumes from new writes without a reset-driven clear.

### DRC-06 - Unlimited five-minute retry cycle

**Deliverables**

- On failed post-reset qualification, remain Resetting.
- Schedule the next complete reset five minutes after the prior attempt completes.
- Repeat without an attempt limit until Healthy or API process cancellation.
- Reset attempt count remains diagnostic and cannot overflow into invalid state.
- Publish failed attempt and next retry time without per-second log noise.

**Exit verification**

- Ten or more deterministic failed attempts execute sequentially without overlap.
- Advancing fake time by less than five minutes cannot start the next attempt.
- A later successful attempt enters Healthy and cancels the retry delay.
- Long-running attempts shift the next retry from completion and never pile up.
- Cancellation exits promptly and no delayed continuation restarts the feed.

### DRC-07 - Recovery activation and compatibility fencing

**Deliverables**

- Add `MarketDataRecovery:Enabled`, default `true` in Development after qualification.
- Remove/disable competing UI health recovery and unused production recovery utilities.
- Route authorized manual reset through the same serialized lifecycle executor.
- Fence stale reset-complete/fail events by command/correlation/state revision.
- Preserve existing query and UI notification compatibility where reasonable.

**Exit verification**

- Exactly one automatic recovery owner is active.
- Manual and automatic reset cannot overlap.
- Delayed terminal events cannot overwrite a later Healthy generation.
- Disabling recovery preserves health visibility without reset side effects.

### DRC-08 - Recovery status, metrics, and System Console

**Deliverables**

- Expose current lifecycle state, state revision, health reason, attempt, attempt timestamps, last
  outcome, and next retry through a typed query.
- Publish typed health/recovery transition events.
- Add bounded counters and latency for probes, resets, cleanup failures, startup qualification, and
  successful recovery.
- Publish System Console messages for Failed, Resetting, attempt failure, next retry, and Healthy.
- Do not publish routine Healthy polls as console messages.

**Exit verification**

- UI can reconnect during attempt N and query the complete current state.
- Metrics distinguish probe failure, teardown failure, startup failure, and qualification failure.
- Correlation IDs appear in console, structured log, and actor status.
- Metrics/status recording cannot throw into lifecycle or market-data processing.

### DRC-09 - C++ and Rust parity qualification

**Deliverables**

- Verify both native backends expose equivalent lifecycle, terminal, statistics, and up/down probe
  behavior required by the reset executor.
- If an ABI extension is required, implement identical exports, layouts, version, status mapping,
  and validation in C++ and Rust.
- Run the shared capability-manifest and binary contract suites.
- Inject terminal completion, connection failure, disposed handles, slow-reader warning, and restart.

**Exit verification**

- The same managed binaries and recovery tests pass against both native backends.
- ABI versions, struct sizes, enum values, calling conventions, and export names match.
- Slow-reader advisory status does not become false terminal completion.
- No use-after-free, leaked handle, or backend-specific recovery rule remains.

### DRC-10 - Stage 2 BDD, integration, runtime, and UI qualification

**Deliverables**

- Add BDD scenarios for Healthy, Yellow, Failed, Resetting, repeated failure, eventual recovery,
  optional degradation, and process cancellation.
- Add actor/NATS tests for health-to-reset-to-terminal status correlation.
- Add managed/native integration tests for complete teardown/restart.
- Add UI tests for Orange Resetting, retry detail, later Healthy, reconnect, and no dialogs.
- Run API-only overnight-equivalent fake-time verification.
- Run focused live/synthetic smoke and bounded resource checks for the selected development backend.

**Exit verification**

- All deterministic suites pass for unlimited recovery and later success.
- UI remains responsive and read-only data remains visible throughout Resetting.
- No expected lifecycle condition produces an unhandled/first-chance operational exception.
- Resource counts return to a stable bound across repeated resets.
- Full affected-project suites pass or unrelated baselines are documented.

### DRC-11 - Stage 2 and plan acceptance boundary

Stage 2 and this plan are complete only when:

1. one actor-owned component is the sole Databento lifecycle authority;
2. health is evaluated once per minute without blocking market-data producers;
3. core Failed transitions exactly once into Resetting for a state revision;
4. reset fully tears down and replaces the Databento generation and managed routes;
5. failed qualification retries five minutes after attempt completion without a limit;
6. attempts never overlap and manual reset uses the same executor;
7. a later valid probe returns the lifecycle to Healthy;
8. API process cancellation terminates retries and disposes development resources safely;
9. System Console, structured logs, typed events, queries, and metrics expose the current reason and
   recovery progress;
10. UI participates only as an observer and never shows a recovery dialog;
11. C++ and Rust parity requirements pass; and
12. all required BDD, unit, integration, verification, runtime, and UI/system suites pass.

## 7. Cross-stage verification matrix

| Behavior | Unit | BDD | Actor/NATS integration | Runtime/native | UI/system |
| --- | --- | --- | --- | --- | --- |
| Bootstrap health split | Required | Required | Required | Required | Observe only |
| Automatic command dispatch | Required | Required | Required | Required | Query result |
| Participant ordering/idempotency | Required | Required | Required | Required | Status display |
| UI lifecycle removal | Architecture | Required | Required | Required | Required |
| System Console/no dialogs | Required | Required | Required | Required | Required |
| One-minute health evaluation | Required | Required | Required | Required | Status display |
| Full Databento reset | Required | Required | Required | Required for C++/Rust | Required |
| Unlimited five-minute retry | Required | Required | Required | Fake-time/resource | Required |
| Eventual Healthy recovery | Required | Required | Required | Required | Required |
| Process cancellation | Required | Required | Required | Required | No restart |

## 8. Required test projects

At minimum, execute the affected portions and then the complete suites for:

- `TomasAI.IFM.Domain.Application.Actor.UnitTests`
- `TomasAI.IFM.Domain.Application.Actor.BDDTests`
- `TomasAI.IFM.Application.Actor.IntegrationTests` (actor-host composition build)
- `TomasAI.IFM.Application.Api.IntegrationTests`
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests`
- `TomasAI.IFM.Domain.MarketData.Feed.BDDTests`
- `TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests`
- `TomasAI.IFM.Framework.MarketData.DataBento.UnitTests`
- `TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests`
- `TomasAI.IFM.UI.Net.Presentation.UnitTests`
- `TomasAI.IFM.UI.Net.SystemTests`

Build and test filters must be recorded with duration, result counts, skips, and any unrelated
baseline failures. Native qualification records the backend artifact, ABI version, and capability
manifest hash.

## 9. Execution record

| Gate | Status | Commit/worktree evidence | Automated tests | Runtime/UI evidence | Notes |
| --- | --- | --- | --- | --- | --- |
| ASR-01 | Complete | Startup ownership source audit and characterization tests | Application and UI architecture tests | Historical ownership inventory recorded | Baseline section retained as historical evidence |
| ASR-02 | Complete | `ApplicationBootstrapReadiness`; health tags; `/health/bootstrap` | Dispatcher/architecture tests; API build | Bootstrap evaluation excludes application/Databento health | No HTTP self-probe |
| ASR-03 | Complete | `ApplicationStartupCommandDispatcher`; Development configuration | Exact-once, disabled, and post-`ApplicationStarted` unit tests | Typed NATS command dispatch; bounded bootstrap wait | Dispatcher has no participant operations |
| ASR-04 | Complete | Lifecycle DTOs, status store, terminal events, typed NATS status query | Contract round-trip and query-route tests | Late UI observer queries current status | Stage 1 status is process-local; API restart initiates fresh reconciliation |
| ASR-05 | Complete | Authoritative seven-activity plan and coordinator | Ordering, dependency skip, required/optional failure, duplicate tests | Every declared activity reaches a typed result | Actor mailbox serialization coalesces queued duplicates |
| ASR-06 | Complete | Contract reconciliation and actor-command feed activation; legacy hosted owner removed | Application workflow plus 502 Feed unit, 319 Feed BDD, 48/52 Feed integration | Bounded Databento qualification probe | Four Feed integration cases are intentionally skipped |
| ASR-07 | Complete | Reference import, historical warmup, realtime analytics, and final qualification adapters | 1,000 Analytics unit and 50 Analytics integration tests | Startup mutations are issued through typed actor APIs | Historical warmup remains optional/degrading |
| ASR-08 | Complete | Automatic lifecycle calls removed from `IFMAppViewModel`; status-query UI service | 289 UI presentation and 75 UI system tests | UI close has presentation-only cleanup | Explicit operator command services remain available |
| ASR-09 | Complete | Per-activity structured logs and `IStatusConsoleWriter` reporting | Workflow and UI architecture tests | Status Console failure is isolated from activity outcome | No lifecycle modal-error publication |
| ASR-10 | Complete | BDD, unit, integration, architecture and native regression coverage | 14 Application unit; 4 Application BDD; all recorded suites green | API composition reaches container validation | Live external-store run is environment-dependent |
| ASR-11 | Complete | API owns dispatch and participants; UI is observer | API/UI builds: 0 warnings, 0 errors | API-only composition verified without UI startup dependencies | Stage 2 recovery remains disabled/deferred |
| DRC-01 | Not started | | | | |
| DRC-02 | Not started | | | | |
| DRC-03 | Not started | | | | |
| DRC-04 | Not started | | | | |
| DRC-05 | Not started | | | | |
| DRC-06 | Not started | | | | |
| DRC-07 | Not started | | | | |
| DRC-08 | Not started | | | | |
| DRC-09 | Not started | | | | |
| DRC-10 | Not started | | | | |
| DRC-11 | Not started | | | | |

### 9.1 Stage 1 implementation record (2026-09-02)

The implemented startup order is authoritative and strictly sequential:

1. resolve market-session/value-date authority;
2. reconcile optional reference data;
3. reconcile quarterly ES plus front/second VX contracts;
4. start or qualify Market Data through its typed actor API;
5. request optional historical Analytics warmup;
6. start configured realtime Analytics signals through typed actor APIs; and
7. qualify Market Outlook processor and Databento operational state.

Each invocation owns a non-cancellation exception boundary. A failed prerequisite produces
`SkippedDependency`; independent activities continue. Only after all seven results are available is
the status aggregated to Running, ScheduledStopped, Degraded, or Failed and a terminal event sent.
The event projector no longer fabricates startup or shutdown completion before the corresponding
Application event-family handler performs the work.

The status store deliberately remains process-local in Stage 1. This is sufficient for UI restarts:
the UI subscribes first and then retrieves the current typed snapshot over NATS. An API restart
creates a new boot identity and runs reconciliation again. Cross-process lifecycle-history
persistence is not claimed by this stage.

### 9.2 Qualification evidence

| Suite/build | Result |
| --- | --- |
| Application actor unit | 14 passed, 0 failed |
| Application actor BDD | 4 passed, 0 failed |
| Market Data Feed unit | 502 passed, 0 failed |
| Market Data Feed BDD | 319 passed, 0 failed |
| Market Data Feed integration | 48 passed, 4 intentionally skipped, 0 failed |
| Databento unit (including native C++ build) | 133 passed, 0 failed |
| Databento integration | 7 passed, 0 failed |
| Market Data Analytics unit | 1,000 passed, 0 failed |
| Market Data Analytics integration | 50 passed, 0 failed |
| UI presentation unit/architecture | 289 passed, 0 failed |
| UI system | 75 passed, 0 failed |
| API Server build | succeeded, 0 warnings, 0 errors |
| UI SystemTests composition build | succeeded, 0 warnings, 0 errors |
| Application actor integration-host composition | succeeded, 0 warnings, 0 errors |

An API process bootstrap probe passed configuration and dependency-injection/container validation,
then encountered the pre-existing local-environment absence of Scylla keyspace `reference_db` in
the unrelated Trade Strategy Family bootstrapper. That external data-store condition is recorded
separately and is not counted as successful Stage 1 runtime evidence.

## 10. Definition of complete

The plan is not complete when code merely compiles or a command is accepted. Completion requires:

- the actor workflow owns real participant effects;
- startup succeeds without launching the UI;
- UI lifecycle cannot alter backend services;
- current state is queryable after missed events or UI restart;
- Failed Databento is continuously reset until a later Healthy result;
- all retry, cancellation, concurrency, and native parity conditions are proven;
- no startup/recovery modal dialog remains; and
- the execution record contains reproducible automated and runtime evidence for every gate.
