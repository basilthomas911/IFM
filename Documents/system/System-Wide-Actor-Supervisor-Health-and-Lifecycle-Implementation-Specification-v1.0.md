# IFM System-Wide Actor Supervisor Health and Lifecycle Implementation Specification

**Work package:** Supervisor domain, managed actor lifecycle, polling, metrics, and logging
**Status:** Implemented through the approved Supervisor lifecycle, health, durable-history, projector-observability, and Actor Health operational stages; locally executable Stage 9 qualification passed, with production observation gates retained
**Version:** 1.1
**Created:** 2026-09-28
**Owner:** IFM engineering
**Architectural authority:** `System-Wide-Actor-Supervisor-Runtime-Design-v1.0.md`, version 1.12 or later
**Supersedes for this work:** direct API ownership of non-Supervisor actor startup/shutdown and unrestricted Supervisor runtime access from domain actors

## Implementation progress record (2026-09-28)

The four final implementation increments are complete:

1. Supervisor command and event actors now execute only authorized, bounded, generation-fenced lifecycle operations;
   every accepted or rejected operation is retained as immutable audit evidence and event publication is best-effort.
2. Minute-level aggregate health history is retained in memory for immediate range queries and written through a
   bounded asynchronous channel to a compacted seven-day host journal. The polling thread performs no file I/O.
3. Projector health includes the real durable pending/blocked/terminal-failure backlog, expired leases, outbox backlog
   and age, retry count, and worker utilization in addition to recovery readiness.
4. Actor Health exposes hierarchy/detail, polling liveness, process/GC/thread-pool metrics, incidents, lifecycle
   operations, durable history, projector/replay evidence, and bounded time-range refresh through the operational HTTP
   endpoint. Actor mutations remain on the authorized NATS Supervisor command boundary.

Verification completed on 2026-09-28:

- full solution build: succeeded with zero warnings and zero errors;
- production composition-root startup verification: succeeded;
- Supervisor unit tests: 41 passed;
- focused actor runtime/restart/metrics tests: 40 passed;
- UI architecture tests: 366 passed;
- Actor Health rendering system test: passed;
- `ParameterSetActorRuntimeTests`: passed through isolated PostgreSQL, Redis, NATS, CQL, and Kestrel;
- complete Reference integration project: 17 passed;
- full sequential solution matrix: every discovered runnable test passed;
- 30-minute accelerated synthetic soak, runtime counters/trace, and BenchmarkDotNet qualification: passed and retained in `Supervisor-Stage-9-Qualification-2026-09-28.md`;
- automatic lifecycle mutation: disabled by default pending the full trading-session observe-only production gate.

## 1. Purpose

This specification translates the approved Supervisor runtime design into concrete projects, contracts, state machines,
configuration, implementation stages, migrations, verification gates, and acceptance criteria.

The first end state is one Supervisor domain that:

1. starts before every other domain actor and stops after every other domain actor;
2. exclusively owns discovery, construction, registration, infrastructure binding, startup, readiness, shutdown,
   restart, and recycling of all non-Supervisor actors;
3. stores current actor and `ActorThreadId` metrics in one directly readable privileged context;
4. runs one dedicated, narrow, exception-contained metrics polling thread after healthy managed startup;
5. attempts one compiled metrics heartbeat log every 60 seconds;
6. exposes deterministic actor health and bounded incidents without depending on domain actor mailboxes; and
7. provides the controlled foundation for later Supervisor commands, queries, events, and trader-authorized actions.

The implementation must preserve business behavior and NATS actor messaging. It changes lifecycle and observability
ownership; it does not move business-domain state into the Supervisor.

## 2. Verified current state

As of this specification:

1. `ActorRuntimeStartup.StartAsync` resolves `IActorRegistry` and `IActorFactory`, constructs every actor, registers it
   with `IActorSupervisor`, registers producers/consumers, starts all actors concurrently, starts external consumers,
   and sets readiness.
2. All actor types are currently treated as one startup population; Supervisor actors are not implemented or started
   first.
3. `ActorSupervisor.AddActor` registers the actor in `SupervisorRuntimeContext` before actor `StartAsync`.
4. `ActorRuntimeHealthCheck` reports `IActorSupervisor.IsReady`; it does not validate a detailed actor-health tree.
5. Normal web-host startup completes before actor startup begins, so an ordinary hosted polling service would start too
   early unless explicitly gated.
6. Command, query, event, and function contexts expose the concrete `SupervisorRuntimeContext`.
7. Many domain context adapters expose the complete `IActorSupervisor`, including registry, routing, producer,
   consumer, lifecycle, container, and thread-pool capabilities.
8. Actor-owned mailbox metrics and a direct `SupervisorRuntimeContext.CaptureSnapshot` implementation exist, but do not
   yet retain all required capacity, age, rejection, health, restart, and timing evidence.
9. Current transport overload warnings identify actor type and reason but not the exact `ActorThreadId`/subject that was
   rejected.
10. The Actor Health API and UI exist and read runtime snapshots, but current health rollup is intentionally basic.

## 3. Goals

1. Create `TomasAI.IFM.Domain.Supervisor` and `TomasAI.IFM.Domain.Supervisor.Shared`.
2. Implement standard `SupervisorCommandActor`, `SupervisorQueryActor`, and `SupervisorEventActor` components.
3. Give only those actors the privileged `ISupervisorActorContext`.
4. Replace ordinary actor access with actor-scoped `IActorSupervisorObservationContext`.
5. Expose one managed `StartupActorsAsync` and one managed `ShutdownActorsAsync` operation.
6. Move all non-Supervisor actor lifecycle work out of API startup/shutdown.
7. Implement a dedicated 60-second polling thread with minimal dependencies and no escaping recoverable exception.
8. Atomically store its latest immutable metrics snapshot in the privileged Supervisor context before optional work.
9. Log expected, collected, failed, healthy, degraded, critical, and unknown actor counts every cycle.
10. Standardize compiled base actor entry, exit, elapsed-time, and exception logs with full routed actor identity.
11. Complete actor-owned and per-`ActorThreadId` metrics needed for diagnosis and policy.
12. Apply the baseline mailbox saturation, degradation, recovery, and controlled restart policies.
13. Keep all state, memory, cardinality, logging, history, and operation queues bounded.
14. Prove the design through architecture, unit, integration, failure-injection, benchmark, trace, counter, and soak
    evidence before automatic mutation is enabled.
15. Ensure no recoverable exception ever escapes a public Supervisor method, actor-message boundary, polling boundary,
    lifecycle operation, cleanup path, logger, exception logger, or last-resort handler.

## 4. Non-goals

The initial implementation does not:

- add `ActorType.Supervisor`;
- move business-domain data or decisions into Supervisor;
- replace NATS as the standard actor command/query/event transport;
- make Supervisor actors supervise or restart themselves;
- persist raw actor messages, payloads, credentials, or unrestricted exception objects;
- perform a database query, NATS call, actor message, or LLM call from the dedicated polling thread;
- guarantee execution after process termination, fatal CLR failure, operating-system failure, or loss of every log sink;
- enable automatic restart before safe drain, generation fencing, and integration gates pass;
- enable mutable Actor Health UI controls before authorization and audit gates pass; or
- implement the speculative LLM advisory capability.

## 5. Core architecture decisions

### 5.1 Supervisor remains a standard domain actor boundary

The logical Supervisor uses existing actor types:

```text
Command.Supervisor.<Verb>.<EntityId>
Query.Supervisor.<Verb>.<EntityId>
Event.Supervisor.<Verb>.<EntityId>
```

The three actors use normal mailboxes, NATS routing, base logging, actor-owned metrics, failure containment, and shared
worker scheduling. Their privilege derives only from `ISupervisorActorContext`.

### 5.2 Managed population excludes Supervisor actors

The managed actor set is every registered non-Supervisor domain actor. Supervisor actors retain normal logs and metrics
but are monitored through host fallback telemetry. They cannot be targets of Supervisor-issued lifecycle operations.

### 5.3 One normal lifecycle path

The API host bootstraps Supervisor actors and then invokes one managed startup method. On stop it invokes one managed
shutdown method and then stops Supervisor actors. Direct non-Supervisor lifecycle calls outside this path are forbidden.

### 5.4 Current metrics remain in process memory

Raw metrics remain physically owned by each actor. The polling service reads lock-free snapshots and atomically replaces
one immutable aggregate value inside the privileged context. No publication transport or database is required.

### 5.5 Polling is deliberately simpler than policy

The polling thread only reads, stores, counts, and logs metrics. Health-policy evaluation, incidents, history, API/UI,
and control actions consume the stored snapshot outside the polling loop.

## 6. Projects, folders, and dependency direction

```text
TomasAI.IFM.Domain.Supervisor/
  Command/
    Actor/
    Context/
    Extensions/
    Model/
  Query/
    Actor/
    Context/
    Extensions/
    Model/
  Event/
    Actor/
    Context/
    Extensions/
  Health/
    Collection/
    Evaluation/
    Policy/
    Incidents/
    Operations/
    History/
    Model/
  Lifecycle/
  Logging/
  Metrics/

TomasAI.IFM.Domain.Supervisor.Shared/
  Commands/
  Queries/
  Events/
  ReadModels/
  Enums/
  ServiceApi/

TomasAI.IFM.Shared/EventModelActor/
  Contracts/
  Metrics/
  SupervisorRuntime/
  Logging/

TomasAI.IFM.Application.Api.Server/
  SupervisorBootstrap/
  HealthChecks/
  OperationalEndpoints/
```

Dependencies point as follows:

```text
Domain.Supervisor -> Domain.Supervisor.Shared
Domain.Supervisor -> Shared.EventModelActor contracts
API.Server -> Domain.Supervisor composition/service API
Shared.EventModelActor -/-> Domain.Supervisor
ordinary domains -/-> Domain.Supervisor implementation
```

Low-level runtime instrumentation stays in `Shared.EventModelActor`. Supervisor policy, actors, lifecycle orchestration,
and domain logs/metrics stay in `Domain.Supervisor`.

## 7. Capability contracts

### 7.1 Ordinary actor observation context

```csharp
public interface IActorSupervisorObservationContext
{
    ActorMailboxId ActorId { get; }
    IActorMetricsRecorder Metrics { get; }
    IActorFailureRecorder Failures { get; }
    ActorObservationPolicy Policy { get; }
}
```

The implementation is permanently scoped to one `ActorMailboxId`. It validates every supplied `ActorThreadId` matches
that actor type/name. It cannot expose global snapshots, actors, mailboxes, container, worker pool, transports, routes,
readiness, incidents, or lifecycle operations.

### 7.2 Privileged Supervisor context

```csharp
public interface ISupervisorActorContext
{
    ISupervisorManagedActorLifecycle ManagedActors { get; }
    ISupervisorActorMetricsState ActorMetrics { get; }
    ISupervisorHealthManager Health { get; }
    ISupervisorIncidentStore Incidents { get; }
    ISupervisorOperationStore Operations { get; }
    ISupervisorHistoryStore History { get; }
}
```

Only Supervisor actor contexts receive this interface. It returns immutable reads and named operations, never mutable
actors, queues, workers, dictionaries, arbitrary delegates, DI container, or the concrete runtime root.

### 7.3 Managed lifecycle interface

```csharp
public interface ISupervisorManagedActorLifecycle
{
    ValueTask<SupervisorActorsStartupResult> StartupActorsAsync(
        CancellationToken cancellationToken);

    ValueTask<SupervisorActorsShutdownResult> ShutdownActorsAsync(
        CancellationToken cancellationToken);

    ValueTask<SupervisorActorOperationResult> ExecuteAsync(
        SupervisorActorOperationRequest request,
        CancellationToken cancellationToken);
}
```

Every method on `ISupervisorManagedActorLifecycle`, and every other public Supervisor service method, is nonthrowing for
recoverable failures. Invalid input, unavailable dependencies, caller cancellation, timeout, primary failure, rollback
failure, cleanup failure, reply failure, and logging failure are represented in typed results and retained evidence;
they do not cross the caller boundary as exceptions.

`ExecuteAsync` supports only named, validated operations: pause, drain, resume, stop, restart, quarantine, retire, and
recycle. The request includes target, expected generation, operation/incident ID, requester, reason, authorization,
timeout, and cancellation.

### 7.4 Minimal host bootstrap

```csharp
public interface ISupervisorBootstrap
{
    ValueTask<ISupervisorActorContext> StartSupervisorAsync(
        CancellationToken cancellationToken);

    ValueTask StopSupervisorAsync(CancellationToken cancellationToken);
}
```

The bootstrap may construct/start/stop only Supervisor actors and the low-level process runtime. Emergency teardown is
separate, explicit, audited, and unavailable as an ordinary lifecycle path.

### 7.5 Runtime-only instrumentation

```csharp
internal interface IActorRuntimeInstrumentation
{
    void RegisterActor(IActor actor, IActorMetricsStore metrics);
    void RemoveActor(IActor actor);
    void RegisterWorker(ISupervisorWorkerMetricsSource worker);
    void RemoveWorker(ISupervisorWorkerMetricsSource worker);
    void RecordRuntimeFailure(ActorRuntimeFailureObservation observation);
}
```

This interface is inaccessible to domain assemblies. The lifecycle coordinator invokes internal registration primitives
through a narrow runtime adapter rather than exposing `IActorSupervisor`.

