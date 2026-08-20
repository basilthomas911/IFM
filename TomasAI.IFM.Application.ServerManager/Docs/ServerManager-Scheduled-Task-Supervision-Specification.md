# IFM Server Manager Scheduled-Task Supervision Specification

Status: **Proposed for review**

Version: **1.7**

Date: **2026-08-20**

Owner: **IFM engineering**

## 1. Purpose

Extend the IFM Server Manager into the operator-facing control surface for durable scheduled-task execution without
placing scheduling responsibility inside `TomasAI.IFM.UI.Net` or `TomasAI.IFM.Application.Api.Server`.

The capability must:

- schedule approved one-shot console applications;
- continue scheduling when the IFM desktop or API is stopped or restarting;
- persist schedules, next-fire state, run history, and audit history across restarts;
- launch each task as an owned operating-system process;
- capture and retain its standard output and standard error independently;
- expose schedule editing, manual execution, cancellation, history, and live logs in Server Manager;
- prevent overlapping, duplicate, stale, or unauthorized execution by default; and
- provide explicit recovery semantics when the scheduler or workstation stops unexpectedly.

This specification also defines how the scheduling capability fits the separate Server Manager modernization that
replaces the obsolete Telemetry, Event, and Predictive Model launch slots with the current API Server and UI.Net
processes.

## 2. Decision summary

1. Use **Quartz.NET** as the scheduling engine instead of implementing cron parsing, trigger persistence, calendars,
   misfire detection, or scheduler thread management locally.
2. Run Quartz in a new headless .NET Worker named
   `TomasAI.IFM.Application.ServerManager.SchedulerHost`.
3. Deploy Scheduler Host as a Windows Service for unattended operation. It must also support console mode for
   development and automated tests.
4. Keep the existing WPF `IFMServerManager` process as the interactive API/UI supervisor, schedule editor, task-run
   monitor, and combined log viewer.
5. Connect the WPF client to Scheduler Host through a versioned, local-only named-pipe contract. The WPF client must
   never read or mutate scheduler database tables directly.
6. Use PostgreSQL as the authoritative Quartz ADO job store. Prefer a dedicated `ifm_scheduler` database with
   separate Quartz-owned and IFM-owned schemas. PostgreSQL also stores schedule metadata, authoritative run state,
   and audit history.
7. Use one stable Quartz job implementation, `ExternalProcessJob`. Persist task-definition identifiers in Quartz job
   data rather than persisting a different CLR job type for every executable.
8. Register executable identities in an administrator-controlled task catalog. Normal schedule editing may select an
   approved executable but may not introduce an arbitrary executable, shell, script, or secret.
9. Forbid overlapping execution in the first release. A later concurrency-policy extension requires a separate
   safety review.
10. Default time-sensitive jobs to the `DoNothing` misfire policy. A missed market-open or market-close operation must
    not run hours later merely because the workstation restarted.
11. Treat stdout/stderr as diagnostics, not as authoritative completion. Process exit code and, where applicable,
    durable domain command/query results determine task success.
12. Treat this as a greenfield scheduler implementation. Dormant Reference-domain schedule contracts are historical
    code artifacts, not existing schedules or runtime state, and are not imported automatically.
13. Treat the WPF Server Manager as a transitional development and paper-trading control surface. A later production
    Aspire migration is expected to replace its generic process-supervision and telemetry responsibilities, but not
    the independent Scheduler Host or the scheduler's durable business policies automatically.

## 3. Why Scheduler Host is separate from the WPF window

The current Server Manager is a WPF/tray application. If Quartz were hosted directly by the window process, closing
the manager, signing out of Windows, or restarting the console to correct a display issue would stop scheduling.
That would make the operator UI the hidden availability boundary for market-close, backup, and future reconciliation
work.

A Windows Service provides unattended ownership, but Windows services run in Session 0 and must not launch the
interactive `TomasAI.IFM.UI.Net` desktop. The responsibilities are therefore split deliberately:

```text
Windows Service: IFM Scheduler Host
    -> Quartz scheduler and persistent trigger store
    -> approved scheduled-task child processes
    -> stdout/stderr files, task-run history, scheduler health
    -> local named-pipe control and event stream

Interactive process: IFM Server Manager
    -> API Server and UI.Net process supervision
    -> API/UI stdout and stderr
    -> schedule editor and task-run dashboard
    -> scheduled-task stdout/stderr viewer through named pipe
```

Stopping UI.Net or the API must not stop Scheduler Host. Closing the Server Manager console must normally hide it to
the tray. An explicit `Exit Server Manager` action may close the interactive process, but it must not stop the Windows
Service. Stopping Scheduler Host is a separate privileged operation.

## 4. Scope

### 4.1 Included

- one-time, simple-interval, cron, and approved calendar-aware schedules;
- explicit schedule timezone and daylight-saving behavior;
- enable, disable, create, edit, delete, and preview operations;
- manual `Run now` and cancellation operations;
- one trigger per task definition in the first release;
- persistent Quartz scheduling and explicit misfire policy;
- dependency checks before child-process launch;
- direct process launch without a shell;
- stdout and stderr capture, live viewing, persistence, filtering, and retention;
- exit-code, timeout, cancellation, forced-termination, and recovery status;
- task-run and schedule-change audit history;
- scheduler health and operator-visible failure status;
- creation of new task definitions, disabled by default; and
- tests for timing, persistence, process ownership, output volume, failures, and restart recovery.

### 4.2 Excluded from the first release

