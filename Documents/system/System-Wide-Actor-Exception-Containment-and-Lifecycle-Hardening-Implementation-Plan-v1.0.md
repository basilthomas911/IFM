# IFM System-Wide Actor Exception Containment and Lifecycle Hardening Implementation Plan

**Work package:** Supervisor Runtime actor hardening  
**Status:** Implemented and verified  
**Version:** 1.1  
**Created:** 2026-09-11  
**Owner:** IFM engineering  
**Depends on:** `System-Wide-Actor-Supervisor-Runtime-Design-v1.0.md`

**Implementation completed:** 2026-09-11  

The implemented release includes the standard command, query, event/realtime, and Function exception boundaries;
actor-owned mailbox metrics; bounded Supervisor failure evidence; shared-worker visibility; serialized actor and
entity-mailbox lifecycle operations; admission closure, bounded drain, generation renewal, and quarantine state;
processing-based JetStream acknowledgement; removal of the unused denormalizer actor infrastructure; the read-only
Actor Health API/UI; and focused actor, transport, allocation, API, UI, and projector verification. Actor Health reads
runtime state directly and refreshes only while its form is open; it introduces no database polling service.

Mutation controls remain intentionally absent from the first Actor Health release. The lifecycle backend exists for
controlled host use, while UI authorization and audit remain part of the later controlled-operations increment.

## 1. Purpose

This plan hardens the current actor runtime so every recoverable exception has a deterministic containment boundary,
complete structured evidence, correct caller or durable-delivery behavior, and no unintended termination of an actor
or shared worker. It then adds the lifecycle controls required for the Supervisor to stop, start, pause, drain,
quarantine, and restart actors or entity mailboxes safely.

The plan preserves the current shared-worker design. An `ActorThreadId` identifies an entity mailbox; it does not own a
permanent thread.

## 2. Verified current behavior

### 2.1 Shared scheduling

1. The V2 actor pool contains shared workers.
2. An entity mailbox is identified by `ActorThreadId`.
3. The scheduling bit ensures that no two workers process the same entity mailbox concurrently.
4. One worker processes at most 64 messages from that mailbox in one turn.
5. If messages remain, the mailbox is rescheduled and may move to another shared worker.
6. When empty, scheduling ownership is released.
7. The mailbox object is retained while the actor is within its retained-idle-mailbox allowance, currently 1,024 by
   default.
8. Beyond that allowance, an idle mailbox may be retired.
9. The shared worker returns to the pool; it is not retired with the mailbox.

### 2.2 Message exception behavior

The shared V2 worker currently catches an exception escaping one message, records `Failed`, logs it, disposes the
message, and continues. A mailbox infrastructure exception is also caught by the outer mailbox boundary so the worker
can continue. Only a failure escaping the ready-queue loop faults the shared worker.

Base actors attempt their own handling first:

| Actor kind | Existing inner behavior |
| --- | --- |
| Command | Catch processing failure, call `OnExceptionAsync`, attempt reply |
| Query | Catch failure, call `OnExceptionAsync` or parsing fallback, remove reply context |
| Event | Catch failure and call `OnExceptionAsync` |
| Denormalizer | Legacy framework only; no production implementation or registration was found |
| Function | Build typed failed terminal result and attempt reply |
| Realtime | Concrete implementations vary and require inventory |

### 2.3 Confirmed gaps

The current implementation does not guarantee:

- containment when an actor-specific exception handler throws;
- preservation of the primary exception when cleanup also throws;
- a failure response when command/query/function reply fails;
- consistent stage and correlation details in every log;
- distinction between internally handled failure and successful processing in outer metrics;
- an actor health transition after repeated or critical message failure;
- safe serialization of concurrent start and stop requests;
- closure of actor or entity-mailbox admission during stop;
- safe drain of already accepted messages;
- generation fencing against late work from a stopped actor/mailbox; or
- fine-grained entity-mailbox lifecycle controls.

## 3. Meaning of “all exceptions”

