# Application Startup and Databento Recovery Actor System Design v0.1

**Status:** Proposed implementation-ready design  
**Date:** 2026-09-02  
**Scope:** API bootstrap handoff, actor-owned application startup, UI startup removal, System Console reporting, and continuous Databento recovery  
**Current host:** `TomasAI.IFM.Application.Api.Server`  
**Future host:** Dedicated Aspire-managed Market Data service without changing actor-facing contracts  
**Implementation plan:** `Documents/system/Application-Startup-and-Databento-Recovery-Two-Stage-Implementation-Plan-v0.1.md`  

> **Immediate development scope:** implement startup and continuous recovery first. Production
> `ShutdownApplicationAsync` orchestration, including the scheduled 17:01 market-close workflow,
> is intentionally deferred to a later design revision. API process cancellation must still dispose
> process-local resources safely, but it is not the production market-close design.

## 1. Purpose

This design establishes one authoritative application-startup workflow behind the existing
`StartApplicationAsync` command and one authoritative Databento lifecycle and recovery cycle.

The API server remains continuously running in production. It owns only process bootstrap and
hosting. After API bootstrap is healthy, it posts `StartApplicationCommand`. The Application actor
then coordinates operational startup through typed actor commands, events, and queries.

The UI becomes an observer and command client. Starting or closing the UI must never start, stop,
reset, or otherwise own Databento or another backend operational service.

All startup, feed-health, and recovery messages that might previously have caused a UI
dialog must be sent through the existing System Console notification path. These background
workflows never require a modal operator response.

## 2. Authoritative decisions

1. The API server is the long-running production host and remains running across daily futures
   market closures.
2. API process bootstrap and application operational startup are separate phases.
3. API bootstrap must be healthy before `StartApplicationCommand` is posted.
4. The host-to-actor handoff contains no domain startup logic. Its only responsibility is to post
   the command once bootstrap prerequisites are healthy.
5. `StartApplicationAsync` is a repeatable reconciliation command. It ensures every required
   startup participant is in its desired state without creating duplicate workers, consumers,
   feeds, or subscriptions.
6. All operational startup previously initiated by the UI moves behind the application-startup
   actor workflow.
7. UI construction, control initialization, event subscription, and queries needed to render the
   current state remain UI responsibilities. They are presentation concerns, not application
   startup.
8. Closing the UI only unsubscribes and disposes presentation resources. It never calls the
   market-data stop operation.
9. Production `ShutdownApplicationAsync` orchestration is deferred. This revision does not change
   the scheduled market-close task or define its final participant shutdown ordering.
10. The externally scheduled market-open task calls `StartApplicationAsync` at 17:59 Toronto time.
    Databento is started for the new enabled session.
11. The scheduler is authoritative for production session-boundary commands. The API does not
    replace it with a second internal wall-clock scheduler.
12. An API restart performs the same startup reconciliation after bootstrap health. It restores an
    enabled session but respects a scheduled closed or weekend interval.
13. Databento health is evaluated every minute while the market-data session is enabled.
14. When Databento reaches Failed, the lifecycle enters Resetting and performs a complete reset.
15. If Healthy has not been established after five minutes, another complete reset is attempted.
    Attempts never overlap and continue without an attempt limit until Healthy or API process
    cancellation in this development increment.
16. `Resetting` is Orange. Unexpected `Failed` is Red. `ScheduledStopped` is expected inactivity
    and is not a failure.
17. One actor-owned market-data lifecycle coordinator is the only authority allowed to start,
    stop, reset, replace, or roll the Databento runtime.
18. The Market Outlook processor remains a local single-reader processor. Databento reset never
    clears valid Market Outlook state; new source updates overwrite it after recovery.
19. Startup and recovery code must not call `MessageBox`, `ShowDialog`, set a presentation error
    for display as a modal dialog, or wait for UI interaction.
20. Every startup and recovery transition is published to the System Console and structured server
    log with correlation, value date, participant, attempt, and outcome information.

## 3. Relationship to existing designs

This document specializes and, where they conflict, supersedes the lifecycle rules in:

- `Databento-Market-Data-Service-Resiliency-System-Design-v0.1.md`; and
- `Market-Data-Reliability-Three-Stage-Implementation-Plan-v1.0.md`.