- arbitrary PowerShell, command prompt, batch, or user-entered script execution;
- editing secrets or unrestricted environment variables in the UI;
- multiple active Scheduler Host nodes or Quartz clustering;
- remote administration or browser-based management;
- Windows Task Scheduler synchronization;
- attaching to stdout/stderr of a process that Scheduler Host did not create;
- declaring a job successful solely because it wrote a particular log line;
- silently retrying an ambiguous domain command;
- paper- or live-trading authorization;
- using the historical Reference-domain scheduled-job projection as the active Quartz store; and
- assuming that scheduling a currently broken historical executable makes that task operational.

## 5. Current-state inventory

### 5.1 Server Manager

The current Server Manager hard-codes Telemetry, Event, and Predictive Model child launchers. It captures stdout only,
uses a fixed startup delay, mutates the manager's global current directory, does not retain lifecycle tasks, and kills
children abruptly. Its Development and Production settings files are copied but not loaded.

The modernization must replace these launchers with a generic, race-safe process supervisor for the API and UI. The
same process-execution primitives may be shared with Scheduler Host, but interactive application supervision and
durable schedule ownership remain separate services.

### 5.2 Existing one-shot task applications

| Executable/project | Current state | Initial scheduling disposition |
| --- | --- | --- |
| `TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose` | .NET 10, NATS command APIs, external scheduling expected | First candidate after Development validation |
| `TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen` | .NET 7, stale REST references, unchecked command result, forced self-kill | Blocked until modernized to .NET 10 and current NATS APIs |
| `TomasAI.IFM.Application.ScheduledTask.SetClosingPrice` | .NET 7, stale REST references, fixed 16:00 assumption, forced self-kill | Blocked until messaging, session-calendar, cancellation, and completion semantics are repaired |
| `SceduledTask.TrainFuturesItiPredictiveModel` | .NET 8 with stale REST/assembly references | Blocked until rebuilt and behaviorally specified |
| `TomasAI.ScheduledTasks` | Legacy .NET Core 3.1 placeholder host | Excluded; do not deploy or import |

Every approved task must be brought into the solution, build on the supported .NET runtime, return meaningful exit
codes, propagate cancellation, avoid forced self-termination, and document its durable success criteria before it is
enabled.

### 5.3 Historical schedule-shaped code

The Reference domain contains `ScheduledJobReadModel` plus add/change/remove messages and database projections. These
are dormant historical code artifacts. They show an earlier schedule-shaped model, but they are not evidence of an
operational scheduler, deployed schedules, or schedule data that must be migrated.

The scheduler is a greenfield implementation. Scheduler Host starts with an empty authoritative store, and operators
create new task definitions explicitly; every new definition is disabled by default. The Reference-domain contracts
and projections are not queried, exported, imported, or used as Quartz trigger authority. Their later cleanup can be
handled separately without blocking Server Manager or Scheduler Host implementation.

## 6. Target architecture

```text
IFM Server Manager WPF
    | local named pipe (Windows ACL + versioned messages)
    v
IFM Scheduler Host (.NET Worker / Windows Service)
    |-- Quartz.NET scheduler
    |-- Quartz persistent ADO job store (PostgreSQL/Npgsql)
    |-- IFM schedule metadata, task runs, attempts, and audit tables
    |-- task catalog and definition validator
    |-- dependency/readiness probes
    |-- ExternalProcessJob
    |     |-- ProcessStartInfo with no shell
    |     |-- Windows Job Object ownership
    |     |-- stdout reader -> durable stdout.log -> bounded UI stream
    |     `-- stderr reader -> durable stderr.log -> bounded UI stream
    `-- health, metrics, retention, and recovery services
```

### 6.1 Proposed projects

| Project | Responsibility |
| --- | --- |
| `TomasAI.IFM.Application.ServerManager` | Existing WPF/tray operator UI and interactive API/UI supervision |
| `TomasAI.IFM.Application.ServerManager.Contracts` | Versioned scheduler commands, queries, events, DTOs, validation results, and enums |
| `TomasAI.IFM.Application.ServerManager.SchedulerHost` | Headless Quartz host, persistence, IPC service, process execution, logging, recovery, and Windows Service integration |
| `TomasAI.IFM.Application.ServerManager.UnitTests` | Options, schedule, state-machine, security, retention, and view-model tests |
| `TomasAI.IFM.Application.ServerManager.IntegrationTests` | Real Quartz/PostgreSQL, IPC, helper-process, restart, output, timeout, and cancellation tests |

The contracts assembly must contain data contracts only. It must not reference WPF, Quartz implementation types,
database contexts, or `System.Diagnostics.Process`.

### 6.2 Quartz package policy

SM-S0 selected the stable Quartz.NET `3.19.1` package family after a .NET 10/PostgreSQL persistence proof. Scheduler
Host must reference the exact same version of `Quartz`, `Quartz.Extensions.Hosting`, and
`Quartz.Serialization.SystemTextJson`; never use a floating or prerelease version. Npgsql is pinned to `10.0.3` to
match the repository's existing PostgreSQL provider line. A later upgrade is a reviewed migration with schema,
persistence-restart, release-note, and rollback evidence.

Required capabilities are:

- Microsoft dependency-injection and hosted-service integration;
- cron, simple, one-time, recurrence, and calendar support as approved;
- trigger, job, and scheduler listeners;
- explicit misfire instructions;
- ADO persistent job store using the PostgreSQL Npgsql provider;
- System.Text.Json persistence with `useProperties=true`; and
- orderly scheduler shutdown integrated with the .NET host.

Quartz owns trigger calculation and persistence. IFM owns task authorization, process launch, run identity, output,
completion rules, audit, and operator presentation.

## 7. Scheduler lifecycle

### 7.1 Startup

Scheduler Host must:

1. acquire a single-instance service identity;
2. load base, environment-specific, environment-variable, and command-line configuration in documented precedence;
3. validate storage paths, permissions, task-catalog signatures/hashes, timezones, retention, and concurrency limits;
4. open and migrate the local scheduler database under an exclusive schema-migration lock;
5. start the local named-pipe endpoint;
6. reconcile incomplete prior runs before accepting new executions;
7. start Quartz in standby mode;
8. reconcile approved IFM definitions with Quartz jobs and triggers transactionally;
9. start Quartz scheduling;
10. publish scheduler readiness; and
11. begin retention and health monitoring.

If configuration, database migration, or recovery fails, the service must remain visibly unhealthy and must not fire
tasks. It must not silently fall back to an in-memory Quartz store.

### 7.2 Normal shutdown

Scheduler Host must:

1. stop accepting schedule mutations and manual runs;
2. place Quartz in standby so no new triggers start;
3. publish `Stopping` state;
4. request cancellation of active task processes according to each task's stop policy;
5. wait only for the configured bounded shutdown period;
6. terminate remaining owned process trees and record forced termination;
7. flush stdout, stderr, run state, audit, and metrics;
8. shut down Quartz; and
9. close IPC and database resources.

`WaitForJobsToComplete` must never create an unbounded Windows Service stop. IFM owns the outer deadline.

### 7.3 Unexpected shutdown recovery

Every launched process must be assigned to a Windows Job Object configured to terminate the owned tree when Scheduler
Host loses its ownership handle. This prevents orphaned scheduled-task processes after service failure.

On restart, runs left in `Starting` or `Running` become `Abandoned` unless an explicit reconciliation protocol proves
a durable terminal result. Scheduler Host must not assume failure means the business operation did not happen, and it
must not automatically repeat an ambiguous occurrence.

## 8. Task catalog and editable schedule model

### 8.1 Administrator-controlled task catalog

The catalog defines what may execute. Each entry contains:

| Field | Requirement |
| --- | --- |
| `TaskKey` | Immutable, unique, stable identifier |
| `DisplayName` | Operator-facing name |
| `Description` | Purpose and operational effect |
| `ExecutablePath` | Absolute path or path relative to an approved deployment root |
| `WorkingDirectory` | Explicit directory; never inherited from global current directory |
| `DefaultArguments` | Structured argument list, not a shell command string |
| `AllowedArgumentNames` | Optional allowlist for editable parameters |
| `EnvironmentAllowlist` | Environment-variable names the task may receive |
| `RequiredEnvironment` | Development, Paper, Production, or another explicit identity |
| `SuccessExitCodes` | Normally only `0` |
| `GracefulStopMode` | Standard IFM control pipe, main-window close, or none |
| `RequiresApi` | Whether API readiness is a prerequisite |
| `RequiredEndpoints` | Named readiness probes such as NATS |
| `MaximumRuntime` | Hard execution deadline |
| `RiskClassification` | Maintenance, market lifecycle, backup, trading-sensitive, or other approved class |
| `ManifestVersion` | Version used in every run record |
| `FileHash` | Optional deployment integrity check |

Normal operators can schedule catalog entries but cannot type an arbitrary executable path. Catalog modification is a
privileged deployment action because allowing arbitrary executable editing is equivalent to granting code execution.

### 8.2 Schedule definition

The UI-editable schedule contains:

- immutable schedule definition ID;
- selected `TaskKey` and catalog manifest version;
- unique name and description;
- enabled state;
- schedule kind: one-time, simple interval, cron, or approved recurrence rule;
- schedule expression and human-readable explanation;
- canonical timezone ID;
- optional start and end boundaries;
- optional named holiday/session calendar;
- explicit misfire policy;
- maximum runtime override within catalog limits;
- dependency-failure policy;
- retry policy, when approved;
- output retention policy within administrative limits;
- optimistic-concurrency version;
- created/updated UTC timestamps and Windows identities; and
- next scheduled fire times calculated by Quartz.

V1 supports one active trigger per definition. Multiple schedules for the same executable require distinct schedule
definition IDs and names.

### 8.3 Time and timezone rules

- Persist instants in UTC.
- Require an explicit timezone for operator-entered wall-clock schedules.
- Use `America/New_York` for United States market schedules unless the task specification names another exchange
  timezone.
- Display both local scheduled time and corresponding UTC time.
- Preview at least the next ten fire times before saving.
- Include DST transition examples in validation output when the schedule crosses a transition.
- Do not infer exchange holidays or early closes from the general economic calendar.
- Market-session tasks must use an approved exchange/session calendar or an explicitly reviewed exception.

## 9. Trigger, misfire, overlap, and retry policy

### 9.1 Misfires

A misfire means a scheduled time passed while the scheduler could not run the trigger. It is different from a task
that started and failed.

Supported policies:

| Policy | Behavior | Allowed use |
| --- | --- | --- |
| `DoNothing` | Record the missed occurrence and wait for the next fire time | Default; required for market-sensitive jobs |
| `FireOnceNow` | Run one recovery occurrence and resume the normal schedule | Explicitly approved maintenance/backup work only |

Quartz smart/default behavior must not be relied upon. Every trigger must persist an explicit IFM misfire policy.
`IgnoreMisfirePolicy` is prohibited.

The trigger listener must record misfires even when no task process is launched.

### 9.2 Overlap

V1 forbids overlap for every schedule definition:

- one schedule occurrence may own at most one active process tree;
- a trigger that fires while its definition is active is recorded as `SkippedOverlap`;
- repeated clicks on `Run now` cannot bypass the rule; and
- the UI must show which active run caused the skip.

Quartz non-concurrent execution protection must be combined with an IFM database uniqueness/lease rule. Quartz alone
must not be the only duplicate-launch guard.

