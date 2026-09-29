# IFM System-Wide Actor Supervisor Runtime Design

**Document type:** System-wide target design and implementation contract  
**Status:** Initial runtime foundation and read-only Actor Health release implemented  
**Version:** 1.12
**Created:** 2026-09-11  
**Last updated:** 2026-09-28
**Owner:** IFM engineering  
**Initial major ownership:** Actor operations and actor metrics

## 1. Purpose

This document defines `SupervisorRuntimeContext` as the single process-wide ownership boundary for actor operations,
actor health, actor metrics, mailbox activity, shared worker activity, event-projector operations, durable queue status,
and the history exposed through the Actor Health user interface.

The first implementation is deliberately focused on actor operations and metrics. The context is designed as an
extensible root so additional actor-wide services can be centralized behind Supervisor commands, queries, and events
over time without changing the initial ownership model.

The design must provide a fast operational answer to the following questions:

1. Is the actor system available?
2. Which domain, actor, or entity mailbox is unhealthy?
3. How many messages are waiting, scheduled, or processing?
4. Is a quiet actor healthy and available, or unexpectedly inactive?
5. Which messages are slow, failed, cancelled, rejected, or blocked?
6. Which event projectors have pending, blocked, replaying, or terminally failed work?
7. What occurred during a selected historical time range?
8. Did a requested Supervisor operation start, complete, recover, or fail?

## 2. Binding decisions

1. There is exactly one `SupervisorRuntimeContext` singleton per running IFM server process.
2. `SupervisorRuntimeContext` is the logical owner and sole application-facing access boundary for actor operational
   data.
3. The existing `ActorSupervisor` remains the low-level authority for actor registration, lifecycle, mailbox routing,
   and shared worker scheduling.
4. The logical Supervisor domain uses standard `Command`, `Query`, and `Event` actor types. A new
   `ActorType.Supervisor` is not introduced.
5. The Supervisor actor classes and messages use the `Supervisor` prefix so their system-wide responsibility is
   immediately visible.
6. `SupervisorCommandContext`, `SupervisorQueryContext`, and `SupervisorEventContext` receive the privileged
   `ISupervisorActorContext`, backed by the process runtime root without exposing mutable runtime objects.
7. Every non-Supervisor actor context receives only an actor-scoped `IActorSupervisorObservationContext` for its own
   low-latency metrics, cached policy, and nonblocking observations. It cannot read or mutate other actors.
8. Raw current actor metrics reside inside their owning actor. Each actor owns one metric store containing actor-level
   counters and entity-mailbox metrics keyed by `ActorThreadId`.
9. `SupervisorRuntimeContext` reads actor-owned metrics directly through lock-free snapshot contracts. It does not
   send a query to every actor mailbox to assemble system health.
10. Actor activity is recorded directly at existing runtime transitions. A second actor message is not generated for
    every observed actor message.
11. Rare coordinated actions, consistent operational queries, and meaningful notifications use Supervisor commands,
    queries, and events. Their additional actor-message latency is an accepted cost for ordering and isolation.
12. Current health is held in bounded, thread-safe process memory. Historical data is accessed through repositories
    owned by `SupervisorRuntimeContext`.
13. NATS JetStream remains authoritative for messages physically held in durable queues. The event-projector execution
   store remains authoritative for durable projector execution state. `SupervisorRuntimeContext` owns their combined
   operational representation.
14. No background database polling loop is introduced. Durable data is read on an explicit query, a relevant
    transition, or a bounded UI refresh while Actor Health is open.
15. Monitoring failure cannot reject, delay, duplicate, or change a business operation.
16. Empty mailboxes and workers waiting for messages are healthy. Inactivity is red only when the component is
    expected to be running or progressing and is not.
17. Detailed message payloads and live exception objects are never retained by the Supervisor runtime.
18. The initial UI is read-only. Operational commands can be added behind explicit authorization and audit in later
    increments.

## 3. Scope

### 3.1 Initial scope

The first delivery owns:

- actor catalog and topology;
- actor-owned metric stores keyed by `ActorThreadId`;
- direct, nonblocking Supervisor runtime access from every actor context;
- actor lifecycle state;
- entity-mailbox state and depth;
- shared actor worker-pool state;
- accepted, processed, failed, cancelled, and rejected message counts;
- queue wait, handler, and processing-stage timing;
- actor health evaluation and hierarchy rollup;
- actor incidents and recoveries;
- event-projector catalog, readiness, and execution status;
- durable process and replay queue observations;
- replay-attempt history;
- bounded historical actor activity;
- Supervisor query contracts and read models;
- the Actor Health UI; and
- removal of the standalone Strategy observation toolbar entry because strategy already has its own view.

### 3.2 Planned expansion boundary

Future services may be placed behind `SupervisorRuntimeContext` when they are system-wide operational concerns. Likely
candidates include:

- domain start, stop, pause, resume, and drain controls;
- dependency and readiness management;
- controlled actor restart and recovery policy;
- configuration inspection;
- deployment identity and version health;
- process, memory, allocation, garbage-collection, and thread-pool summaries;
- alert routing and incident acknowledgement;
- trading hold and emergency-control coordination;
- future agent-assisted fault investigation; and
- cross-host actor-system aggregation.

These future capabilities must be introduced as named sections or services. They must not turn the root context into
an unstructured service locator.

### 3.3 Initial exclusions

The first delivery does not:

- replace OpenTelemetry, logs, traces, NATS, or projector execution storage;
- send one observability message for every business message;
- retain business message payloads for inspection;
- automatically restart actors or place trading on hold;
- expose mutable Supervisor controls in the UI;
- implement cross-process aggregation; or
- change business-domain actor behavior.

## 4. Verified current architecture

The design builds on the following existing behavior.

### 4.1 Actor identity and routing

`ActorMailboxId` contains `ActorType` and actor name. `ActorThreadId` contains actor type, actor name, and entity ID.
An `ActorSubject` adds the verb. The operational hierarchy is therefore naturally expressed as:

```text
Domain -> Actor mailbox -> Entity mailbox -> Message verb
```

The term `ActorThreadId` is retained in existing code for compatibility, but it identifies an entity mailbox. It does
not identify a dedicated operating-system thread.

### 4.2 Entity mailbox scheduling

Each actor owns a set of entity mailboxes. An entity mailbox is a bounded multi-producer/single-consumer queue. A
scheduling bit ensures an entity mailbox appears at most once in the shared ready queue. A shared actor worker takes a
scheduled entity mailbox, processes a bounded batch, and either reschedules or retires it. No two workers process the
same entity mailbox concurrently.

Consequently, the UI must display a transient worker assignment only while an entity mailbox is processing. It must
not imply that every entity owns a permanent thread.

### 4.3 Existing actor metrics

The actor runtime already defines low-cardinality `System.Diagnostics.Metrics` instruments for:

- accepted, processed, failed, and cancelled messages;
- aggregate mailbox depth;
- active mailbox count;
- ready-queue depth;
- worker capacity, busy count, available count, and utilization;
- enqueue wait, queue wait, and handler duration;
- processing-stage duration and failures;
- duplicate commands; and
- admission usage, payload size, would-reject, and rejected outcomes.

These instruments are primarily tagged by actor type. They are useful for aggregate telemetry, but they cannot by
themselves provide domain, actor, and entity-mailbox drill-down.

### 4.4 Existing event-projector operations

Event projectors already expose:

- actor name and projector name;
- durable process and replay queue names;
- projected event types and descriptors;
- current readiness;
- paged operational states;
- exact retry and skip operations; and
- access to event-source storage and the durable replay queue.

Current projector storage includes pending, blocked, terminal-failed, expired-lease, outbox-pending, and outbox-retry
information. Execution state contains replay status, attempt number, outcome, stage, error, timestamps, event identity,
lease, retry, and checkpoint fields.

### 4.5 Existing durable queues

Each durable projector has isolated NATS JetStream process and replay streams and consumers. Consumer information can
provide live pending, acknowledgement-pending, delivery, and redelivery evidence. Projector execution storage and
JetStream answer different questions and must be shown together without treating either as a substitute for the
other.

### 4.6 Existing actor-context access

The standard command, query, event, function, and denormalizer contexts already receive `IActorSupervisor` during
construction and retain it privately. They currently expose the supervisor container and selected messaging/routing
operations rather than a common Supervisor runtime contract. The actor base classes also receive `IActorSupervisor`
during startup. The first increment therefore extends an existing dependency relationship; it does not require actors
to discover an unrelated global service.

The new contract must expose `SupervisorRuntimeContext` explicitly. Domain actors must not resolve it ad hoc from the
container, and they must not receive unrestricted access to the core supervisor's lifecycle and routing mutations.

## 5. Terminology

| Term | Definition |
| --- | --- |
| Core actor supervisor | Existing `ActorSupervisor` runtime object that owns actors, routing, mailboxes, and workers |
| Supervisor domain | Logical system-wide command/query/event actor boundary introduced by this design |
| Supervisor runtime | The singleton `SupervisorRuntimeContext`, actor-owned metric access, and its operational sections |
| Actor metric store | Current actor and entity-mailbox metrics physically owned by one actor |
| Direct runtime access | Synchronous, bounded, nonblocking access to metrics and cached Supervisor state |
| Supervisor messaging | Command/query/event communication used for infrequent coordination and durable outcomes |
| Actor mailbox | One registered actor identified by `ActorType + Name` |
| Entity mailbox | One ordered queue identified by `ActorType + Name + EntityId` |
| Worker | One member of the shared actor worker pool |
| Activity | Whether messages are idle, queued, scheduled, or processing |
| Health | Whether a component is operating within its expected policy |
| Incident | A bounded record of degradation, fault, recovery, or rejected operation |
| Projector execution | Durable state for one event/projector pair |
| Replay attempt | One append-only historical attempt to recover projector processing |

## 6. Target architecture

```text
IFM UI
  |
  | Command / Query
  v
+-------------------------------------------------------+
| Supervisor domain                                     |
|                                                       |
|  SupervisorCommandActor   SupervisorQueryActor        |
|              \                 /                      |
|               \               /                       |
|                SupervisorRuntimeContext                |
|               /       |        \                      |
|  SupervisorEventActor  |         History repositories |
+-------------------------|-----------------------------+
                          |
         +----------------+----------------+
         |                |                |
   ActorSupervisor   Projector catalog   Durable queue readers
         |
  registered actors
         |
  actor-owned metric stores keyed by ActorThreadId
```

The Supervisor actors form one logical root. They remain separate physical actors because command, query, and event
messages have different contracts, delivery rules, and lifecycle semantics.

## 7. Supervisor actor topology

### 7.1 Actor classes

The initial Supervisor domain contains:

```text
SupervisorCommandActor
SupervisorQueryActor
SupervisorEventActor
```

The classes follow existing actor conventions and use standard extension-handler maps. Domain-specific calculations,
health evaluation, rollup, and history conversion belong in a `Model` folder rather than actor receive methods.

### 7.2 Context classes

```text
SupervisorCommandContext
SupervisorQueryContext
SupervisorEventContext
```

Each typed context retains its normal actor-context contract and implements a small common contract:

```text
ISupervisorActorContext
  ISupervisorRuntimeSnapshotReader Snapshots { get; }
  ISupervisorHealthManager Health { get; }
  ISupervisorActorOperations Operations { get; }
  ISupervisorIncidentStore Incidents { get; }
```

All three contexts are backed by the same process runtime root, but expose only immutable reads and named privileged
operations. They never return the concrete `SupervisorRuntimeContext` or mutable actor/runtime objects.

### 7.3 Naming and subjects

The actor name is `Supervisor`. Standard actor type remains part of the subject:

```text
Command.Supervisor.<Verb>.<EntityId>
Query.Supervisor.<Verb>.<EntityId>
Event.Supervisor.<Verb>.<EntityId>
```

Actor-wide commands use the target actor or domain as their entity ID so operations against the same target are
serialized while unrelated targets may proceed concurrently. Host-wide commands use the host-instance ID.

### 7.4 Why no `ActorType.Supervisor`

The actor framework currently assigns distinct message semantics and delivery rules to `Command`, `Query`, `Event`,
`Notify`, `Realtime`, and `Function`. Numeric value 1 was formerly Supervisor and remains reserved so persisted
subjects cannot be reinterpreted. Reintroducing a combined type would require new clients, transports, consumers,
parsers, reply rules, and base actor contracts. The logical Supervisor domain achieves the required visibility while
preserving current conventions.

### 7.5 Access from domain actors

Every non-Supervisor actor context receives an actor-scoped view backed by the process runtime root:

```text
IActorSupervisorObservationContext
  own-actor metrics and failure recorder
  immutable cached own-actor policy
  own-actor observation only
```

This applies to command, query, event, realtime, function, and denormalizer contexts. Specialized contexts may expose
the narrow interface through their typed contract, but cannot expose or resolve the concrete root or core supervisor.

Actor-to-Supervisor interaction has two deliberately different paths:

```text
Actor
  +-- scoped IActorSupervisorObservationContext call
  |     own metrics, own cached policy, bounded own observation
  |
  +-- Supervisor actor message
        rare command, query, or event requiring coordination
```

Direct access is allowed only when the call is synchronous, constant-time, nonblocking, bounded, and free of database,
network, filesystem, and actor-message work. Normal examples are metric increments, timestamp/state updates, and
reading an immutable cached policy.

Supervisor messaging is required when an actor requests a restart, pause, routing or lifecycle change, durable
history, coordinated system state, authorization, audit, or an operation result. Messaging adds latency but supplies
mailbox serialization and protects actors from shared-state locking and lifecycle races.

An ordinary actor cannot call mutable core lifecycle operations because its scoped interface does not contain them.
Capabilities are separated by interface and composition rather than caller convention:

```text
IActorSupervisorObservationContext   direct own-actor observation
IActorClient                         Supervisor commands, queries, and events
ISupervisorActorContext              privileged Supervisor-domain actors only
```

The initial metrics implementation uses the direct scoped path. Coordinated Supervisor commands and events use NATS
actor messaging and the privileged context exists only inside `TomasAI.IFM.Domain.Supervisor`.

The binding selection matrix is:

| Actor need | Access path | Reason |
| --- | --- | --- |
| Increment accepted/processed/failed counter | Direct metric entry | Per-message, constant-time operation |
| Update mailbox depth or activity timestamp | Direct metric entry | Per-message, nonblocking observation |
| Read immutable cached Supervisor policy | Direct root-context read | Low-latency decision with no coordination |
| Report a bounded immediate health observation | Direct root-context call | Must never wait behind another mailbox |
| Restart, stop, pause, resume, or reroute an actor | Supervisor command | Requires validation, ordering, and audit |
| Obtain durable history or a coordinated system result | Supervisor query | May perform I/O and return a typed result |
| Announce degradation, recovery, or operation completion | Supervisor event | Infrequent meaningful transition |
| Persist history or inspect NATS durable state | Supervisor handler/service | I/O is excluded from direct actor access |

## 8. `SupervisorRuntimeContext` ownership model

### 8.1 Root contract

`SupervisorRuntimeContext` is a sealed, process-wide singleton. It owns explicit sections:

```text
SupervisorRuntimeContext
  Identity
  Actors
  Mailboxes
  Workers
  Messages
  Projectors
  DurableQueues
  Operations
  Incidents
  History
  HealthPolicy
  SnapshotFactory
```

These sections may be separate internal services, but their public ownership and access remain rooted at
`SupervisorRuntimeContext`.

### 8.2 Logical and physical ownership

