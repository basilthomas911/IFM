# System Admin Scheduled Tasks Implementation Plan

**Version:** 1.1  
**Status:** Implementation and Windows live business exercise completed; full rollout acceptance awaits the next normal daily cycle and separate Linux business deployment  
**Date:** 2026-10-07  
**Source:** [System Admin Scheduled Tasks Design v1.1](System-Admin-Scheduled-Tasks-Design.md)  
**Accepted engine:** Existing Quartz scheduler, using the current compatible Quartz 3.19.1 baseline  
**Scope:** System Admin scheduling UI and actors, portable SchedulerHost, and futures market close/open workflows

### Operational value-date rollover (owner correction)

The market clock closes the session at 17:00 Eastern but does not advance the operational value date. The close task carries the held date, confirms feed shutdown, and commits every position EOD command. Only then does a committed `PositionsFinalized` run-stage event permit the operational date to advance to the next trading date. Rejection, timeout or partial finalization retains the current date. Persist the completed date in ScyllaDB and restore it before startup; neither 18:00 nor midnight can bypass missing EOD completion. Later maintenance or backup failure does not undo a successfully finalized session. Opening must reject an active-session date different from the held operational date. EOD commands use the existing retry-identity contract: duplicate reservations must re-enter validation and committed-state loading rather than return automatic success. The retry identity retains the position, routing, value date and close boundary unchanged.


## Implementation evidence (2026-10-07)

| Stage | Current evidence | Remaining acceptance |
| --- | --- | --- |
| 1 Portable host | Windows and Linux: 21 runtime/persistent Quartz/containment passes each; final environment/readiness: 14 each; cron regression: 3 each | Linux runtime qualified independently; separately deployed Linux business workflows remain outside this exercise |
| 2 Administrative actors | 19 actor guard passes; earlier real Scylla restart test; live catalog/definition/run command-query round trips and matching Applied revisions | None for the exercised development boundary |
| 3 UI | FlaUI regression passes; actual System Admin screen loaded via live queries; editor initialization and run diagnostics implemented; default height/resizing corrected | Full production UI failure matrix remains rollout qualification |
| 4 Close/positions | Live Quartz close: feed stop before both EOD commitments, rollover confirmed; daily lifecycle 4 passes; earlier date/retry/storage tests retained | Normal 17:01 automatic close and per-column live history readback remain acceptance observations |
| 5 Open | Live opening succeeded; automatic occurrence also succeeded; healthy generation checked; actual daily cron defect fixed and regression tested on both platforms | Next corrected 18:00 daily cron fire; independent seed coverage remains a separate data qualification |
| 6 Deployment | Dedicated scheduler DB/role and artifact manifests; host in development startup; both market definitions enabled at desired/applied revision 2; temporary trigger removed | Linux business deployment not activated |
| 7 Qualification | [Acceptance report](System-Admin-Scheduled-Tasks-Acceptance.md), final solution build and focused platform/domain/FlaUI evidence | Exact-time daily cycle and remaining platform/business acceptance boundaries explicitly recorded |

This records implemented work and actual development activation, with acceptance limits detailed in the linked report. Administrative read models use additive tables on the configured TradeDb Scylla connection, with partition bounds; they never query command memory. Daily position rows retain their first-mark history key when finalized to EOD; Open/Closed rows remain separate. Daily PnL is current realized plus unrealized PnL minus cumulative PnL at the start of the value date, in the existing spread-point units. Existing cash/multiplier accounting remains on its financial path.

## 1 Objective

Implement the design through the existing Quartz scheduler. Operators manage deployed task schedules under **System Admin â†’ Scheduled Tasks**. Command actors own configuration and run decisions; event actors coordinate the scheduler and business workflows; query actors read persisted ScyllaDB projections.

This document specifies implementation work and verification. The implementation has activated the two required development schedules and exercised close/open through Quartz; exact-time daily acceptance remains a separate observation.

### Required defaults

| Task | Days | Eastern time | Quartz cron | Time zone |
| --- | --- | --- | --- | --- |
| FuturesMarketClose | Monday through Friday | 17:01 | `0 1 17 ? * MON-FRI` | `America/New_York` |
| FuturesMarketOpen | Sunday through Thursday | 18:00 | `0 0 18 ? * SUN-THU` | `America/New_York` |