### 9.3 Retry

Misfire recovery, process retry, and observation retry are separate concepts.

- The default process retry count is zero.
- A retry policy must name the transient outcomes it covers, maximum attempts, and bounded backoff.
- Each retry receives a new attempt ID while retaining the same logical occurrence ID.
- The stable occurrence ID, run ID, and attempt number are injected into the child environment.
- An ambiguous run is never retried automatically.
- Domain commands issued by a child task must remain independently idempotent; process-level protection cannot prove
  exactly-once business execution across a crash boundary.

## 10. Run identity and state

### 10.1 Identity

Each occurrence has:

- `OccurrenceId`: stable across approved retries;
- `RunId`: identifies one scheduler run envelope;
- `AttemptId`: identifies one operating-system process attempt;
- Quartz fire-instance identity;
- schedule definition and version;
- catalog task and manifest version;
- scheduled fire time in UTC;
- actual start/finish times in UTC; and
- origin: `Scheduled`, `Manual`, `MisfireRecovery`, or `Retry`.

Scheduler Host injects the following non-secret environment variables:

```text
IFM_SCHEDULED_OCCURRENCE_ID
IFM_SCHEDULED_RUN_ID
IFM_SCHEDULED_ATTEMPT_ID
IFM_SCHEDULED_FIRE_UTC
IFM_SCHEDULED_ORIGIN
IFM_ENVIRONMENT
IFM_TASK_CONTROL_PIPE
```

### 10.2 States

| State | Meaning |
| --- | --- |
| `Planned` | Occurrence accepted before launch |
| `BlockedDependency` | Required dependency was not ready; no process launched |
| `SkippedOverlap` | Another run owned the definition |
| `Misfired` | Scheduled instant was missed and recorded by policy |
| `Starting` | Durable run row exists and process creation is in progress |
| `Running` | Process started and PID/process-start identity were recorded |
| `Succeeded` | Process exited with an approved code and any required completion contract passed |
| `Failed` | Process exited unsuccessfully or a required completion contract failed |
| `TimedOut` | Maximum runtime expired |
| `Cancelling` | Cooperative cancellation was requested |
| `Cancelled` | Process stopped after cancellation without forced termination |
| `ForceTerminated` | Scheduler killed the owned process tree |
| `Abandoned` | Scheduler restarted with an incomplete prior run and outcome is uncertain |

State transitions must be durable, validated, and monotonic. A terminal state cannot return to `Running`.

## 11. Child-process execution contract

Scheduler Host must create processes with:

- `UseShellExecute = false`;
- `RedirectStandardOutput = true`;
- `RedirectStandardError = true`;
- `CreateNoWindow = true` for scheduled console tasks;
- explicit `FileName` and `WorkingDirectory`;
- `ProcessStartInfo.ArgumentList` rather than shell-concatenated arguments;
- an allowlisted environment;
- `EnableRaisingEvents = true`; and
- Windows Job Object assignment before treating the run as active.

The process runner must begin draining stdout and stderr immediately and concurrently. It must retain and await the
process-exit task plus both end-of-stream readers. It must not block a Quartz worker thread with synchronous
`WaitForExit()`.

The runner must record PID and process start time so PID reuse cannot be mistaken for ownership. Stopping a run may
target only the exact owned process/job object.

### 11.1 Cooperative cancellation

Modernized IFM tasks should implement a small local control-pipe protocol. Scheduler Host passes a unique pipe name,
and the task translates `Cancel` into its host cancellation token, stops accepting work, drains bounded owned work,
and exits with the documented cancellation code.

For a legacy task without the protocol:

1. mark the run `Cancelling`;
2. apply any approved main-window/console close mechanism;
3. wait the configured grace period; and
4. terminate the Windows Job Object process tree as a fallback.

Every forced termination must be visible in the UI and audit history.

### 11.2 Completion contract

At minimum, success requires an approved exit code. A task specification may additionally require a durable result
query, recovery-operation ID, output manifest, or other typed completion evidence.

Log text matching is not a completion contract. For example, a line containing `backup submitted` cannot replace the
durable DatabaseBackup recovery-operation identity returned by the command API.

## 12. Dependency checks

Each catalog task declares its prerequisites. Before launch, Scheduler Host performs bounded checks such as:

- API `/health/ready` status;
- NATS endpoint reachability;
- required executable/configuration file existence;
- required credential presence without reading or displaying its value;
- environment match;
- required storage destination availability; and
- approved trading/session state where defined by that task's domain contract.

A failed dependency check creates a terminal `BlockedDependency` occurrence. It does not start the child process.
Retry or next-fire behavior follows the explicit schedule policy.

Scheduler Host connects to PostgreSQL directly. It must not depend on API or NATS merely to load schedules, edit
them, display authoritative history, or record a blocked dependency. PostgreSQL is an explicit infrastructure
prerequisite; API and NATS are task-specific dependencies.

## 13. stdout and stderr capture

### 13.1 Required behavior

For every process attempt, create:

```text
%ProgramData%\TomasAI\IFM\ServerManager\TaskRuns\<TaskKey>\<RunId>\
    run.json
    stdout.log
    stderr.log
```

Each displayed line includes:

- UTC timestamp assigned when Scheduler Host receives it;
- task name;
- run and attempt IDs;
- PID;
- stream (`stdout` or `stderr`);
- sequence number within that stream; and
- message text.

Raw output ordering between stdout and stderr cannot be proven because they are separate operating-system streams.
The combined view orders by receive timestamp and clearly retains the originating stream and sequence.

### 13.2 Backpressure and bounds

- Durable file writers must continuously drain both streams so a full child pipe cannot deadlock the task.
- The live UI broadcast is separately bounded and may coalesce refresh notifications, but it must not block file
  capture.