The implementation catches, records, and contains every recoverable `Exception` crossing an application-owned actor
stage boundary. It separately recognizes caller/runtime cancellation and does not log expected cancellation as an
error.

No application can guarantee recovery from process termination, stack exhaustion, corrupted process state, operating
system termination, hardware failure, or an out-of-memory condition severe enough to prevent logging. The acceptance
claim is therefore:

> Every recoverable exception thrown by actor parsing, validation, state loading, execution, persistence, projection,
> publication, exception handling, cleanup, or reply is caught at a defined boundary and produces bounded structured
> evidence without terminating the actor or shared worker.

## 4. Goals

1. Standardize exception boundaries across every current base actor type.
2. Ensure an exception-handler failure cannot escape without last-resort evidence.
3. Preserve primary and secondary exceptions separately.
4. Return a deterministic failure result whenever a request/reply transport remains available.
5. Preserve correct JetStream acknowledgement/redelivery behavior for durable events.
6. Record actor-owned mailbox metrics for successful, handled-failed, escaped-failed, cancelled, and reply-failed
   outcomes.
7. Keep the shared worker alive after a single-message or single-mailbox failure.
8. Detect and expose a genuinely faulted shared worker.
9. Add Supervisor-visible lifecycle, admission, drain, and generation state.
10. Serialize stop/start/restart operations per actor or entity-mailbox target.
11. Never abandon work after an irreversible commit boundary.
12. Maintain bounded memory and Ring 2 performance.

## 5. Non-goals

The work does not:

- claim recovery from fatal CLR or process failures;
- restart an actor automatically after every business validation failure;
- turn expected validation failures into exceptions;
- retain complete message payloads for diagnostics;
- use exception throwing as normal actor control flow;
- stop a shared worker to restart one entity mailbox;
- cancel work after a confirmed durable/external commit;
- add unlimited retry loops; or
- enable autonomous trading-control decisions.

## 6. Required invariants

### EH-INV-001: One primary outcome

Every accepted message ends in exactly one primary processing outcome:

```text
Succeeded
HandledFailure
EscapedFailure
CancelledBeforeCommit
CompletedAfterCancellation
```

Reply and publication outcomes are recorded separately because business processing can succeed while a response or
notification fails.

### EH-INV-002: Primary failure preservation

Cleanup, exception handling, telemetry, publication, or reply failure cannot replace the original processing failure.
Secondary failures are attached as separate bounded records.

### EH-INV-003: No silent failure

Every recoverable exception produces at least one direct actor-owned failure record. The normal logging pipeline and
Supervisor incident pipeline may supplement it. Failure of those pipelines must not recursively throw into actor
processing.

### EH-INV-004: Worker containment

A message failure cannot fault the shared worker. A mailbox infrastructure failure cannot fault unrelated mailboxes.
A ready-queue or worker-loop failure is recorded as a worker fault and exposed through host-level Supervisor health.

### EH-INV-005: Transport correctness

Command, query, function, Core event, and JetStream event failure paths preserve their respective delivery contracts.
No durable event is acknowledged merely because a failure was logged.

### EH-INV-006: Safe lifecycle

Stop/restart closes admission, resolves accepted work to a documented safe boundary, fences the previous generation,
and only then exposes the new generation.

### EH-INV-007: Per-target serialization

Only one lifecycle operation mutates a given actor or entity mailbox at a time. Unrelated targets remain independent.

### EH-INV-008: Bounded recovery

Every retry or recovery policy has a maximum attempt count, maximum duration, terminal outcome, and manual follow-up
state. Recovery cannot loop forever.

## 7. Failure taxonomy

Introduce shared bounded enums:

### `ActorFailureStage`

```text
Unknown
Admission
Parsing
Validation
Deduplication
StateReplay
Execution
Persistence
Projection
Publication
ExceptionHandling
Cleanup
Reply
MailboxInfrastructure
WorkerLoop
Startup
Shutdown
Drain
Restart
```

### `ActorMessageOutcomeType`

