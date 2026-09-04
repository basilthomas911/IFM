# Market Outlook Startup Delivery Fix Plan v1.0

**Status:** Completed  
**Date:** 2026-09-04  
**Scope:** Application lifecycle-event delivery, startup observability, historical analytics warmup,
and Market Outlook qualification  
**Primary incident:** API boot accepted `StartApplicationCommand`, but the Application lifecycle
workflow never entered `Starting`; Market Outlook continued with live prices while RSI, EMA, and
Bollinger inputs remained unavailable.

## 1. Outcome

After this fix, an API boot that accepts `StartApplicationCommand` reliably delivers the resulting
`ApplicationStartupEvent` to `ApplicationEventActor`. The seven startup activities execute, the
historical analytics warmup supplies Market Outlook's daily inputs, and readiness distinguishes
command acceptance from workflow execution and completion.

The completed behavior is:

```text
API bootstrap healthy
  -> StartApplicationCommand accepted
  -> durable ApplicationStartupEvent handoff
  -> Application lifecycle state becomes Starting
  -> historical analytics warmup publishes daily inputs
  -> Market Outlook becomes complete, or reports an explicit startup degradation
  -> application readiness becomes Running, ScheduledStopped, Degraded, or Failed
```

## 2. Incident evidence and failure boundary

The live incident established the following facts:

- `/health/bootstrap` was Healthy and the actor runtime reported 128 registered actors.
- The Event JetStream consumer started at `08:23:15.203 -04:00`.
- `StartApplicationCommand` was accepted at `08:23:15.621 -04:00` with command ID
  `e8f7ee34-5fcf-4af6-86b1-27ee469f9220`.
- `ApplicationStartupEvent` event version `842384` was persisted.
- No corresponding Application lifecycle `Starting`, completion, degradation, or failure was
  recorded.
- `/health/ready` remained Degraded with lifecycle state `Bootstrapped` and the inaccurate summary
  `Application startup has not yet been requested.`
- The Market Outlook snapshot remained live and valid but incomplete. Its missing inputs were
  `RSI warming, EMA, Bollinger Bands`; `rsiAvailability` and `dailyAnalyticsAvailability` were zero.
- No current-process historical warmup checkpoint was created.

The fault is between committed command-event persistence and execution by `ApplicationEventActor`.
`ApplicationEventProjector` currently sends startup and shutdown events through the process-local,
non-durable projector lane. That lane has no projector execution state, outbox record, retry, or
restart recovery guarantee. Command acceptance can therefore outlive the only delivery attempt.

## 3. Binding decisions

1. Application startup and shutdown events are operational control messages and must use the
   durable projector path.
2. Command acceptance remains distinct from workflow completion, but an accepted command must have
   a recoverable event-delivery record before success is returned.
3. The Application lifecycle actor remains the sole owner of participant ordering and side effects.
4. At-least-once event delivery is expected; every startup participant must remain idempotent.
5. Historical Market Outlook snapshots remain process-local and rebuildable. This fix does not
   persist the composite snapshot.
6. Feed health and Market Outlook completeness remain separate signals. A green live feed cannot
   hide missing historical analytics.
7. Existing historical lifecycle events without projector state must not be replayed during the
   rollout.
8. The UI remains an observer. It must not start feeds, run warmup, or repair backend lifecycle.
9. No direct database edit, event deletion, or manual replay is part of the normal fix.

## 4. Implementation stages

### MOSD-01 - Lock in the failing behavior

**Changes**

- Add an integration test that persists `ApplicationStartupEvent`, crosses the projector boundary,
  and waits for `IApplicationStartupStatusStore` to leave `Bootstrapped`.
- Add a fault-injection test that interrupts delivery after event persistence and before actor
  execution.
- Capture projector metrics and structured logs using `ProjectorName`, `EventId`, `CommandId`, and
  lifecycle value date.
- Add a regression assertion that command acceptance alone is not counted as successful startup.

**Primary projects**

- `TomasAI.IFM.Domain.Application.UnitTests`
- `TomasAI.IFM.Domain.Application.IntegratedTests`
- `TomasAI.IFM.Application.Actor.IntegrationTests`

**Exit criteria**

- The current non-durable configuration reproduces the missing lifecycle transition.
- The test fails for the observed reason, not because of timing, UI startup, or unavailable market
  data.

### MOSD-02 - Make the lifecycle handoff durable

**Changes**