## 8. Identity and immutable models

### 8.1 Actor identity

Every operational model carries structured:

```text
ActorType
ActorName
EntityId
Verb (when message-specific)
MailboxGeneration
RuntimeGeneration
HostInstanceId
```

`ActorMailboxId` is `{ActorType, ActorName}`. `ActorThreadId` is `{ActorType, ActorName, EntityId}`. No operational API
uses a formatted identity string as its only identity.

### 8.2 Aggregate metrics snapshot

```csharp
public sealed record SupervisorActorMetricsSnapshot(
    long Revision,
    DateTime ObservedUtc,
    long StartedTimestamp,
    long CompletedTimestamp,
    TimeSpan Elapsed,
    DateTime NextPollUtc,
    int ExpectedActors,
    int CollectedActors,
    int FailedActors,
    int HealthyActors,
    int DegradedActors,
    int CriticalActors,
    int UnknownActors,
    int EntityMailboxes,
    long QueueDepth,
    long Rejected,
    long Failed,
    SupervisorSnapshotQuality Quality,
    IReadOnlyList<SupervisorActorMetrics> Actors);
```

Required invariant:

```text
HealthyActors + DegradedActors + CriticalActors + UnknownActors
    == CollectedActors + FailedActors
```

A failed actor read increments `FailedActors` and `UnknownActors`; it never increments healthy.

### 8.3 Actor and mailbox snapshots

`SupervisorActorMetrics` contains actor ID/domain/type, lifecycle/generation, health/reasons, aggregate counts, queue
depth, restart history, latest failure, snapshot quality, and a bounded mailbox list.

Each mailbox snapshot contains thread ID, depth/capacity/peak/oldest age, accepted/dequeued/succeeded/handled-failed/
escaped-failed/cancelled/rejected totals, rejections by reason, first/latest rejection, current verb/stage/elapsed,
queue-wait and handler-duration summaries, worker assignment, trace/correlation evidence, lifecycle, health, incident,
restart, generation, and revision.

All collections are immutable and bounded. Payloads and live exception objects are excluded.

## 9. Supervisor managed lifecycle state machines

### 9.1 Process lifecycle

```text
NotStarted
  -> BootstrappingSupervisor
  -> SupervisorRunning
  -> StartingManagedActors
  -> ManagedActorsReady
  -> StoppingManagedActors
  -> SupervisorRunning
  -> StoppingSupervisor
  -> Stopped

Any startup stage -> RollingBack -> SupervisorRunning with failed operation evidence
Any shutdown stage -> PartiallyStopped or EmergencyTeardown
```

Transitions are serialized. Repeated concurrent calls join or return the active deterministic operation result.

### 9.2 Actor lifecycle

```text
Discovered -> Constructed -> Registered -> Bound -> Starting -> Running
Running -> Pausing -> Paused -> Resuming -> Running
Running -> Draining -> Stopped
Running/Paused -> Quarantined
Faulted/Quarantined -> Restarting/Recycling -> Registered(next generation) -> Running
```

No old generation may admit, execute, publish, or commit after its fence.

### 9.3 Startup transaction

`StartupActorsAsync` performs:

1. acquire the process lifecycle gate;
2. set readiness false and keep external intake closed;
3. discover non-Supervisor descriptors;
4. construct actor instances internally;
5. register actor and metrics before start;
6. bind producers, consumers, routes, projectors, and delivery policy;
7. start actors with bounded concurrency;
8. validate expected/registered/started/running/required sets;
9. validate required producer, projector, replay/recovery, and dependency readiness;
10. start external Core and JetStream consumers;
11. revalidate and set readiness true;
12. start the dedicated metrics poller;
13. store/log its immediate initial snapshot;
14. complete the operation.

Failure retains the primary stage/exception, closes intake, stops the poller if started, and rolls back generation-owned
resources in reverse order. Cleanup failures are linked, never substituted.

### 9.4 Shutdown transaction

`ShutdownActorsAsync` performs:

1. acquire the lifecycle gate and set readiness false;
2. signal the polling thread and join its active cycle within the configured deadline;
3. close Core/JetStream intake;
4. close actor admission;
5. drain accepted messages;
6. allow handlers to reach safe commit boundaries;
7. flush required projector, durable, operation, and history evidence;
8. stop managed actors in reverse dependency order;
9. remove routes, consumers, producers, projectors, mailboxes, metrics bindings, and registrations;
10. store deterministic clean/partial/timed-out/cancelled result;
11. return control so the host stops Supervisor actors last.

### 9.5 Recycle transaction

Recycle closes admission, drains, fences the generation, removes generation-owned bindings, reconstructs from the
registered descriptor, registers/binds/starts/validates the next generation, and reopens admission. It is not an
untracked stop/start pair.

### 9.6 Nonthrowing authoritative boundary

The Supervisor is the last in-process authoritative control boundary. All public Supervisor entry points therefore use
a non-overridable final containment wrapper around their complete body, including argument validation, gate acquisition,
dependency calls, cancellation, timeout, result construction, audit, logging, reply, cleanup, and `finally` work.

The rule applies to:

- bootstrap start/stop and emergency teardown;
- managed startup/shutdown and every individual lifecycle operation;
- Supervisor command/query/event handlers and replies;
- health evaluation, incidents, history, and operation stores;
- polling thread start/stop/status and thread entry point;
- Actor Health query/API service and fallback reads; and
- all Supervisor logging/metrics adapters.

Recoverable outcomes are returned as one of:

```text
Succeeded
Rejected
Cancelled
TimedOut
Failed
PartiallyCompleted
EmergencyFallback
```

Cancellation is data at this boundary, not an escaping `OperationCanceledException`. Invalid arguments return
`Rejected`; unavailable dependencies return `Failed` or `EmergencyFallback`. Primary and secondary failures remain
separate. The last-resort boundary uses a minimal preconstructed emergency result if normal result construction fails,
updates host-level atomics, attempts the exception log, and returns without invoking another Supervisor actor.

Nonthrowing does not mean silent. Every failed outcome carries an operation/failure ID when allocation permits, stage,
bounded safe reason, timestamps, target/generation, primary/secondary relationship, cleanup status, and evidence quality.
If normal evidence storage/logging fails, host fallback atomics expose that loss.

The design cannot contain process termination, stack exhaustion, corrupted runtime state, hardware/OS failure, or an
out-of-memory condition that prevents the CLR from executing the boundary. If the authoritative core cannot preserve
its invariants after a recoverable failure, it sets readiness false, stops new control mutations, retains read-only
fallback state, and requests host/process recovery. It does not continue as an apparently healthy authority.

### 9.7 Supervisor authority health invariant

Supervisor authority health is separate from managed actor health. Its states are:

```text
Available
AvailableWithDegradedCapability
HostRecoveryRequired
```