```text
Unknown
Succeeded
HandledFailure
EscapedFailure
CancelledBeforeCommit
CompletedAfterCancellation
```

### `ActorDeliveryOutcomeType`

```text
NotRequired
Succeeded
Failed
Deferred
Redelivered
Terminal
```

### `ActorFailureSeverity`

```text
Information
Warning
Error
Critical
```

Failure reason codes are bounded stable values. Exception type names, messages, stack traces, entity IDs, and trace IDs
are not metric tags.

## 8. Structured failure record

`ActorFailureRecord` contains:

- failure ID;
- observed UTC;
- actor mailbox ID;
- entity mailbox ID;
- actor/mailbox generation;
- message verb and bounded message type;
- failure stage;
- outcome and severity;
- error code;
- exception type and bounded message;
- bounded stack-trace reference or durable detail ID;
- command/query/event/function identity;
- trace, correlation, causation, and operation identities;
- whether a commit boundary was crossed;
- whether the actor-specific exception handler succeeded;
- whether fallback reply/publication succeeded;
- retryability and delivery attempt;
- primary failure ID when this is a secondary failure; and
- occurrence count for deduplicated incidents.

The hot path retains only bounded scalar data and references. Full stack detail is materialized and persisted only on
failure.

## 9. Exception-boundary architecture

### 9.1 Boundary order

Every base actor follows this conceptual order:

```text
materialize message
  -> parse
  -> validate
  -> load/replay state
  -> execute
  -> persist/project
  -> publish
  -> build result
  -> reply
  -> cleanup
```

Each stage records the active stage before execution. The outer actor boundary always knows the last active stage and
commit state.

### 9.2 Primary catch boundary

The base actor, rather than each derived actor, owns the non-overridable outer boundary. It:

1. Classifies cancellation versus failure.
2. Captures the primary failure record.
3. Invokes the typed actor exception handler inside a secondary boundary.
4. Builds a generic fallback outcome if the typed handler fails.
5. Attempts required response/publication inside its own boundary.
6. Runs cleanup inside a boundary that cannot replace the primary result.
7. Updates actor-owned metrics exactly once.
8. Returns normally when the failure has a deterministic terminal outcome.

### 9.3 Last-resort failure sink

Add a minimal `IActorFailureSink` backed by actor-owned metrics and `SupervisorRuntimeContext`. Its core record method is
synchronous, bounded, and nonthrowing during normal operation. It does not send an actor message.

If normal structured logging throws, the last-resort sink still stores bounded failure evidence. If the sink itself
cannot record, the shared worker writes a minimal fallback log without recursively invoking the actor.

### 9.4 Logging ownership

The narrowest boundary that can classify the failure writes the primary structured log. Outer boundaries record an
additional log only when they observe a secondary escape or infrastructure failure. Logs carry the same failure ID so
duplicate evidence is recognizable.

## 10. Actor-kind requirements

### 10.1 Command actors

Command hardening must cover:

- message deserialization;
- missing or invalid command ID;
- validation;
- command-audit reservation;
- state replay;
- handler execution;
- state persistence;
- event publication;
- actor-specific exception handling;
- `OnCommandFinishedAsync`;
- result construction; and
- reply transport.

When the typed exception handler throws, the base actor returns a generic `ServiceFailed<GuidResult>` retaining the
original command ID and a bounded failure reference. Cleanup failure is secondary. Reply failure is recorded but does
not re-execute a command whose commit is confirmed.

### 10.2 Query actors

Query hardening must cover:

- parse and correlation-context registration;
- validation;
- handler execution;
- typed exception handler;
- fallback failure reply;
- reply-context removal; and
- reply transport.

Reply context is removed exactly once. A failed reply is visible as `ReplyFailed` and cannot be mistaken for successful
delivery. The actor remains available for later queries.

### 10.3 Function actors

Function actors retain their typed completed/failed terminal model. Hardening must preserve the difference between:

- failure before commit;
- commit outcome unknown;
- confirmed commit followed by observer/cache failure;
- projection/persistence failure; and
- reply failure after a confirmed terminal result.

