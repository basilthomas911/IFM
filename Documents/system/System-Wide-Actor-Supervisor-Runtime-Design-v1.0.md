# IFM System-Wide Actor Supervisor Runtime Design

**Document type:** System-wide target design and implementation contract  
**Status:** Initial runtime foundation and read-only Actor Health release implemented  
**Version:** 1.2  
**Created:** 2026-09-11  
**Last updated:** 2026-09-11  
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
6. `SupervisorCommandContext`, `SupervisorQueryContext`, and `SupervisorEventContext` embed the same singleton
   `SupervisorRuntimeContext` by reference.
7. Every standard actor context receives a bounded direct reference to the same `SupervisorRuntimeContext`. This
   access exists for low-latency metrics, cached policy, and nonblocking runtime observations; it is not unrestricted
   access to mutate the core `ActorSupervisor`.
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
  SupervisorRuntimeContext SupervisorRuntime { get; }
```

The embedded property on all three contexts must be reference-equal to the process singleton.

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

Every actor context receives the same process singleton through a common runtime-context contract:

```text
IActorRuntimeContext
  SupervisorRuntimeContext SupervisorRuntime { get; }
```

This applies to command, query, event, realtime, function, and denormalizer contexts. Specialized domain contexts may
expose the property through their own typed interface, but the reference must resolve to the same singleton.

Actor-to-Supervisor interaction has two deliberately different paths:

```text
Actor
  +-- direct SupervisorRuntimeContext call
  |     metrics, cached policy, bounded observation
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

An actor must never call a mutable core `ActorSupervisor` lifecycle operation through its direct runtime reference.
The `SupervisorRuntimeContext` public surface separates direct capabilities from actor-message capabilities:

```text
SupervisorRuntime.Metrics   direct, synchronous, nonblocking
SupervisorRuntime.Health    direct cached policy and observations
SupervisorRuntime.Client    Supervisor commands, queries, and events
```

The initial metrics implementation requires only the direct path and Supervisor snapshot reads. Coordinated
Supervisor commands and events remain later increments.

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
5. Create Supervisor command, query, and event contexts with the same singleton reference.
6. Register and start Supervisor actors.
7. Register remaining domain actors and projector descriptors.
8. Open external actor-message intake only after required startup dependencies are ready.

Shutdown order is reversed for intake and persistence:

1. Reject new external intake.
2. Record Supervisor shutdown state.
3. Drain accepted actor work within the configured bound.
4. Flush pending Supervisor history batches.
5. Stop domain actors and projectors.
6. Stop Supervisor actors.
7. Dispose `SupervisorRuntimeContext` once.
8. Dispose the core actor supervisor and transports.

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

Current counters are converted into interval deltas by a bounded, asynchronous history writer. The initial bucket
period is configurable and defaults to 15 seconds while the Actor Health UI is open and 60 seconds otherwise. A
transition to degraded or failed state is written immediately through the bounded writer.

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
interval is 15 seconds and is configurable. Manual refresh is always available. Live refresh must not run historical
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
TomasAI.IFM.Domain.SystemAdmin/
  Supervisor/
    Command/
      Actor/
      Extensions/
      Model/
    Query/
      Actor/
      Extensions/
      Model/
    Event/
      Actor/
      Extensions/
    Runtime/
      SupervisorRuntimeContext.cs
      Actors/
      Mailboxes/
      Workers/
      Projectors/
      DurableQueues/
      Operations/
      Incidents/
      History/

TomasAI.IFM.Domain.SystemAdmin.Shared/
  Supervisor/
    Commands/
    Queries/
    Events/
    ViewModels/
    Enums/
    ServiceApi/

TomasAI.IFM.Shared/EventModelActor/
  Contracts/
    IActorMetricsStore.cs
    IActorRuntimeContext.cs
  Metrics/
    ActorMetricsStore.cs
    ActorMailboxMetrics.cs
    ActorMetricsSnapshot.cs
    ActorMailboxMetricsSnapshot.cs

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

If SystemAdmin project boundaries make the runtime dependency point inward toward domain code, the root interfaces
and low-level recorder move to a shared application/framework assembly while the Supervisor actors remain in the
SystemAdmin domain. Dependency direction takes precedence over folder symmetry.

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