Use Quartz's six-field cron syntax, with an optional year field. Preview, validation, runtime installation, and persisted-trigger reload must use the same parser and time-zone resolver. Support one-time schedules with a one-shot trigger, and optional start/end dates for recurring schedules.

Calendar rules determine eligible sessions and value dates. Sunday evening opening normally belongs to Monday's value date. Holidays skip ineligible work; early closes require a dated override. Host-local time and UTC calendar dates do not determine the trading value date.

## 2 Implementation rules

Follow [Actor Implementation Conventions](Actor-Implementation-Conventions.md), [Actor Event Modeling Conventions](Actor-Event-Modeling-Conventions.md), [Actor Message Types and Delivery Conventions](Actor-Message-Types-and-Delivery-Conventions.md), and [Structured Logging Conventions](Structured-Logging-Conventions.md).

- Each concrete mapped command/query/event has its own extension handler.
- Command computation produces immutable business-named changes. Failure guards precede the single default state-update expression.
- Guards use `command.UpdateFailed(ref errorMsg, reason)`. Event creation explicitly assigns `CommandId = command.CommandId`.
- Each subsequent Record command keeps its own CommandId. Retain the initiating request separately as OperationCommandId.
- State mutation occurs in the state object's event-application switch. Document all extension-handler methods with XML comments.
- Add permanent serialization keys without renumbering existing fields. Preserve stable actor routes and existing event readability.
- Query actors read ScyllaDB. They do not return command memory, Quartz runtime state, or an in-memory read-store substitute.
- Accepted, source committed, projected, runtime applied, process started, and business completed are distinct outcomes.
- Administrative persistence and bounded recovery cannot introduce replay or extra verification into disposable Iron Condor trade-plan processing.
- Keep scheduler operations and EOD enumeration outside option tick handlers.

### Operational invariants

1. Quartz owns task timers on both platforms. Windows Service/systemd manages the host process.
2. Exactly one active runtime owner operates the initial unclustered environment/scheduler identity.
3. Definition revisions fence delayed updates. One integration writer maintains runtime materializations.
4. A scheduled occurrence is identified by schedule ID and intended UTC time; changing revision does not create another occurrence.
5. Close confirms all Databento workers and owned option feeds stopped before issuing EOD commands.
6. An EOD source commitment seals its value date. Later ticks cannot change that dated position.
7. Next-session marks create/update a separate MTM row. An EOD position row does not close the trade.
8. History projection failure is visible and cannot cause an unbounded next-session feed-start wait.
9. Task success requires its defined business completion. Command acceptance or process exit alone is insufficient.
10. Preserve financial order/execution messages when clearing disposable realtime backlog.

## 3 Existing foundation and affected projects

Existing components are reusable foundations, not proof that this plan is complete.

| Area | Existing foundation | Implementation destination |
| --- | --- | --- |
| Scheduling engine | Quartz registration, PostgreSQL engine tables, validation, reconciliation | `Application.ServerManager.SchedulerHost` |
| Task execution | ExternalProcessJob, ScheduledTaskExecutionService, ScheduledProcessRunner, catalog and overlap checks | Existing SchedulerHost; platform interfaces around containment/control |
| Domain administration | System Admin command/query/event patterns and Shared project | `Domain.SystemAdmin/ScheduledTask` and mirrored `Domain.SystemAdmin.Shared/ScheduledTask` |
| Storage | PostgreSQL event sourcing and application storage abstractions | New scheduled-task Scylla contexts/projections and additive schemas in owning Storage/Storage.Shared projects |
| Client API | DatabaseBackup command/query APIs and registration pattern | Scheduled-task APIs in `Application.Api.Nats.Client` and owning API contracts |
| System Admin UI | SystemAdminForm, SystemAdminViewModel, backup service and event consumer | `UI.Net.Views`, `UI.Net.ViewModels`, `UI.Net.Services`, `UI.Net.Models`, `UI.EventConsumer` |
| Task executables | FuturesMarketClose, FuturesMarketOpen, SetClosingPrice projects | Existing `Application.ScheduledTask.*` projects and `ScheduledTask.Shared` |
| Feed lifecycle | Typed stop/start commands, completion events, supervised Databento lifecycle | `Domain.MarketData.Feed`, `Application.MarketData`, owning startup/session components |
| Position lifecycle | StrategyPositionSnapshot, state machine, typed strategy EOD commands, TradeDb projection | `Domain.Trade`, `Domain.Trade.Shared`, `Application.Storage/TradeDb` |
| Deployment | SchedulerHost settings, migrations and Windows installation tooling | SchedulerHost operations/deployment files and existing VS Code development startup |