A reply failure cannot cause the function to execute again without idempotency/reconciliation rules.

### 10.4 Event actors

Event actor hardening must distinguish Core and JetStream delivery:

- Core delivery failure is recorded because the transport cannot redeliver a consumed best-effort message.
- JetStream failure returns the correct NAK/defer/terminal disposition to the owning consumer.
- A log entry is not an acknowledgement.
- Actor-specific exception-handler failure falls back to the transport-safe failure disposition.
- Poison events obey bounded delivery and terminal policy.

### 10.5 Legacy denormalizer removal

Denormalizer actors are obsolete. Repository verification found no production class deriving from
`BaseDenormalizerActor`, implementing `IDenormalizerActor`, or registering a denormalizer actor. The only concrete
derivative is a cancellation test fixture. Current read-model updates use event projectors, which provide explicit
process/replay queues and durable operational state.

Remove the unused framework surface:

- `BaseDenormalizerActor`;
- `DenormalizerActorContext`;
- `IDenormalizerActor`;
- `IDenormalizerActorContext`;
- denormalizer-only helpers and registrations, if any are found during the complete inventory; and
- denormalizer-only test fixtures.

Before deletion, perform a solution-wide source and compiled-reference inventory, including reflection-based
registration conventions and configuration. After deletion, require a complete solution build and architecture tests
that prohibit new production dependencies on the removed pattern.

Any behavior discovered during removal must be migrated to a conventional event projector before the legacy type is
deleted. The removal must not silently eliminate a read-model update, event route, completion notification, or durable
recovery path.

### 10.6 Realtime actors

Inventory every realtime actor because there is no single shared base context covering all implementations. Bring
each under the standard outcome and failure sink. A realtime failure must not introduce durable retry implicitly. The
domain explicitly decides whether to discard, coalesce, rebuild from the latest value, or escalate.

Realtime hot paths do not allocate failure infrastructure during successful processing.

## 11. Shared-worker hardening

### 11.1 Per-message boundary

The worker retains its final containment boundary. It records `EscapedFailure` only when a failure escapes the base
actor boundary. It must not classify an internally handled actor failure as success merely because
`HandleMessageAsync` returned.

Actor-owned outcome state supplies the final classification without requiring a Supervisor message. The implementation
may use a scoped processing token or an additive outcome-aware actor interface; the selected mechanism must avoid a
per-message heap allocation.

### 11.2 Mailbox boundary

If queue access, drain completion, rescheduling, or retirement fails:

1. Record mailbox-infrastructure failure.
2. Preserve the mailbox scheduling invariant.
3. Reschedule only when ownership remains valid.
4. Quarantine the mailbox if ownership cannot be proven.
5. Continue servicing unrelated mailboxes.

### 11.3 Worker-loop boundary

If the ready-queue loop fails, record the worker as faulted with its last assignment and exception. The pool must expose
reduced capacity. A replacement worker may be started only through bounded, generation-fenced pool policy. Host-level
health remains available if the actor scheduler cannot execute Supervisor queries.

## 12. Metrics changes

Actor-owned metrics distinguish:

- accepted;
- succeeded;
- handled failed;
- escaped failed;
- cancelled before commit;
- completed after cancellation;
- reply failed;
- publication failed;
- exception handler failed; and
- cleanup failed.

The existing aggregate `Processed` metric is documented either as terminal handling or split into unambiguous success
and failure metrics. It must not be presented as business success if the actor returned normally after handling an
exception.

Every failure metric includes only bounded actor type, stage, outcome, and reason tags. Detailed identity remains in
Supervisor failure records.

## 13. Supervisor hardening

### 13.1 Dependency rules

Supervisor command/query/event actors depend only on bounded runtime services required for their operation. Failure of
history persistence, UI notification, OpenTelemetry, or an optional projector cannot prevent current health reads or
core lifecycle control.

### 13.2 Self-failure behavior