`SupervisorRuntimeContext` is the logical access and management root. Raw current actor metrics are physically stored
inside the actor that produces them:

```text
Actor
  Mailbox
  ActorMetricsStore
    ActorMetrics
    MailboxMetrics[ActorThreadId]
```

This placement keeps metric updates local and avoids routing metrics through a centralized Supervisor mailbox. The
root context enumerates registered actors through `ActorSupervisor.Children` and reads each actor's metric store by a
snapshot contract. It may aggregate or briefly cache an immutable query snapshot, but it does not maintain a second
mutable copy of every actor counter.

Physical ownership is therefore:

| Data | Physical authority | Supervisor responsibility |
| --- | --- | --- |
| Current actor metrics | Owning actor | Enumerate, snapshot, aggregate, evaluate |
| Current entity-mailbox metrics | Owning actor, keyed by `ActorThreadId` | Read and expose bounded snapshots |
| Shared worker metrics | Actor worker pool | Read and combine with actor activity |
| Actor registration/lifecycle | Core `ActorSupervisor` and actor | Catalog and health interpretation |
| Historical activity/incidents | Supervisor history store | Write, query, retain |
| Durable queue contents | NATS JetStream | Observe and combine |
| Projector execution state | Event-projector store | Observe, correlate, and present |

### 8.3 Identity

The root identity contains:

- host-instance ID;
- service name;
- process ID;
- application version;
- environment;
- started UTC;
- actor-runtime generation; and
- snapshot revision.

The host-instance ID separates historical records from different process runs. The actor-runtime generation changes
when the runtime is replaced within the same process.

### 8.4 Actor catalog

The actor catalog contains one entry per registered actor mailbox:

- stable actor mailbox ID;
- display name;
- actor CLR type and namespace;
- domain and subdomain path;
- actor type;
- expected lifecycle policy;
- registered, starting, running, stopping, stopped, or faulted state;
- registration, start, stop, fault, and recovery timestamps;
- last exception summary;
- entity-mailbox count;
- projector identities owned by the actor; and
- whether the actor is required for host readiness.

Domain and subdomain are explicit registration metadata. CLR namespace is displayed as technical evidence and may be
used as a migration fallback, but it is not the stable domain key.

### 8.5 Actor-owned metric store

Each actor exposes an `IActorMetricsStore` through the actor contract. The store contains actor-level lifecycle and
aggregate counters plus one `ActorMailboxMetrics` entry per retained `ActorThreadId`.

The steady-state message path holds a direct reference to its mailbox metric entry. It must not perform a dictionary
lookup for every update when the queue can retain that reference safely.

`ActorMailboxMetrics` contains atomic scalar state for:

- accepted, dequeued, processed, failed, cancelled, and rejected counts;
- current and maximum queue depth;
- queue capacity and oldest queued timestamp;
- last accepted, started, completed, failed, and retired timestamps;
- current activity and lifecycle state;
- current message verb and processing-started timestamp;
- current shared-worker identity while processing;
- bounded latest failure identity/code; and
- revision.

The store provides:

```text
CaptureActorSnapshot()
CaptureMailboxSnapshots(continuation, pageSize)
TryGetMailboxSnapshot(ActorThreadId)
```

These methods are observational reads, not actor queries, and must not execute on the target actor's mailbox.

### 8.6 Entity-mailbox registry

The actor-owned mailbox metric store contains one bounded entry for each retained entity mailbox:

- `ActorThreadId` identity;
- lifecycle state;
- activity state;
- queue depth and capacity;
- peak depth since start;
- oldest queued-message timestamp;
- scheduled timestamp;
- processing-started timestamp;
- current message verb;
- current trace/correlation identifiers when available;
- assigned shared-worker ID while processing;
- accepted, processed, failed, cancelled, and rejected counters;
- last accepted, dequeued, completed, failed, and retired timestamps;
- latest failure summary; and
- snapshot revision.

Retired healthy mailboxes may be removed from current memory after their bounded history is flushed. Failed or
incident-associated mailboxes remain discoverable through history.

### 8.7 Shared worker registry

Worker state is reported separately from entity mailbox state:

- configured capacity;
- started and available workers;
- workers currently owning mailbox batches;
- worker utilization;
- ready-queue depth;
- current mailbox assignment for each busy worker;
- batch start time and messages completed in the batch;
- last fault and restart time; and
- drain state during shutdown.

### 8.8 Projector catalog

The projector catalog contains:

- owning actor;
- projector name;
- projected event types;
- process and replay queue names;
- configured replay interval and maximum attempts;
- prepared, enabled, idle, processing, stopped, or faulted worker state;
- readiness state and reason;
- last received, completed, deferred, failed, replayed, and terminal timestamps;
- operational execution snapshot;
- stream checkpoint; and
- current incident reference.

### 8.9 Durable queue registry

The durable queue section combines cached JetStream observations with projector execution state:

- stream and consumer identity;
- process or replay lane;
- consumer availability;
- pending count;
- acknowledgement-pending count;
- redelivery count;
- oldest pending age when available;
- last delivered and acknowledged sequence;
- next eligible replay time;
- worker enabled/running/faulted state;
- last successful observation time; and
- observation source and freshness.

The context must mark stale or unavailable observations as unknown. It must never convert missing evidence into green.

### 8.10 Supervisor operations

The operation registry tracks commands that can outlive their request:

- operation ID;
- command ID;
- operation type;
- target type and target ID;
- requested, started, completed, or failed status;
- requested, started, completed, and failed timestamps;
- requester identity when authorization is introduced;
- current step and progress text;
- result code and structured failure;
- resulting event IDs; and
- trace and correlation IDs.

The registry is bounded in memory. Durable operation outcomes are stored in history.

### 8.11 Incidents

An incident represents a health transition rather than every repeated sample. It contains:

- incident ID;
- component kind and identity;
- severity;
- reason code and message;
- first observed, last observed, and recovered timestamps;
- occurrence count;
- relevant queue depths and durations;
- source message, event, trace, and correlation identifiers;
- recovery operation ID when present; and
- acknowledgement metadata when later enabled.

Repeated observations with the same bounded incident key increment the occurrence count instead of creating an
unbounded series of objects.

## 9. Lifecycle and dependency order

The required process startup order is:

1. Register storage, NATS, time, serialization, and configuration dependencies.
2. Create the core `ActorSupervisor`.
3. Create the singleton `SupervisorRuntimeContext`.
4. Connect actor-runtime transition sinks to the singleton.
5. Create Supervisor command, query, and event contexts with the same privileged context backed by the singleton root.
6. Use the minimal host bootstrap to register and start the three Supervisor actors only.
7. Call `ISupervisorActorContext.ManagedActors.StartupActorsAsync` exactly once.
8. Inside that operation, resolve, construct, register, bind, start, and initially validate every non-Supervisor actor.
9. Open external actor-message intake only after the complete managed-actor startup transaction succeeds.
10. Mark runtime readiness healthy and start the dedicated 60-second Supervisor actor-metrics polling thread.

Host shutdown calls `ISupervisorActorContext.ManagedActors.ShutdownActorsAsync` exactly once. That operation owns the
managed shutdown sequence:

1. Reject new external intake.
2. Record Supervisor shutdown state.
3. Drain accepted actor work within the configured bound.
4. Flush pending Supervisor history batches.
5. Stop and unregister all non-Supervisor actors, projectors, producers, consumers, and routes.

After the managed shutdown result is recorded, the host bootstrap stops the three Supervisor actors, disposes
`SupervisorRuntimeContext` once, and finally disposes the core runtime and transports. Supervisor actors are never
members of their own managed lifecycle set.

Constructor dependencies must avoid a cycle between `ActorSupervisor` and `SupervisorRuntimeContext`. Runtime hooks
should depend on a narrow `IActorOperationsRecorder` contract that is connected during composition, or both objects
should share an independently constructed registry owned by the root context.

## 10. Data collection model

### 10.1 Hot-path rules

Actor hot paths may update only bounded state using operations such as:

- atomic counter increments;
- atomic UTC timestamp/tick replacement;
- atomic state transitions;
- bounded maximum updates; and
- stable identity references created when a mailbox is created.

The queue retains a direct reference to its actor-owned `ActorMailboxMetrics` entry. Steady-state updates use
`Interlocked` and `Volatile`; they do not send Supervisor messages or look up the entry through the root context.

Hot paths must not:

- serialize an observation;
- write to a database;
- call NATS for observability;
- allocate a health event per actor message;
- retain actor messages or payload buffers;
- format error strings when the measurement is disabled; or
- take a global lock.

`ConcurrentDictionary` or a narrow lifecycle lock may be used when an entity mailbox is first created or retired.
Those structural operations are outside the steady-state per-message path. No lock may be held across an `await`, and
Supervisor snapshot reads must never acquire a lock that blocks actor message processing.

### 10.2 Runtime transition hooks

The following transitions update the actor-owned metric store or the applicable shared runtime store. The
`SupervisorRuntimeContext` reads and combines them:

| Transition | Required update |
| --- | --- |
| Actor registered | Core catalog plus actor metric store: add identity and expected policy |
| Actor starting/running | Actor metric store: record lifecycle state and timestamp |
| Actor stopping/stopped | Actor metric store: record state and expected/unexpected reason |
| Actor faulted | Actor metric store: record failure; Supervisor incident sink receives bounded transition |
| Entity mailbox created | Actor metric store: add bounded entry keyed by `ActorThreadId` |
| Message accepted | Actor mailbox metrics: increment accepted/depth; set last accepted |
| Mailbox scheduled | Actor mailbox metrics and worker store: mark scheduled; update ready depth |
| Message dequeued | Actor mailbox metrics: decrement depth; record queue wait and oldest item |
| Handler started | Actor mailbox metrics: mark processing; record verb and start time |
| Handler completed | Actor mailbox metrics: increment processed; record duration; clear current message |
| Handler failed | Actor mailbox metrics: increment failed; record bounded structured failure |
| Handler cancelled | Actor mailbox metrics: increment cancelled and cancellation reason |
| Admission rejected | Actor mailbox metrics: increment rejection counter and reason |
| Mailbox retired | Actor metric store: flush rollup and remove healthy current entry when eligible |
| Worker takes/releases batch | Update busy/available state and assignment |
| Projector transition | Update stage, outcome, checkpoint, and readiness |
| Replay attempt | Append attempt history and update current replay state |

### 10.3 Current snapshot consistency

Snapshots are observational and do not participate in business correctness. The root context enumerates the core
supervisor's registered actor references and calls each actor metric store directly. It never sends a fan-out query to
the actors.

Scalar fields are read with `Volatile`; counters are updated with `Interlocked`. A published, volatile array or an
equivalent copy-on-structural-change view supplies lock-free enumeration of mailbox metric-entry references. A
concurrent dictionary may remain the lookup authority for creation and exact identity lookup.

Snapshot creation reads component revisions, copies bounded scalar state, and retries once if a component revision
changes during capture. A snapshot may identify itself as `Consistent`, `PartiallyConcurrent`, or `Unavailable`. It
must never stop actor processing to obtain a perfectly atomic system-wide view. A partially concurrent snapshot is
valid for observability as long as every field is memory-safe and its observation time/revision is reported.

### 10.4 Historical rollups

Current counters are converted into interval deltas by a bounded, asynchronous history writer. Supervisor actor-health
collection runs every 60 seconds after managed startup reaches healthy readiness. Historical buckets use the same
60-second sample and are independent of whether the Actor Health UI is open. Hard-limit warnings are emitted immediately
at the rejecting runtime boundary; aggregate health transitions are evaluated from the next completed poll.

The history writer is signalled by activity or an open UI subscription. It is not a database polling loop. If its
bounded channel is full, it coalesces actor activity into the next bucket and preserves failure incidents separately.

## 11. Health and activity model

### 11.1 Separate dimensions

Health and activity must be represented independently.

`SupervisorHealthStatus`:

```text
Unknown
Healthy
Degraded
Critical
NotExpected
```

`SupervisorActivityStatus`:

```text
Unknown
Idle
Queued
Scheduled
Processing
Recovering
Stopped
```

### 11.2 Color rules

| Display | Meaning |
| --- | --- |
| Green health | Registered, available, and within policy |
| Yellow health | Degraded, stale, slow, retrying, or above warning threshold |
| Red health | Faulted, unexpectedly stopped, terminally blocked, or unavailable |
| Grey health | Intentionally stopped or not expected for the current session |
| Yellow activity pulse | Currently processing a message |
| Queue badge | Current waiting-message count |

An idle actor with an empty mailbox remains green when it is registered and available. Market-closed and intentionally
disabled actors are grey when policy says they are not expected. An actor becomes red only when expected activity or
availability evidence is absent beyond its policy.

### 11.3 Configurable thresholds

Thresholds are defined by actor policy and may include:

- maximum queue depth and utilization;
- maximum oldest-message age;
- maximum current handler duration;
- failure-rate window;
- admission-rejection threshold;
- required progress interval;
- projector pending-age threshold;
- replay-attempt warning and critical limits;
- terminal projector failure count; and
- evidence freshness.

Defaults exist at the system and actor-type level. Domain or actor overrides require explicit configuration. An actor
with no progress requirement is not marked unhealthy merely because it is quiet.

### 11.4 Hierarchical rollup

Parent nodes roll up the worst health of required descendants and show counts of optional descendants separately.
Activity is summarized rather than converted into health.

Example:

```text
Market Data  [Green]  3 processing | 12 queued
Portfolio    [Yellow] 1 blocked projector
Trade        [Red]    1 faulted entity mailbox
```

A red optional actor does not silently turn the entire host red. The parent displays the optional failure and applies
the configured readiness policy.

## 12. Message contracts

All contracts use standard IFM command, query, and event conventions. They carry command/query identity, correlation,
trace propagation, requested UTC, and target identity as applicable.

### 12.1 Initial queries

| Query | Result |
| --- | --- |
| `GetSupervisorHealthQuery` | Whole-system summary and root status |
| `GetSupervisorActorTreeQuery` | Bounded domain/actor/entity hierarchy |
| `GetSupervisorActorDetailQuery` | Current actor detail and aggregate activity |
| `GetSupervisorMailboxDetailQuery` | Current entity-mailbox detail |
| `GetSupervisorWorkerPoolQuery` | Shared worker and ready-queue state |
| `GetSupervisorActivityHistoryQuery` | Time-bucketed activity for a selected node |
| `GetSupervisorIncidentHistoryQuery` | Paged incidents for a selected node and date range |
| `GetSupervisorProjectorDetailQuery` | Current projector and execution status |
| `GetSupervisorReplayHistoryQuery` | Paged replay attempts for a date range |
| `GetSupervisorOperationQuery` | Status and outcome of one Supervisor operation |

Date-range queries require `FromUtc`, exclusive `ToUtc`, bounded page size, and keyset continuation. UI local time is
converted to UTC before the query is sent.

### 12.2 Future commands

The first UI is read-only, but the context and contracts reserve these controlled operations:

| Command | Purpose |
| --- | --- |
| `RestartSupervisorActorCommand` | Restart one supported actor target |
| `PauseSupervisorActorIntakeCommand` | Pause external intake for one actor/domain |
| `ResumeSupervisorActorIntakeCommand` | Resume previously paused intake |
| `RetrySupervisorProjectorEventCommand` | Retry one exact projector execution |
| `SkipSupervisorProjectorEventCommand` | Explicitly skip one blocked execution with reason |
| `AcknowledgeSupervisorIncidentCommand` | Record operator acknowledgement |
| `CaptureSupervisorDiagnosticsCommand` | Capture a bounded diagnostic snapshot |