The following newer decisions are binding:

| Earlier rule | Rule established here |
| --- | --- |
| Runtime start at 18:00 | Scheduled start command buffer at 17:59 Toronto time; final scheduled-stop design is deferred |
| Recovery stops after three failed attempts | Recovery repeats every five minutes without an attempt limit |
| Exhausted recovery shuts down Databento during the enabled session | Databento remains Resetting and continues trying until Healthy or process cancellation in this increment |
| API-hosted startup services independently own lifecycle | `StartApplicationAsync` coordinates lifecycle through actors |
| UI may start or stop the feed | UI is observation-only for automatic lifecycle |

The official futures value-date boundary remains 18:00 through 17:00 Toronto/New York time. The
17:59 and 17:01 scheduled commands provide a one-minute operational buffer around that boundary.

The existing Market Outlook local-channel, versionless cache, unconditional partial-write, and
whole-snapshot-read decisions remain unchanged.

## 4. Verified current baseline

The current implementation is divided across three owners.

### 4.1 API bootstrap

`Program.cs` currently:

1. builds and configures the API;
2. creates selected schemas and reference rows;
3. starts the actor runtime before `RunAsync`; and
4. starts all registered hosted services as the web host starts.

There is currently no post-bootstrap call from the API server to `StartApplicationAsync`.

### 4.2 API-hosted operational startup

`FuturesContractRolloverStartupService` currently initializes rollover data and independently
starts or stops `IMarketDataApi` from an API `BackgroundService`. Other hosted services, including
Market Outlook processing, FMP import, Trade Position, and Trade Plan, also start as host side
effects rather than as application-startup participants.

### 4.3 UI-owned operational startup

`IFMAppViewModel.StartApplicationCoreAsync` currently performs or initiates:

- currently traded contract loading;
- automatic reference-data imports;
- market-session loading and transition monitoring;
- Market Outlook hydration/listeners;
- historical analytics warmup;
- futures bar and trade-placement listeners;
- Market Data reset listener startup;
- market-data feed and futures stream startup;
- intraday signal service startup; and
- market-data feed-health monitoring.

The UI shutdown path can stop the feed. This creates dual ownership between the API-hosted rollover
service and the UI.

### 4.4 Existing Application actor behavior

`StartApplicationAsync` already posts `StartApplicationCommand` through NATS. The command creates
`ApplicationStartupEvent`, but `ApplicationEventActor` currently handles that event with a completed
no-op. The UI listener calls an empty `StartupOpenTrades` method. Consequently, command acceptance
does not prove that any operational startup participant is ready.

### 4.5 Existing System Console behavior

`IStatusConsoleWriter` already publishes `StatusConsoleLoggedEvent` notifications. The main UI also
has a separate `LastError -> ShowErrorMessage` path that creates modal errors. Application startup
and recovery currently use both patterns inconsistently.

## 5. Target architecture

```text
API process bootstrap
  configuration / logging / DI / schema prerequisites
  NATS connectivity / actor registration / actor consumers
  Kestrel / bootstrap health
                  |
                  | one StartApplicationCommand after bootstrap is Healthy
                  v
ApplicationLifecycleActor
  durable/reconcilable startup state
  dependency-ordered participant orchestration
  correlated completion/failure summary
                  |
       +----------+-----------+------------------+
       |                      |                  |
       v                      v                  v
MarketDataLifecycleActor   Analytics actors   Other service actors
       |
       v
DatabentoRecoveryRuntime
  single serialized lifecycle executor
  one-minute health loop
  unlimited five-minute reset cycle
       |
       v
C++ or Rust native Databento implementation

All lifecycle transitions
       |
       +--> StatusConsoleLoggedEvent --> System Console listener
       +--> structured API log
       +--> typed status query/event
```

The actors own decisions, state transitions, ordering, and results. Process-local executors and
channels may perform long-running or high-throughput work, but they are constructed idle and can be
activated or stopped only through their owning actor.

## 6. API bootstrap and automatic command handoff

### 6.1 Bootstrap boundary

The following remain API host bootstrap responsibilities because the actor workflow depends on
them:

- configuration, secrets, and logging;
- dependency injection and native backend selection;
- minimum schema compatibility required to load actors;
- NATS connections and actor producers;
- actor registration, actor consumers, and actor supervisor readiness;
- HTTP binding and health endpoints; and
- the System Console event producer.

These are not application participants and cannot be moved behind `StartApplicationAsync` without
creating a dependency cycle.

### 6.2 Bootstrap health must be separate

The existing `/health/ready` includes `MarketDataRuntimeHealthCheck`. Waiting for that combined
result before starting Databento would create this deadlock:

```text
StartApplication waits for Databento health
Databento waits for StartApplication
```

Health registrations must therefore distinguish:

- `bootstrap`: host, storage prerequisites, NATS, actor runtime, and command intake;
- `application`: startup workflow and participant states; and
- `ready`: combined operator-facing state after application startup has begun.

Databento is not part of the bootstrap gate.

### 6.3 Startup dispatcher

Add a minimal `ApplicationStartupCommandDispatcher` hosted bridge. It must:

1. wait for `IHostApplicationLifetime.ApplicationStarted`;
2. wait until all `bootstrap` checks are Healthy;
3. resolve the authoritative operational value date;
4. post `StartApplicationCommand` through `IApplicationCommandApi`/NATS;
5. record the returned command ID;
6. publish command acceptance or rejection to the System Console; and
7. finish without owning any participant lifecycle.

The dispatcher must not call the API's HTTP endpoint from inside the same process. It uses the same
typed NATS API as an external scheduled task.

One automatic dispatch is permitted per API process boot. Duplicate scheduled or operator commands
remain safe because the application actor reconciles desired state.

### 6.4 Boot-time schedule reconciliation

The automatic command is always posted after bootstrap health. Its market-data participant then
uses the authoritative session state:

- inside an enabled session: ensure Databento and required feeds are running;
- inside the scheduled 17:01-17:59 closure: report `ScheduledStopped` and do not start Databento;
- during the weekend closure: report `ScheduledStopped`; and
- after an API restart: rebuild current process-local services from durable/configured authority.

The scheduled 17:59 `StartApplicationAsync` remains the daily production activation. The boot-time
dispatch is restart reconciliation, not a replacement scheduler.

## 7. Application lifecycle actor

### 7.1 Command meaning

`StartApplicationCommand` means:

> Reconcile all registered application startup participants to the desired operational state for
> this value date and publish a correlated aggregate result.

It does not mean merely publish an event, and it does not mean blindly create everything again.

The final meaning, participant ordering, and production scheduling of
`ShutdownApplicationCommand` are deferred. This startup design requires only that host process
cancellation can safely stop process-local resources.

### 7.2 Application states

```text
Bootstrapped
    |
    v
Starting ---- participant failure ----> Degraded or Failed
    |                                      |
    +------------- reconciliation --------+
    v
Running or ScheduledStopped
```

Recommended state values:

- `Bootstrapped`
- `Starting`
- `Running`
- `Degraded`
- `Failed`
- `ScheduledStopped`

The actor stores the current value date, process boot ID, active command ID, workflow correlation
ID, participant results, start time, completion time, and failure summary.

### 7.3 Idempotency and concurrency

- A duplicate command for the same desired state joins or observes the current reconciliation.
- A participant already Healthy returns `AlreadySatisfied` without creating another runtime.
- A participant in Starting is not started again.
- A new value date first fences or stops the previous value-date resources before starting new ones.
- Only one startup reconciliation executes at a time.
- Actor redelivery uses command/event identity and cannot duplicate side effects.
- A process restart reconstructs durable intent and probes actual process-local state before acting.

### 7.4 Participant dependency graph

The initial workflow is:

```text
Phase A: authority
  value date and market-session authority
  required schema/reference readiness

Phase B: reference and contracts
  scheduled reference imports that are operational prerequisites
  futures rollover reconciliation
  authoritative currently traded contracts

Phase C: local processors
  Market Outlook update processor readiness
  operational-health recorder readiness
  other required local processors in idle/ready state

Phase D: market data
  Databento runtime and native backend
  required ES quarterly, VX front, and VX second feeds
  tick aggregation, bar streaming, and required publication routes

Phase E: analytics
  historical analytics warmup/hydration
  realtime RSI, TDI, ITI, EMA, Bollinger, MDI, EOD, and composite signal paths

Phase F: trading operations
  Trade Position, Trade Plan, and other configured operational services

Phase G: qualification
  query participant status
  calculate aggregate Running/Degraded/Failed result
  publish ApplicationStartupComplete or ApplicationStartupFail/Degraded
```