- A disconnected or slow WPF client cannot slow the child process.
- Enforce configurable per-stream byte and line limits.
- When a limit is reached, continue draining and discarding excess bytes, mark output `Truncated`, and publish one
  visible warning.
- Reject or safely truncate individual lines beyond the configured maximum length.
- Keep only a bounded recent-line collection in WPF memory.

Suggested initial limits, subject to load testing:

| Limit | Initial value |
| --- | --- |
| WPF recent combined lines | 5,000 |
| Individual line length | 64 KiB |
| stdout per attempt | 100 MiB |
| stderr per attempt | 100 MiB |
| Successful-run retention | 30 days or newest 100 runs per task |
| Failed/ambiguous-run retention | 180 days or explicit operator archive |

Retention cleanup must never delete logs for an active run. History deletion is audited.

### 13.3 Secret handling

Arguments and environment values marked secret must never be rendered. Logs pass through the shared secret-redaction
policy before persistence and display, but redaction is defense in depth: task applications remain responsible for not
writing secrets. Task-run directories require restrictive Windows ACLs.

## 14. Persistent storage

### 14.1 Database and availability

PostgreSQL is the authoritative scheduler database. Prefer a dedicated database named `ifm_scheduler` so scheduler
retention, migration, restore, and permissions do not share the event-source schema accidentally. Within that
database use separate schemas, for example:

```text
quartz      Quartz-owned QRTZ_* tables
scheduler   IFM schedule metadata, runs, attempts, audit, outbox, and schema history
```

Scheduler Host connects directly through Npgsql using a dedicated least-privilege service identity. It does not
obtain scheduling data through API Server and does not share API process lifetime.

PostgreSQL unavailability is a critical system fault. Scheduler Host must report unhealthy, place Quartz in standby
or allow its persistent store to remain disconnected according to the tested Quartz recovery behavior, and start no
task whose occurrence cannot be recorded durably. After connectivity returns, Quartz applies each trigger's explicit
misfire policy. There is no SQLite, file-definition, or RAMJobStore fallback outside isolated unit tests.

Task stdout/stderr files remain under the service-owned
`%ProgramData%\TomasAI\IFM\ServerManager\TaskRuns` directory. Large log bodies do not belong in PostgreSQL; the
database stores their metadata, paths, sizes, hashes where required, retention state, and terminal disposition.

### 14.2 Ownership and read projections

- Quartz owns its documented `QRTZ_*` tables.
- IFM migrations own task catalog snapshots, schedule metadata, run, attempt, audit, retention, and schema-version
  tables.
- Application code must not modify Quartz tables directly.
- The WPF client accesses data only through Scheduler Host contracts.
- Scheduler Host is the sole writer.

Authoritative schedule editing, trigger reconciliation, active-run ownership, and terminal transitions read from
PostgreSQL because they require current transactional state. A future ScyllaDB projection may serve read-heavy task
history, dashboards, and cross-system reporting. If added, it must be populated asynchronously from a PostgreSQL
outbox and must never become Quartz trigger authority or require a PostgreSQL/ScyllaDB dual write. Scheduler Host can
return current command results directly while clearly identifying any lagging ScyllaDB projection.

### 14.3 Backup and migration

- Apply idempotent, versioned schema migrations before Quartz starts.
- Create an engine-consistent PostgreSQL backup before a destructive schema migration.
- Record migration version, start, result, duration, and failure.
- Provide an operator action to export definitions and audit metadata without secrets.
- Restore requires Scheduler Host to be stopped and must validate schema and task-catalog compatibility before start.

## 15. Local control contract

Use a named pipe with Windows ACLs limited to the service identity and approved local operator group.

Required operations:

### Queries

- scheduler health and version;
- task catalog;
- schedule list and details;
- validation and next-fire preview;
- active runs;
- paged run history;
- run details and output metadata;
- paged/tailing stdout and stderr; and
- schedule and run audit history.

### Commands

- create, update, enable, disable, and delete a schedule;
- run now;
- request cancellation;
- retry an approved failed occurrence;
- export definitions/history;
- invoke retention cleanup; and
- pause/resume scheduling as a privileged global operation.

Every mutating request contains a request ID, caller Windows identity, expected entity version, originated timestamp,
and reason where required. Repeated request IDs return the original result rather than applying the mutation twice.

The named-pipe protocol must be cancellation-aware, length-prefixed, size-limited, and versioned. Never enable unsafe
polymorphic deserialization.

## 16. Server Manager UI

### 16.1 Navigation

Add these destinations to the WPF Server Manager console:

1. `Applications` — API and UI process status and controls;
2. `Scheduled Tasks` — definitions, enabled state, next/previous fire, and active status;
3. `Task Runs` — searchable run and attempt history;
4. `Logs` — combined API, UI, Scheduler Host, and task output with filters; and
5. `Scheduler Health` — store, service, clock, queue, retention, and IPC status.

This is Server Manager UI, not the legacy IFM System Administration `JobScheduler` screen. The legacy screen remains
hidden until a separate decision either removes it or converts it to a read-only link to Server Manager.

### 16.2 Scheduled-task list

Show:

- enabled/disabled state;
- name and approved task identity;
- environment and risk classification;
- schedule explanation and timezone;
- previous scheduled time and outcome;
- next ten fire times on demand;
- current run, duration, and cancellation state;
- misfire and dependency-block indicators; and
- last editor and update time.

Actions are `New`, `Edit`, `Enable`, `Disable`, `Run now`, `Cancel run`, `View history`, and `View logs`. Destructive or
trading-sensitive operations require confirmation and a reason.

### 16.3 Editor