A managed actor failure never makes Supervisor unavailable. Failure of history, UI, NATS notifications, policy,
logging, or one lifecycle operation degrades only that capability while core metrics storage, read-only fallback, and
operation containment remain available. `HostRecoveryRequired` means the process must be recovered; it is never
reported as healthy and never permits further mutable operations. Supervisor actors themselves are observed by host
fallback checks rather than self-supervision.

## 10. Dedicated actor-metrics polling service

### 10.1 Contract and ownership

```csharp
public interface ISupervisorActorMetricsPollingService
{
    SupervisorPollingServiceState State { get; }
    SupervisorPollingServiceStatus CaptureStatus();
    void Start();
    bool Stop(TimeSpan timeout);
}
```

Only managed startup calls `Start`; only managed shutdown calls `Stop`. Calls are idempotent and serialized. The service
owns no actor and sends no actor messages.

### 10.2 Thread construction

The implementation creates one `Thread` with:

```text
Name = IFM.Supervisor.ActorMetricsPoller
IsBackground = true
Priority = Normal
Culture/CurrentCulture = invariant operational defaults where required
```

It uses a stop signal (`ManualResetEventSlim` or equivalent) and monotonic deadlines. It does not use `Task.Run`,
ThreadPool work, `System.Threading.Timer`, `PeriodicTimer`, async continuations, or one task per actor.

### 10.3 Poll loop pseudocode

```text
ThreadMain:
  record thread started
  deadline = monotonic now
  while stop not requested:
    RunCycleWithFinalContainment()
    deadline += 60 seconds
    if deadline <= now:
      record overrun
      deadline = now + 60 seconds
    wait(stop signal, deadline - now)
  record final heartbeat and thread stopped
```

```text
RunCycleWithFinalContainment:
  initialize bounded cycle builder
  try:
    managedActors = context.ManagedActorReferences.Current
    for actor in stable managedActors:
      try:
        snapshot = actor.Metrics.CaptureSnapshot()
        builder.Add(snapshot)
      catch recoverable exception:
        builder.AddUnavailable(actor)
        ExceptionLog.ActorSnapshotFailed(...)
    builder.Add(runtime/worker/poller counters)
    snapshot = builder.BuildImmutable()
  catch recoverable exception:
    snapshot = builder.BuildPartialOrMinimalHeartbeat()
    ExceptionLog.PollCycleFailed(...)
  finally:
    context.ActorMetrics.Store(snapshot)       // atomic reference replacement
    TryHeartbeatLog(snapshot)                  // compiled Information log
    TryOptionalAdvisory(snapshot)              // future no-op by default
```

The `Store` operation precedes normal logging and optional work.

### 10.4 Storage contract

```csharp
public interface ISupervisorActorMetricsState
{
    SupervisorActorMetricsSnapshot Latest { get; }
    SupervisorActorMetricsSnapshot Previous { get; }
    void Store(SupervisorActorMetricsSnapshot snapshot);
    bool TryGetActor(ActorMailboxId actorId, out SupervisorActorMetrics actor);
}
```

`Store` performs one atomic reference exchange after validating snapshot invariants. Readers never lock the polling
thread. A rejected invalid snapshot increments a host fallback counter and preserves the last good value.

### 10.5 Mandatory heartbeat

Every cycle attempts one source-generated Information event:

```text
SupervisorActorMetricsPollCompleted
```

Required structured properties:

```text
Revision, ObservedUtc, ElapsedMilliseconds, NextPollUtc, PollerThreadId,
ExpectedActors, CollectedActors, FailedActors,
HealthyActors, DegradedActors, CriticalActors, UnknownActors,
EntityMailboxes, QueueDepth, Rejected, Failed,
SnapshotQuality, PreviousSnapshotAgeSeconds, ExceptionFallbackUsed
```

Zero-actor, partial, failed, and stale cycles still attempt a summary.

### 10.6 Exception-log fallback

```csharp
public interface ISupervisorExceptionLog
{
    void ActorSnapshotFailed(ActorSnapshotFailure failure);
    void PollCycleFailed(PollCycleFailure failure);
    void PollLoggerFailed(PollLoggerFailure failure);
}
```

The implementation is bounded and independent of Supervisor actor mailboxes. Normal-logger failure triggers this sink
and increments `ifm.supervisor.logging.dropped`. Failure of both sinks is contained and reflected in atomic host fallback
counters. No recoverable exception escapes the thread entry point.

### 10.7 Poller liveness

Host-readable atomics include thread state/ID, started/stopped UTC, last cycle start/completion, next deadline, revision,
heartbeat age, cycles completed/partial/failed, overruns, actor-read failures, logger failures, and exception-log
failures. `/health/actors` uses these even if Supervisor query actors cannot run.

### 10.8 Explicit limits of the guarantee

The poller guarantees containment and a log attempt while the process and dedicated thread are schedulable. It cannot
guarantee operation after process termination, fatal runtime failure, hardware/OS failure, severe OOM, storage
exhaustion, or simultaneous failure of every log destination.

## 11. Actor-owned metrics implementation

### 11.1 Store contract

```csharp
public interface IActorMetricsStore
{
    ActorMailboxId ActorId { get; }
    ActorMetricsSnapshot CaptureSnapshot();
    bool TryGetMailboxSnapshot(
        ActorThreadId threadId,
        out ActorMailboxMetricsSnapshot? snapshot);
}
```

Each actor owns one store and a bounded `ConcurrentDictionary<ActorThreadId, ActorMailboxMetrics>`. Structural changes
publish an immutable entry-reference array. Hot-path queue objects retain direct metric-entry references.

### 11.2 Required transitions

| Runtime transition | Metric update |
| --- | --- |
| Actor registered | Identity, policy, lifecycle generation |
| Actor starting/running | State and timestamp |
| Mailbox created | Add bounded `ActorThreadId` entry with capacity |
| Message accepted | Accepted, depth, last accepted, oldest timestamp |
| Mailbox scheduled | Scheduled timestamp and ready state |
| Message dequeued | Dequeued, depth, queue wait, oldest recomputation |
| Handler entered | Current verb/stage/start/worker |
| Handler completed | Succeeded, handler/end-to-end duration, clear current |
| Handler failed | Handled/escaped counters and bounded failure |
| Handler cancelled | Cancellation counter/reason |
| Admission rejected | Rejected by reason, first/latest time, incident ID |
| Mailbox limit | Saturation first time and warning limiter state |
| Mailbox retired | Roll up and remove only healthy eligible entry |
| Lifecycle operation | State, operation ID, generation, outcome |

Updates use `Interlocked`, `Volatile`, bounded histograms, and stable references. No per-message Supervisor event,
serialization, database call, NATS call, or global lock is permitted.