Retain `ifm_quartz.qrtz_*` engine tables and existing run evidence. Current `ifm_scheduler` definitions need adoption through actors; their existing CRUD cannot remain independent authority.

## 4 Stages and dependencies

| Stage | Deliverable | Dependency | Completion gate |
| --- | --- | --- | --- |
| 1 | Portable existing Quartz host and bounded process ownership | Existing host baseline | Windows regression and actual Linux host execution |
| 2 | Actor contracts, source persistence, Scylla queries and runtime integration | Stage 1 runtime interfaces | Revision-safe configuration and authoritative run admission |
| 3 | Scheduled Tasks UI and client APIs | Stage 2 | FlaUI verifies persisted Pending/Applied/Failed outcomes |
| 4 | Ordered close workflow and dated position sealing | Stage 2; bounded feed stop | Stop-complete precedes EOD; late marks cannot change sealed rows |
| 5 | Open workflow and next-session MTM | Stage 4 lifecycle rules | Actual startup completion and prior EOD preservation |
| 6 | Deployment catalog and existing-definition adoption | Stages 1â€“5 | One writer/host, preserved IDs/settings/history |
| 7 | Full integration and daily development activation | Stage 6 | Stored outcomes, UI evidence, Windows/Linux results |

Stages are **In progress**; the evidence table records implementation and remaining acceptance separately. UI work and close-workflow work can proceed independently once Stage 2 contracts are stable. Linux portability can be tested while domain/UI work proceeds; it remains required before claiming both-platform completion.

## 5 Stage 1 â€” Make the existing Quartz host portable

### Work

1. Establish focused regression coverage for existing cron validation, DoNothing misfires, stable job/trigger identities, overlap, persistence reload and host lifecycle.
2. Retarget portable SchedulerHost/core components from the Windows target to `net10.0`. Isolate Windows dependencies instead of changing the engine.
3. Introduce a platform process-containment interface. Keep WindowsJobObject on Windows; implement owned Linux process-group/cgroup containment with bounded termination. Inspect the existing Databento supervisor's process-group approach without coupling the two services.
4. Bound task startup, cancellation, post-termination exit waits, stdout/stderr draining, and shutdown. Remove unbounded post-termination waits from ScheduledProcessRunner. Preserve exit/uncertain results and output limits.
5. Select Windows Service registration only on Windows. Add Linux systemd service and foreground execution instructions.
6. Isolate SchedulerPipeServer Windows SID/ACL behavior. Add typed actor-message control for adopted schedules; any retained Windows dashboard compatibility must not write definitions independently.
7. Add a shared IANA/Windows time-zone resolver for validation, preview, trigger application and reload. Ensure Linux zone data is available.
8. Retain SchedulerBootstrapService and SchedulerRuntimeService ordering; do not add a second hosted Quartz instance.
9. Retain the local instance lock and implement/verify a PostgreSQL session-held runtime ownership lease. Acquire it before starting triggers; loss of the ownership connection puts Quartz in standby and rejects new launches.
10. Make deployment, run and log roots configurable; validate platform artifacts, case-sensitive Linux paths, executable permissions and environment allowlists.

### Main files

`SchedulerHostApplication.cs`, `SchedulerBootstrapService.cs`, `SchedulerRuntimeService.cs`, `QuartzScheduleReconciler.cs`, `ScheduleValidationService.cs`, `ScheduledProcessRunner.cs`, `WindowsJobObject.cs`, `SchedulerPipeServer.cs`, host project/deployment settings.

### Acceptance

- One host starts and reloads persistent triggers on Windows and on an actual Linux environment.
- Preview and installed next-fire instants match across differing local host zones and both Eastern DST transitions.
- A second host cannot become active for the same identity; ownership loss stops new firing.
- Timeout, cancellation, crash and host shutdown do not leave an untracked task process or unbounded exit/output waits.
- Existing Windows scheduling behavior remains covered by regression tests.

## 6 Stage 2 â€” Add actor ownership and integrate Quartz

### Contracts and handlers

Implement the design's concrete command families:

| Owner | Commands |
| --- | --- |
| Catalog/capabilities | RegisterScheduledTaskProject; RecordScheduledTaskHostCapability |
| Definition | CreateScheduledTask; ChangeScheduledTaskSchedule; EnableScheduledTask; DisableScheduledTask; RemoveScheduledTask; AdmitScheduledTaskRun |
| Installation outcomes | RecordScheduledTaskInstallation; RecordScheduledTaskInstallationFailure |
| Run lifecycle | RequestScheduledTaskRun; RecordScheduledTaskRunAdmission; RecordScheduledTaskRunStarted; CompleteScheduledTaskRun; FailScheduledTaskRun; RecordScheduledTaskRunUncertain |

Require a definition to be disabled before removal and retain its run/audit history.

Implement GetScheduledTaskCatalog, GetScheduledTasksDashboard, GetScheduledTask, PreviewScheduledTaskSchedule, GetScheduledTaskRun, ListScheduledTaskRuns and GetScheduledTaskHostHealth queries.

Create ScheduledTaskCommandActor, ScheduledTaskCatalogCommandActor, ScheduledTaskRunCommandActor, ScheduledTaskQueryActor and ScheduledTaskEventActor with conventional maps, parsing, validation, state, compute and projector ownership. Define public Complete/Fail notifications separately from committed source events and runtime observations.

### Persistence

Persist authoritative catalog, definition, run and later close-stage decisions through existing PostgreSQL event-source infrastructure. Add Scylla read models:

| Table | Read purpose |
| --- | --- |
| `scheduled_task_catalog` | Catalog by environment/host and task key |
| `scheduled_tasks_by_environment` | Bounded environment/host list |
| `scheduled_task_definition` | One definition's desired/applied revisions and timing |
| `scheduled_task_runs_by_schedule` | Paged run history by schedule/month bucket |
| `scheduled_task_run` | One run's outcome, stages and timestamps |
| `scheduled_task_host_health` | Persisted availability and observed drift |

Select the owning Scylla connection/keyspace using repository storage conventions; do not assume existing SQL backup storage is a Scylla read store. Implement partition-bounded queries and additive schema deployment.

### Configuration application

1. Commit the requested definition change with expected-revision validation.
2. Project desired settings to Scylla and publish the configuration-request event.
3. Coordinate a typed host apply request containing definition revision, fingerprint, target, OperationCommandId and deadline.
4. Serialize updates per definition through a proposed IQuartzScheduledTaskRuntime wrapper around existing reconciliation.
5. Inspect the actual Quartz job/trigger, zone, enabled state and fingerprint.
6. Send a concrete Record outcome command; commit/project the result before reporting operation Complete/Fail.
7. Reconcile drift against the latest committed revision. Never dispatch missed business work during reconciliation.

Keep `ifm_scheduler` runtime materializations under the integration writer. Read-model outage shows unavailable/stale/Pending, not command-memory fallback. Older apply requests cannot overwrite newer installed revisions.

### Run admission and launch

- ExternalProcessJob requests a run using ScheduleId, applied revision, ScheduledFireTimeUtc and trigger source.
- The run actor asks the definition actor to admit the occurrence, validating enablement, revision, deployment, host, session eligibility, launch window and conflicting lifecycle work.
- Record admission before launching through existing catalog/dependency/execution services.
- Derive scheduled occurrence identity from ScheduleId plus intended UTC instant. Give manual runs explicit separate identities.
- Retain DisallowConcurrentExecution and persistent overlap checks.
- On restart/redelivery, inspect the same RunId and existing launch receipt. A crash after launch with no reliable receipt is Uncertain; do not automatically relaunch ambiguous work.
- Record skipped, rejected, running, failed, cancelled and uncertain outcomes with clear reasons; only confirmed business completion becomes Succeeded.

The proposed 60-second dispatch tolerance remains an owner-review setting from the design. Implement a separately configurable launch-window check and test it independently of Quartz's misfire threshold. New market schedules use DoNothing misfires.

### Acceptance

Serialization/maps, guard failures, source event CommandIds, revision conflicts, duplicate occurrences, stale installation results and restart queries pass. No direct UI CRUD of runtime tables, duplicate launch from Scylla lag, or false Applied/Succeeded outcome remains.

## 7 Stage 3 â€” Implement System Admin Scheduled Tasks UI

### Work