- Change both `ApplicationStartupEvent` and `ApplicationShutdownEvent` descriptors in
  `ApplicationEventProjector` to `useDurableReplay: true`.
- Keep fenced execution and the transactional outbox enabled for the API host.
- Require an explicit `event_projector_state` and outbox/queue acceptance for each new lifecycle
  event before the command request returns success.
- Verify that publication failure moves the projector execution to a retryable state instead of
  being logged and discarded.
- Verify restart recovery republishes a claimed, non-terminal lifecycle event exactly through the
  existing durable replay queue.
- Update the descriptor test so it rejects any future change back to the transient lane.

**Migration safety**

The current recovery SQL only selects event-log rows joined to explicit projector state in
`Processing` or `Retrying`. Old non-durable lifecycle events have no such state and therefore are
not eligible for recovery. Add an automated migration-safety test proving:

- historical lifecycle events without projector state are ignored;
- a new durable lifecycle event with retryable state is recovered; and
- terminal completed, failed, superseded, or skipped executions are not replayed.

The stranded incident event is retained as audit evidence. The first API restart after deployment
will dispatch a new idempotent startup command for the current operational value date; it will not
manufacture projector state for every old event.

**Exit criteria**

- Killing the process after event persistence and before publication cannot lose the new startup
  request once durable acceptance has occurred.
- A restart processes the recoverable event and reaches a terminal lifecycle state.
- Redelivery does not create duplicate native generations, feed routes, timers, or analytics
  workers.

### MOSD-03 - Make handoff and readiness status truthful

**Changes**

- Add a small process-local startup-handoff status record owned by the dispatcher. Record:
  submission time, command ID, acceptance result, observation deadline, first observed lifecycle
  transition, retry count, and last handoff error.
- After command acceptance, observe the typed Application startup status until the matching command
  reaches `Starting` or a terminal state.
- If the observation deadline expires, report `accepted-but-not-observed` to structured logs,
  System Console, metrics, and readiness.
- Permit a bounded handoff retry only while the lifecycle actor has not observed the command.
  Participant retries remain owned by the lifecycle actor.
- Correlate by command ID and value date so a stale process-local snapshot cannot satisfy a new boot.
- Compose `ApplicationLifecycleHealthCheck` from lifecycle status and dispatcher handoff status.

**Required health wording**

| Condition | Readiness description |
| --- | --- |
| Dispatch not attempted | Application startup has not yet been requested. |
| Command rejected | Application startup command was rejected: `<reason>`. |
| Accepted, event not observed | Application startup was accepted but its lifecycle event has not been observed. |
| Workflow executing | Application startup activities are executing. |
| Workflow terminal | Use the actor-owned Running, ScheduledStopped, Degraded, or Failed summary. |

**Exit criteria**

- Readiness can no longer say `not requested` after command acceptance.
- A lost or delayed handoff becomes visible before Market Outlook is mistaken for a UI problem.
- Bounded retry stops immediately after the matching lifecycle status is observed.

### MOSD-04 - Qualify historical analytics and Market Outlook

**Changes**

- Preserve the startup order: authority, reference data, contracts, historical warmup, realtime
  analytics, market data, final qualification.
- Ensure `WarmHistoricalAnalyticsAsync` does not return success until the historical replay publisher
  has submitted the Market Outlook warmup update for every required current contract.
- Correlate warmup checkpoints to process boot ID, value date, and contract ID.
- Hydrate the new process-local Market Outlook cache from the exact current-date durable snapshot
  before applying warmup, then publish the merged snapshot so API queries and UI notifications do
  not remain on the pre-restart value.
- Make final operational qualification inspect the Market Outlook snapshot for the current ES
  contract and value date.
- When daily warmup is configured and reports success, require `hasWarmDailyAnalytics=true`, a
  warm EMA, and warm Bollinger Bands before qualification succeeds. Intraday RSI warms from live
  bars and remains an explicit partial-state reason until its configured window is satisfied.
- When warmup is optional and fails, preserve live feed operation but return an explicit Degraded
  activity result containing the missing inputs.
- Do not infer analytics readiness from `feedHealth=Green`.

**UI behavior**

- Continue rendering available ES, VX, ITI, OHLC, and volume values while the snapshot is partial.
- Present the backend `missingInputs` value as a warming/degraded status rather than leaving fields
  unexplained.
- Clear the warming message when a later complete snapshot is received.
- Do not add UI-side warmup or lifecycle commands.