The Supervisor never sends a failure event to itself from its last-resort exception boundary. It records directly into
its root context and host-level fallback state. A later healthy event actor may publish a durable incident.

### 13.3 Control availability

Normal operations use Supervisor actors. Minimal host-level controls remain outside the shared actor query path so an
operator can inspect and recover the actor scheduler when the Supervisor mailbox cannot run.

### 13.4 Bounded operation execution

Every Supervisor operation has:

- operation and command IDs;
- target and expected generation;
- accepted/start/terminal timestamps;
- cancellation and commit policy;
- maximum duration;
- maximum attempts;
- current step;
- primary and secondary failures; and
- deterministic completed, failed, timed-out, or manual-action-required outcome.

## 14. Actor lifecycle state machine

Use explicit states:

```text
Unregistered
Registered
Starting
Running
Pausing
Draining
Stopped
Restarting
Quarantined
Faulted
```

Allowed transitions are defined centrally and tested exhaustively. Invalid transitions return a structured failure;
they do not throw as routine control flow.

### 14.1 Actor stop

```text
validate target and expected generation
  -> close actor admission
  -> mark Pausing
  -> allow current handlers to reach safe boundary
  -> drain accepted work within policy
  -> stop actor-owned projectors/producers/resources
  -> fence current generation
  -> mark Stopped
  -> publish terminal operation outcome
```

The operation times out into `Quarantined` or `ManualActionRequired`; it does not loop indefinitely.

### 14.2 Actor start

```text
validate Stopped/Faulted target
  -> allocate next generation
  -> initialize mailbox/resources
  -> start owned projectors/producers
  -> verify readiness
  -> open admission
  -> mark Running
```

Partial startup failure rolls back only resources created by that attempt and records every rollback failure as
secondary evidence.

### 14.3 Actor restart

Restart is a composed, idempotent stop/start operation under one target operation gate. A retry with the same command
ID returns the original operation. A stale expected generation is rejected without mutating the current actor.

## 15. Entity-mailbox lifecycle

`ActorThreadId` controls are mailbox controls, not worker-thread controls.

### 15.1 States

```text
Created
Active
Paused
Draining
Idle
Retiring
Retired
Quarantined
```

### 15.2 Pause and drain

Pause closes admission for the target entity while leaving other entity mailboxes active. Drain waits for accepted
messages and the current handler to reach a safe terminal boundary. It never holds a lock while awaiting completion.

### 15.3 Restart generation

```text
pause entity admission
  -> finish current safe boundary
  -> resolve queued accepted messages by transport contract
  -> fence old mailbox generation
  -> retire old queue/state
  -> create next generation
  -> reload event-sourced state when applicable
  -> reopen admission
```

Late completion from an old generation cannot update new-generation metrics, state, readiness, or operation results.

### 15.4 Accepted-message treatment

- Commands and queries already consumed from Core transport must complete or receive a deterministic failure when a
  reply channel remains available.
- Durable events may be NAKed/deferred according to their consumer contract.
- Realtime latest-value messages may be coalesced only when the domain contract explicitly permits it.
- Confirmed committed work is never rolled back by cancellation.

## 16. Concurrency model

Lifecycle mutations use a per-target asynchronous gate owned by `SupervisorRuntimeContext.Operations`. The gate key is
actor mailbox ID plus optional entity ID. There is no global actor-system lifecycle lock.

The gate:

- serializes operations against the same target;
- supports bounded cancellation while waiting;
- is removed when idle and safe;
- never wraps unrelated actor work; and
- never remains held while waiting for an external caller response.

Hot-path metrics remain atomic/volatile and do not use the operation gate.

## 17. Implementation tranches and gates

### Tranche EH-0: Complete inventory and baseline

**Work**

- Inventory every `IActor` implementation and every base actor derivative.
- Identify overrides or custom `HandleMessageAsync` implementations.
- Map exception hooks, reply paths, message disposal, transport acknowledgement, and lifecycle resources.
- Capture current tests, exception logs, allocations, and throughput.