Independent participants within a phase may run concurrently. A phase does not release dependent
participants until required predecessors report completion.

### 7.5 Participant contract

Every startup participant implements the conceptual operations:

```csharp
EnsureStartedAsync(ApplicationStartupContext context, CancellationToken cancellationToken)
GetStatusAsync(ApplicationStartupContext context, CancellationToken cancellationToken)
StopAsync(ApplicationShutdownContext context, CancellationToken cancellationToken)
```

Actor-facing implementations use typed commands, complete/fail events, and queries. Direct service
calls are permitted only inside the participant's owning actor adapter.

Each result includes:

- participant ID and version;
- required or optional classification;
- `Started`, `AlreadySatisfied`, `ScheduledStopped`, `Degraded`, or `Failed` outcome;
- value date and correlation ID;
- start/end timestamps;
- bounded error code and reason; and
- status details suitable for queries and System Console messages.

### 7.6 Completion semantics

Command acceptance and workflow completion are different facts.

- `StartApplicationAsync` initially returns the accepted command ID.
- `ApplicationStartupStartedEvent` identifies the workflow correlation.
- Participant terminal events report individual outcomes.
- `ApplicationStartupCompleteEvent` is emitted only after all required participants are satisfied or
  intentionally scheduled stopped.
- Required participant failure produces `ApplicationStartupFailEvent`.
- Optional participant failure produces a Degraded aggregate result without falsely reporting full
  health.
- A query returns current workflow and participant status if a client missed an event.

## 8. UI ownership removal

### 8.1 Operations that move out of the UI

| Current UI operation | Target owner |
| --- | --- |
| Automatic reference import | Reference/import actor participant |
| Contract rollover/startup selection | Market Data lifecycle actor |
| Historical analytics warmup | Analytics startup participant |
| Start/stop market-data feed | Market Data lifecycle actor |
| Start futures tick/bar streams | Market Data lifecycle actor |
| Start intraday signal services | Analytics startup participants |
| Market-data health decisions | Market Data lifecycle actor/recovery runtime |
| Market-session lifecycle transitions | Application/Market Data actors |
| Reset initiation after failure | Market Data lifecycle actor |
| Stop feed when main UI closes | Removed |

### 8.2 Responsibilities that remain in the UI

- construct and dispose views and view models;
- start and stop presentation event subscriptions;
- query value date, market session, application status, current contracts, current Market Outlook,
  and historical display data;
- render System Console events and current health;
- submit explicit authorized user commands; and
- display read-only state during `ScheduledStopped`, Degraded, or recovery conditions.

The UI initialization becomes:

```text
connect to API/NATS
subscribe to System Console and presentation events
query ApplicationStartup status and current snapshots
render the current state
```

It must contain no automatic backend-start side effects.

### 8.3 UI close behavior

Closing the UI:

- cancels UI-only asynchronous operations;
- unsubscribes UI consumers;
- closes trade views; and
- disposes presentation resources.

It does not send `StopMarketDataFeedCommand`, `ShutdownApplicationCommand`, or reset commands.

## 9. System Console interaction policy

### 9.1 No modal interaction

Startup, health monitoring, and recovery are unattended system workflows. They cannot:

- call WinForms `MessageBox.Show` or a `ShowErrorMessage` extension;
- set `LastError` for a view to turn into a modal dialog;
- wait for an operator acknowledgement; or
- fail because the UI or System Console listener is not currently connected.

User-initiated business validation and destructive confirmation dialogs outside these lifecycle
workflows are not changed by this design.

### 9.2 Message path

Each lifecycle component uses `IStatusConsoleWriter` to publish `StatusConsoleLoggedEvent` through
the existing `Notify.StatusConsoleEvent` route. It also writes the equivalent structured server log.

Minimum fields are:

- timestamp;
- severity/status code;
- source (`ApplicationLifecycle`, `MarketDataFeed`, `DatabentoRecovery`, or participant name);
- value date;
- command and correlation IDs;
- current and previous state;
- recovery attempt number when applicable;
- bounded reason; and
- next action or next retry time.

Routine one-minute Healthy polls update metrics but do not flood the System Console. Messages are
published for state transitions, startup phase changes, reset attempts, recovery, scheduled stop,
and actionable failures.

### 9.3 Listener availability

System Console notification is best effort when no UI is connected; lifecycle work continues. The
actor workflow status query is authoritative for the latest startup result and participant detail,
and structured API logs preserve unattended diagnostics. When the UI connects, it first subscribes
and then queries current status so it does not depend on having observed the original startup event.

## 10. Databento lifecycle and continuous reset cycle

### 10.1 Single owner

`MarketDataLifecycleActor` owns all lifecycle decisions. A process-local
`DatabentoRecoveryRuntime` performs serialized long-running work on behalf of that actor.

The runtime is registered by the API host but starts idle. No hosted service, UI view model,
streaming actor, or analytics component may call `IMarketDataApi.StartAsync`, `StopAsync`, or reset
directly outside this owner.

### 10.2 State model

```text
ScheduledStopped
       |
       | StartApplication during enabled session
       v
    Starting -------- failure --------+
       |                               |
       v                               v
    Healthy ---- health failure ----> Failed
       ^                               |
       |                               v
       +------ successful probe ---- Resetting
                                       |
                          five-minute unsuccessful cycle
                                       |
                                       +----> Resetting again

```

States:

- `ScheduledStopped`: expected close/weekend; native runtime is absent.
- `Starting`: initial session startup is in progress.
- `Healthy`: native runtime, managed workers, and required routes satisfy health policy.
- `Degraded`: optional feed or noncritical stage is impaired; core feeds continue.
- `Failed`: a core failure has been established and reset must begin.
- `Resetting`: complete teardown/restart/probe cycle is active or awaiting the next retry.

### 10.3 Session boundaries

At the scheduled 17:59 Toronto market-open command:

1. reconcile the new value date;
2. load/validate the required current-contract set;
3. perform rollover reconciliation;
4. start a fresh native generation;
5. start required managed aggregation and routes;
6. qualify health;
7. enter Healthy or Resetting; and
8. start the one-minute monitoring timer.

The scheduled 17:01 Toronto market-close command, final shutdown ordering, and resulting production
state transitions are deliberately deferred. They must be added by a later approved design update
before production scheduling is enabled.

### 10.4 Health evaluation

Every minute while enabled, one evaluation reads a coherent snapshot containing:

- the bounded synchronous Databento up/down probe;
- native lifecycle/terminal state available from the selected C++ or Rust backend;
- managed aggregation-worker state;
- required currently traded contract route state;
- last accepted cache update for required feeds;
- processing/publication failure counters; and
- current session state.

Existing freshness thresholds remain inputs to the health policy:

- Green: required live data received within five minutes;
- Yellow: no required live data for more than five and no more than fifteen minutes;
- Failed/Red: required live data absent beyond fifteen minutes during the policy interval, or a
  confirmed native/managed core failure;
- Orange: Resetting, or an optional/noncritical feed failure while core feeds remain usable; and
- Inactive: scheduled stop.

A quiet-feed freshness decision and a confirmed connection/runtime failure remain separately
recorded reasons even when they produce the same recovery decision.

### 10.5 Full reset algorithm

When health becomes Failed:

1. atomically transition to `Resetting`;
2. publish the reason and reset correlation to the System Console;
3. acquire the single lifecycle operation permit;
4. stop and detach all feed-specific publishers and routes;
5. stop managed aggregation workers;
6. capture native terminal information;
7. stop and dispose every native feed and the complete epoch;
8. tolerate and record cleanup failures while continuing best-effort disposal;
9. reload authoritative value date and current contracts;
10. rerun rollover reconciliation;
11. create one new native generation using the configured C++ or Rust backend;
12. start all required feeds, aggregation workers, routes, and publishers;
13. perform the bounded health probe;
14. enter Healthy and publish recovery if qualification succeeds; otherwise
15. remain Resetting, publish the failed attempt, and schedule the next reset five minutes after
    the attempt completes.