Command results report acceptance and operation ID. Long-running completion is reported through events and
`GetSupervisorOperationQuery`.

### 12.3 Events

| Event | Meaning |
| --- | --- |
| `SupervisorHealthDegradedEvent` | A component crossed into degraded state |
| `SupervisorHealthCriticalEvent` | A component crossed into critical state |
| `SupervisorHealthRecoveredEvent` | A prior incident recovered |
| `SupervisorOperationRequestedEvent` | A controlled action was accepted |
| `SupervisorOperationStartedEvent` | Execution began |
| `SupervisorOperationCompletedEvent` | Execution succeeded |
| `SupervisorOperationFailedEvent` | Execution failed with structured details |
| `SupervisorProjectorReplayStartedEvent` | A replay attempt began |
| `SupervisorProjectorReplayCompletedEvent` | A replay attempt completed |
| `SupervisorProjectorReplayFailedEvent` | A replay attempt failed or terminalized |

Routine metric samples are not domain events. Events represent meaningful transitions and auditable operator actions.

## 13. Read models

### 13.1 Root snapshot

`SupervisorHealthSnapshot` includes:

- schema version and snapshot revision;
- host identity and observed UTC;
- snapshot consistency and evidence freshness;
- overall health;
- actor/domain/mailbox counts by health and activity;
- total current queue depth;
- oldest queued-message age;
- worker capacity, busy, and available counts;
- projector pending, blocked, replaying, and terminal counts;
- open incident counts by severity; and
- links/identities for bounded drill-down queries.

### 13.2 Actor tree node

`SupervisorActorTreeNode` includes:

- stable node ID and optional parent ID;
- node kind: host, domain, subdomain, actor, entity mailbox, projector, or durable queue;
- display name and technical identity;
- health and activity;
- queued and processing counts;
- failure and incident counts;
- last activity UTC;
- child count and whether children are loaded; and
- snapshot revision.

Children are loaded lazily. The root response must not materialize every high-cardinality entity mailbox.

### 13.3 Failure detail

Failures use a bounded structured model containing:

- error code, type, message, and safe detail;
- processing stage;
- actor, entity, and verb;
- occurred UTC;
- trace, correlation, command, query, source-event, and operation IDs;
- retryability and attempt number;
- recovery status; and
- occurrence count.

Stack traces may be stored in durable diagnostics under configured retention, but are not held in current mailbox
records and are loaded only when failure detail is requested.

## 14. Durable storage design

### 14.1 Logical tables

The first implementation requires the following logical records. Physical naming follows the selected storage
provider's existing convention.

#### `SupervisorActorActivityBucket`

Composite identity:

```text
HostInstanceId + BucketStartUtc + ActorType + ActorName
```

Fields include domain path, bucket duration, accepted, processed, failed, cancelled, rejected, maximum depth, maximum
oldest age, queue-wait aggregates, handler-duration aggregates, stage-failure counts, and snapshot revision.

#### `SupervisorMailboxActivityBucket`

Stored only for configured, active, slow, backlogged, or failed entity mailboxes. Identity adds `EntityId`. This
selective policy prevents unlimited history from high-cardinality entities.

#### `SupervisorHealthIncident`

Identity is `IncidentId`. Indexes support component identity plus first/last observed UTC, open incidents, severity,
and correlation identifiers.

#### `SupervisorOperation`

Identity is `OperationId`. A unique constraint protects `CommandId`. Indexes support target plus requested UTC and
non-terminal operations.

#### `SupervisorProjectorReplayAttempt`

Append-only identity:

```text
EventId + ProjectorName + AttemptNumber
```

Fields include actor name, source event, stream identity/version, starting stage, last completed stage, outcome,
started/completed/failed UTC, next attempt UTC, error details, execution token, trace/correlation IDs, and terminal
status.

The append-only attempt record complements the existing latest execution-state row. It does not replace it.

### 14.2 Retention

Retention is configurable by record class:

- actor aggregate buckets: longer retention;
- selected mailbox buckets: shorter retention;
- recovered informational incidents: shorter retention;
- failed/critical incidents: longer retention;
- replay attempts and Supervisor operations: audit retention; and
- detailed stack traces: shortest bounded retention unless attached to critical incidents.

Retention work runs as scheduled maintenance outside Ring 2 processing. Deletion is bounded and indexed.

### 14.3 History write behavior

History writes use a bounded asynchronous channel owned by the root context. Activity buckets coalesce by key.
Critical transitions and operation outcomes use a protected incident/operation lane. Database failure marks historical
persistence degraded but cannot fail the observed business operation.

## 15. Event-projector and replay visibility

For each projector, the UI combines three evidence sources:

1. Projector readiness and worker lifecycle from the in-process projector catalog.
2. Current process/replay stream and consumer information from NATS JetStream.
3. Durable execution, outbox, blocked, terminal, and replay-attempt records from storage.

The detail view must distinguish:

- process messages waiting for initial execution;
- replay messages waiting for another attempt;
- delivered but unacknowledged messages;
- deferred processing that is expected flow control;
- retryable failures;
- blocked executions;
- exhausted/terminal failures;
- pending outbox publication; and
- stale or unavailable queue evidence.

The UI must not describe every redelivery as an exception. Expected deferral and retry are explicit outcomes. A genuine
failure retains comprehensive structured error details.

## 16. Actor Health UI

### 16.1 Navigation

The main toolbar becomes:

```text
Feed Health | Operations health | Actor health
```

The standalone Strategy observation toolbar button is removed. Strategy-specific observation remains available in
the Strategy view.

### 16.2 Form layout

The Actor Health form uses the dark trading theme and contains:

```text
+--------------------------------------------------------------------------+
| Actor Health   From [date/time]  To [date/time]  [Live] [Refresh]        |
| [All] [Unhealthy] [Active] [Search________________]  Updated: hh:mm:ss    |
+-----------------------------+--------------------------------------------+
| Supervisor tree             | Selected-node detail                       |
|                             |                                            |
| > Host                      | Summary cards                              |
|   > Domain                  | Overview | Mailboxes | Activity | Failures  |
|     > Actor                 | Projectors | Replays                       |
|       > Entity mailbox      |                                            |
|       > Projector           | Tables, timeline, charts, error detail     |
+-----------------------------+--------------------------------------------+
```

The splitter is resizable. Tree children load lazily. Selection is retained across refresh when the node still exists.

### 16.3 Top summary

Summary cards display:

- overall health;
- registered/running/faulted actors;
- active entity mailboxes;
- queued messages and oldest age;
- processing mailboxes;
- worker utilization;
- pending/blocked/replaying projectors; and
- open warning/critical incidents.

### 16.4 Tree behavior

The initial hierarchy is:

```text
Supervisor / Host
  Domain
    Subdomain
      Actor mailbox
        Entity mailbox
        Event projector
          Durable process queue
          Durable replay queue
```

Each node displays a health dot, activity glyph, queue badge, and last-activity age. Filters do not change health
calculation. A parent remains visible when any descendant matches.

### 16.5 Detail tabs

**Overview** shows identity, lifecycle, current state, counts, timestamps, latency, policy, and evidence freshness.

**Mailboxes** shows entity ID, activity, depth/capacity, oldest age, current verb, processing duration, shared worker,
last completion, last failure, and admission outcomes.

**Activity** shows time-bucketed accepted, processed, failed, cancelled, and rejected counts plus queue-depth and
latency charts for the selected range.

**Failures** shows comprehensive structured error information and correlation identities without retaining message
payloads.

**Projectors** shows readiness, execution state, checkpoints, process/replay queue status, blocked work, terminal
failures, and outbox backlog.

**Replays** shows append-only replay attempts for the selected range with event identity, attempt, stage, outcome,
duration, next retry, and error detail.

Tabs irrelevant to the selected node are hidden or disabled.

### 16.6 Date range and live behavior

The From and To controls apply to every historical tab and descendant selection. `To` is exclusive in service
contracts. The UI displays local time and sends UTC.

Live mode shows the current in-memory snapshot and refreshes only while the form is visible. The initial refresh
interval is 60 seconds and is configurable. Manual refresh is always available. Live refresh must not run historical
queries unless a historical tab is visible.

## 17. Query and API boundaries

The UI calls `SupervisorQueryActor` through a dedicated UI service. The UI does not independently query:

- `ActorSupervisor` internals;
- NATS JetStream;
- event-source tables;
- OpenTelemetry exporters; or
- individual domain actors.

`SupervisorQueryActor` handles only the original user/API request. It calls
`SupervisorRuntimeContext.CaptureSnapshot()` and reads actor-owned metric stores directly. It does not submit a query
to every actor, wait behind each domain mailbox, or ask actors to allocate read models on their processing path.

```text
UI/API query
  -> SupervisorQueryActor
      -> SupervisorRuntimeContext.CaptureSnapshot()
          -> direct actor metric-store reads
          -> direct worker metric-store read
          -> no actor query fan-out
```

If an HTTP endpoint is needed as a transport adapter, it calls the Supervisor query API and returns the same shared
read models. Responses have bounded size, cancellation, timeout, schema version, and keyset continuation.

A small host-level liveness endpoint remains outside the actor query path. If the actor scheduler cannot process the
Supervisor query mailbox, the host may read a minimal `SupervisorRuntimeContext` snapshot directly and must still
report that the actor system is unavailable.

## 18. Performance and GC requirements

### 18.1 Ring 2 requirements

The actor message path must preserve the existing low-allocation design:

- no per-message health record allocation;
- no string construction for normal metric updates;
- no database or network observation calls;
- no global locks;
- no unbounded dictionaries, queues, or histories;
- no retained message payload; and
- no observability exception used as control flow.

Direct runtime calls exposed to domain actors must additionally be synchronous, constant-time, bounded, and
nonthrowing during normal operation. Direct access cannot perform actor lifecycle changes, routing changes, database
work, NATS work, or filesystem work. Those operations use Supervisor messaging.

### 18.2 Cardinality controls

OpenTelemetry tags remain low-cardinality. Actor type and bounded stage/reason values are acceptable. Arbitrary entity
IDs, command IDs, event IDs, and trace IDs are not emitted as metric tags.

High-cardinality identities are available through bounded Supervisor queries and durable incident/history records.

### 18.3 Snapshot allocation

Read models allocate only when requested. Tree children and historical pages are lazy and bounded. Repeated live
refreshes reuse UI rows where practical. Snapshot caches have one latest immutable snapshot per requested aggregate,
not an accumulating sequence.

### 18.4 Acceptance measurements

Implementation must measure:

- added nanoseconds per mailbox accept/dequeue/complete cycle;
- added allocation per message with Actor Health closed;
- direct metric-update latency compared with a Supervisor actor-message round trip;
- memory per retained actor and entity-mailbox record;
- snapshot time for expected and stress actor counts;
- history-writer backlog and coalescing; and
- UI refresh query duration and response size.

No production activation threshold is approved until these measurements are recorded.

## 19. Resilience and self-observation

The Supervisor runtime must not claim green health based solely on its own ability to answer. It reports:

- last successful runtime observation;
- last successful projector and queue observation;
- history-writer health;
- snapshot freshness;
- dropped/coalesced noncritical history buckets; and
- its own open incidents.

The host-level fallback reports at least:

- process alive;
- actor supervisor created;
- actor supervisor ready;
- Supervisor query actor registered/running;
- latest Supervisor snapshot age; and
- last Supervisor query failure.

Supervisor failures never trigger automatic trading action in the first delivery. Future recovery and trading-control
policies require explicit, independently reviewed commands and safeguards.

## 20. Configuration

Configuration is grouped under `SupervisorRuntime` and includes:

```text
SupervisorRuntime:Enabled
SupervisorRuntime:LiveRefreshInterval
SupervisorRuntime:HistoryBucketInterval
SupervisorRuntime:HistoryWriterCapacity
SupervisorRuntime:CurrentIncidentCapacity
SupervisorRuntime:OperationCapacity
SupervisorRuntime:EntityMailboxRetention
SupervisorRuntime:DetailedMailboxHistoryPolicy
SupervisorRuntime:ActorThresholds
SupervisorRuntime:ProjectorThresholds
SupervisorRuntime:HistoryRetention
```

Configuration validation fails startup for invalid bounds when Supervisor Runtime is enabled. A missing optional
history store degrades historical capability and reports that status; it does not falsely report complete history.

## 21. Authorization and audit

Read-only health queries are available under the current application access policy. Future state-changing Supervisor
commands require explicit authorization distinct from ordinary reference-data editing.

Every accepted Supervisor command records:

- requester identity;
- command and operation IDs;
- target and requested action;
- UTC timestamps;
- result and failure detail; and
- resulting event identities.

The UI must never make restart, pause, retry, skip, or trading-control actions appear to be ordinary refresh actions.

## 22. Implementation structure

The intended projects and folders are:

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
      Operations/
      Incidents/
      History/
      Model/
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
    IActorMetricsStore.cs
    IActorSupervisorObservationContext.cs
    IActorRuntimeInstrumentation.cs
    ISupervisorManagedActorLifecycle.cs
    ISupervisorBootstrap.cs
  Metrics/
    ActorMetricsStore.cs
    ActorMailboxMetrics.cs
    ActorMetricsSnapshot.cs
    ActorMailboxMetricsSnapshot.cs
  SupervisorRuntime/
    SupervisorRuntimeContext.cs
    Actors/
    Mailboxes/
    Workers/
    Projectors/
    DurableQueues/

TomasAI.IFM.Application.Storage/
  Supervisor/
    schema and repository implementation

TomasAI.IFM.UI.Net.Models/
  Supervisor/

TomasAI.IFM.UI.Net.Services/
  Supervisor/

TomasAI.IFM.UI.Net.ViewModels/
  Supervisor/

TomasAI.IFM.UI.Net.Views/
  Supervisor/