### 11.3 Bounds

- healthy idle mailboxes retained per actor: 1,024;
- active/failed/incident mailboxes: retained until resolved and rolled up;
- latest failure message: 2,048 characters;
- latest failure stack evidence: 8,192 characters in failure history only;
- verb: 128 characters;
- histograms: fixed buckets;
- incident history: 10,000 or 24 hours in memory;
- history: seven days of minute rollups and 30 days of incident summaries.

## 12. System-wide actor logging implementation

### 12.1 Base events

One shared source-generated logging class defines:

| Event ID | Name | Level |
| ---: | --- | --- |
| 4100 | `ActorMessageEntry` | Information |
| 4101 | `ActorMessageExit` | Information |
| 4102 | `ActorMessageException` | Error |
| 4103 | `ActorMessageSecondaryException` | Error |
| 4104 | `ActorMessageWarning` | Warning |

Every processed message emits exactly one entry and one exit. Exception records are additional.

### 12.2 Required fields

All base records include:

```text
ActorType, ActorName, EntityId, Verb,
ActorKind, MessageType, MailboxGeneration, RuntimeGeneration,
TrafficClass, DeliveryClass, TraceId, SpanId, CorrelationId
```

Exit additionally includes outcome, terminal stage, processing elapsed milliseconds, queue wait, delivery/reply outcome,
commit-boundary state, and Supervisor failure ID.

### 12.3 Timing

The non-overridable base boundary captures `Stopwatch.GetTimestamp()` and uses `Stopwatch.GetElapsedTime`. Processing
elapsed covers parse through cleanup. Queue wait is separate. Numeric fields remain numeric.

### 12.4 Implementation rules

- use `[LoggerMessage]` partial methods; precompiled `LoggerMessage.Define` only when generation cannot be used;
- no interpolated templates, anonymous property bags, payload serialization, or identity `ToString()` calls;
- emit exit from the outer `finally` exactly once;
- preserve primary and secondary failures separately;
- domain logs inherit actor scope and use separate stable event ranges;
- asynchronous sinks are bounded and cannot block actors indefinitely;
- sink loss is measured and cannot alter business outcomes.

## 13. Supervisor-specific logs and metrics

### 13.1 Logs

Required compiled Supervisor events include managed startup/shutdown stage/terminal records, actor child-operation
records, polling thread start/stop, poll completed/failed/overrun, health transition, incident transition, operation
requested/completed/failed, history failure, and deferred notification.

Successful polling emits one summary per minute, not one record per actor. Actor snapshot exceptions receive Error
evidence because otherwise the health summary would hide the cause.

### 13.2 Metrics

Initial instruments:

```text
ifm.supervisor.poll.duration
ifm.supervisor.poll.completed
ifm.supervisor.poll.partial
ifm.supervisor.poll.failed
ifm.supervisor.poll.overruns
ifm.supervisor.poll.actor_failures
ifm.supervisor.poll.heartbeat_age
ifm.supervisor.snapshot.quality
ifm.supervisor.incidents.active
ifm.supervisor.incidents.transitions
ifm.supervisor.operations.duration
ifm.supervisor.operations.outcomes
ifm.supervisor.restarts
ifm.supervisor.history.failures
ifm.supervisor.notifications.deferred
ifm.supervisor.logging.dropped
```

Allowed tags are bounded enums such as outcome, stage, health, severity, quality, operation, automatic/manual, and actor
type. Actor IDs, entity IDs, incidents, operations, subjects, and traces never become metric dimensions.

## 14. Health policy

### 14.1 Baseline states

```text
Healthy -> Elevated -> CriticalPressure -> Degraded -> Restarting -> Recovering -> Healthy
                                                         \-> Critical
```

Actor snapshots expose `Healthy`, `Degraded`, `Critical`, or `Unknown`. The poller only counts these states. Policy
evaluation happens outside the poll loop.

### 14.2 Defaults

| Policy | Default |
| --- | ---: |
| Elevated depth | 75% for 30 seconds |
| Critical depth | 90% for 30 seconds |
| Limit trigger | Capacity or `mailbox_limit` rejection |
| Limit warning | Immediate, then no more than once per mailbox/minute |
| Degrade | Saturation active for 5 minutes |
| Controlled restart request | Saturation active for 15 minutes |
| Saturation recovery | Below 90%, no rejection, progress for 60 seconds |
| Health recovery | Below 75%, no rejection, progress for 2 minutes |
| No-progress warning | Nonempty without dequeue completion for 60 seconds |
| Handler warning | One handler active for 60 seconds |
| Repeated-restart critical | 2/hour or 4/24 hours; diagnostic only |

Repeated restart thresholds do not disable safe recovery. There is no daily restart quota.

### 14.3 Rollup

Mailbox health rolls to actor, actor to domain, and domain to host by most severe current state. Every rollup retains
causal IDs/counts. Supervisor actors appear in a separate host-observed Supervisor section and are not self-managed.

### 14.4 Automatic-action gate

Automatic restart remains disabled until exception containment, admission closure, safe drain, commit-boundary handling,
generation fencing, reconstruction, rollback, and integration tests all pass. Before that gate, the system records a red
restart-required incident without performing unsafe mutation.

## 15. Supervisor messages and handlers

### 15.1 Queries

```text
GetSupervisorHealthSnapshotQuery
GetSupervisorActorDetailQuery
GetSupervisorMailboxDetailQuery
GetSupervisorIncidentHistoryQuery
GetSupervisorOperationQuery
GetSupervisorPollingStatusQuery
```

Queries read immutable current state and bounded repositories. They never trigger polling or execute lifecycle mutation.

### 15.2 Commands

```text
AcknowledgeSupervisorIncidentCommand
CaptureSupervisorDiagnosticsCommand
PauseSupervisorMailboxCommand
ResumeSupervisorMailboxCommand
RestartSupervisorMailboxCommand
QuarantineSupervisorMailboxCommand
RecycleSupervisorActorCommand
```

Command handlers authorize, validate expected generation, create an operation, invoke the managed coordinator, and
return operation identity/status. Long-running work is not performed by holding a client request open indefinitely.

### 15.3 Events

```text
SupervisorHealthDegradedEvent
SupervisorHealthCriticalEvent
SupervisorHealthRecoveredEvent
SupervisorOperationRequestedEvent
SupervisorOperationCompletedEvent
SupervisorOperationFailedEvent
```

Routine metric polls are not events. Events represent meaningful transitions or auditable operations.

## 16. Configuration and validation

Proposed configuration:

```json
{
  Supervisor: {
    ManagedLifecycle: {
      StartupTimeout: 00:05:00,
      ShutdownTimeout: 00:02:00,
      DrainTimeout: 00:01:00,
      MaximumStartupConcurrency: 8,
      MaximumShutdownConcurrency: 8,
      AutomaticMutationEnabled: false
    },
    ActorMetricsPolling: {
      Enabled: true,
      Interval: 00:01:00,
      StopTimeout: 00:00:10,
      ThreadName: IFM.Supervisor.ActorMetricsPoller,
      ThreadPriority: Normal
    },
    HealthPolicy: {
      ElevatedUtilization: 0.75,
      CriticalUtilization: 0.90,
      PressurePersistence: 00:00:30,
      DegradedAfter: 00:05:00,
      RestartAfter: 00:15:00,
      SaturationRecovery: 00:01:00,
      HealthRecovery: 00:02:00,
      WarningInterval: 00:01:00
    },
    Retention: {
      HealthyIdleMailboxesPerActor: 1024,
      MaximumIncidents: 10000,
      IncidentMemoryAge: 1.00:00:00,
      MinuteRollupDays: 7,
      IncidentSummaryDays: 30
    },
    LlmAdvisory: {
      Enabled: false
    }
  }
}
```

Validation fails startup for nonpositive intervals/timeouts/concurrency, invalid percentage ordering, degradation not
before restart, recovery thresholds at/above entry thresholds, unsupported thread priority, or automatic mutation
without the release gate. The 60-second polling interval is fixed for the first release; actor-specific overrides are
not implemented initially.

## 17. Actor Health API and UI

`/api/actor-health` moves behind an application query service that first attempts the Supervisor Query actor and falls
back to the atomically stored snapshot if the query actor is unavailable. The fallback is read-only and performs no
polling or lifecycle operation.

The view displays:

- polling-thread liveness, last/next poll, age, revision, elapsed, and quality;
- expected/collected/failed actors;
- healthy/degraded/critical/unknown counts;
- domain and actor hierarchy;
- per-actor `ActorThreadId` metrics;
- active incidents and restart history;
- operation state and failure evidence;
- separate host-observed Supervisor actor status.

Automatic UI refresh defaults to 60 seconds. Manual refresh reads the latest stored value and never starts a poll.

Mutable controls remain hidden until authorization/audit and operation-specific safety gates pass.

## 18. Privacy, security, and authorization

1. Read-only operational health follows the approved application operational-access policy.
2. Mutable commands require separate authorization, authenticated requester, supplied reason, target/generation, and
   immutable audit evidence.
3. Metrics/logs exclude payloads, credentials, account secrets, unrestricted entity values, and raw exception objects.
4. Entity IDs use approved redaction/masking where sensitive.
5. Logs/traces may contain bounded high-cardinality IDs; metrics may not.
6. Diagnostic capture is bounded, explicit, authorized, and cannot capture message payloads by default.

## 19. Optional future LLM advisory seam

The current release provides only the disabled interface/no-op implementation:

```csharp
public interface ISupervisorHealthLlmAdvisorySink
{
    void Observe(SupervisorHealthAdvisoryObservation observation);
}
```

The poller calls it only after storing the snapshot and attempting the mandatory heartbeat. The method is constant-time,
nonblocking, nonthrowing, and returns no task/result/acknowledgement. The future implementation may use a capacity-one
latest-wins handoff. No retry, backpressure, durable queue, NATS, actor message, callback, or health consequence is
allowed. LLM integration is explicitly outside the implementation scope of this specification.

## 20. Staged implementation

Each stage is independently buildable and has an exit gate. Later stages do not claim completion until all prior gates
remain green.

### Stage 0: Baseline and inventory

Work:

- capture current solution build/test baseline;
- inventory every `IActorSupervisor`, `SupervisorRuntimeContext`, `SupervisorRuntime`, `Children`, `ThreadState`,
  `Container`, `ThreadPool`, actor startup/shutdown, producer/consumer/router mutation, and Actor Health access;
- record current startup/shutdown logs and readiness behavior;
- measure current actor-message logging allocation/latency and actor metrics snapshot time;
- record representative actor and mailbox counts during trading.

Evidence:

- inventory document or checked-in generated report;
- baseline test report;
- `dotnet-counters`, `dotnet-trace`, allocation, lock-contention, CPU, and latency artifacts;
- no behavior change.

Exit gate: every authority use is classified as runtime instrumentation, Supervisor bootstrap, managed lifecycle,
ordinary observation, NATS messaging, operational query, or removable legacy access.

### Stage 1: Supervisor projects and contracts

Work:

- create Supervisor and Supervisor.Shared projects/folders;
- define identities, enums, results, operations, read models, messages, and interfaces;
- add project references with dependency tests;
- register no-op/read-only implementations where needed;
- create Supervisor command/query/event actors using standard base classes;
- add privileged contexts and DI restriction.

Exit gate:

- Supervisor actors start as ordinary actors in focused tests;
- only their contexts resolve `ISupervisorActorContext`;
- ordinary domains cannot reference Supervisor implementation;
- no mutable behavior enabled.

### Stage 2: Supervisor-first bootstrap and managed lifecycle

Work:

- implement `ISupervisorBootstrap`;
- implement serialized `StartupActorsAsync`/`ShutdownActorsAsync`;
- move discovery, construction, registration, producer/consumer/router/projector binding and rollback into coordinator;
- start Supervisor actors first and managed actors second;
- validate initial health before intake/readiness;
- stop managed actors first and Supervisor actors last;
- reduce API startup/shutdown to bootstrap plus one managed method call.

Exit gate:

- startup/shutdown integration tests prove exact ordering and rollback;
- every recoverable failure at every startup/shutdown stage returns a typed result and no exception escapes;
- expected/registered/started/running counts agree;
- intake remains closed on any startup failure;
- no API code directly manages non-Supervisor actors;
- existing integration suites retain business behavior.

### Stage 3: Bulletproof polling core

Work:

- implement atomic metrics state and immutable snapshot models;
- implement dedicated named background thread and monotonic 60-second loop;
- implement sequential stable actor traversal using deterministic fake sources first;
- implement per-actor/per-cycle/logger exception boundaries;
- implement exception-log fallback and host liveness counters;
- implement mandatory compiled heartbeat;
- gate start after healthy managed startup and stop before managed shutdown;
- add disabled no-op LLM advisory seam only.

Exit gate:

- thread runs independently of ThreadPool starvation tests;
- every injected recoverable exception is contained and next heartbeat occurs;
- one failed actor does not prevent remaining collection;
- stored snapshot precedes logger/advisory calls;
- normal and fallback logger failure cannot terminate thread;
- stop/join is bounded and deterministic;
- no database, NATS, actor-message, async, or policy dependency exists in the polling assembly path.

### Stage 4: Restricted context migration

Work:

- implement actor-scoped `IActorSupervisorObservationContext`;
- migrate base and specialized contexts;
- remove concrete `SupervisorRuntimeContext` properties;
- remove `IActorSupervisor` from domain context adapters;
- replace cross-actor needs with NATS actor producer/client contracts;
- keep instrumentation and bootstrap interfaces internal/restricted.

Exit gate:

- architecture tests forbid prohibited references;
- spoofed other-actor metric/failure recording is rejected;
- complete domain actor test suite passes through NATS;
- no compatibility property exposes broader authority.

### Stage 5: System-wide base actor logging

Work:

- create source-generated base logging methods/event IDs;
- instrument non-overridable outer boundaries for command/query/event/realtime/function and remaining actor kinds;
- include full actor identity and monotonic elapsed time;
- standardize primary/secondary exceptions and final outcomes;
- migrate inconsistent base log calls;
- retain domain-specific compiled logging.

Exit gate:

- every actor kind produces exactly one entry/exit in all terminal paths;
- full identity and elapsed fields are present;
- exceptions cannot suppress exit;
- no payload leakage;
- allocation/latency and sink-pressure gates pass.

The Stage 5 base policy includes startup-compiled immutable actor-type and exact-route suppression sets. Event and
Realtime successful messages suppress routine base Information entry/exit by default. Command and Query messages log
both records by default and may be selectively suppressed through exact {ActorType, ActorName, Verb} entries in
SuppressedRoutes. Failures and cancellations retain entry, exit, elapsed time, and exception evidence; metrics,
traces, health, warnings, degradation, restarts, and recovery are unaffected. Invalid, incomplete, or duplicate
configuration fails API startup before actor construction. Logging elapsed time has its own valid monotonic timestamp
and is never derived from the optional metric timer or its disabled zero sentinel.

Stage 5 verification additionally proves exact-match behavior, failure/cancellation retention, unchanged metric
recording, startup validation, and valid elapsed logging while the duration metric is disabled.

### Stage 6: Complete actor/thread metrics

Work:

- add capacity, peak, oldest age, reasoned rejection, current stage, worker, timing, health, incident, and restart fields;
- retain direct metric-entry references in queues;
- instrument all runtime transitions;
- implement bounded histograms/history;
- adapt every actor to final polling snapshot contract;
- enrich heartbeat counts and Actor Health details.

Exit gate:

- capacity rejection identifies exact `ActorThreadId` and reason;
- hot-path benchmarks remain within approved budgets;
- polling snapshot invariant always holds;
- bounded memory remains stable under mailbox churn and history outage.

### Stage 7: Health evaluation and incidents

Work:

- implement baseline state machines, warning limiter, timestamps, hysteresis, rollups, incident store, recovery, history;
- consume stored snapshots outside polling thread;
- expose Supervisor queries and API fallback;
- update Actor Health UI.

Exit gate:

- five-minute degradation and fifteen-minute restart-required transitions use original monotonic incident time;
- causes are visible by actor/thread;
- UI/query/policy failures cannot affect polling heartbeat;
- actor/domain/host rollups preserve causal evidence.

### Stage 8: Controlled lifecycle operations

Work:

- complete pause/drain/resume/quarantine/restart/recycle operations;
- implement safe commit boundary and generation fencing;
- add authorization/audit and trader acknowledgement/diagnostics;
- use identical coordinator for automatic and manual operations;
- retain automatic mutation disabled until qualification.

Exit gate:

- no accepted required work is silently lost;
- stale generations cannot commit/publish;
- operation failure is deterministic and visible;
- repeated restart is critical evidence but does not impose a daily recovery quota.

### Stage 9: Qualification and rollout

Work:

- full solution and integration verification;
- synthetic and live-like burst/pressure/recovery scenarios;
- dedicated-thread, logger, disk/sink, policy, UI, NATS, history, and lifecycle fault injection;
- BenchmarkDotNet and before/after runtime profiles;
- 30-minute synthetic soak and full trading-session soak;
- observe-only deployment before automatic mutation.

Exit gate: all acceptance criteria in section 24 pass and evidence is reviewed.

## 21. Migration and deletion map

| Existing surface | Target action |
| --- | --- |
| `ActorRuntimeStartup` non-Supervisor orchestration | Move into managed lifecycle; retain only compatibility wrapper temporarily, then delete |
| API direct `IActorSupervisor` startup/shutdown | Replace with `ISupervisorBootstrap` and managed methods |
| Context `SupervisorRuntimeContext SupervisorRuntime` | Replace with actor-scoped observation context |
| Domain context `IActorSupervisor` exposure | Remove; use NATS client/producer or scoped observation |
| Direct API `CaptureSnapshot` | Replace with Supervisor query plus atomic-state fallback |
| Generic transport overload warning | Add exact actor/thread/verb/reason evidence and rate limiting |
| Existing actor metrics fields | Migrate additively, then remove superseded snapshot types after all consumers move |
| Existing Actor Health UI models | Version/migrate to new read models and polling status |

Compatibility wrappers may exist only within an explicit migration stage and must be removed before final acceptance.

## 22. Verification matrix

### 22.1 Unit tests

- state transitions and invalid transitions;
- startup/shutdown idempotency and concurrency;
- snapshot invariants and atomic replacement;
- per-actor exception isolation;
- warning rate limiter and hysteresis;
- generation validation/fencing;
- compiled logging event fields/outcomes;
- metric bounds and retirement;
- redaction and cardinality policy.

### 22.2 Integration tests

- Supervisor-first startup and last shutdown;
- full managed actor registration/binding/start/readiness;
- rollback at every startup stage;
- shutdown timeout and partial drain;
- Core and JetStream intake sequencing;
- exact mailbox overload attribution;
- actor restart/recycle with queued and executing work;
- health API/query fallback while Supervisor mailbox is saturated;
- API and UI production-like startup.

### 22.3 Poller fault-injection tests

- every actor source throws independently;
- actor list changes between cycles;
- snapshot invariant construction failure;
- normal logger throws;
- exception logger throws;
- both loggers throw;
- policy evaluator/query/UI/history/NATS unavailable;
- ThreadPool saturated;
- polling cycle overruns;
- stop requested during actor read and log attempt;
- last good snapshot remains readable;
- next heartbeat occurs after every recoverable fault.

### 22.4 Performance evidence

- BenchmarkDotNet for actor metric updates/snapshots and compiled logs;
- allocation profiles with logging disabled/enabled;
- `dotnet-counters` for CPU, GC, allocation, ThreadPool, exceptions;
- `dotnet-trace` for contention, pauses, poller scheduling and duration;
- mailbox/request latency histograms before/after;
- polling CPU/duration/memory by actor/mailbox cardinality;
- async logging sink throughput and loss behavior.

### 22.5 Architecture tests