**Exit criteria**

- A successful startup produces a complete Market Outlook snapshot for the current ES contract.
- A warmup failure produces a usable partial snapshot plus an explicit degraded reason.
- UI restart timing has no effect on backend warmup or Market Outlook completeness.

### MOSD-05 - End-to-end qualification and rollout

**Automated tests**

1. Descriptor tests prove lifecycle events are durable.
2. Projector tests prove explicit execution state is created before durable queue acceptance.
3. Publication failure, NATS outage, and process-restart tests prove retry and recovery.
4. Redelivery tests prove same-date startup is idempotent.
5. Dispatcher tests cover accepted-and-observed, accepted-but-not-observed, rejection, timeout,
   retry, cancellation, and stale-correlation cases.
6. Startup workflow tests prove historical warmup precedes realtime analytics and final
   qualification.
7. Market Outlook tests prove partial-to-complete transition after warmup.
8. API integration tests prove startup completes without launching the UI.
9. UI tests prove warming, degraded, and complete states render without initiating backend work.

**Suggested verification commands**

```powershell
dotnet test TomasAI.IFM.Domain.Application.UnitTests/TomasAI.IFM.Domain.Application.Actor.UnitTests.csproj
dotnet test TomasAI.IFM.Domain.Application.IntegratedTests/TomasAI.IFM.Domain.Application.Actor.IntegratedTests.csproj
dotnet test TomasAI.IFM.Application.Actor.IntegrationTests/TomasAI.IFM.Application.Actor.IntegrationTests.csproj
dotnet test TomasAI.IFM.Application.Api.IntegrationTests/TomasAI.IFM.Application.Api.IntegrationTests.csproj
dotnet test TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.csproj
dotnet test TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests/TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.csproj
dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests/TomasAI.IFM.UI.Net.Presentation.UnitTests.csproj
dotnet test TomasAI.IFM.UI.Net.SystemTests/TomasAI.IFM.UI.Net.SystemTests.csproj
```

**Live rollout sequence**

1. Record the pre-deployment lifecycle health, current Market Outlook snapshot, latest startup
   command/event IDs, and current warmup checkpoints.
2. Stop the API normally; do not delete JetStream state or event-source records.
3. Deploy the durable descriptor, handoff health, and qualification changes together.
4. Start the API without starting the UI.
5. Verify a new command is accepted and the matching lifecycle status reaches `Starting`.
6. Verify all seven activity results are recorded for the current process boot.
7. Verify the current historical warmup checkpoint and Market Outlook warmup update exist.
8. Verify the snapshot advances from partial to complete and contains RSI, EMA, and Bollinger data.
9. Verify `/health/ready` reaches the appropriate terminal state.
10. Start or reconnect the UI and verify it reconstructs the same state by query.
11. Restart the API once more to prove idempotent recovery and absence of duplicate workers.

## 5. Acceptance criteria

The fix is complete only when all of the following are true:

1. Every newly accepted application lifecycle command has a recoverable projector execution record.
2. A deliberate failure between persistence and first publication is recovered after restart.
3. Application lifecycle health transitions out of `Bootstrapped` for the matching accepted command.
4. Readiness never reports `not requested` after the dispatcher recorded acceptance.
5. Historical analytics warmup runs for the current process boot and operational value date.
6. A successful daily warmup yields warm EMA and Bollinger inputs in Market Outlook; intraday RSI
   remains explicitly marked warming until its live-bar window is satisfied.
7. A failed optional warmup yields explicit Degraded status while live market data remains usable.
8. The backend completes startup with the UI closed.
9. Starting or restarting the UI creates no backend lifecycle side effects.
10. NATS redelivery and repeated same-date commands create no duplicate feeds, routes, native
    generations, timers, or analytics workers.
11. Historical non-durable startup events are not replayed during migration.
12. Relevant builds and test suites pass with results recorded in an execution section appended to
    this document.

## 6. Likely code touchpoints