```

The Supervisor actors, contexts, health policies, operations, logs, and domain metrics live in the dedicated
`TomasAI.IFM.Domain.Supervisor` project. Low-level actor-runtime instrumentation and the concrete process root remain in
`TomasAI.IFM.Shared/EventModelActor` so the framework never depends inward on the Supervisor domain. Shared messages and
read models live in `TomasAI.IFM.Domain.Supervisor.Shared`. Dependency direction takes precedence over folder symmetry.

## 23. Delivery increments

### Increment 1: Runtime foundation

- Add root contracts and singleton composition.
- Add `SupervisorRuntimeContext` access to every standard actor context.
- Add one actor-owned metrics store per actor and expose it through the actor contract.
- Add bounded mailbox metric entries keyed by `ActorThreadId`.
- Retain direct metric-entry references in entity queues so steady-state updates avoid dictionary lookup.
- Use atomic/volatile hot-path updates and lock-free Supervisor snapshot reads.
- Prohibit query fan-out to domain actor mailboxes during snapshot collection.
- Add actor catalog and lifecycle recording.
- Add entity-mailbox and worker snapshots.
- Bridge existing aggregate actor metrics without changing their public names.
- Add health/activity enums and policy evaluation.
- Add focused unit tests and allocation benchmarks.

### Increment 2: Supervisor query domain

- Add Supervisor query actor, context, extension handlers, service API, and bounded read models.
- Add root, tree, actor, mailbox, and worker queries.
- Add host-level fallback liveness.
- Verify the same singleton is embedded in all Supervisor contexts.

### Increment 3: Projectors and durable queues

- Register projector descriptors with the root context.
- Add cached JetStream consumer observations.
- Combine readiness, queue, execution, outbox, and checkpoint evidence.
- Add append-only replay-attempt history.
- Add projector/replay queries and failure detail.

### Increment 4: History

- Add bounded history writer, activity buckets, incidents, operations, and retention.
- Add UTC date-range queries with keyset pagination.
- Verify database unavailability cannot affect business-message outcomes.

### Increment 5: Actor Health UI

- Add Actor health beside Operations health.
- Remove the standalone Strategy observation toolbar button.
- Implement dark-theme tree/detail form, summary, filters, lazy loading, date range, and live refresh.
- Add Overview, Mailboxes, Activity, Failures, Projectors, and Replays tabs.
- Verify accessibility, resizing, bounded rendering, and unavailable-state behavior.

### Increment 6: Controlled operations

- Add Supervisor command and event actors.
- Add operation tracking, authorization, and audit.
- Introduce approved restart/retry/pause actions one at a time with integration tests.
- Keep the first Actor Health release read-only until this increment is accepted.

## 24. Verification strategy

### 24.1 Unit tests

- singleton and embedded-context identity;
- identical `SupervisorRuntimeContext` references across all standard actor-context kinds;
- one metric-store instance per actor;
- metric entry identity and isolation by `ActorThreadId`;
- lock-free snapshot enumeration during concurrent metric updates;
- actor/domain registration and hierarchy;
- actor and mailbox lifecycle transitions;
- accepted/dequeued/completed/failed counter balance;
- oldest-message age and processing-duration calculations;
- idle-is-healthy and expected-inactive-is-critical policies;
- parent health rollup with required and optional children;
- incident deduplication and recovery;
- bounded mailbox retirement;
- projector evidence combination and staleness;
- snapshot revision and partial-concurrency behavior;
- date-range validation and keyset continuation; and
- error-detail redaction and bounds.

### 24.2 Integration tests

- live actor registration through `ActorSupervisor` into the root context;
- message enqueue, schedule, processing, completion, and failure visibility;
- Supervisor snapshot collection without a query message to any domain actor;
- direct hot-path metric access and message-based coordinated Supervisor access;
- shared worker assignment without violating entity ordering;
- NATS JetStream process/replay consumer status;
- projector initial processing, deferred delivery, retry, recovery, and terminal failure;
- history persistence and query by UTC range;
- database outage while actor processing continues;
- Supervisor query actor timeout when the scheduler is unavailable;
- host fallback accurately reporting Supervisor unavailability; and
- no test host remaining after integration completion.

### 24.3 UI and system tests

- toolbar order and Strategy observation removal;
- dark trading theme;
- tree hierarchy and lazy child loading;
- green healthy idle, yellow processing/degraded, red failed, and grey not-expected rendering;
- queue badges and last-activity display;
- date-range propagation to every historical tab;
- selection preservation during refresh;
- projector/replay drill-down;
- comprehensive failure details;
- inaccessible service and stale snapshot states;
- large actor/mailbox sets without UI lockup; and
- proper disposal of refresh activity when the form closes.

### 24.4 Performance verification

- Benchmark actor mailbox transitions before and after instrumentation.
- Benchmark direct metric updates separately from intentional Supervisor message calls.
- Run allocation checks with no listener and with Actor Health open.
- Soak high-cardinality entity creation and retirement.
- Verify bounded memory under history-store outage.
- Verify no per-message database or NATS observability call.
- Verify no recurring exception is used to represent healthy idle or expected deferral.

## 25. Acceptance criteria

The initial actor operations and metrics ownership is complete when:

1. One `SupervisorRuntimeContext` exists per API server process.
2. All Supervisor actor contexts reference that exact singleton.
3. Every standard actor context can access the same singleton through the bounded runtime-context contract.
4. Every actor owns its current metrics and mailbox metrics keyed by `ActorThreadId`.
5. Steady-state metrics use direct atomic/volatile updates without Supervisor messages or blocking locks.
6. Supervisor snapshots read actor metrics directly without query fan-out to domain actor mailboxes.
7. Every registered domain actor appears under a stable domain hierarchy.
8. Current entity-mailbox depth and activity are visible without database polling.
9. Worker-pool capacity, utilization, and assignments are visible.
10. Healthy idle actors remain green.
11. Unexpectedly stopped or faulted required actors appear red with a reason.
12. Actor and mailbox failures include comprehensive bounded error and correlation details.
13. Projector detail combines readiness, durable queues, execution state, outbox, and checkpoints.
14. Replay attempts can be queried for an arbitrary bounded UTC date range.
15. The Actor Health form provides tree navigation and detail views in the dark trading theme.
16. The toolbar shows Actor health beside Operations health and no standalone Strategy observation button.
17. Actor monitoring adds no database polling loop and no observability message per business message.
18. Coordinated actor actions use Supervisor messages rather than direct core-supervisor mutation.
19. Hot-path allocation and latency remain within measured and approved limits.
20. Monitoring or history-storage failure cannot change a business operation's outcome.
21. Every mailbox-capacity rejection identifies the owning `ActorMailboxId`, `ActorThreadId`, verb, traffic class,
    depth, capacity, rejection reason, and incident age in the actor-owned snapshot and structured warning.
22. A continuously saturated entity mailbox emits no more than one warning per minute, becomes degraded after five
    minutes, and enters controlled restart after fifteen minutes.
23. Entity-mailbox restart closes admission, preserves or deterministically resolves accepted work, fences the old
    generation, and exposes its operation and outcome through the Supervisor snapshot.
24. A saturated mailbox and its owning actor cannot return to green until the configured recovery conditions are met.

## 26. Future evolution rules

New system-wide services may be added to `SupervisorRuntimeContext` when all of the following are true:

1. The service manages or observes more than one actor or domain.
2. Central ownership improves consistency, control, or operational visibility.
3. The service has a narrow named contract and bounded state.
4. Its failure cannot create a circular dependency that prevents basic health reporting.
5. Hot-path interaction satisfies Ring 2 allocation and latency requirements.
6. Commands, queries, and events retain their standard actor semantics.
7. Durable business truth remains in its owning domain or authoritative external store.

Each expansion updates this document's ownership map, contracts, health policy, storage, UI exposure, and verification
requirements before implementation.

## 27. Initial ownership summary

The first major ownership of `SupervisorRuntimeContext` is actor operations and metrics. Raw current metrics reside
inside each actor, with entity-mailbox records keyed by `ActorThreadId`. The singleton root reads those stores directly
without query fan-out and provides one central, queryable model of actor registration, lifecycle, mailboxes, workers,
message activity, failures, projectors, durable queues, replays, incidents, and history.

Every actor context receives bounded direct access to the same root for low-latency metrics and cached observations.
Rare coordinated actions use Supervisor command, query, and event messaging so mailbox serialization handles ordering
without shared operational locks. The design leaves a controlled path for later system-wide services without
requiring those future changes in the first implementation.

## 28. Actor exception containment and lifecycle-control clarification

### 28.1 Verified shared-worker behavior

The V2 actor scheduler does not permanently assign one worker to one `ActorThreadId`. An `ActorThreadId` identifies an
entity mailbox. A shared worker takes exclusive scheduling ownership of that mailbox and processes at most 64 messages
in one turn. If messages remain, the mailbox is rescheduled and may be processed by a different shared worker. The
scheduling bit preserves single-consumer entity ordering across that handoff.

When the entity mailbox becomes empty, its scheduling ownership is released. The entity mailbox object is retained as
part of the actor's warm working set while the actor remains within its configured retained-idle-mailbox allowance,
currently 1,024 by default. Beyond that allowance, an idle mailbox may be retired. The shared worker is returned to the
pool; it is not retired with the mailbox.

The operational model and UI therefore use these terms:

- **shared worker** for a member of the process worker pool;
- **entity mailbox** for the queue identified by `ActorThreadId`;
- **processing turn** for one worker's bounded batch; and
- **mailbox generation** for a future retire/recreate lifecycle fence.

### 28.2 Verified current exception behavior

The shared V2 worker catches exceptions escaping one actor message, records a failed-message metric, logs the failure,
disposes the message, and continues processing. It also catches a mailbox-level infrastructure exception, logs it,
restores its waiting state, and continues servicing other mailboxes. The worker becomes faulted only when an exception
escapes the outer ready-queue loop.

The base actor classes currently apply these inner boundaries:

| Actor kind | Current behavior after a processing exception |
| --- | --- |
| Command | Calls `OnExceptionAsync`, then attempts to return its `ServiceResult` |
| Query | Calls `OnExceptionAsync` or attempts a parsing-failure reply |
| Event | Calls `OnExceptionAsync`; no request reply is expected |
| Denormalizer | Calls `OnExceptionAsync` |
| Function | Converts failure to a typed terminal result and attempts a reply |
| Realtime | Behavior depends on its concrete implementation and requires inventory |

The worker remains available after an ordinary handler failure. The actor normally remains marked running, and the
entity mailbox continues with later messages.

This is useful fault containment, but it is not yet a complete operational guarantee:

- an exception handler can itself throw;
- final cleanup can replace or obscure the original exception;
- command/query/function reply failure can leave the caller waiting;
- an internally handled failure can be counted as processed by the outer worker;
- an ordinary handler failure does not automatically change actor health;
- comprehensive structured failure information is not guaranteed at every boundary; and
- the Supervisor does not yet receive a durable incident transition.

Cancellation requested by the actor runtime is an expected lifecycle outcome and must remain distinct from a genuine
failure. Fatal CLR/process failures such as stack exhaustion cannot be promised recoverable through application-level
exception handling.

### 28.3 Supervisor hardening requirement

Supervisor actors require a stronger boundary than ordinary business actors because they coordinate observation and
future recovery. Every recoverable failure in parsing, validation, execution, history, publication, cleanup, result
construction, and reply must be contained at the narrowest owning boundary, recorded once with complete structured
context, and converted into a deterministic outcome where a reply contract exists.

If a domain-specific exception handler fails, the base runtime must invoke a non-overridable last-resort boundary. That
boundary records both the original and secondary failures and attempts a bounded generic failure reply. Failure of the
last reply is recorded directly in `SupervisorRuntimeContext`; it must not recursively send an event to the failing
Supervisor actor.

The Supervisor must retain a host-level direct health/control boundary because a Supervisor query or command actor
cannot recover the shared scheduler if its own message is unable to run.

### 28.4 Actor and entity-mailbox lifecycle control

Current `ActorSupervisor.StartAsync(ActorMailboxId)` and `StopAsync(ActorMailboxId)` delegate directly to the actor.
They do not yet serialize concurrent start/stop requests, close entity-mailbox admission, fence old work by generation,
or guarantee that accepted messages have reached a safe boundary. A worker also does not use `IActor.IsRunning` as an
admission or execution fence.

Reliable lifecycle control requires:

```text
Registered -> Starting -> Running -> Pausing -> Draining -> Stopped
                                  \-> Quarantined
                                  \-> Faulted -> Restarting -> Running
```

Each operation has an operation ID, target identity, expected generation, timeout, and deterministic terminal result.
Operations against one target are serialized. Intake closes before drain. An executing handler is allowed to reach a
safe boundary, particularly after durable or external commit. The old generation is fenced before a new mailbox or
actor generation is opened.

Because `ActorThreadId` is an entity-mailbox identity, future fine-grained controls are named pause, drain, quarantine,
retire, and restart entity mailbox. Stopping a shared worker is not an entity operation and could affect unrelated
actors.

### 28.5 Implementation sequencing

Actor-owned metrics and direct Supervisor snapshots remain the first implementation. They must capture lifecycle,
generation, admission, current processing stage, commit-boundary status, queue depth, oldest age, primary failure, and
exception-handler failure so later lifecycle controls are observable before they are enabled.

Exception containment is the next actor-runtime hardening work package. Controlled actor and entity-mailbox restart is
enabled only after the exception boundary, operation serialization, safe drain, generation fence, and integration
tests are complete.

## 29. Baseline actor and entity-mailbox health policy

### 29.1 Purpose and application

This section defines the initial uniform operational baseline for every registered actor and entity mailbox. It is
actor-agnostic so the runtime first produces comparable evidence across the complete actor system. Domain-specific
thresholds and actions may be introduced only after representative measurements exist, and every override must expose
its configured value and reason in the Supervisor snapshot.

The baseline distinguishes an actor mailbox identified by `ActorMailboxId`, an entity mailbox identified by
`ActorThreadId`, a shared worker that temporarily processes an entity mailbox, and a saturation incident that preserves
evidence and elapsed time across repeated samples and rejections. `ActorThreadId` remains an entity-mailbox identity;
it is not an operating-system thread.

Health collection is observational. It never enqueues a health query into the actor or entity mailbox being observed.
The owning actor retains current atomic measurements, and `SupervisorRuntimeContext` reads those stores directly and
rolls entity-mailbox health up to actor, domain, and host health.

### 29.2 Baseline configuration defaults

The following defaults apply to every entity mailbox until explicitly overridden:

| Policy | Default | Explanation |
| --- | ---: | --- |
| Mailbox capacity | Existing configured capacity; currently 2,048 in the API host | The actual runtime capacity is reported per mailbox rather than duplicated as a health setting. |
| Elevated depth | 75% for 30 seconds | Gives early evidence of a growing backlog without declaring a progressing actor unhealthy. |
| Critical depth | 90% for 30 seconds | Captures context while some burst headroom remains. |
| Limit trigger | At capacity or a `mailbox_limit` rejection | A rejection is authoritative even if a concurrent dequeue lowers the next snapshot. |
| Pressure warning | At most once per mailbox per minute | The first limit event is immediate; later evidence is coalesced to prevent a log storm. |
| Degraded | Saturated for 5 minutes | Sustained inability to accept work degrades the entity mailbox and actor. |
| Restart | Saturated for 15 minutes | Requests controlled restart of only that entity mailbox. |
| Saturation recovery | Below 90%, no rejection, and dequeue progress for 60 seconds | A momentary dequeue cannot erase an incident that is still rejecting. |
| Health recovery | Below 75%, no rejection, and progress for 2 minutes | Hysteresis prevents status flapping. |
| Nonempty no-progress warning | No dequeue completion for 60 seconds | Detects a queued mailbox not being serviced. |
| Handler-duration warning | One handler active for 60 seconds | Identifies potentially blocked or unexpectedly expensive work without cancelling it. |
| Repeated-failure summary | At most once per mailbox per minute | Equivalent failures are counted and summarized. |
| Healthy idle retention | 1,024 entries per actor | Preserves the bounded warm set; active incidents and failures are not healthy-idle eviction candidates. |
| In-memory incidents | 10,000 incidents or 24 hours | Supplies useful recent evidence with a deterministic memory bound. |
| Actor-health collection and rollup | 60 seconds for every managed actor after healthy startup | Establishes one narrow, uniform business-actor heartbeat; critical market-feed monitoring remains separate. |
| Historical retention | 7 days of minute rollups; 30 days of incident summaries | Provides an initial comparison window. |
| Restart eligibility | Once for every qualifying saturation incident | Automatic recovery remains available throughout the nearly 24-hour trading day. A new restart cannot overlap an existing lifecycle operation or bypass generation fencing. |
| Repeated-restart critical threshold | 2 restarts in 60 minutes or 4 in 24 hours | Repeated recovery indicates an unresolved fault. It creates or updates a critical incident and requests trader review, but does not disable later safe automatic restarts. |
| Post-restart observation | 2 minutes | The mailbox remains `Recovering` while progress, rejection, and backlog behavior are verified. |

Percentage boundaries use `ceiling(capacity * percentage)`. Validation requires elevated below critical, critical at or
below capacity, degradation before restart, and recovery thresholds below their entry thresholds.

### 29.3 Required entity-mailbox measurements

Every actor owns one bounded `ActorMailboxMetrics` entry for every retained `ActorThreadId`. Its snapshot contains:

- identity: `ActorMailboxId`, `ActorThreadId`, domain, implementation, mailbox generation, and host/runtime generation;
- capacity: current depth, configured capacity, peak depth, remaining slots, and utilization;
- age/progress: oldest queued timestamp/age, scheduled time, last dequeue/completion, and interval rates/deltas;
- outcomes: accepted, dequeued, succeeded, handled-failed, escaped-failed, cancelled, and rejected totals;
- rejection evidence: bounded counts by reason, first/latest rejection time, latest verb, traffic class, safe subject,
  and saturation-incident ID;
- execution: current verb, processing start/duration/stage, commit-boundary state, shared worker, trace ID, and
  correlation ID when available;
- latency: bounded queue-wait, handler-duration, and end-to-end histograms or interval summaries;
- failure evidence: latest bounded exception type, code, stage, severity, message, identities, and failure/recovery time;
- lifecycle: admission, lifecycle and health states, explicit reasons, restart count/outcome, and revision; and
- consistency: observation time and `Consistent`, `PartiallyConcurrent`, or `Unavailable` snapshot quality.

Payloads, credentials, unrestricted exception text, and sensitive entity values are not retained. Identities use safe
structured representations and redaction. The queue retains a direct reference to its metric entry. Hot-path updates
are atomic/volatile and do not allocate a health event, take a global lock, serialize, access storage, or send a
Supervisor message per business message.

### 29.4 Saturation incident state machine

```text
Normal
  -> Elevated (>= 75% for 30 seconds)
  -> Critical (>= 90% for 30 seconds)
  -> AtLimit (capacity reached or mailbox_limit rejection)
  -> Degraded (incident active for 5 minutes)
  -> Restarting (incident active for 15 minutes and restart is safe)
  -> Recovering (below 90%, no rejection, and progress for 60 seconds)
  -> Healthy (below 75%, no rejection, and progress for 2 minutes)