1. Add the ScheduledTasks function reference through the owning Reference configuration path and map it in SystemAdminForm.
2. Add ScheduledTasksView, view model, service, UI models and command/query clients using the existing backup administration pattern.
3. List deployed catalog tasks and persisted schedules. Add an editor for cron, days/time, time zone, date bounds, one-time date/time, target and runtime bound.
4. Preview the next ten local/UTC occurrences through the backend's shared Quartz validation. Reject five-field POSIX cron with a useful explanation.
5. Wire Add Task, Save, Enable, Disable, Run Now and Refresh. New schedules start disabled; Save preserves requested enablement.
6. Display desired and observed enablement/revision separately. Show Pending until the confirmed installation is projected, then Applied or Failed with reason.
7. Add bounded Run History, Run Details and Logs. Show intended/actual times, lateness, value date, stage, duration and terminal result.
8. Subscribe through SystemAdminUIEventConsumer and reload persisted models on notifications/reconnect. Dispose view listeners and UI reads without altering schedules or active runs.
9. Apply dark trading styles, colon-ended labels and layout checks. Disable duplicate submission while preserving Refresh.

### Acceptance

FlaUI adds a disabled fixture schedule, previews it, saves edits, enables/disables it, observes apply failure and revision conflict, runs a harmless catalog fixture and loads its persisted results. Closing/reopening the view preserves settings and restores data. Disable prevents later admission without cancelling already-admitted work.

## 8 Stage 4 â€” Implement market close and position value-date sealing

### Position model and storage prerequisites

1. Extend StrategyPositionSnapshot and command-owned position state with explicit ValueDate, LatestSealedValueDate, and market observation/close-boundary timestamps. Append serialization fields; define compatibility for older snapshots without inventing a value date from UTC.
2. Update the strategy state-machine compute and state Apply handlers for Iron Condor, Vertical Spread and Futures.
3. Keep the fill-backed opening position immutable with status Open. On first accepted market mark for a value date, create its MTM row; subsequent marks update that daily row.
4. Implement the ended-date EOD transition using deterministic command/row identities. Seal the daily MTM row as EOD; repeated EOD does not create another final row.
5. Include still-open positions without a tick that day using the last known mark and original observation time.
6. Reject market marks for a sealed date, including queued old-generation inputs. Next-session marks cannot reopen that EOD row.
7. Separate current-position, daily MTM/EOD and opening/closing history keys/projections so per-tick updates do not append unlimited duplicate daily rows.
8. Verify daily PnL against the opening basis on the first date and the prior finalized mark on later dates, with existing quantity, multiplier and commission conventions. Preserve cumulative trade PnL separately; document the calculation before wiring projections.

An opposite filled closing trade still creates the final Closed position through the financial execution workflow. Scheduled EOD must not set the established trade to Closed.

### Ordered close workflow

Create FuturesMarketCloseWorkflowCommandActor/EventActor and concrete commands for dated request, stopped-feed confirmation, position outcomes, maintenance outcomes and completion.

1. Resolve the ended session even when the watchdog already cleared ActiveValueDate. Capture close boundary, workflow/run ID and generation.
2. Record close requested and fence resubscription/recovery for that closed session.
3. Send StopMarketDataFeed through the existing typed API. Stop every dataset worker and option-leg owner, fence stopped-generation publications and clear disposable realtime backlog.
4. Observe correlated MarketDataFeedStoppedComplete and matching lifecycle state. Already stopped is idempotently confirmable. A timeout/acknowledgement cannot advance to EOD.
5. Enumerate still-open strategy positions in bounded persisted-query pages and send typed EOD commands carrying ended ValueDate and close boundary.
6. Track source finalization separately from history projection. Source commitment seals the date; projection remains Pending/Failed until confirmed.
7. After source finalization and bounded projection checks, run configured transport maintenance and submit backups with the history boundary recorded explicitly.
8. Report each stage/count/duration and terminal run outcome. Keep the API alive; backup Submitted is separate from backup completion.

Preserve existing transport-purge protection for uncompleted financial messages. Feed-stop failure prevents EOD dispatch. Partial failures identify affected positions; explicit repair targets only unfinalized positions without starting feeds or duplicating EOD.

Extend the existing FuturesMarketClose worker to request and observe this workflow. Align any manual EOD entry point with the same value-date/close-boundary rules instead of a hard-coded UTC end-of-day time.

### Acceptance

Use an emulator-backed Iron Condor plus Futures/Vertical fixtures. With feeds active, prove stop-complete precedes every EOD command; inject late ticks and verify immutable stored EOD rows. Test no-tick positions, watchdog-already-stopped, duplicate close, partial source failure, history write failure and bounded repair. Opening Open and final Closed position rows remain distinct.