The editor must:

- select only from the approved task catalog;
- show the resolved executable and manifest version read-only;
- provide structured schedule fields plus an advanced cron editor;
- validate cron/recurrence syntax through Scheduler Host;
- require timezone and misfire selection;
- display a plain-language schedule explanation;
- preview the next ten local and UTC occurrences;
- identify DST, holiday-calendar, overlap, and dependency implications;
- prevent timeout/retention values outside catalog policy;
- show optimistic-concurrency conflicts rather than overwriting another edit; and
- save disabled first for every new or high-risk task unless explicit approval permits enablement.

### 16.4 Task-run log viewer

The viewer supports:

- combined, stdout-only, and stderr-only views;
- live follow and historical paging;
- source, task, run, time, severity-text, and stream filters;
- copy and export with redaction;
- visible truncation and dropped-live-update indicators;
- exit code, duration, timeout, and forced-termination summary; and
- navigation from a log line to its task and run.

The UI must not load an entire historical file into memory. It requests pages or tails from a byte/line cursor.

## 17. Configuration shape

The exact options types are defined during implementation, but the intended surface is:

```json
{
  "ServerManager": {
    "Environment": "Development",
    "Scheduler": {
      "Enabled": true,
      "PipeName": "IFM.ServerManager.Scheduler.v1",
      "TaskRunRoot": "C:\\ProgramData\\TomasAI\\IFM\\ServerManager\\TaskRuns",
      "MaximumConcurrentProcesses": 2,
      "ShutdownTimeoutSeconds": 45,
      "MisfireThresholdSeconds": 60
    },
    "TaskCatalog": [
      {
        "TaskKey": "futures-market-close",
        "DisplayName": "Futures Market Close",
        "ExecutablePath": "Tasks\\FuturesMarketClose\\TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose.exe",
        "WorkingDirectory": "Tasks\\FuturesMarketClose",
        "RequiredEnvironment": "Development",
        "SuccessExitCodes": [ 0 ],
        "MaximumRuntimeMinutes": 30,
        "RequiresApi": true,
        "RequiredEndpoints": [ "NATS" ],
        "RiskClassification": "MarketLifecycle"
      }
    ]
  }
}
```

Paths are resolved relative to the Scheduler Host deployment root unless explicitly absolute and allowlisted.
Environment-specific JSON, environment variables, and command-line overrides must use documented precedence.
Secrets are referenced by name and supplied through the service environment or an approved secret provider; they are
not stored in schedule definitions.

The PostgreSQL connection belongs under the normal connection-string hierarchy and must be overridden securely in
deployed environments:

```json
{
  "ConnectionStrings": {
    "SchedulerDbConnection": "Host=localhost;Port=5432;Database=ifm_scheduler;Username=ifm_scheduler"
  }
}
```

Passwords, certificates, and tokens must not be committed to JSON.

## 18. Security and authorization

- Installing, starting, stopping, or reconfiguring Scheduler Host requires administrative authority.
- Named-pipe ACLs restrict access to approved local identities.
- Normal operators may edit schedule timing and enabled state only within catalog policy.
- Catalog editing, executable-path changes, environment changes, and production enablement are privileged.
- No command shell is used.
- Relative path traversal outside approved deployment roots is rejected after canonical path resolution.
- Executable existence, file type, ACL, optional hash/signature, and working directory are validated before save and
  again before every run.
- Arguments use a structured allowlist and `ProcessStartInfo.ArgumentList`.
- The UI never displays secret values.
- Production schedules show an unmistakable environment banner and require stronger confirmation.
- Schedule mutations, manual runs, cancellations, retries, exports, and retention deletions are audited.
- Scheduler control must remain local-only in this milestone.

## 19. Observability

Scheduler Host publishes structured logs and metrics for:

- scheduler/store readiness;
- definitions enabled/disabled;
- next-fire lateness and misfires;
- active/queued/blocked/skipped runs;
- process startup latency and duration;
- outcomes and exit codes;
- timeout, cancellation, forced termination, and abandonment;
- stdout/stderr byte and line counts;
- truncation and UI-stream drops;
- dependency-probe failures;
- retention activity; and
- IPC connections, authorization failures, and request latency.

Metric labels must remain low cardinality. Do not use run ID, PID, raw executable path, arguments, or log text as metric
dimensions.

Scheduled-run trace roots, stdout/stderr correlation, the bounded Server Manager telemetry summary, sampling, and the
future full OTLP backend follow the system-wide tracing design rather than a scheduler-specific tracing protocol.

Initial alerts are visible in the tray and Scheduler Health view. External notification is a later capability.

## 20. Failure policy

| Failure | Required response |
| --- | --- |
| Invalid schedule | Reject save with field-specific errors |
| Missing/unapproved executable | Disable or block definition; never attempt shell fallback |
| Scheduler database unavailable | Scheduler unhealthy; do not use RAM fallback |
| API/NATS unavailable for a dependent task | Record `BlockedDependency`; apply explicit retry/next-fire policy |
| Child start failure | Record `Failed` with exception and no PID |
| Child writes excessive output | Continue draining, truncate persistence at limits, warn visibly |
| Child exceeds timeout | Cancel, wait grace period, terminate owned tree, record `TimedOut`/forced detail |
| Scheduler crashes during a run | Job Object removes process tree; recover run as `Abandoned` |
| UI disconnects | Scheduling and file capture continue; UI resynchronizes through cursors |
| Duplicate schedule command | Return original request result |
| Duplicate Quartz callback | Suppress through durable occurrence uniqueness and record diagnostic |
| Clock/DST change | Quartz recalculates; display resulting fire times and misfire outcome |
| Disk retention limit approached | Warn, run safe cleanup, preserve active/ambiguous evidence |