```

States may be entered directly: a rejection enters `AtLimit` without waiting for elevated/critical timers. The clock
starts at the first capacity observation or rejection and is not reset by a momentary dequeue. A limit event during
recovery returns to `AtLimit` and continues the incident clock. The incident closes only after saturation recovery. An
empty mailbox needs no artificial progress: 60 seconds empty without rejection satisfies saturation recovery; it may
return green after the two-minute health-recovery interval.

### 29.5 Health rollup and readiness policy

- `Normal`, `Elevated`, `Critical`, and `AtLimit` younger than five minutes remain green for rollup while pressure is
  still visible.
- Five minutes saturated, five minutes nonempty without progress, or an unavailable snapshot is yellow/degraded.
- Restarting, repeatedly restarting, unexpected stop, or unsafe/unresolved restart is red.
- Planned stop/drain or an actor not expected to run is grey and does not degrade its parent.

Actor health is the most severe mailbox, lifecycle, or failure health. Domain health is the most severe actor, and host
actor health is the most severe domain. Every rollup includes child counts and causal `ActorThreadId` values.

During baseline collection, yellow does not change host readiness or trading behavior. Red makes `/health/actors`
unhealthy but does not automatically halt trading. Readiness and trading-safety consequences require later explicit
required-actor policy; this never suppresses warnings, incidents, snapshots, or restart safeguards.

### 29.6 Structured warning and incident policy

Reaching the hard limit emits an immediate structured warning. During the same incident, no more than one pressure
warning per `ActorThreadId` is emitted per minute. It summarizes deltas rather than logging every rejected input.

Every pressure warning includes host/runtime generation, domain, actor/thread IDs, mailbox generation, incident ID and
age, health and next deadline, current/peak depth, capacity, utilization, oldest age, accepted/dequeued/completed/
failed/cancelled/rejected deltas, rejection reason/verb/traffic class/safe subject, current processing information,
worker assignment, last progress, ready-queue and worker state, process CPU, .NET ThreadPool queue, allocation rate, GC
pause context, memory pressure, and trace/correlation identity when available.

First limit, degraded, restart requested/started/completed/failed, recovering, and recovered are separate transition
events and are not suppressed by the periodic limiter. Logging failure cannot block processing or alter a business
outcome. Metrics use bounded dimensions such as actor type, traffic class, outcome, and reason. Entity, subject,
message, and trace identities stay in snapshots, traces, and structured logs rather than metric dimensions.

### 29.7 Diagnosing the cause of saturation

The snapshot must distinguish likely causes without inference from generic warnings:

| Evidence | Plausible cause |
| --- | --- |
| One entity grows while siblings and workers are healthy | Hot entity or handler throughput below arrival rate |
| Long current handler with available workers | Blocked or computationally expensive handler |
| Scheduled mailbox waits without worker assignment | Ready-queue delay, worker starvation, or scheduling defect |
| Many actors grow with busy workers and high CPU | Insufficient worker/CPU capacity or cross-actor blocking |
| Sudden enqueue spike after NATS recovery | Transport backlog or redelivery burst |
| Enqueue rate unexpectedly exceeds source rate | Duplicate subscription, retry loop, or feedback publication |
| Rejections continue while depth briefly falls | Arrival remains above dequeue rate; the incident has not recovered |
| Nonempty depth without a scheduling timestamp | Lost scheduling signal or scheduler defect |
| GC pause/allocation coincides with ready-queue growth | Runtime memory pressure delaying processing |

The Actor Health UI shows evidence and timestamps, not only a color. Operators navigate
`Domain -> ActorMailboxId -> ActorThreadId` to see current measurements, incident transitions, restart history, and
bounded failure evidence.

### 29.8 Controlled entity-mailbox restart policy

After one incident remains saturated for fifteen minutes, `SupervisorRuntimeContext` requests controlled restart of
that `ActorThreadId`; it does not stop a shared worker or restart sibling entity mailboxes. The operation has an ID,
expected actor/mailbox generation, reason, incident ID, timestamps, deadline, and deterministic outcome.

A restart must:

1. serialize lifecycle operations for the target;
2. compare the expected generation and reject stale operations;
3. close admission for the old generation;
4. identify accepted queued and executing work and its delivery guarantees;
5. let the executing handler reach a known safe commit boundary;
6. drain, replay, defer, or deterministically fail every accepted item under its delivery contract;
7. fence and retire the old generation so it cannot later publish or commit;
8. create the next generation with fresh queue/scheduling state;
9. reopen admission and verify dequeue/completion progress; and
10. retain the incident and restart outcome through recovery.

Restart never silently discards accepted commands, queries, durable events, or externally committed work. If safe
drain, replay, or deterministic completion cannot be proven, restart is not performed: the mailbox and actor become
red, the reason is logged, and intervention is required. Safety takes precedence over the fifteen-minute target.

Every distinct qualifying saturation incident may initiate one safe automatic restart, at any time of day. There is no
daily restart quota and no trading-hours suppression. The runtime only prevents overlapping lifecycle operations and
duplicate restart requests for the same incident/generation. If the mailbox saturates again after recovery, the new
incident follows the same five-minute degradation and fifteen-minute restart policy.

Two restarts in a rolling 60 minutes or four in a rolling 24 hours mark the mailbox and actor red and create or update a
critical repeated-restart incident. This threshold is diagnostic, not a circuit breaker: safe automatic recovery stays
available. The Actor Health view prominently shows restart counts and timestamps, time since last stable processing,
the causal incidents, recovery results, and recommended manual actions. Successful restart enters `Recovering` for at
least two minutes; it does not erase counters or immediately restore green.

Authorized manual actions during trading hours include acknowledging the incident, capturing diagnostics, pausing or
resuming admission, requesting a controlled restart, draining or quarantining the affected entity mailbox, and viewing
the status/outcome of the operation. Every action requires an operator identity and reason, is audited, uses the same
safe lifecycle and generation fences as automatic recovery, and never operates on the shared worker or unrelated
mailboxes. The UI remains read-only until those command, authorization, audit, and safety contracts are implemented and
accepted; until then it displays the action recommendation and complete evidence needed for an external operator.

Automatic restart remains disabled until exception containment, serialized lifecycle operations, admission closure,
safe drain, generation fencing, and section 29.11 tests exist. Until then, fifteen minutes creates the red incident and
restart-required operation record but does not perform an unsafe stop/start.

### 29.9 Message delivery baseline

Every actor message declares a bounded delivery class:

| Class | Baseline behavior at the mailbox limit |
| --- | --- |
| Required | Never silently dropped; use bounded backpressure or durable deferral/replay and record delay. |
| Important | Reject only with an observable outcome and recover through retry, replay, or source reconciliation. |
| Optional | May be rejected by declared overload policy, but attribute every rejection to the exact `ActorThreadId` and incident. |

Unclassified traffic defaults to `Required`. Making traffic `Optional` requires explicit registration and an explanation
of downstream correctness after loss. Events that advance a watermark, close an aggregation period, or trigger required
domain progress must be Required or recoverable from an authoritative source; they cannot be best-effort only.

### 29.10 History, bounds, and privacy

Current state is bounded by the retained-mailbox policy. Active incidents, red mailboxes, and recent restarts are not
evicted as healthy idle entries. At the incident bound, recovered history is compacted first; an overflow counter and
warning disclose lost diagnostic detail.

Minute rollups retain rates, depths, ages, timing summaries, health durations, rejection reasons, and restart outcomes.
Incident summaries retain transitions and bounded evidence. Neither retains payloads, credentials, account secrets,
unrestricted exception text, or high-volume trace data. API/UI access follows operational authorization. Detailed
traces remain in the trace system and are correlated by ID rather than copied into Supervisor memory.

### 29.11 Verification requirements

The baseline is complete only when tests prove:

- one hot `ActorThreadId` saturates without misidentifying siblings;
- capacity logs immediately and repeated warnings are limited to one per minute per incident/mailbox;
- concurrent dequeue cannot lose a rejection or prematurely reset the incident clock;
- five minutes of continuous saturation degrades the mailbox and actor;
- fifteen minutes requests restart for only the affected mailbox;
- recovery hysteresis restores health only after sustained progress;
- slow/stuck handlers, lost scheduling, worker starvation, transport bursts, duplicate delivery, and GC pauses produce
  distinguishable snapshots;
- required traffic is never silently discarded and optional rejection is attributed to the exact mailbox;
- snapshots remain available while target and Supervisor actor mailboxes are saturated;
- restart safely handles queued work, pre-commit and post-commit handlers, stale generations, cancellation, timeout,
  failure, duplicate/overlapping requests, repeated incidents, and continuous trading-hour availability;
- repeated-restart thresholds create critical evidence and trader-review guidance without disabling safe automatic
  recovery, and authorized manual actions are audited and generation-fenced;
- actor/domain/host rollups identify the causal `ActorThreadId` and preserve incident/restart evidence;
- current state, history, logs, metrics, traces, API output, and UI apply bounds and redaction; and
- BenchmarkDotNet, allocation profiles, lock-contention events, latency histograms, `dotnet-counters`, and
  `dotnet-trace` confirm observation stays within measured hot-path budgets.

Representative burst, sustained-load, NATS-recovery, and paper-trading soaks establish the initial baseline. The 75%,
90%, one-minute, five-minute, and fifteen-minute defaults remain until evidence justifies an actor-specific override.

## 30. System-wide actor logging standard

### 30.1 Purpose and scope

Every base actor implementation provides the same minimum structured processing evidence. Domain actors may add
domain-specific logs, but those logs supplement and never replace the base entry, exit, and exception records. The
standard applies to command, query, event, realtime, denormalizer, function, Supervisor, and future actor kinds, and to
every message outcome including parse failure, validation failure, cancellation, handled failure, escaped failure,
reply failure, and successful completion.

The base standard answers, without reconstructing free-form text:

- exactly which actor mailbox and entity mailbox processed the message;
- which verb and typed message were processed;
- when processing entered and exited the base actor boundary;
- the terminal outcome and processing stage;
- how long processing took; and
- which exception and correlated Supervisor failure record explain an unsuccessful outcome.

### 30.2 Complete actor-message identity

Every base actor log call carries the complete routed identity as separate structured properties:

| Property | Source |
| --- | --- |
| `ActorType` | `ActorSubject.ThreadId.ActorType` |
| `ActorName` | `ActorSubject.ThreadId.Name` |
| `EntityId` | `ActorSubject.ThreadId.EntityId` |
| `Verb` | `ActorSubject.Verb` |

Together these fields are the full operational actor-message identity. `ActorMailboxId` is `{ActorType, ActorName}` and
`ActorThreadId` is `{ActorType, ActorName, EntityId}`. Logs may additionally carry their safe canonical representations,
but never replace the four searchable fields with one formatted string. Identity comes from the validated routing
subject and is available even when typed payload parsing fails.

All records also carry `MessageType`, `ActorKind`, mailbox generation, host/runtime generation, traffic class, delivery
class, trace ID, span ID, and correlation/command/event ID when available. Raw payloads and sensitive entity values are
never logged; configured identity redaction is applied before the base logger receives a value.

### 30.3 Mandatory records for each processed message

The base processing boundary emits at least two Information records for every dequeued message:

1. `ActorMessageEntry` after routing identity is validated and the message has entered the base processing pipeline;
2. `ActorMessageExit` exactly once from the outer `finally` boundary after processing reaches its terminal outcome.

`ActorMessageEntry` contains the full identity, typed message name when parsing succeeded, entry stage, queue-wait
duration, attempt/delivery information, and correlation fields. When typed payload parsing later fails, the entry still
identifies the routed subject and reports the initial message/envelope type.

`ActorMessageExit` contains the same identity and correlation fields plus `Outcome`, terminal `FailureStage`,
`ProcessingElapsedMilliseconds`, queue-wait duration, delivery outcome, reply outcome where applicable, commit-boundary
state, and Supervisor failure ID when one exists. It is emitted for success, handled failure, escaped failure,
cancellation, parsing failure, and cleanup/reply failure. Cleanup cannot suppress the exit record or replace the
primary outcome.

Any exception additionally emits one Error record at the narrowest owning base boundary. The exception record includes
the same full identity, elapsed time at failure, failure stage, exception type, error/HResult, bounded safe message,
primary and secondary failure IDs, and correlation fields. A handled exception remains an Error record with
`Outcome=HandledFailure`; handling does not make the original failure successful. Secondary exception-handler, cleanup,
publication, or reply exceptions receive their own error record linked to the primary failure ID.

Expected lifecycle cancellation may use Information when it is genuinely caused by a recorded shutdown/drain request.
Unexpected cancellation uses Warning or Error according to its outcome. Warning is reserved for abnormal conditions
that did not throw, such as rejected admission, prolonged processing, or recovery transitions.

### 30.4 Elapsed-time semantics

The base boundary obtains a monotonic timestamp with `Stopwatch.GetTimestamp()` before emitting entry and calculates
elapsed time at exception and exit with `Stopwatch.GetElapsedTime(startTimestamp)`. It does not use wall-clock
subtraction. `ProcessingElapsedMilliseconds` measures the complete base processing boundary, including parsing,
validation, state load/replay, handler execution, persistence, publication, reply, and cleanup as applicable.

Queue wait is measured separately from accepted/enqueued time to dequeue/entry time. Domain actors may report narrower
stage timings, but must not redefine the base elapsed field. Timing values are numeric structured properties, not
formatted duration strings.

### 30.5 Compiled structured logging

Base actor logs use source-generated `[LoggerMessage]` partial methods, or precompiled `LoggerMessage.Define` delegates
only where source generation cannot be used. The standard templates, property names, event IDs, and levels are declared
once in a shared base-actor logging class. Actor processing code calls typed methods and never declares or interpolates
a template per message.

The initial stable event contract is:

| Event ID | Name | Level | Required purpose |
| ---: | --- | --- | --- |
| 4100 | `ActorMessageEntry` | Information | Message entered the base processing pipeline |
| 4101 | `ActorMessageExit` | Information | Message reached one terminal base outcome with elapsed time |
| 4102 | `ActorMessageException` | Error | Primary parsing/validation/execution/persistence/publication failure |
| 4103 | `ActorMessageSecondaryException` | Error | Exception handling, cleanup, reply, or observation also failed |
| 4104 | `ActorMessageWarning` | Warning | Abnormal non-exception processing or admission condition |

Template property names and meanings are versioned contracts. Domain logging uses a separate allocated Event ID range
and does not reuse these IDs. Templates contain constant text only; values are passed as typed arguments. Logging calls
must not create dictionaries, anonymous objects, interpolated strings, `ToString()` actor identities, or serialized
payloads on the hot path.

### 30.6 Base-boundary ownership

Entry/exit/error emission belongs in the non-overridable outer processing boundary shared by the base actors, not in
individual domain `ReceiveAsync` implementations. Each actor kind adapts its stages to the same outcome model. One
message has one base entry and one base exit even when it invokes multiple virtual hooks. Domain hooks cannot disable,
duplicate, or claim completion of the base records.

The base boundary establishes log scope/correlation once, records the metric transition, emits entry, executes the
pipeline, records exceptions at their owning boundary, determines the final outcome, emits exit in `finally`, and then
disposes the message. If logging itself fails, processing continues and a bounded logging-failure counter is incremented;
logging never changes the business result.

### 30.7 Domain logging rules

Domain actors add logs only for meaningful domain decisions, state transitions, external effects, or diagnostic facts
not present in the base records. Domain logs inherit the established actor scope and therefore do not repeat ad hoc
identity formatting. They use source-generated or precompiled templates, stable event IDs, structured values, bounded
text, and the same redaction rules.

Domain logs must not log entire commands, queries, events, state objects, database rows, market-data payloads,
credentials, account identifiers without approved masking, or exception `ToString()` as a separate property. Routine
method-entry/method-exit logging inside domain handlers is prohibited unless it represents a measured named stage; the
two system-wide base records already define the processing boundary.

### 30.8 Sink, retention, and overload behavior

Two Information records per actor message can be high volume. This is an explicit initial baseline and is not sampled
or omitted by actor type. Production logging uses a nonblocking asynchronous sink with bounded buffering, batching, and
rolling retention. Actor workers never wait for disk or network log delivery. Sink pressure is measured through queued,
dropped, flushed, and failure counts and appears in operational health.

The system must distinguish actor-message rejection from log-record loss. If the logging buffer is full, business
processing continues, a bounded counter records lost records by event name/level, and one rate-limited critical logging
health warning is emitted through an independent fallback where possible. Error/critical records receive preferential
retention, but preserving them must not block actors indefinitely.

Before reducing the two-Information-record baseline, representative evidence must quantify log volume, allocation,
latency, storage, and diagnostic value. Any later sampling policy is a documented system-wide policy and never silently
removes exception, limit, degradation, restart, or recovery records.

### 30.9 High-frequency Information suppression

The base standard emits one Information entry and one Information exit for Command and Query messages. Event and
Realtime actor messages suppress both routine Information records by default because their fan-out and market-data
frequency make per-message base logging operationally unsafe. API startup compiles an immutable set of suppressed actor
types plus explicitly suppressed routes. Route matching is exact on {ActorType, ActorName, Verb}; entity IDs, wildcards,
runtime mutation, and sampling are not supported. Command or Query routes may be added to SuppressedRoutes when
measured evidence justifies quieter operation. Invalid, incomplete, or duplicate configuration fails startup before
the Supervisor and domain actors are constructed.

Suppression never changes actor execution, metrics, tracing, admission, health evaluation, or failure capture. Failed
and cancelled messages still emit entry and terminal exit records, and Warning, Error, Critical, exception, mailbox
pressure, degradation, restart, and recovery evidence is never suppressed. The policy lookup is frozen and performs no
per-message allocation.

Logging duration and optional metric duration use independent monotonic start timestamps. Disabling the handler
duration instrument may cause its metric start value to be zero, but an emitted logging duration must always use a
valid Stopwatch timestamp captured at the base processing boundary. This prevents elapsed log values from representing
process uptime.

### 30.10 Logging verification

Tests and benchmarks verify:

- every actor kind emits exactly one entry and one exit for every processed message and includes all four identity
  fields;
- success, parsing failure, validation failure, handled/escaped failure, cancellation, reply failure, and cleanup
  failure produce the correct outcome, stage, exception linkage, and elapsed time;
- exception-handler failure cannot suppress the primary exception or final exit;
- templates are source-generated/precompiled and no interpolated/template string is allocated per call;
- disabled-level calls are allocation-free apart from unavoidable caller state, and enabled logging stays within the
  measured budget of the configured provider;
- payloads and sensitive values never appear in base logs;
- asynchronous sink saturation cannot block or alter message processing and exposes log-loss health; and
- entry/exit records correlate with actor metrics, traces, and Supervisor failure IDs.

## 31. Supervisor actor health-management authority

### 31.1 Initial responsibility and evolution

The logical Supervisor actor is the single application authority for system-wide actor health management and mutable
actor operations. Its initial responsibility is deliberately narrow: collect and evaluate actor/mailbox/worker health,
retain bounded incidents, serve Actor Health queries, and coordinate safe lifecycle actions required by the baseline
policy. It does not own business-domain state or make trading decisions.

The framework retains standard actor types rather than adding `ActorType.Supervisor`. The logical Supervisor boundary is
implemented through `SupervisorCommandActor`, `SupervisorQueryActor`, and `SupervisorEventActor` with one shared
`ISupervisorActorContext`. This permits future Supervisor commands, queries, and events without giving ordinary actors
direct access to the core runtime. `SupervisorRuntimeContext` remains the host-level state/control root so health stays
observable when a Supervisor actor mailbox itself cannot run.

### 31.2 Verified current exposure and migration inventory

The current implementation exposes more authority than ordinary actors require:

- `ICommandActorContext`, `IQueryActorContext`, `IEventActorContext`, and `IFunctionActorContext` expose the concrete
  mutable `SupervisorRuntimeContext` through `SupervisorRuntime`;
- that concrete context publicly permits global snapshot capture, projector registration, and failure recording for an
  arbitrary supplied `ActorMailboxId`/`ActorThreadId`;
- assembly-internal runtime methods register/remove actors and workers, serialize mailbox operations, and start, stop,
  or restart actors;
- `ActorSupervisor` uses that context for actor registration and lifecycle operations;
- the worker pool and workers register worker state and record failures directly;
- base actor lifecycle and processing boundaries record failures through the same concrete context;
- the API reads the global snapshot directly for `/api/actor-health`; and
- many domain context adapters expose the complete `IActorSupervisor`, whose contract includes actor/thread
  registration, mutable thread state, mailbox creation, lifecycle control, producer/consumer/router mutation, runtime
  readiness, transport startup/shutdown, and direct access to children, thread state, container, and thread pool.

Current searches show ordinary domain actors primarily depend on these references for runtime plumbing and message
production rather than legitimate system-wide lifecycle policy. Nevertheless, exposing the concrete objects makes
accidental mutation and authority growth possible. The migration must inventory each compile-time use, classify it as
own-actor observation, actor messaging, runtime infrastructure, host lifecycle, operational query, or privileged
control, and replace it with the narrow contract for that category. No compatibility property may return the concrete
runtime or `IActorSupervisor` to an ordinary actor context.

### 31.3 Capability-separated contexts

The target separates five capabilities:

| Contract | Consumer | Authority |
| --- | --- | --- |
| `IActorSupervisorObservationContext` | Every non-Supervisor actor context | Record and read only the owning actor/thread observations and immutable cached policy |
| `ISupervisorActorContext` | Supervisor command/query/event actors only | Read all managed-actor health and own the managed-actor lifecycle coordinator |
| `ISupervisorManagedActorLifecycle` | Exposed only through `ISupervisorActorContext` | Resolve, construct, register, bind, start, monitor, stop, restart, recycle, and unregister all non-Supervisor actors |
| `IActorRuntimeInstrumentation` | Base runtime, mailbox, scheduler, worker pool, lifecycle guard | Register runtime components and record trusted low-level transitions |
| `ISupervisorBootstrap` | Host startup/shutdown composition only | Bootstrap and finally stop only the Supervisor actors and provide emergency process teardown |

The interfaces may be backed by one internal state root, but capability separation is enforced by DI registration,
constructor types, assembly visibility, and architecture tests. Downcasting or service-location of a broader context is
prohibited.

### 31.4 Restricted ordinary-actor context

Every non-Supervisor actor receives an `IActorSupervisorObservationContext` scoped permanently to its own
`ActorMailboxId`. It provides only:

- an atomic own-mailbox recorder selected internally from the routed `ActorThreadId`;
- recording of own processing stage, success, handled/escaped failure, cancellation, delivery outcome, and bounded
  correlation evidence;
- reading immutable own-actor policy such as delivery class, health thresholds, and shutdown token;
- reading its own current health/incident identifier when required for diagnostic correlation; and
- a narrow nonthrowing failure sink that supplies the actor identity rather than accepting an arbitrary actor ID.

It does not expose global snapshots, other actors, mutable actor/thread state, projector or worker registration,
mailboxes, producers, consumers, routers, DI container, thread pool, readiness mutation, start/stop/restart/pause/resume,
incident mutation, health-policy mutation, or arbitrary `ActorMailboxId`/`ActorThreadId` writes. An own-thread recorder
validates that actor type/name match its scoped actor and rejects spoofed identities without changing business outcome.

Ordinary actors communicate with other actors exclusively through the standard NATS actor producer/client contracts.
They never use `IActorSupervisor` as a service locator or direct actor invocation path. Domain-specific logging and
metrics remain local observations; they do not grant control authority.

### 31.5 Privileged Supervisor actor context

`ISupervisorActorContext` is constructed only for the Supervisor actor trio and contains explicit named capabilities:

- execute the single managed-actor `StartupActorsAsync` and `ShutdownActorsAsync` transactions;
- start, monitor, stop, restart, and recycle individual non-Supervisor actors through the same lifecycle coordinator;
- capture current topology and lock-free health snapshots for every actor, entity mailbox, worker, projector, and
  durable operational source;
- query bounded incident, activity, restart, and operation history;
- evaluate and update actor/mailbox health state and incident transitions;
- request serialized pause, drain, resume, quarantine, retire, actor restart, or entity-mailbox restart operations;
- inspect operation status and deterministic outcomes;
- update health policies through validated, authorized commands;
- append audited operator acknowledgements and action reasons; and
- publish Supervisor health, incident, operation, and recovery events.

The privileged context does not return mutable actor objects, queue objects, worker objects, dictionaries, container,
or unrestricted delegates. Reads return immutable snapshots. Writes are named operations with target ID, expected
generation, operation/incident ID, requester, reason, authorization evidence, timeout, cancellation, and audited
outcome. Even the Supervisor actor cannot bypass safe drain, commit-boundary, or generation-fence rules.

Only Supervisor commands mutate health policy or request runtime control. Supervisor queries are read-only. Supervisor
events update bounded observation/history projections or notify other operational components and cannot smuggle direct
runtime mutation through event handlers.

### 31.6 Runtime, Supervisor lifecycle, and host-bootstrap authority

Low-level hot-path measurement is not routed through Supervisor actor mailboxes. The trusted runtime uses
`IActorRuntimeInstrumentation` to bind metric entries and record transitions. Registration/removal primitives remain
internal implementation mechanisms and are invoked only by the Supervisor managed-actor lifecycle coordinator or the
minimal Supervisor bootstrap. The instrumentation interface is unavailable to ordinary domain code.

The host uses `ISupervisorBootstrap` only to start the Supervisor actors, obtain their privileged context, and finally
stop them after managed shutdown. Normal host startup makes one `StartupActorsAsync` call; normal host shutdown makes
one `ShutdownActorsAsync` call. Actor discovery, construction, registration, producer/consumer/router binding,
readiness, intake, drain, stop, recycle, and managed cleanup no longer live in API startup code.

Emergency process teardown remains available if the Supervisor domain is unavailable. It is a last-resort host safety
boundary, not an alternate normal lifecycle path, and records the bypass and incomplete managed-shutdown evidence.

`IActorSupervisor` is decomposed so its current mixed registry, routing, producer, consumer, state, lifecycle, container,
and thread-pool surface is no longer a domain dependency. Runtime implementation may retain a private coordinating
class, but ordinary domain contexts receive neither that class nor its interface.

### 31.7 Periodic health collection

A singleton `SupervisorActorMetricsPollingService`, owned by the Supervisor domain but hosted independently of all actor
mailboxes, is the final core observability function. Its responsibilities are intentionally restricted to obtaining the
current immutable managed-actor list, reading each actor's lock-free metrics snapshot, storing the latest aggregate
directly in the privileged Supervisor context's in-memory state, and attempting one compiled summary log every 60
seconds. It performs no database access, NATS calls, actor messages, lifecycle action, policy mutation, history query,
UI work, or domain calculation.

The service runs on one dedicated named operating-system thread created specifically for this loop, rather than a
ThreadPool work item, `Task.Run`, timer callback, or `PeriodicTimer` continuation. The initial defaults are:

| Setting | Default | Reason |
| --- | ---: | --- |
| Thread name | `IFM.Supervisor.ActorMetricsPoller` | Makes the core health loop identifiable in dumps and traces |
| Thread type | Dedicated background thread | Isolates polling progress from ThreadPool starvation while allowing process shutdown |
| Thread priority | Normal | Avoids pre-empting higher-priority critical market-data feed monitoring |
| Poll and mandatory log-attempt cadence | 60 seconds | Provides a simple business-actor heartbeat with predictable cost |
| Actor traversal | Sequential immutable actor-reference snapshot | Avoids tasks, continuations, parallel coordination, and collection overlap |
| Overlapping cycles | Never | One dedicated loop cannot start a second cycle while the first is active |
| Overrun behavior | Log the overrun and immediately rebase the next deadline | Never creates a backlog of missed timer callbacks |
| Current snapshot retention | Latest plus previous stored snapshot | Enables interval deltas without unbounded sample history |
| History rollup | 60 seconds for all managed actors | Uses the completed heartbeat snapshot and is not accelerated by UI activity |
| Actor-specific cadence overrides | Disabled initially | The base release treats all actors uniformly; a later measured policy may explicitly override selected actors |
| Clock | Monotonic elapsed time plus UTC observation timestamps | Timers remain correct across wall-clock adjustments |

On each monotonic deadline, the thread reads the atomically stored managed-actor reference array and iterates it in a
stable order. For each actor it directly calls the lock-free metric snapshot contract and copies bounded scalar/thread
records into the cycle builder. One actor failure is caught at the actor boundary, recorded in the cycle result and
exception log, and iteration continues with the next actor. After traversal, the service captures bounded worker/runtime
evidence, atomically replaces the privileged context's immutable latest snapshot, updates its heartbeat counters, and
attempts exactly one compiled `SupervisorActorMetricsPollCompleted` Information log for the cycle, including zero-actor
and partial cycles.

The stored snapshot and summary log include cycle/revision, start/end/elapsed, next deadline, actors expected/collected/
failed, entity mailboxes observed, actors healthy/degraded/critical/unknown, aggregate depth/rejections/failures,
snapshot quality, prior snapshot age, poller thread identity, and whether exception-log fallback was required. The log
does not enumerate every actor or serialize the snapshot.

`ISupervisorActorContext.ActorMetrics` exposes the current immutable value to the three Supervisor actors:

```text
SupervisorActorMetricsSnapshot Latest { get; }
bool TryGetActor(ActorMailboxId actorId, out SupervisorActorMetrics actorMetrics)
```

The backing `SupervisorRuntimeContext` owns one volatile reference to this immutable snapshot. The polling thread builds
the next value privately and replaces the reference with one atomic/volatile write before attempting any normal log,
event, history, policy, API, or UI work. No NATS publication, actor message, database operation, or Supervisor mailbox is
required to store or read it. If the Query, Command, or Event Supervisor actor, logging provider, history store, UI, or
health-policy evaluator is unavailable, the latest collected value remains directly readable from the special context.

Every successfully read actor snapshot supplies its current health state. The poller performs only a count by the four
bounded states `Healthy`, `Degraded`, `Critical`, and `Unknown`; it does not execute lifecycle control or domain policy.
An actor whose snapshot cannot be read is counted once in both `FailedActors` and `UnknownActors`, never as healthy.
The four health counts must sum to `CollectedActors + FailedActors`, and the heartbeat log records that invariant.

Every recoverable operation is enclosed by both a narrow per-actor exception boundary and a final per-cycle exception
boundary. A cycle exception is written with a compiled Error record to the normal logger and the dedicated exception-log
sink, after which the loop stores a partial/stale heartbeat summary and continues to the next deadline. Logger
exceptions are also caught. If the normal logger fails, the poller attempts the exception-log fallback and increments a
host-level lost-log counter. No recoverable exception may escape and terminate the thread.

No application can guarantee execution or durable logging after process termination, fatal CLR failure, operating-system
failure, storage exhaustion, or failure of every log destination. The enforceable requirement is that while the process
and dedicated thread are schedulable, every cycle makes a primary summary-log attempt, every caught failure makes an
exception-log attempt, and logging failure cannot terminate the loop.

The poller owns no degradation or restart decision. After storing a completed snapshot, the Supervisor health-policy
component reads that immutable snapshot and evaluates section 29 state machines separately. Therefore, failure in
policy evaluation, incident persistence, Actor Health UI, Supervisor actor messaging, or lifecycle control cannot stop
the core metrics heartbeat. Five- and fifteen-minute policies use original monotonic incident timestamps rather than
sample counts and are applied by the Supervisor on the first available completed snapshot at or after their deadlines.

Mailbox admission still records metrics and emits the rate-limited hard-limit warning immediately when a rejection
occurs. This is runtime evidence, not a polling cycle. Aggregate metric collection and heartbeat logging remain on the
dedicated 60-second thread; health-policy and lifecycle work remain outside it.

The poller reports its thread start/stop, last cycle start/completion, elapsed duration, overruns, actor/cycle/logger
failures, current revision, next deadline, heartbeat age, and staleness through host-level atomic counters that do not
depend on Supervisor actor messages. The last good snapshot remains available when a cycle is partial or failed.

The Supervisor Query actor reads the latest immutable snapshot from its privileged context and serves bounded history. It does not run collection inside
a query handler. The Actor Health API obtains data through the Supervisor query/service boundary, with a host-level
read-only fallback to the same atomically stored snapshot if the query actor cannot run. UI refresh reads snapshots; it does
not cause database polling or trigger one collection per client.

Meaningful transitions are offered to the Supervisor Event actor through a bounded internal transition channel. The
host-level poller remains the metric-snapshot authority if that event mailbox is unavailable. Routine 60-second polls
are never actor messages.

### 31.8 Supervisor health-management flow

```text
Actor/mailbox/worker atomic metric stores
                  |
                  v