**Gate EH-G0**

- Every production actor maps to one reviewed exception boundary.
- Every transport maps to an explicit failure disposition.
- Baseline results are recorded before implementation.

### Tranche EH-1: Failure contracts and actor-owned evidence

**Work**

- Add failure-stage, outcome, severity, and delivery enums.
- Add bounded failure record and primary/secondary relationship.
- Add nonthrowing `IActorFailureSink` to actor-owned metrics and `SupervisorRuntimeContext`.
- Add outcome counters keyed by actor and `ActorThreadId`.
- Define one-log ownership and correlation requirements.

**Gate EH-G1**

- Failure recording works when normal logging is unavailable.
- Successful message processing adds no per-message heap allocation.
- Primary failure cannot be overwritten by secondary failure.

### Tranche EH-2: Command actor boundary

**Work**

- Harden parse, validation, audit, replay, execution, persistence, publication, exception handler, cleanup, and reply.
- Add generic failure result when typed exception handling fails.
- Preserve command ID and commit outcome.
- Record reply failure separately.

**Gate EH-G2**

- Exception injection at every command stage produces the expected result and log.
- A failed command cannot terminate the actor or shared worker.
- The following command on the same entity mailbox still executes when domain policy permits.

### Tranche EH-3: Query and function boundaries

**Work**

- Harden query parsing, context registration/removal, handler, exception reply, and transport reply.
- Harden function pre-commit, unknown-commit, post-commit, projection, persistence, result, and reply paths.
- Ensure correlation context is released exactly once.

**Gate EH-G3**

- Callers receive deterministic failures whenever transport permits.
- Reply failure is observable without re-executing confirmed work.
- No reply-context leak remains after any injected failure.

### Tranche EH-4: Event and realtime boundaries; denormalizer removal

**Work**

- Harden event typed exception handlers.
- Connect failure outcome to Core or JetStream delivery disposition.
- Inventory and standardize realtime actors.
- Add bounded poison-message and terminal policies.
- Complete source, compiled-reference, reflection-registration, and configuration inventory for denormalizers.
- Migrate any unexpectedly discovered production behavior to event projectors.
- Remove the legacy denormalizer base class, context, interfaces, helpers, registrations, and obsolete tests.
- Add an architecture rule preventing reintroduction of denormalizer actors.

**Gate EH-G4**

- Durable failures are redelivered/deferred/terminalized exactly as configured.
- Core and realtime loss limitations are explicit and observable.
- Later messages continue without corrupting entity ordering.
- No production or test assembly references the removed denormalizer actor contracts.
- Every former read-model responsibility, if one is discovered, has verified event-projector coverage.
- The complete solution builds and the actor/event-projector test suites pass after removal.

### Tranche EH-5: Shared worker and mailbox containment

**Work**

- Distinguish succeeded, handled-failed, and escaped-failed outcomes.
- Harden drain completion, rescheduling, retirement, and disposal.
- Add mailbox quarantine when scheduling ownership is uncertain.
- Expose genuine worker-loop fault and reduced pool capacity.
- Add bounded replacement-worker policy.

**Gate EH-G5**

- One actor failure cannot fault a worker.
- One mailbox infrastructure failure cannot affect unrelated mailboxes.
- Worker-loop failure remains visible through host fallback.

### Tranche EH-6: Supervisor actor boundary

**Work**

- Apply the hardened command/query/event boundary to Supervisor actors.
- Add nonrecursive self-failure recording.
- Separate required core dependencies from optional history/telemetry dependencies.
- Add bounded operation records and host-level fallback.

**Gate EH-G6**

- Supervisor actor-specific exception-handler failure is contained.
- Current health remains readable when history, telemetry, or Supervisor actor messaging fails.
- Supervisor cannot enter a self-reporting loop.

### Tranche LC-1: Actor lifecycle serialization

**Work**

- Add actor lifecycle state, admission gate, generation, and target operation gate.
- Replace direct best-effort start/stop with validated state-machine operations.
- Add safe drain and bounded terminal outcomes.
- Fence late old-generation work.