## 9 Stage 5 â€” Implement market open and next-session continuation

### Work

1. Resolve calendar eligibility and the new value date for Sunday through Thursday 18:00.
2. Inspect prior close results. Surface unresolved EOD source commitments as position faults; report history-only projection failures separately.
3. Request the existing idempotent application/session startup and indicator seeding. API reachability is a dependency; this task does not start the API process.
4. Confirm matching startup-complete and healthy Databento generation before returning success.
5. Restore only enabled monitoring owners with valid leases. Disposed views cannot regain subscriptions.
6. Create/update the new date's MTM row while retaining prior EOD, opening and closing history.
7. Extend FuturesMarketOpen worker beyond request acceptance to observe bounded business completion and persist its run result.

Missing history projections cannot cause an unbounded feed-start wait. Unsealed prior dates must remain visible faults and cannot be changed by new-session ticks. Manual Run Now evaluates the current session, not a missed historical opening.

### Acceptance

Sunday opening resolves Monday's value date; duplicate starts converge; seeded indicators remain available; new-session marks create one daily MTM row and update daily PnL. Previous EOD is unchanged. Test prior history failure, unresolved source finalization, startup timeout and invalid monitoring-owner restoration.

## 10 Stage 6 â€” Deploy the catalog and adopt existing schedules

### Work

1. Inventory actual `ifm_scheduler.schedule_definition` rows and Quartz triggers. Record IDs, revisions, zone, cron, enabled state, operator changes and linked run history; config defaults are not deployment evidence.
2. Deploy approved manifests for FuturesMarketClose, FuturesMarketOpen and SetClosingPrice. Include project/version, platform artifact/digest, structured arguments, dependencies, completion protocol and runtime bound.
3. Register catalog/capabilities through commands; register no arbitrary assembly path supplied by the UI.
4. Import existing definitions through System Admin commands, preserving IDs and timing.
5. Perform bounded standby cutover: stop legacy CRUD/seeding for adopted definitions, wait for actor projections/application outcomes, reconcile the latest revision, inspect it, then resume the single owner.
6. Route Server Manager changes through the same command API or make adopted settings read-only.
7. Keep existing Quartz tables and run records. Missing deployment defaults may create disabled definitions once; later startup cannot overwrite operator settings.
8. Add SchedulerHost startup/readiness to the development launch script. Use configured API readiness; the current local endpoint is `http://localhost:22543/health/launch-ready`, not the old port-5000 template.
9. Provide Windows Service and Linux systemd service deployment instructions with environment-specific roots, permissions and credentials.
10. Verify applied required defaults, then enable the two market schedules through their command API in the selected development environment.

During cutover, retain a definitions export and prior host artifact/settings. Before any rollback, place Quartz in standby and stop the current settings writer. Restore one explicitly selected ownership mode; never reactivate two writers/hosts or blindly rerun uncertain tasks.

### Acceptance

Operator edits, stable trigger identities and old run evidence survive adoption. New UI edits are the only authoritative mutations for adopted schedules. A host restart cannot seed old settings over them. Only one Windows or Linux host owns a given unclustered scheduler identity.

## 11 Stage 7 â€” Qualify the complete system

### Test matrix

| Layer | Required evidence |
| --- | --- |
| Cron/calendar | Exact defaults, invalid cron, one-time trigger, date bounds, holidays, dated early close, DST, alternate local zone, preview/reload parity |
| Actors/contracts | Maps, serialization compatibility, business guards, state Apply, CommandId/OperationCommandId, expected revisions, completion semantics |
| Storage | Source commit/project/query/restart; bounded history partitions; projection outage shows Pending/stale; no memory read shortcut |
| Quartz/runtime | Edit/disable during firing, old apply revision, duplicate occurrence, misfire DoNothing, overlap, ownership loss, host restart |
| Task process | Windows/Linux launch, bounded cancellation/runtime expiry/output, host crash, containment, ambiguous launch receipt |
| Dependency failures | API/NATS/PostgreSQL/Scylla unavailable, missing artifact/capability, no false Applied/Succeeded |
| Market close | All dataset/leg feeds stopped first, no-tick positions, EOD seal, late tick rejection, duplicate/partial close and projection failure |
| Market open | Sunday value date, actual healthy startup, seeded inputs, valid monitoring ownership, next-date MTM and previous EOD preservation |
| UI | FlaUI catalog/edit/preview/enable/disable/run details, pending/failure states, refresh/reconnect and listener disposal |
| Platform acceptance | Real Windows Quartz close/open cycle and actual Linux host integration, reported independently |