SupervisorActorMetricsPollingService (dedicated 60-second heartbeat thread)
                  |
        evaluate thresholds/incidents
                  |
      latest immutable snapshot stored in privileged context + bounded history
             /                         \
Supervisor Query actor            Supervisor Event actor
Actor Health API/UI               transition notifications

Supervisor Command actor
        |
authorized/audited named operation
        |
ISupervisorManagedActorLifecycle / safe mailbox lifecycle coordinator
        |
pause -> drain -> fence -> restart/resume -> verify recovery
```

This arrangement centralizes policy and mutable actions in the Supervisor actor boundary while keeping measurement and
fallback health independent of the mailbox whose health is being assessed.

### 31.9 Initial Supervisor messages

The first implementation provides:

| Kind | Message | Purpose |
| --- | --- | --- |
| Query | `GetSupervisorHealthSnapshotQuery` | Current actor/domain/mailbox/worker health tree |
| Query | `GetSupervisorActorDetailQuery` | One actor and its paged `ActorThreadId` health map |
| Query | `GetSupervisorMailboxDetailQuery` | Exact mailbox metrics, incidents, and restart history |
| Query | `GetSupervisorIncidentHistoryQuery` | Bounded UTC incident range with keyset paging |
| Query | `GetSupervisorOperationQuery` | One control operation and deterministic status |
| Command | `AcknowledgeSupervisorIncidentCommand` | Audited trader acknowledgement and notes |
| Command | `CaptureSupervisorDiagnosticsCommand` | Bounded correlated diagnostic capture |
| Command | `PauseSupervisorMailboxCommand` | Close admission for one entity mailbox safely |
| Command | `ResumeSupervisorMailboxCommand` | Reopen a safely paused mailbox generation |
| Command | `RestartSupervisorMailboxCommand` | Request controlled restart of one entity mailbox |
| Command | `QuarantineSupervisorMailboxCommand` | Isolate an unsafe mailbox without affecting siblings |
| Event | `SupervisorHealthDegradedEvent` | Health crossed to degraded |
| Event | `SupervisorHealthCriticalEvent` | Health crossed to critical/restarting |
| Event | `SupervisorHealthRecoveredEvent` | Incident met sustained recovery conditions |
| Event | `SupervisorOperationCompletedEvent` | Auditable control operation reached terminal outcome |

Automatic five-minute degradation and fifteen-minute restart use the same internal command handler and operation model
as authorized manual requests. Automatic operations identify the system policy as requester; trader actions identify
the authenticated operator and supplied reason.

### 31.10 Migration sequence

1. Inventory and classify every `IActorSupervisor`, `SupervisorRuntimeContext`, `SupervisorRuntime`, `Children`,
   `ThreadState`, `Container`, and `ThreadPool` access in runtime, host, API, tests, and domain actors.
2. Introduce the four capability interfaces and scoped ordinary-actor implementation.
3. Move low-level measurement to internal runtime instrumentation and all non-Supervisor actor lifecycle ownership to
   `ISupervisorManagedActorLifecycle`.
4. Replace ordinary actor contexts' concrete `SupervisorRuntimeContext` property with
   `IActorSupervisorObservationContext` and remove arbitrary-identity mutation.
5. Remove `IActorSupervisor` from domain context adapters; replace messaging needs with NATS actor client/producer
   contracts and classify any remaining use explicitly.
6. Implement Supervisor command/query/event actors, minimal bootstrap, privileged context, and single managed
   startup/shutdown transactions without exposing mutable runtime objects.
7. Replace current API startup/shutdown orchestration with exactly one privileged managed startup/shutdown call.
8. Add the independent periodic collection service, immutable snapshot publisher, bounded history, and stale fallback.
9. Route Actor Health API/UI reads through Supervisor queries with host-level fallback.
10. Enable read-only health first, then acknowledgement/diagnostic commands, and finally pause/drain/restart/quarantine
   one operation at a time after its safety and authorization tests pass.
11. Add architecture tests forbidding concrete runtime/core-supervisor references from domain actor contexts and API
    lifecycle ownership beyond `ISupervisorBootstrap` plus the two managed calls.

### 31.11 Verification and acceptance

The authority design is complete when:

- no ordinary actor context exposes `SupervisorRuntimeContext`, `IActorSupervisor`, mutable runtime collections, DI
  container, worker pool, or lifecycle operations;
- own-actor recorders cannot observe or mutate another actor/thread identity;
- only Supervisor command handlers can initiate application-level mutable actor operations;
- the Supervisor managed lifecycle exclusively resolves, constructs, registers, binds, starts, monitors, stops,
  restarts, recycles, and unregisters non-Supervisor actors;
- normal API startup/shutdown calls only `StartupActorsAsync`/`ShutdownActorsAsync`, while the host bootstrap controls
  only the Supervisor actors and emergency teardown;
- the dedicated 60-second polling thread sequentially snapshots the immutable managed-actor view without ThreadPool,
  task, timer-callback, actor-message, or database dependencies;
- polling produces deterministic actor/thread identity and rollups, isolates every per-actor and per-cycle failure,
  attempts one compiled summary log per cycle, and continues after every recoverable exception;
- slow, failing, or unavailable collection exposes stale quality without blocking actors or losing the last good view;
- Supervisor mailbox saturation does not make host-level current health unavailable;
- automatic and trader-requested operations use identical serialization, authorization, audit, safe-drain, and
  generation-fence mechanisms;
- Actor Health displays every actor keyed by `ActorMailboxId` with its `ActorThreadId` map, metrics, incidents, and
  operations;
- architecture tests prevent authority from leaking back into domain contexts; and
- load/soak tests prove collection, query refresh, logging, and incident evaluation remain bounded during live trading.

### 31.12 Supervisor domain placement and ordinary-actor semantics

The Supervisor is a first-class domain in `TomasAI.IFM.Domain.Supervisor`, with shared contracts in
`TomasAI.IFM.Domain.Supervisor.Shared`. It is not placed under SystemAdmin and is not implemented as a special runtime
singleton pretending to be an actor. Its command, query, and event components use the same registration, NATS routing,
mailboxes, scheduling, base classes, lifecycle, structured logging, actor-owned metrics, failure containment, and health
evaluation as every other domain actor.

The physical actors have standard identities:

```text
Command.Supervisor.<Verb>.<EntityId>
Query.Supervisor.<Verb>.<EntityId>
Event.Supervisor.<Verb>.<EntityId>
```

They appear in Actor Health under their normal `ActorMailboxId` and expose their own `ActorThreadId` maps. They can
become backlogged, degraded, failed, restarted, or recovered under the same baseline policy. Supervisor health is not
implicitly green and is never excluded from actor/domain/host rollups.

Their only enhanced characteristic is the injected `ISupervisorActorContext`. Privilege is a context capability, not a
different scheduling model or an exemption from ordinary actor standards. The contexts are registered only for actor
types in `TomasAI.IFM.Domain.Supervisor`; attempting to resolve one for another domain fails composition. Architecture
tests enforce the project, namespace, constructor, and DI boundaries.

The independently hosted health collector belongs operationally to this domain but is not a fourth business actor. It
exists so Supervisor actor-mailbox pressure cannot make current health unavailable. It publishes observations and
transitions for the standard Supervisor actors to query and process; privileged mutable action still enters through the
Supervisor Command actor and its audited operation model.

### 31.13 Supervisor actor logging and metrics

#### 31.13.1 Base logging and metrics

Every Supervisor command, query, and event message follows section 30 without exception. It emits one compiled
`ActorMessageEntry`, one compiled `ActorMessageExit` with elapsed time, and the applicable compiled exception records.
Every record contains `ActorType`, `ActorName=Supervisor`, `Verb`, and `EntityId` plus the standard generation,
correlation, outcome, stage, and timing fields.

The Supervisor actors also own the same base `ActorMetricsStore` as all domain actors. Their snapshots contain accepted,
dequeued, succeeded, failed, cancelled and rejected messages; depth/capacity/peak/oldest age; queue and handler timing;
current verb/worker; lifecycle; saturation incidents; and restart history for every Supervisor `ActorThreadId`.

Supervisor-specific logging or metrics never replaces the base records. A Supervisor operation normally has both the
base actor entry/exit evidence and domain evidence describing the health or control decision.

#### 31.13.2 Supervisor-specific compiled logs

Supervisor domain logs use source-generated `[LoggerMessage]` methods with a reserved stable Event ID range. The minimum
domain events are:

| Event | Level | Required evidence |
| --- | --- | --- |
| `SupervisorActorMetricsPollCompleted` | Information | Revision, actors/mailboxes observed, partial failures, duration, thread identity, next deadline, snapshot quality |
| `SupervisorCollectionFailed` | Error | Revision, stage, duration, successful/failed actor counts, bounded exception and last-good revision |
| `SupervisorHealthTransition` | Information/Warning/Error | Target actor/thread, prior/current state, reason, incident ID, first-observed time and elapsed duration |
| `SupervisorOperationRequested` | Information | Operation, target/generation, incident, requester type/identity, reason and automatic/manual source |
| `SupervisorOperationCompleted` | Information | Operation, target/generation, outcome, elapsed time and resulting health |
| `SupervisorOperationFailed` | Error | Operation, target/generation, failure stage, bounded exception, elapsed time and safe-work state |
| `SupervisorSnapshotServed` | Debug | Revision, age, quality, query scope and result counts |
| `SupervisorHistoryWriteFailed` | Error | Rollup/incident identity, failure stage and last durable checkpoint |
| `SupervisorNotificationDeferred` | Warning | Transition identity, destination, reason, retry/defer state |

The once-per-minute successful collection produces one Information completion record, not per-actor start/completion
records. Actor-level collection failures are accumulated into the completion summary and receive an Error record only
when evidence would otherwise be lost. Critical health transitions and operation boundaries are logged immediately and
are not delayed merely to combine them with the next collection record.

All templates are compiled and structured. They never serialize the full system snapshot, actor collections, mailbox
maps, exception objects, or message payloads into a log record. Target identity is represented by the same separate
actor fields used by the system-wide standard. Repeated equivalent health warnings follow the once-per-minute
per-`ActorThreadId` limiter.

#### 31.13.3 Supervisor-specific metrics

The Supervisor domain defines one `System.Diagnostics.Metrics` meter and the following initial instruments:

| Instrument | Kind | Meaning |
| --- | --- | --- |
| `ifm.supervisor.poll.duration` | Histogram | Complete dedicated-thread metrics poll duration |
| `ifm.supervisor.collection.actors` | Histogram | Actors captured in one cycle |
| `ifm.supervisor.collection.mailboxes` | Histogram | Entity mailboxes captured in one cycle |
| `ifm.supervisor.collection.failed_actors` | Counter | Actor snapshots unavailable or failed |
| `ifm.supervisor.collection.skipped` | Counter | Timer ticks skipped because the prior cycle was active |
| `ifm.supervisor.snapshot.age` | Observable gauge | Age of the latest successfully stored snapshot |
| `ifm.supervisor.snapshot.quality` | Observable gauge | Current consistent/partial/stale/unavailable state encoded as documented values |
| `ifm.supervisor.incidents.active` | Observable gauge | Active incidents by bounded state/severity |
| `ifm.supervisor.incidents.transitions` | Counter | Degraded, critical, recovering and recovered transitions |
| `ifm.supervisor.operations.duration` | Histogram | Authorized control-operation duration |
| `ifm.supervisor.operations.outcomes` | Counter | Requested, completed, failed, timed-out and rejected operations |
| `ifm.supervisor.restarts` | Counter | Automatic/manual entity-mailbox or actor restarts and outcomes |
| `ifm.supervisor.history.failures` | Counter | Failed bounded-history writes |
| `ifm.supervisor.notifications.deferred` | Counter | Supervisor transitions awaiting required delivery |
| `ifm.supervisor.logging.dropped` | Counter | Supervisor log records rejected by the asynchronous sink |

Metric tags are bounded to values such as operation kind, outcome, health state, severity, snapshot quality, automatic
or manual source, and actor type. `ActorMailboxId`, `ActorThreadId`, `EntityId`, subject, incident ID, operation ID,
requester identity, and trace ID are never metric dimensions; those belong in snapshots, traces, and structured logs.

Polling metrics include thread identity/state, cycle deadline, completion count, overrun count, failed actor count,
logger/exception-log failures, heartbeat age, and snapshot quality. The poller's own failure must remain observable
through a host-level fallback gauge/counter even
if the Supervisor actors or normal logging pipeline are unhealthy.

#### 31.13.4 Recursion and failure isolation

Supervisor actors are not members of the managed actor-health collection or lifecycle-control set and therefore never
supervise or restart themselves. Their base actor metrics remain available to host-level fallback health, external
telemetry, logs, and the Actor Health root's separate Supervisor-status section. The managed collection contains only
non-Supervisor actors. Collector metrics are published only after a cycle terminates and cannot recursively enter the
current partially built aggregate snapshot.

A Supervisor logging, metrics, history, query, or notification failure cannot recursively enqueue another mandatory
Supervisor message. The narrowest boundary records a bounded fallback counter and retains the last good snapshot.
Operation failures remain available through the host-level operation store even if the Supervisor Query actor is
temporarily unavailable.

#### 31.13.5 Verification

Tests verify that Supervisor actors:

- emit the same complete entry/exit/exception base records and actor-owned metrics as ordinary actors;
- appear in Actor Health with their own mailboxes, thread IDs, backlog, failures, saturation, and restart evidence;
- emit compiled Supervisor-specific collection, transition, and operation logs without snapshot serialization;
- publish only bounded-cardinality metric tags;
- remain diagnosable when their mailboxes, log sink, history store, or event notifications are unhealthy;
- cannot recursively observe the current incomplete collection or create an observability-message loop;
- receive privileged context only from the `TomasAI.IFM.Domain.Supervisor` composition boundary; and
- meet the base logging and dedicated 60-second sequential-poll allocation, latency, CPU, and thread-liveness budgets.

### 31.14 Single managed-actor startup and shutdown boundary

#### 31.14.1 End-state contract

The first lifecycle end state exposes one startup and one shutdown method through the privileged Supervisor context:

```text
ISupervisorActorContext.ManagedActors
  ValueTask<SupervisorActorsStartupResult> StartupActorsAsync(CancellationToken cancellationToken)
  ValueTask<SupervisorActorsShutdownResult> ShutdownActorsAsync(CancellationToken cancellationToken)