There is no maximum attempt count. A slow or failed attempt cannot overlap the next attempt. The
five-minute interval is measured after completion so two native generations cannot race.

### 10.6 Cancellation and race precedence

Lifecycle precedence for this development increment is:

```text
API process cancellation
    > explicit authorized development stop
    > reset/recovery
    > startup reconciliation
    > watchdog poll
```

API process cancellation must stop the recovery loop and dispose process-local resources. Detailed
production shutdown races and scheduled-close precedence are deferred.

### 10.7 Recovery does not depend on the UI

The reset loop continues overnight even when no UI is running. The maximum period without a human
observing it may therefore be overnight, but software monitoring and recovery continue throughout
the enabled session.

## 11. Actor contracts

### 11.1 Existing public commands retained

- `StartApplicationCommand`
- `ResetMarketDataFeedCommand` for authorized on-demand reset

Scheduled tasks and the API boot dispatcher use only the application commands. They do not call
native or managed market-data APIs directly.

### 11.2 Required lifecycle events

- application startup started;
- application participant started/completed/degraded/failed;
- application startup completed/degraded/failed;
- market-data startup started/completed/failed;
- Databento health changed;
- recovery started/attempt failed/completed;
- scheduled Databento stop completed/failed; and
- lifecycle reconciliation ignored/already satisfied.

Events carry command ID, correlation ID, value date, participant/feed identity, timestamps, outcome,
bounded reason, and recovery attempt where applicable.

### 11.3 Required queries

- get current application lifecycle status;
- get current and last application-startup workflow;
- get participant status and failure detail;
- get current Market Data lifecycle status;
- get current Databento health snapshot; and
- get current recovery attempt, start time, last result, and next retry time.

Queries never initiate startup or reset.

## 12. Configuration

Recommended initial configuration:

```json
{
  "ApplicationStartup": {
    "AutoStartAfterBootstrap": true,
    "BootstrapTimeoutSeconds": 120,
    "ParticipantTimeoutSeconds": 120
  },
  "MarketDataRecovery": {
    "HealthPollSeconds": 60,
    "NativeProbeTimeoutSeconds": 1,
    "YellowAfterMinutes": 5,
    "FailedAfterMinutes": 15,
    "ResetRetryMinutes": 5,
    "ScheduledOpenToronto": "17:59"
  }
}
```

The time values document and validate policy; external scheduled tasks remain authoritative for
production start and close delivery. Tests use `TimeProvider` and do not wait in real time.

## 13. Failure policy

- Bootstrap failure prevents command dispatch and leaves the API NotReady with a structured fatal
  startup record.
- A rejected automatic `StartApplicationCommand` is logged and sent to the System Console when
  possible; it is not retried through an independent loop that could duplicate the scheduler.
- A required application participant failure produces a truthful Failed or Degraded application
  result according to participant criticality.
- A Databento startup failure enters Resetting during the enabled session; it does not terminate
  the API or wait for the UI.
- Databento reset failures are isolated and recorded, then retried after five minutes.
- Status Console publication failure cannot terminate startup or recovery; structured logging and
  lifecycle state remain available.
- Market Outlook publication failure cannot initiate a Databento reset.

## 14. Testing and verification requirements

### 14.1 Unit tests

- bootstrap health excludes application-managed Databento readiness;
- dispatcher posts exactly once per process boot after bootstrap becomes Healthy;
- dispatcher never posts before actor command intake is ready;
- startup participant dependency ordering and safe parallel groups;
- required versus optional participant aggregation;
- duplicate startup commands reconcile without duplicate side effects;
- boot reconciliation inside enabled, daily-close, and weekend states;
- one-minute polling and five-minute retry timing with `FakeTimeProvider`;
- unlimited sequential retries without overlap or stack growth;
- successful probe exits Resetting and enters Healthy;
- API process cancellation stops the reset loop and disposes process-local resources;
- UI close does not submit feed-stop or application-shutdown commands; and
- lifecycle errors use System Console output rather than presentation errors.

### 14.2 Actor and NATS integration tests