Use isolated emulator trades and controllable clock/calendar fixtures for destructive lifecycle scenarios. Test runs must not close an unrelated user's trade or run unreviewed transport purge/backup work. The final development exercise uses the actual enabled Quartz triggers and observes their actor/feed/position results.

### Evidence and completion

Record test command/result, build/platform, artifact/manifest version, schedule revision, RunId, intended/actual timestamps, feed generation, source commitment, projection outcome and relevant UI capture. Store a concise acceptance report beside this plan with links to detailed artifacts.

A missing Linux environment blocks Linux runtime qualification, not unrelated Windows/domain/UI work. Missing runtime permissions, dependencies or deployment artifacts are recorded against the affected gate. Do not report compilation on Windows as Linux acceptance.

## 12 Logging and performance work

Use the existing Serilog â†’ OTel Collector configuration for SchedulerHost/task boundary logging. Include method name and bounded relevant arguments at selected admission, apply, launch, workflow-stage and failure boundaries.

- Structured fields: TaskKey, ScheduleId, RunId, CommandId, OperationCommandId, HostId, Environment, DefinitionRevision, IntendedFireTimeUtc, ActualStartUtc, ValueDate, Stage, DurationMs and business error reason.
- Logs retain per-run/position identities; metrics use bounded task/stage/environment dimensions.
- Measure apply duration/failure, launch lateness/missed fire, active runs/overlap rejection, stage duration, feed-stop failure, position-finalization counts/failures and drift.
- Bound history pages, log retrieval, stdout/stderr and arguments; exclude credentials and full payload dumps.
- Use async I/O, per-definition serialized engine updates and bounded retries with visible terminal outcomes.
- Keep schedule installation recovery independent of realtime mailbox recovery. Never retry disposable plan projection as a side effect of this feature.

## 13 Definition of done

The plan is complete when:

1. All seven stage gates pass with evidence; any platform limitation is explicit.
2. System Admin can manage deployed tasks using cron or one-time scheduling and display persisted desired/applied settings and runs.
3. Actor conventions, durable source decisions and Scylla query boundaries are verified.
4. Quartz survives restart and applies revision-safe settings under one host/settings owner.
5. Close stops Databento before EOD, seals the ended value date, and reports maintenance/backup outcomes accurately.
6. Open observes real startup completion and resumes a new-date MTM without mutating previous EOD.
7. Both required development schedules are enabled and their actual Quartz cycles are observed.
8. Documentation covers deployment, cutover, bounded failure handling, explicit repair and rollback.

**Review item carried forward:** confirm the design's proposed 60-second dispatch tolerance before selecting its deployed value. This does not delay creating the implementation document or defining/test-driving the configurable boundary.

## Scheduled Tasks Logs and Setup tabs (2026-10-07)

The main Scheduled Tasks control has two tabs in this order: **Logs**, **Setup**. Logs opens by default. Setup retains the existing catalog, schedule editor, preview, enable/disable, Run Now and explicit uncertain-run resolution controls.

### Logs layout and semantics

- Left: one root per persisted task definition, including disabled/removed definitions whose run history remains available. The root displays task name, next scheduled date/time, latest actual start date/time, and status. Times use the task's configured timezone and include the UTC offset; an absent start is shown as absent rather than invented.
- Drill down: task ? year ? month ? day ? occurrence, grouped by the intended scheduled timestamp in that timezone. Each occurrence shows scheduled time, actual start time, and the recorded outcome.
- Status circles: blue = scheduled/requested/admitted; gray = unscheduled/disabled; yellow = running; green = successful run; red = failed/rejected/uncertain. An enabled root shows its latest run outcome, or blue when there is no outcome; a disabled root is gray. Historical occurrence colors preserve their own outcomes even when the definition is disabled.
- Right: read-only standard output from the selected run's retained stdout artifact. The first page loads on selection; **Load more output** continues until the retained end. This is the complete captured stdout, subject to existing capture/retention limits, rather than only the persisted diagnostic tail. Missing/expired artifacts are explicitly identified and the existing tail is shown as a fallback.
- **Load older runs** continues through opaque Scylla paging state; no arbitrary recent-100 cutoff prevents access to retained history. Queries are scoped to environment/host/schedule. Public notifications refresh current outcomes; node expansion and selected occurrence are retained.