```

These are the only normal process-level entry points for the complete non-Supervisor actor population. The current API
startup code calls `StartupActorsAsync` once after the Supervisor actors are running. Normal application shutdown calls
`ShutdownActorsAsync` once before the host bootstrap stops the Supervisor actors. Neither method accepts an arbitrary
actor list, factory delegate, producer, consumer, route, container, or runtime object from the API layer.

The implementation is an idempotent, serialized lifecycle transaction. A concurrent duplicate call joins or returns
the existing operation result; it cannot run a second startup/shutdown transaction. Results contain operation ID,
generation, start/end time, elapsed time, expected/registered/started/healthy/stopped counts, readiness, health-service
state, per-stage failures, rollback outcome, and final deterministic status.

#### 31.14.2 Minimal Supervisor bootstrap

The host retains only `ISupervisorBootstrap` to break the unavoidable ownership cycle. It:

1. constructs the process runtime root and low-level core runtime;
2. constructs, registers, binds, and starts `SupervisorCommandActor`, `SupervisorQueryActor`, and
   `SupervisorEventActor`;
3. supplies their privileged `ISupervisorActorContext`;
4. invokes the single managed startup method;
5. invokes the single managed shutdown method during normal stop; and
6. stops the Supervisor actors and process runtime only after managed shutdown completes.

It cannot normally construct, register, start, stop, or recycle a non-Supervisor actor. Emergency teardown may bypass
the Supervisor only when the privileged domain is unavailable, and must record that managed shutdown was bypassed.

#### 31.14.3 Managed startup transaction

`StartupActorsAsync` exclusively owns the lifecycle stages currently spread across `ActorRuntimeStartup`, API startup,
the core supervisor, and transport registration:

1. **Enter startup**: serialize the transaction, set managed readiness false, keep external actor intake closed, and
   record operation/generation/start time.
2. **Discover**: read `IActorRegistry` and select every expected non-Supervisor actor descriptor. Supervisor actor types
   are rejected from the managed set.
3. **Construct**: resolve each actor through `IActorFactory` inside the coordinator; the API host never receives actor
   instances.
4. **Register**: add each actor to the low-level runtime and Supervisor managed catalog before actor startup, attach its
   actor-owned metrics store, policy, expected readiness, and lifecycle evidence.
5. **Bind infrastructure**: create/register the required producer, JetStream producer, consumer type, event/realtime
   routes, projector descriptors, and other actor-owned runtime bindings.
6. **Start actors**: start non-Supervisor actors with bounded concurrency. Every actor start is a tracked child
   operation with compiled entry/exit/exception logging and elapsed time.
7. **Initial health gate**: directly verify the expected and registered sets match; every required actor completed its
   startup contract, is running, has its required producer/projector/recovery dependencies ready, and has no startup
   fault, quarantine, or unresolved rollback evidence.
8. **Open intake**: start Core NATS and JetStream consumers only after the initial gate succeeds.
9. **Publish readiness**: set managed runtime readiness true only after intake is open and revalidate the required actor
   set using the existing startup/readiness contract plus the new per-actor initial snapshot.
10. **Enable health activity**: start the dedicated `SupervisorActorMetricsPollingService` thread, immediately capture
    and log the first managed snapshot, then sequentially poll all non-Supervisor actors every 60 seconds.
11. **Complete**: publish the startup result and Supervisor operation/health evidence.

The initial health gate does not depend on periodic polling, avoiding a startup cycle. It extends the current meaning
of healthy startup—every actor `StartAsync` succeeded and consumers opened—with explicit expected/registered/running
actor evidence. Periodic health activity is disabled until this gate is green.

Any failure before completion keeps readiness false, keeps or recloses external intake, prevents health polling from
starting, and rolls back every binding/actor created by that generation in reverse dependency order. Rollback failures
are retained without replacing the primary failure.

#### 31.14.4 Managed shutdown transaction

`ShutdownActorsAsync` exclusively owns normal non-Supervisor shutdown:

1. serialize shutdown and set readiness false;
2. signal the dedicated polling thread to stop, await its active bounded cycle, and record its final heartbeat;
3. close external Core NATS and JetStream intake;
4. close managed actor admission and drain accepted messages within the configured deadline;
5. let executing handlers reach safe commit boundaries;
6. flush required projector, durable queue, history, and operation evidence;
7. stop non-Supervisor actors in reverse dependency order with bounded concurrency where dependencies permit;
8. stop/remove consumers, producers, routes, projectors, mailboxes, and actor registrations owned by the generation;
9. publish the deterministic shutdown and incomplete-drain evidence; and
10. return control to the host so it can stop the Supervisor actors last.

The shutdown result distinguishes clean, timed-out, cancelled, partially drained, rollback/cleanup failed, and
emergency-bypassed outcomes. A timeout never causes silent loss of accepted required work.

#### 31.14.5 Individual lifecycle and recycle operations

After startup, every pause, drain, resume, stop, restart, quarantine, retire, or recycle request for a non-Supervisor
actor or entity mailbox uses the same managed coordinator. A recycle operation means controlled stop/drain, generation
fence, disposal/removal of generation-owned bindings, reconstruction through the registered descriptor/factory,
registration of the new generation, startup validation, and admission reopen. It is not a direct `StopAsync` followed
by an untracked `StartAsync`.

Automatic health actions and authorized trader commands share this coordinator, operation serialization, logging,
metrics, audit, generation checks, and safe-boundary rules. No domain actor, API service, hosted service, or direct
`IActorSupervisor` reference may perform an alternate lifecycle operation.

#### 31.14.6 Health-service activation and managed scope

The health service has explicit states:

```text
Disabled -> Starting -> Active -> Stopping -> Disabled
                       \-> Faulted