**Gate LC-G1**

- Concurrent start/stop/restart tests are deterministic.
- Stopped actors accept no new messages.
- Accepted messages are drained or resolved by documented transport policy.
- Old-generation completion cannot affect the new generation.

### Tranche LC-2: Entity-mailbox control

**Work**

- Add per-`ActorThreadId` admission and lifecycle state.
- Add pause, drain, quarantine, retire, and restart-generation operations.
- Preserve ordering and state reload.
- Keep shared workers independent of entity control.

**Gate LC-G2**

- One entity can be controlled without pausing unrelated entities.
- No two workers process the same entity generation concurrently.
- Restart does not lose or duplicate durable work.

### Tranche LC-3: Read-only UI exposure and controlled activation

**Work**

- Show failures, secondary failures, lifecycle state, generation, admission, drain, and operation result in Actor Health.
- Keep controls disabled until their corresponding lifecycle gate passes.
- Add authorization and audit before enabling mutations.

**Gate LC-G3**

- UI always distinguishes requested, running, completed, failed, timed-out, quarantined, and manual-action states.
- No control is enabled before its backend operation is verified.

## 18. Test matrix

### 18.1 Unit exception injection

For every base actor, inject an exception at each applicable stage:

```text
parse
validate
deduplicate
state replay
execute
persist
project
publish
typed exception handler
cleanup
result construction
reply
```

Assert:

- primary stage and exception are preserved;
- secondary failure is linked rather than substituted;
- exactly one terminal processing outcome is recorded;
- expected cancellation is not logged as an error;
- message and pooled payload are disposed exactly once;
- reply context is removed exactly once;
- subsequent message processing continues; and
- actor and worker state remain correct.

### 18.2 Transport tests

- Command request receives typed failure.
- Query request receives typed failure.
- Function returns typed failed terminal result.
- Reply transport failure records an incident without re-execution.
- Core event failure records unrecoverable delivery limitation.
- JetStream event failure NAKs or defers correctly.
- Delivery exhaustion terminalizes once.
- Projector replay has bounded attempts and complete error detail.

### 18.3 Worker and mailbox tests

- Failed message followed by successful message on same entity.
- Failed mailbox followed by work on another actor.
- More than 64 queued messages preserve ordering across worker handoff.
- Entity mailbox cannot be scheduled twice concurrently.
- Idle mailbox retained below limit and retired above limit.
- Queue retirement racing with admission preserves ownership/disposal.
- Worker-loop fault reduces reported capacity and host fallback remains responsive.

### 18.4 Lifecycle concurrency tests

- start/start;
- stop/stop;
- start while stopping;
- stop while starting;
- restart while running;
- restart while draining;
- stale-generation restart;
- timeout during drain;
- cancellation before commit;
- cancellation after commit;
- actor startup partial failure and rollback failure; and
- late old-generation completion.

### 18.5 Denormalizer removal verification

- Source search finds no production or test reference to the removed denormalizer types.
- Compiled assembly/reference inspection finds no dependency on the removed contracts.
- Actor registration tests find no reflection or convention registration for denormalizers.
- Configuration contains no denormalizer actor, subject, consumer, or route entry.
- Every read-model writer is mapped to its owning event projector.
- Event-projector startup, projection, replay, and terminal-failure tests remain green.
- The complete solution builds after removal.

### 18.6 Integration tests

- Real NATS Core command/query request/reply.
- Real JetStream durable event redelivery.
- Event-source actor replay and restart.
- Projector process/replay queues during actor stop/restart.
- Database unavailable during failure recording.
- Telemetry exporter unavailable during failure recording.
- Supervisor query actor unavailable with host fallback still readable.
- Five-minute controlled recovery scenario only after lifecycle gates pass.
- Test host and integration processes are confirmed exited after completion.

### 18.7 Performance and allocation tests