- only Supervisor project resolves privileged context;
- ordinary contexts expose only scoped observation;
- API cannot manage non-Supervisor actors directly;
- domain projects cannot access container/thread pool/core supervisor;
- polling path contains no database/NATS/actor-client dependencies;
- high-cardinality IDs are absent from metric tags;
- source-generated logging is used for base/Supervisor hot paths.

### 22.6 Critical Supervisor startup/shutdown and nonthrowing tests

The lifecycle suite uses a deterministic fault injector at every boundary before, during, and after side effects. Each
case asserts that the public Supervisor call completes with a typed result, retains primary/secondary evidence, leaves
readiness/intake/lifecycle state deterministic, and never throws a recoverable exception to its caller.

Startup injection points include:

- lifecycle gate acquisition and duplicate/concurrent calls;
- registry enumeration and descriptor validation;
- actor factory resolution/construction for each actor position;
- actor and metric-store registration;
- producer, JetStream producer, consumer, route, projector, and policy binding;
- actor start before side effects, during startup, and after partial startup;
- startup logging, metrics, audit, and failure recording;
- initial expected/registered/running/health validation;
- Core and JetStream consumer start;
- readiness transition;
- polling-thread construction/start/initial snapshot/log;
- result construction and completion publication; and
- rollback of every resource in reverse order, including rollback failure layered on primary failure.

Shutdown injection points include:

- duplicate/concurrent shutdown and shutdown during startup;
- readiness false and intake closure;
- polling-thread stop signal, active cycle, final heartbeat, and join timeout;
- actor admission closure and drain timeout;
- handler pre-commit and post-commit safe boundaries;
- projector/durable/history/operation flush;
- every actor stop position and reverse dependency batch;
- consumer, producer, route, projector, mailbox, metrics, and actor removal;
- shutdown logging, metrics, audit, result construction, and cleanup; and
- Supervisor-last stop and emergency host teardown.

Cross-cutting tests combine:

- caller cancellation at every await/yield point;
- timeout racing success/failure;
- exception handler throwing while handling the primary failure;
- logger and exception logger both throwing;
- metrics and operation stores throwing;
- result allocation/construction failure through an injectable result factory;
- stale generation and duplicate operation IDs;
- NATS unavailable, database/history unavailable, and ThreadPool starvation;
- one actor hanging indefinitely while others remain controllable;
- process stop arriving during rollback; and
- repeated startup/shutdown cycles to expose leaked registrations, gates, routes, threads, and handles.

Required assertions for every recoverable case:

```text
No escaping exception
One deterministic terminal result
Readiness and intake match the result
No untracked running actor or open admission
No duplicate producer/consumer/route registration
Primary failure preserved
Secondary/cleanup failure linked
Host fallback state remains readable
Subsequent valid startup/shutdown remains possible or HostRecoveryRequired is explicit
```

Property/state-machine tests generate valid and invalid lifecycle sequences. Stress tests race startup, shutdown,
restart, cancellation, timeout, and health polling for a bounded duration and fail on any unobserved task/thread
exception, deadlock, leaked gate, split generation, or inconsistent terminal state.

## 23. Rollout and rollback

1. Merge contracts/projects with no behavior change.
2. Enable Supervisor-first lifecycle in Development/integration only.
3. Enable polling with heartbeat and stored snapshots; keep old Actor Health read for comparison.
4. Compare actor counts, depths, failures, CPU, allocation, and health for a full session.
5. Cut Actor Health reads to new snapshot/query with fallback.
6. Complete restricted-context migration and delete old authority paths.
7. Enable health incidents in observe-only mode.
8. Enable authorized manual operations individually.
9. Enable automatic restart only after explicit acceptance.

Rollback flags may switch reads/policy/manual controls, but must not restore unrestricted domain access after that
migration is accepted. Emergency host teardown remains available throughout rollout.

## 24. Final acceptance criteria

The work is complete only when:

1. Supervisor actors live in `TomasAI.IFM.Domain.Supervisor`, start first, and stop last.
2. One managed startup and one managed shutdown method own every non-Supervisor lifecycle event.
3. API startup/shutdown contains no direct non-Supervisor actor lifecycle logic.
4. Ordinary actors cannot access the privileged context or core supervisor.
5. All actor communication remains through approved NATS actor clients/producers.
6. The dedicated named polling thread starts only after green managed readiness.
7. It sequentially reads all managed actors and atomically stores the latest immutable snapshot every 60 seconds.
8. It attempts one compiled heartbeat containing expected/collected/failed and healthy/degraded/critical/unknown counts.
9. No recoverable actor, cycle, logger, or fallback-log exception terminates the polling thread.
10. Stored metrics remain readable if Supervisor actors, NATS, policy, history, API, UI, or logging are unavailable.
11. Every processed actor message has compiled entry/exit logs with full identity and exit elapsed time.
12. Every mailbox-limit rejection is attributable to the exact `ActorThreadId`, verb, class, reason, depth, and capacity.
13. Five-minute degradation, fifteen-minute restart requirement, and sustained recovery work with original timestamps.
14. Restart/recycle preserves accepted required work and fences old generations.
15. Actor Health displays polling status, summary counts, actor/thread metrics, incidents, operations, and Supervisor
    fallback status.
16. Memory, metric cardinality, history, logging, and operation queues are bounded.
17. Full tests, benchmarks, profiles, synthetic soak, and full trading-session soak pass.
18. Automatic lifecycle mutation remains disabled until its explicit gate is approved.
19. Legacy authority/startup compatibility paths are removed.
20. Implementation and verification evidence are recorded without claiming unexecuted gates complete.
21. Exhaustive deterministic fault injection proves no recoverable exception escapes any Supervisor public method,
    actor handler, lifecycle stage, polling boundary, logger, fallback logger, cleanup, or result path.
22. Supervisor authority remains `Available` or explicitly `AvailableWithDegradedCapability`; when invariants cannot be
    preserved it becomes `HostRecoveryRequired`, rejects mutation, sets readiness false, and exposes fallback evidence.
23. Startup/shutdown stress and state-machine tests find no deadlock, leaked actor/route/consumer/thread/handle, split
    generation, duplicate terminal result, or hidden partial lifecycle state.

## 25. Deliverables

- Supervisor and Supervisor.Shared projects;
- capability contracts and architecture tests;
- Supervisor actors and contexts;
- bootstrap and managed lifecycle coordinator;
- dedicated polling service and atomic metrics state;
- exception-log fallback and polling liveness health check;
- completed actor metrics stores and runtime instrumentation;
- source-generated base and Supervisor logs;
- health policy, incidents, history, queries, API, and UI;
- controlled lifecycle operations and audit;
- unit/integration/fault-injection/system tests;
- BenchmarkDotNet projects/results;
- before/after counters, traces, allocation, latency, and contention evidence;
- soak reports and rollout/rollback record;
- final implementation and verification report.