## 21. Testing requirements

### 21.1 Unit tests

- task-catalog and schedule validation;
- canonical path containment and argument allowlists;
- timezone, DST, cron explanation, and next-fire preview;
- explicit misfire-policy mapping;
- legal and illegal run-state transitions;
- overlap and duplicate-request suppression;
- retry/occurrence identity;
- redaction and log-line bounds;
- retention selection;
- optimistic concurrency; and
- view-model enablement and validation behavior.

### 21.2 Integration tests

Use a purpose-built helper executable capable of writing controlled stdout/stderr, producing large output, sleeping,
handling cancellation, spawning a child, exiting with requested codes, and crashing.

Prove:

- real Quartz persistence through an isolated PostgreSQL test database;
- schedules and next-fire state survive host restart;
- `DoNothing` and `FireOnceNow` misfires behave exactly as configured;
- stdout and stderr are captured concurrently without deadlock;
- combined display retains stream identity;
- slow/disconnected UI clients do not block file capture or child exit;
- per-line and per-file limits truncate visibly while pipes continue draining;
- overlap is suppressed across scheduled and manual triggers;
- timeout and cancellation own the complete process tree;
- Scheduler Host crash leaves no child process;
- incomplete runs recover as `Abandoned` and are not silently retried;
- named-pipe authorization, framing, cancellation, reconnect, and version rejection;
- schedule CRUD updates Quartz and IFM metadata consistently; and
- retention never removes active or protected failure evidence.

### 21.3 System tests

- install/start Scheduler Host in Development console and Windows Service modes;
- connect, disconnect, restart, and upgrade the WPF manager independently;
- edit and preview a disabled schedule;
- execute an approved harmless task manually;
- observe separate stdout and stderr live and from history;
- stop a running task cooperatively and by bounded forced fallback;
- restart Scheduler Host and prove schedule/history recovery;
- run API/UI supervision and scheduled-task output simultaneously;
- prove API/UI shutdown does not stop Scheduler Host;
- prove Server Manager exit does not stop Scheduler Host; and
- prove service stop leaves no process, pipe, database lock, or output writer.

### 21.4 Task adoption tests

Before enabling each real task:

- build it from the solution on .NET 10;
- validate configuration and credentials without exposing values;
- run it against the approved non-production environment;
- prove meaningful success/failure/cancellation exit codes;
- correlate submitted commands and durable terminal results where required;
- prove retry/idempotency behavior;
- measure realistic maximum runtime and output volume;
- verify timezone/session/holiday assumptions; and
- complete an operator-reviewed rollback and disable procedure.

## 22. Implementation and rollout

### Phase SM-S0 — specification and safety baseline

**Status:** Complete. Evidence is recorded in
`ServerManager-SM-S0-Safety-Baseline-and-Persistence-Proof.md`.

- approve this document;
- inventory every candidate executable and confirm that no operational scheduler or schedules require migration;
- decide service identity and Windows operator groups;
- select and pin Quartz packages; and
- prototype Quartz persistent PostgreSQL/Npgsql behavior on .NET 10.

### Phase SM-S1 — generic process and log foundation

**Status:** Complete. Evidence is recorded in
`ServerManager-SM-S1-Process-and-Log-Supervision.md`.

- replace legacy Server Manager launch slots with API/UI definitions;
- implement reusable async process ownership and stdout/stderr capture;
- add bounded combined API/UI/Manager log views; and
- prove graceful API/UI lifecycle and forced-fallback evidence.

### Phase SM-S2 — Scheduler Host and persistence

**Status:** Complete. Evidence is recorded in
`ServerManager-SM-S2-Scheduler-Host-and-Persistence.md`.

- create Scheduler Host and contracts projects;
- add Windows Service and console modes;
- configure the dedicated PostgreSQL database, Quartz schema, IFM scheduler schema, and migrations;
- implement task catalog, run store, state machine, Job Object ownership, and recovery;
- implement local named-pipe queries and health; and
- expose a read-only Scheduled Tasks/Task Runs dashboard.

### Phase SM-S3 — editing and operations

- implement schedule validation, next-fire preview, CRUD, enable/disable, and audit;
- implement manual run, cancellation, timeout, and explicit retry;
- implement live/historical stdout/stderr views; and
- complete security, retention, and failure-injection tests.

### Phase SM-S4 — real task modernization

Adopt tasks one at a time. Start with Futures Market Close only after its Development acceptance passes. Modernize
Futures Market Open, Set Closing Price, and predictive-model training before catalog registration. Every newly
created definition remains disabled until its individual gate passes.

### Phase SM-S5 — unattended operational acceptance

- install Scheduler Host with automatic delayed start and recovery policy;
- exercise restart, workstation reboot, sign-out, DST, missed-trigger, disk-pressure, API-down, and NATS-down cases;
- run the agreed soak window;
- validate backup/restore of scheduler state; and
- obtain operator approval before enabling production schedules.

### Future production transition to Aspire

This is a future architectural checkpoint, not part of phases SM-S0 through SM-S5 and not approval to begin the
system-wide Aspire migration.

When the production Aspire topology is designed and accepted, Aspire is expected to replace the Server Manager's
generic responsibilities for:

- starting, stopping, restarting, and displaying dependency state for API/UI and other application resources;
- aggregating resource health and topology;
- displaying console and structured logs; and
- navigating production-ready metrics and distributed traces through the selected durable observability backend.

The following responsibilities do not disappear merely because Aspire is introduced:

- Quartz scheduling, misfire/overlap/retry rules, and PostgreSQL persistence remain in the headless Scheduler Host;
- schedule validation, editing, authorization, audit, run history, and manual-run controls still require an approved
  operator interface;