- No-listener success-path allocation baseline.
- Metrics-enabled success-path allocation.
- Exception-path bounded allocation.
- Throughput with one hot entity.
- Throughput with high entity cardinality.
- Supervisor snapshot during active processing.
- Lifecycle-operation gate contention for same and unrelated targets.

## 19. Logging verification

Every primary actor failure log includes:

- failure ID;
- actor type/name/entity;
- verb and bounded message type;
- actor/mailbox generation;
- active stage;
- error code/type/message;
- commit state;
- command/query/event/function identity;
- trace/correlation/operation identity;
- delivery attempt and retryability; and
- whether processing will continue, retry, quarantine, or terminalize.

Secondary logs include their own stage and exception plus the primary failure ID. Tests assert structured properties,
not formatted text alone.

## 20. Migration and compatibility

1. Add new failure and metric contracts without changing subject serialization.
2. Preserve reserved `ActorType` numeric values.
3. Keep existing aggregate metric names during transition; add unambiguous outcome metrics.
4. Migrate base actors before enforcing architecture rules against custom actors.
5. Use architecture tests to prevent new actors from bypassing the standard exception boundary.
6. Keep lifecycle controls read-only/disabled until each transport and state model passes its gate.
7. Preserve event/projector state schemas through additive migrations.

## 21. Risks and controls

| Risk | Control |
| --- | --- |
| Duplicate logging | Stable failure ID and single primary-log owner |
| Swallowed failure | Mandatory terminal outcome and actor-owned failure counter |
| Infinite recovery | Attempt/duration bounds and manual-action terminal state |
| Restart after commit duplicates work | Commit-state tracking, idempotency, reconciliation, generation fence |
| Stop drops Core messages | Admission closure and safe drain before stop |
| Entity restart affects other actors | Per-target gate; never stop shared worker for entity control |
| Metrics increase Ring 2 allocation | Atomic scalar entries created with mailbox; benchmarks gate rollout |
| Supervisor depends on failed scheduler | Direct root-context host fallback |
| Exception handler fails | Non-overridable last-resort boundary |
| Logger fails | Direct bounded failure sink and minimal fallback |

## 22. Definition of complete

Exception containment is complete when:

1. Every production actor implementation is inventoried and uses the standard outer boundary.
2. Every recoverable stage exception is caught and produces structured evidence.
3. Exception-handler, cleanup, publication, and reply failures cannot hide the primary failure.
4. Request/reply callers receive deterministic failure whenever transport remains available.
5. Durable event acknowledgement/redelivery remains correct.
6. One message or mailbox failure cannot terminate an actor or shared worker.
7. Success, handled failure, escaped failure, cancellation, and delivery failure are distinct metrics.
8. Supervisor self-failure cannot recurse and host-level health remains available.
9. Success-path allocation and throughput meet approved baselines.
10. Legacy denormalizer actor infrastructure is removed and architecture tests prevent its reintroduction.

Lifecycle hardening is complete when:

1. Actor operations are serialized per target.
2. Admission closes before stop or restart.
3. Accepted work reaches a documented safe boundary.
4. Actor and entity-mailbox generations fence late work.
5. Actor start/stop/restart is idempotent and returns a deterministic terminal result.
6. Entity-mailbox controls do not stop shared workers or unrelated entities.
7. All concurrency, transport, persistence, and recovery integration gates pass.
8. Actor Health displays the complete lifecycle and failure evidence.

## 23. Recommended execution order

Proceed in this order:

```text
EH-0 inventory/baseline
  -> EH-1 failure contracts and actor-owned evidence
  -> EH-2 command actors
  -> EH-3 query/function actors
  -> EH-4 event/realtime actors and denormalizer removal
  -> EH-5 worker/mailbox containment
  -> EH-6 Supervisor hardening
  -> LC-1 actor lifecycle
  -> LC-2 entity-mailbox lifecycle
  -> LC-3 UI activation
```

No actor restart control should be enabled before EH-G6 and LC-G1 pass. No entity-mailbox restart control should be
enabled before LC-G2 passes.