### Actor and storage boundaries

`GetScheduledTaskRunHistoryQuery` reads native paged Scylla projections. `GetScheduledTaskOutputQuery` first reads the exact persisted run identity, then reads a bounded UTF-8 page from that run's artifact. The UI supplies identities and byte offsets, never arbitrary filesystem paths. Output pages are at most 64 KiB; UTF-8 characters are not split between pages. Selection changes cancel obsolete reads and prevent stale output from replacing the current run.

The API output reader uses `ScheduledTasks:OutputRoot`, which must point to the owning host's retained TaskRuns directory (local or mounted). Development defaults to the normal workstation TaskRuns directory under the repository. Configure this setting explicitly for a different deployment; remote artifacts require a mounted owning-host directory. Paths escaping the root and symlink/junction traversal are rejected. Full stdout is not copied into financial or scheduled-task source events, and no command-state read shortcuts are introduced.

### Implementation and acceptance

1. Add typed history/output page contracts, query routes, bounded Scylla paging, and retained-output reader registration.
2. Wire service/view-model reads with view-owned cancellation and per-request deadlines.
3. Add Logs/Setup tabs, dated tree, circle legend, stdout paging and preserved Setup functionality.
4. Verify grouping across month/year boundaries, all requested colors, older history continuation, full stdout beyond the diagnostic tail, missing artifacts and UTF-8 boundaries.
5. Rebuild API/UI, exercise real WinForms/FlaUI controls, and deploy through normal development startup. Existing scheduler execution, close/open ordering and source persistence remain unchanged by this UI extension.


## Managed development scheduler lifecycle ? 2026-10-08

The VS Code `IFM: API + UI (Development)` configuration launches Server Manager,
which owns API, UI and SchedulerHost in one development process session. SchedulerHost
starts last (order 30), after API launch readiness and UI startup. Server Manager
checks the existing scheduler dashboard pipe for database availability, Quartz
availability and active scheduling with a 90-second deadline. Failures are reported
in Server Manager and trigger the existing managed-startup rollback.

The VS Code prepare task publishes SchedulerHost into
`.artifacts/scheduled-tasks/development/Host`. Its installed development settings
remain at `scheduler.settings.json`; first-time provisioning still uses
`scripts/ScheduledTasks/Install-IFMDevelopmentScheduler.ps1`. Build/publish does not
replace that settings file or its credentials. The standard development start script
uses the same managed process list and no longer starts an independent scheduler.

Shutdown runs in reverse order: SchedulerHost, UI, API. SchedulerHost receives
`shutdown` on managed standard input, requesting normal host/Quartz shutdown; the
existing bounded process cleanup and development kill-on-close Job Object remain.
Scheduler ownership is verified using the Manager session identity, with compatibility
for an independently started scheduler's legacy session file. The VS Code stop script
also includes scheduler processes in its fallback verification.

Verified: 37 Server Manager unit tests passed. A live managed development launch
started all four owned processes and confirmed scheduler readiness. The actual
`.vscode/Stop-IFMDevelopment.ps1` stopped the entire session without leftovers.
This check does not manually run or backfill the missed 6pm Market Open task.


## Feed-only FuturesMarketOpen ? 2026-10-08

Market Open is the feed-start counterpart to Market Close's feed stop. It queries
the authoritative market session and requires a valid open session, no pending prior
EOD commitment, and matching active/operational value dates. It does not call
StartApplication or repeat application initialization, imports or historical warmup.

The task queries feed runtime/readiness first. A healthy subscribed generation for
the admitted date is already satisfied. A stopped feed is started through the
MarketDataFeed command API and correlated feed-start completion/failure events.
The feed lifecycle resolves authoritative date-specific Databento contract manifests;
the task does not pass historical contracts or perform reference reconciliation.
A conflicting running/unhealthy feed is rejected for recovery rather than silently
starting a duplicate. Completion requires healthy GLBX.MDP3 subscription readiness
within the existing one-minute readiness window. The run records `FeedsStarted`.
API/UI, scheduler and backup services remain running.

Verified: task build succeeded with zero warnings/errors; all 42 Server Manager and
scheduled-task tests passed, including valid evening value-date admission and
rejections for pending EOD, mismatched date, closed session and invalid date.
The updated Market Open executable was published to the development task directory.
No live market-open run was triggered by this verification.