- production process ownership belongs to the selected deployment/runtime platform rather than the current WPF
  process; and
- production telemetry requires secured ingestion, durable storage, retention, alerting, capacity validation, and
  runbooks. The in-memory Aspire development dashboard alone is not the production telemetry authority.

The likely end state is to move schedule administration into the main IFM UI or a small dedicated administrative
surface, retain Scheduler Host as an independently operated service, and retire the WPF Server Manager after parity,
security, recovery, and operator-acceptance gates pass. Until then, Server Manager remains useful for development and
paper trading and must not become a prerequisite for Scheduler Host correctness.

## 23. Acceptance criteria

This capability is accepted only when:

- Scheduler Host runs independently of UI.Net, API Server, and the WPF Manager window;
- approved schedules and next-fire state survive restart and reboot;
- every schedule has explicit timezone, misfire, timeout, dependency, and enabled-state policy;
- arbitrary executable and shell launch is impossible through the normal UI;
- overlapping and duplicate launches are suppressed durably;
- stdout and stderr are captured separately, retained, bounded, and viewable live and historically;
- high-volume output cannot deadlock a task or exhaust WPF memory;
- cancellation, timeout, service failure, and shutdown leave no orphan process tree;
- ambiguous crash recovery is visible and never silently rerun;
- all schedule mutations and manual operations are attributable and audited;
- a disabled schedule can be edited while API/NATS are unavailable;
- real tasks are enabled only after their individual modernization and non-production acceptance;
- Scheduler Host service, database, logs, and IPC have approved Windows permissions;
- unit, integration, system, restart, and soak tests pass; and
- operator runbooks cover enable, disable, run now, cancel, failure investigation, service restart, backup, restore,
  and emergency schedule pause.

## 24. Operational rules

1. A schedule being enabled is not proof that its executable is correct.
2. A process starting is not proof that its domain command was accepted.
3. A command being accepted is not proof that the durable operation completed.
4. A log line is never the sole source of truth for completion.
5. A scheduler retry is a new process attempt, not permission to duplicate a business command.
6. Market-sensitive misfires default to skip, not catch-up.
7. New schedules start disabled.
8. The scheduler must remain operable enough to report blocked dependencies when API/NATS are down.
9. No scheduled task receives paper- or live-order authority merely by being registered.
10. Forced termination and abandoned outcomes require operator-visible investigation.

## 25. Related documents

- [SM-S0 safety baseline and PostgreSQL persistence proof](ServerManager-SM-S0-Safety-Baseline-and-Persistence-Proof.md)
- [System-wide telemetry and distributed tracing design](../../Documents/system/System-Wide-Telemetry-and-Distributed-Tracing-Design.md)
- [IFM Aspire actor-system migration overview](../../Documents/system/Aspire%20migration%20overview.md)
- [Server Manager implementation details](ServerManager-Implementation-Details.md)
- [UI terminal-operation tracking and rollout](../../Documents/system/UI-Terminal-Operation-Tracking-and-Rollout.md)
- [IFM operational restoration and trading capability roadmap](../../Documents/system/IFM-Operational-Restoration-and-Trading-Capability-Roadmap.md)
- [Futures Market Open implementation details](../../TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen/Docs/FuturesMarketOpen-Implementation-Details.md)
- [Futures Market Close implementation details](../../TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose/Docs/FuturesMarketClose-Implementation-Details.md)
- [Set Closing Price implementation details](../../TomasAI.IFM.Application.ScheduledTask.SetClosingPrice/Docs/SetClosingPrice-Implementation-Details.md)
- [Quartz.NET documentation](https://www.quartz-scheduler.net/documentation/)
- [Quartz.NET cron-trigger and misfire documentation](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html)
- [Quartz.NET configuration reference](https://www.quartz-scheduler.net/documentation/quartz-3.x/configuration/reference.html)
- [Quartz.NET hosted-service integration](https://www.quartz-scheduler.net/documentation/quartz-3.x/packages/hosted-services-integration.html)

## 26. Revision history

| Version | Date | Change |
| --- | --- | --- |
| 1.7 | 2026-08-20 | Marked SM-S2 complete and linked its Scheduler Host, PostgreSQL/Quartz, catalog/run/attempt recovery, Job Object, pipe-dashboard, and test evidence. |
| 1.6 | 2026-08-20 | Marked SM-S1 complete and linked its API/UI process supervision, graceful/forced shutdown, bounded combined log, and automated-test evidence. |
| 1.5 | 2026-08-20 | Corrected the scheduler baseline to greenfield: there is no operational legacy scheduler or schedule-data export/migration prerequisite; dormant Reference-domain contracts are historical code only. |
| 1.4 | 2026-08-20 | Marked SM-S0 complete and linked its inventory, security/package decisions, PostgreSQL persistence prototype, tests, and SM-S1 entry evidence. |
| 1.3 | 2026-08-20 | Documented Server Manager as a development/paper-trading bridge, the future Aspire replacement boundary, retained Scheduler Host responsibilities, and gated WPF retirement path. |
| 1.2 | 2026-08-20 | Linked scheduled-run telemetry and Server Manager summary behavior to the system-wide tracing design. |
| 1.1 | 2026-08-20 | Selected PostgreSQL/Npgsql as the authoritative Quartz and scheduler-state store, prohibited SQLite/RAM fallback, and documented the optional future ScyllaDB read-projection boundary. |
| 1.0 | 2026-08-20 | Proposed a headless Quartz.NET Scheduler Host, durable scheduling, safe external-process ownership, schedule editing, run history, and bounded stdout/stderr monitoring through the WPF Server Manager. |