```

It is `Disabled` while managed actors are being discovered, registered, started, rolled back, or shut down. Successful
startup moves it to `Starting`, starts the dedicated thread, captures/logs one immediate baseline, and then moves it to
`Active` on the 60-second monotonic schedule.
The collection set is the immutable current generation of all registered non-Supervisor actors. Actor registration,
unregistration, restart, and recycle publish a new generation view atomically for the next cycle.

Supervisor actors retain base logging and metrics but remain outside this collection and all Supervisor-issued
lifecycle actions. Their health is reported by host-level fallback checks and external telemetry because self-control
would create a circular recovery dependency.

#### 31.14.7 Required migration from current startup

The following current `ActorRuntimeStartup` responsibilities move behind `StartupActorsAsync`:

| Current responsibility | End-state owner |
| --- | --- |
| Resolve `IActorRegistry` and `IActorFactory` | Supervisor managed lifecycle |
| Resolve and construct all domain actors | Supervisor managed lifecycle |
| `AddActor` and runtime-context registration | Supervisor managed lifecycle through internal runtime primitive |
| Producer and JetStream producer registration | Supervisor managed lifecycle |
| Consumer registration by delivery type | Supervisor managed lifecycle |
| Concurrent actor startup | Supervisor managed lifecycle |
| Initial actor/readiness validation | Supervisor managed lifecycle |
| Start external consumers and open intake | Supervisor managed lifecycle |
| Set managed readiness | Supervisor managed lifecycle |
| Enable periodic actor health | Supervisor managed lifecycle |
| Startup rollback | Supervisor managed lifecycle |
| Drain, stop, cleanup, and unregister | Supervisor managed lifecycle shutdown |

The API startup helper is reduced to Supervisor bootstrap plus one `StartupActorsAsync` call. The shutdown path is
reduced to one `ShutdownActorsAsync` call plus final Supervisor/bootstrap disposal.

#### 31.14.8 Lifecycle verification

Tests prove Supervisor-first ordering, exclusion of Supervisor actors from the managed set, registration-before-start,
bounded concurrent startup, closed intake before readiness, health-service activation only after green initial health,
dedicated 60-second managed polling and logging, reverse rollback, reverse shutdown, safe recycle, idempotent/concurrent calls, emergency
bypass evidence, and absence of non-Supervisor lifecycle work in API startup/shutdown code.

### 31.15 Required design and implementation order

The implementation specification and delivery follow this dependency order:

1. **Supervisor domain actor and lifecycle authority**: create `TomasAI.IFM.Domain.Supervisor`, shared messages/read
   models, Supervisor command/query/event actors, privileged/restricted context boundaries, minimal bootstrap, managed
   startup/shutdown facade, immutable actor catalog, and operation/audit model. Existing actor metrics may be adapted
   temporarily behind the new snapshot-source contract.
2. **Bulletproof actor-metrics polling service**: implement the dedicated named thread, immutable managed-actor view,
   sequential 60-second loop, atomic latest-snapshot storage in the privileged context, mandatory compiled heartbeat log, per-actor/per-cycle/
   logger exception containment, exception-log fallback, host-level liveness counters, startup gate, and bounded stop/join.
   Develop it against deterministic fake metric sources before depending on complete actor instrumentation.
3. **System-wide actor metrics and logging**: complete the actor-owned and `ActorThreadId` metric records, queue/handler/
   rejection/lifecycle fields, base compiled entry/exit/exception logs, full actor identity, elapsed time, Supervisor
   domain metrics/logs, and all base actor integrations. Replace temporary adapters and prove every registered managed
   actor supplies the final snapshot contract.
4. **Health policy and controlled actions**: consume stored polling snapshots for degradation, incidents, recovery,
   restart/recycle decisions, Actor Health queries/UI, history, trader actions, and actor-specific policy overrides.
5. **Qualification**: execute architecture, lifecycle, exception-injection, logging-sink failure, thread-starvation,
   allocation, latency, CPU, restart, soak, and continuous-trading verification before enabling automatic mutation.

This order keeps the polling core extremely narrow. Later metric enrichment, policy evaluation, persistence, UI, or
control failure cannot expand or destabilize its mandatory function: while the process/thread are schedulable, poll the
current managed actor metrics and attempt one structured heartbeat log every 60 seconds.

### 31.16 Future speculative LLM health advisory

As a future optional capability, each newly stored 60-second actor-metrics snapshot could be offered to an independent
LLM advisory service to produce a concise human-readable system-health summary, highlight unusual combinations, and
suggest diagnostic questions for the trader or operator.

This capability is speculative, is not required by the current design or implementation, and is disabled by default.
It is never part of the dedicated polling thread's responsibilities or success criteria. Any future implementation must
consume the already atomically stored immutable snapshot only after the mandatory heartbeat log attempt, execute
asynchronously with bounded timeout/rate/cost, and be safely skipped when unavailable or when a prior advisory is still
running.

An LLM advisory is non-authoritative. It cannot determine actor health state, acknowledge incidents, change policy,
pause, restart, recycle, or otherwise control an actor. Deterministic metrics and Supervisor policies remain the source
of truth. Advisory failure, timeout, malformed output, provider outage, or logging failure cannot affect polling,
readiness, health evaluation, trading, or lifecycle control.

Only approved, bounded, redacted metric summaries may leave the process. Actor-message payloads, credentials, account
identifiers, unrestricted entity IDs, exception stacks, and confidential trading data are excluded. Any displayed
advisory must be labelled as AI-generated and retain snapshot revision/time, model/provider identity, schema/prompt
version, latency, and failure/provenance status so an operator can compare it with the deterministic Actor Health view.

The polling-side integration is intentionally the smallest possible fire-and-forget contract:

```csharp
public interface ISupervisorHealthLlmAdvisorySink
{
    void Observe(SupervisorHealthAdvisoryObservation observation);
}
```

`Observe` is synchronous, constant-time, nonblocking, nonthrowing by contract, and returns no `Task`, result,
acknowledgement, advisory, or acceptance status. The poller calls it only after atomically storing the metrics snapshot
and attempting the mandatory heartbeat log, then immediately continues. A disabled installation uses a no-op sink.

The optional implementation copies only the bounded redacted advisory observation into an independent capacity-one
latest-wins handoff. If an older observation is waiting, it may be replaced; if allocation or handoff fails, the
observation is discarded. There is no caller backpressure, retry, durable queue, NATS publication, actor message,
completion callback, exception propagation, or health consequence. A defensive polling-boundary catch protects against
a contract-violating implementation, but even that failure only increments an optional advisory diagnostic counter.

An independent advisory worker may call an LLM and store/display its output outside the authoritative Supervisor state.
The polling service never reads that result. Advisory success, failure, timeout, cancellation, replacement, or provider
availability is irrelevant to the polling cycle and to all deterministic health and lifecycle behavior.