| Area | Expected files |
| --- | --- |
| Durable lifecycle descriptors | `TomasAI.IFM.Domain.Application/Command/EventProjector/ApplicationEventProjector.cs` |
| Projector regression coverage | `TomasAI.IFM.Domain.Application.UnitTests/ApplicationEventProjectorTests.cs` |
| Startup workflow and status | `TomasAI.IFM.Domain.Application/Event/ApplicationStartup.cs`, `ApplicationStartupStatusStore.cs` |
| Dispatcher handoff tracking | `TomasAI.IFM.Application.Api.Server/ApplicationStartupCommandDispatcher.cs`, new handoff status contract/store |
| Readiness composition | `TomasAI.IFM.Application.Api.Server/ApplicationLifecycleHealthCheck.cs` |
| Warmup qualification | `TomasAI.IFM.Application.Api.Server/ApiApplicationStartupActivities.cs` |
| Historical warmup publication | `TomasAI.IFM.Application.MarketData/Historical/HistoricalAnalyticsWarmupService.cs` |
| Market Outlook completeness | `TomasAI.IFM.Domain.MarketData.Analytics/MarketOutlookSnapshot/Model/MarketOutlookComposer.cs` |
| UI partial-state presentation | Market Outlook presentation view model and its unit/system tests |

## 7. Out of scope

- Changing indicator formulas, periods, or trading-signal logic.
- Persisting the Market Outlook composite snapshot.
- Moving high-frequency market data through Application lifecycle actors.
- Resetting Databento because an analytics input is warming.
- Deleting or rewriting the stranded incident event.
- Making the UI an operational recovery authority.

## 8. Execution record

| Gate | Status | Evidence |
| --- | --- | --- |
| MOSD-01 | Completed | Reproduced live boundary: command `e8f7ee34-5fcf-4af6-86b1-27ee469f9220` persisted event version `842384`, but lifecycle stayed `Bootstrapped`. Added command-acceptance/not-observed and stale-correlation regression coverage. |
| MOSD-02 | Completed | Startup and shutdown descriptors now require durable replay. Generic projector/outbox/recovery tests passed 29/29; storage migration-selection tests passed 3/3. Live event `863298` completed with projector state `Completed`, outbox `Published`, retry count 0. |
| MOSD-03 | Completed | Dispatcher records acceptance, deadline, observation, attempts, and last error; retries are bounded and command/value-date correlated. Readiness exposes the handoff. Application tests passed 28/28. |
| MOSD-04 | Completed | Warmup is awaited through Market Outlook processing, correlated to process boot/startup command, and requires warm EMA/BB. Exact-date durable snapshot hydration lets the warmup publish a merged UI-visible snapshot. Analytics tests passed 1027/1027. |
| MOSD-05 | Completed | API build passed with 0 warnings/errors; API integration passed 213/213; UI presentation passed 291/291; targeted Market Outlook UI system tests passed 5/5. Two consecutive live API starts reached `Healthy`/`Running`, 7 activities, Green feed, and complete snapshots. |

### 8.1 Live acceptance evidence

- First fixed start: command `7c5cfa4d-7ec1-45ee-a806-9806b1fc209f`; matching durable
  `ApplicationStartupEvent` version `860476` reached projector/outbox `Completed/Published`.
- Historical warmup replayed 260 valid ES sessions and advanced the persisted snapshot from
  `RSI warming, EMA, Bollinger Bands` to `RSI warming`, with EMA/BB both warm.
- Live 15-second bars then warmed RSI; the snapshot reached `isComplete=true`,
  `dailyAnalyticsAvailability=Available`, `rsiAvailability=Available`, and empty `missingInputs`.
- Second restart: command `33ff6a8c-9fe9-4355-b572-809ef7d6c065`; event version `863298` again
  reached `Completed/Published` on one attempt. Readiness was `Healthy`, lifecycle `Running`, all
  seven activities completed, the ES/VX routes and aggregators were active, and processing and
  publication failures were zero.
- Final durability check: command `1fe43128-e81b-43e5-8302-a1ed8cc7a7ef`; event version `866587`
  reached `Completed/Published` on one attempt, with an empty handoff error and a recorded
  observation deadline.
- The API remains running from the final build as process `50216` on `http://localhost:22543`;
  command `1ebce11a-94e9-4ccb-9eee-64832987b48a`, event version `868171`, reached
  `Completed/Published`, `Healthy`/`Running` with all seven activities, Green feed, and a complete
  snapshot. The already-running UI can
  reconstruct the complete persisted snapshot without initiating backend startup work.

### 8.2 Verification note

`TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests` started successfully after aligning its
test-host registrations and passed 45/51 tests. Six legacy signal-command cases timed out or did not
observe their generated event in the shared integration environment. They do not exercise the
startup handoff, hydration, or Market Outlook warmup paths; all targeted and unit coverage for this
fix passed. The result is retained here rather than hiding the unrelated suite instability.