- boot dispatcher to `StartApplicationCommand` round trip;
- startup workflow participant command/terminal-event correlation;
- command acceptance versus final workflow status;
- delayed, duplicate, and out-of-order participant terminal events;
- current-status query after missing the original notification;
- scheduled open starts all required feed roles;
- confirmed feed failure triggers one serialized full reset;
- repeated failed resets occur every five minutes until recovery;
- API process cancellation interrupts recovery and no later retry restarts Databento;
- System Console receives every state transition without a UI dialog path; and
- UI can connect after startup and reconstruct status by query.

### 14.3 Runtime integration tests

- API reaches bootstrap Healthy before automatic command dispatch;
- API startup during an enabled session restores Databento without launching the UI;
- API startup during 17:01-17:59 leaves Databento absent and application queries available;
- closing and reopening the UI does not change native generation or feed state;
- C++ and Rust backends satisfy identical start, probe, full-reset, and cancellation
  behavior;
- native worker completion with an epoch object still present is detected and reset;
- partial stop failure cannot leave two native generations active;
- ten or more injected failures demonstrate unlimited non-overlapping retry;
- recovery after a later successful attempt restores ES/VX routes and Market Outlook updates; and
- API process cancellation safely disposes the native runtime.

### 14.4 UI/system verification

- the UI can open before, during, or after application startup;
- startup progress appears in the System Console without modal dialogs;
- recovery attempts and next retry time appear in the System Console;
- `ScheduledStopped` is visibly intentional and does not disable read-only navigation;
- current application/market-data state is shown after UI restart;
- no main-form, trade-view, or dialog close stops Databento; and
- an authorized manual reset uses an actor command and reports progress through the System Console.

## 15. Implementation sequence

1. Split bootstrap and application health tags and add the post-bootstrap command dispatcher.
2. Convert `ApplicationEventActor` from a no-op into the lifecycle coordinator with durable,
   queryable participant state.
3. Add participant contracts, correlation, idempotency, status aggregation, and System Console
   reporting.
4. Refactor API-hosted operational services so construction is host-owned but activation is
   actor-owned.
5. Move rollover, feed start/stop, historical warmup, realtime analytics startup, and health
   decisions out of `IFMAppViewModel`.
6. Remove feed shutdown from UI disposal and retain only presentation subscription lifecycle.
7. Implement the serialized Databento state machine and unlimited five-minute reset cycle.
8. Retain `StartApplicationAsync` as the scheduled-open and API-restart reconciliation entry point;
   defer scheduled-close changes.
9. Replace startup/recovery `LastError` dialog publication with System Console messages.
10. Complete unit, actor/NATS integration, runtime, C++/Rust parity, and UI verification suites.

## 16. Acceptance criteria

The design is complete when:

1. API bootstrap reaches Healthy independently of Databento operational health.
2. The API posts one `StartApplicationCommand` after each successful process bootstrap.
3. The same command can be safely issued by the 17:59 scheduled task.
4. `ApplicationStartupComplete` represents terminal results from every required participant.
5. No UI code automatically starts, stops, resets, or monitors Databento for lifecycle decisions.
6. Closing every UI instance leaves Databento and backend operational services unchanged.
7. All operational startup formerly owned by the UI is actor-coordinated.
8. Failed Databento health enters Resetting and causes a complete reset.
9. Failed qualification causes another reset five minutes later without an attempt limit or
    overlap.
10. A later Healthy result ends recovery and restores normal one-minute monitoring.
11. API process cancellation stops recovery and disposes process-local runtime resources safely.
12. Startup and recovery progress is available through System Console events, structured logs, and
    typed status queries.
13. No startup or recovery condition displays or requires a modal UI dialog.
14. Both native C++ and Rust implementations pass the same lifecycle and recovery tests.

## 17. Deferred scope

- Extraction into the dedicated Aspire Market Data host.
- Production `ShutdownApplicationAsync` participant orchestration and terminal status semantics.
- The scheduled 17:01 market-close workflow, ordering, cancellation precedence, and verification.
- Market Data administrative screens beyond current status and authorized reset commands.
- Pager, email, SMS, or external incident-management notification.
- Automatic native backend failover between C++ and Rust in one process.
- Changes to trade-entry permission rules; those remain separate from feed lifecycle.
- Changes to the Market Outlook calculation or channel design.
