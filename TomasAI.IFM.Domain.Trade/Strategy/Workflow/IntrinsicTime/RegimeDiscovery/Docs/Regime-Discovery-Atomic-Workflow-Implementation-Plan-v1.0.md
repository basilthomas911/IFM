# Regime Discovery Atomic Workflow Implementation Plan v1.0

| Item | Value |
|---|---|
| Status | Approved design; implementation gates not yet complete |
| Created | 2026-08-27 |
| Scope | Regime Discovery and the Strategy Workflow changes required to execute it safely |

Companion documents:

- `Regime-Discovery-Specification-v1.0.md`
- `Regime-Discovery-Implementation-v1.0.md`

## 1. Purpose

This document converts the approved Regime Discovery design into an ordered,
testable implementation plan. It is deliberately narrower than a general
workflow-engine redesign. It changes only what Regime Discovery needs now and
leaves the PostgreSQL `ConfigurationDbContext` design in place.

The safety objective is fail-closed progression:

> Strategy Workflow may advance only from a committed, current, unexpired
> Regime Discovery completion. Every exception, timeout, process loss, stale
> result, or lost post-commit notification must result in no forward progress.

The implementation provides atomic **state outcomes**, not a transaction that
spans calculation and persistence. Calculation performs no durable workflow
side effects. Its outer command handler commits either one successful terminal
outcome or one expected failure/timeout outcome. An unexpected exception may
commit no Regime Discovery outcome; Strategy Workflow then remains Started and
is recovered by its persisted deadline when a later command arrives.

## 2. Approved invariants

The following rules are mandatory and are gate acceptance criteria, not later
enhancements.

1. Strategy Workflow has one authoritative snapshot event contract:
   `WorkflowStrategyStateUpdatedEvent`. Each accepted state transition appends
   a new instance containing the complete immutable workflow view.
2. The event contract is singular, but one command may append more than one
   instance in one PostgreSQL transaction. In particular, a new Start may
   atomically append `TimedOut` for the expired execution and `Started` for the
   replacement execution.
3. A Started workflow always has a persisted `StartedAtUtc` and `ExpiresAtUtc`.
   `ExpiresAtUtc` is calculated once and is never extended by retries,
   restarts, projection delay, or a later terminal message.
4. Timeout wins at `now >= ExpiresAtUtc`. A completion received at or after the
   deadline cannot merge results or advance the workflow.
5. An unexpired Started workflow makes the entity busy. A second Start is
   rejected without changing state.
6. An expired Started workflow does not permanently make the entity busy. The
   next Start closes the old execution as TimedOut and starts the new execution
   in one atomic event batch.
7. Regime Discovery execution identity includes both
   `IntrinsicTimeStrategyWorkflowEntityId` and `StrategyWorkflowId`. Terminal
   state from an older workflow can neither block nor mutate a newer workflow
   for the same strategy entity.
8. `ExecuteRegimeDiscoveryPipelineCommand` replaces
   `StartRegimeDiscoveryPipelineCommand`. Execute means “attempt the complete
   calculation and commit its terminal outcome”; it does not create a durable
   Processing state.
9. The Regime Discovery private timeout covers snapshot acquisition and the
   full calculation. The outer handler alone owns the terminal state update.
   A timed-out worker is never allowed to commit a later success.
10. Expected domain failures and the private timeout commit a private Failed
    terminal event. Unexpected infrastructure/programming exceptions are
    returned and logged without inventing a successful state transition.
11. The Regime Discovery EventProjector writes the ScyllaDB read model and then
    publishes the public Completed or Failed notification. That notification
    is best effort. There is no outbox replay, projector replay, automatic
    redispatch, or workflow resume in V1.
12. `RegimeDiscoveryPipelineRealtimeActor` is stateless. It translates public
    Regime Discovery Completed/Failed notifications into Strategy Workflow
    Complete/Fail commands. It owns no timer, calculation, or durable state.
13. Lost notifications always fail closed. A missing workflow terminal update
    leaves the workflow Started until a later command observes its persisted
    expiry. It cannot advance toward order execution.
14. Every pipeline Execute command receives an immutable workflow view and the
    complete frozen parameter set selected for that execution, including its
    version/hash identity.
15. Existing PostgreSQL configuration storage remains authoritative. This plan
    does not introduce ScyllaDB configuration tables and does not generalize
    configuration CRUD to other command actors.
16. The revised workflow is not enabled until legacy workflow streams have
    either a valid `WorkflowStrategyStateUpdatedEvent` snapshot or are
    explicitly rejected. A stream with history but no new snapshot must never
    be interpreted as Empty/Free.
17. No Regime Discovery failure path may generate a later-pipeline command.
    This protection must be repeated at every future pipeline boundary before
    Order Execution is introduced.
18. Concurrent Starts for the same entity are serialized by the Command actor
    and protected by event-stream optimistic concurrency. Exactly one may
    observe Free and commit Started.

## 3. State and ownership model

### 3.1 Workflow machine state

The minimum workflow lifecycle states are:

| State | Busy for a new Start? | Can advance? | Meaning |
|---|---:|---:|---|
| `Empty` | No | No | No workflow snapshot exists for the entity. |
| `Started` | Yes, while unexpired | Only from a valid completion | A pipeline execution is outstanding. |
| `Completed` | No | No further stage | Every required pipeline in the workflow completed successfully. |
| `Failed` | No | No | A current, unexpired execution failed. |
| `TimedOut` | No | No | The persisted hard deadline was reached. |
| `Cancelled` | No | No | An explicit supported cancellation closed the workflow. |

`Completed`, `Failed`, `TimedOut`, and `Cancelled` are terminal for the current
workflow execution. Completing Regime Discovery while more pipelines remain
does not set the whole workflow to Completed: the new snapshot remains Started,
moves `CurrentStage` to the next pipeline, and carries the committed Regime
output. “Free” means there is no unexpired Started execution; it does not mean
old history was deleted.

### 3.2 Immutable workflow view

`WorkflowStrategyStateUpdatedEvent` carries a complete immutable view rather
than exposing the mutable Command-state object. The view must contain at least:

- entity identity, workflow identity, correlation identity, and revision;
- lifecycle status and current/last pipeline stage;
- `StartedAtUtc`, `UpdatedAtUtc`, and `ExpiresAtUtc`;
- the trigger-event identity/data required by downstream pipelines;
- an immutable per-pipeline view containing its status, input revision,
  timestamps, parameter identity/hash, committed output, and failure when
  applicable;
- committed outputs from every completed prior pipeline, so a current pipeline
  never has to reconstruct or mutate a previous pipeline actor's state;
- the current failure/timeout descriptor when terminal;
- the frozen configuration/parameter identities and hashes used so far; and
- causation/command identities needed for observation and idempotency.

Collections must be copied to immutable/read-only values at the contract
boundary. A pipeline actor receives this view as input and cannot mutate the
Strategy Workflow aggregate.

The first Regime Discovery implementation may carry the current complete
Regime parameter payload on its Execute command, as it does today. The workflow
view records its immutable identity/version/hash so the calculation is
auditable. Expanding versioned configuration CRUD for other actors is deferred.

### 3.3 State transition rules

All comparisons use the injected `TimeProvider` and UTC.

| Incoming command | Current snapshot | Time/identity condition | Atomic result |
|---|---|---|---|
| Start workflow | Empty or terminal | valid request | Append Started snapshot; dispatch occurs only after commit. |
| Start workflow | Started | `now < ExpiresAtUtc` | Reject Busy; append nothing. |
| Start workflow | Started | `now >= ExpiresAtUtc` | Append TimedOut(old), then Started(new), in one transaction. |
| Complete Regime Discovery | Started | same entity/workflow/revision and `now < ExpiresAtUtc` | Merge result and append the next valid workflow snapshot. |
| Complete Regime Discovery | Started | same identity and `now >= ExpiresAtUtc` | Append TimedOut; discard result; do not advance. |
| Fail Regime Discovery | Started | same identity, non-timeout failure, and `now < ExpiresAtUtc` | Append Failed; do not advance. |
| Fail Regime Discovery | Started | timeout-classified failure or `now >= ExpiresAtUtc` | Append TimedOut; do not advance. |
| Any terminal input | terminal state | exact idempotent duplicate | Succeed/no-op; append nothing. |
| Any terminal input | different or older workflow/revision | stale | Reject/no-op; append nothing. |

The exact deadline boundary is intentionally conservative: equality is a
timeout. An accepted Completed transition must therefore prove
`receivedAtUtc < ExpiresAtUtc` inside the Workflow Command actor.

### 3.4 Notification and recovery semantics

PostgreSQL event commits are authoritative. ScyllaDB projection and Realtime
notifications are conventional post-commit work and are not authoritative.

```text
Workflow state commit
  -> best-effort StateUpdated projection/notification
  -> Execute Regime Discovery
  -> private Regime terminal commit
  -> best-effort public terminal projection/notification
  -> Workflow Complete/Fail command
  -> next Workflow state commit
```

Breaking this chain at any arrow stops progression. V1 does not attempt to
repair the broken arrow by replaying or redispatching it. The persisted
workflow deadline is the recovery boundary: a later Start closes an expired
Started execution before beginning a replacement.

## 4. Current-to-target change map

| Current implementation | Target implementation |
|---|---|
| `StartRegimeDiscoveryPipelineCommand` with verb `Start` | `ExecuteRegimeDiscoveryPipelineCommand` with verb `Execute` |
| Regime state/stream effectively routed by workflow entity | Composite Regime execution subject containing entity + workflow ID |
| Several workflow lifecycle/stage event types | One authoritative `WorkflowStrategyStateUpdatedEvent` snapshot type |
| Workflow repository replays the complete stream | Runtime loads the latest authoritative snapshot event |
| Workflow `ExpectedCompletionAtUtc` may be null | Mandatory persisted `ExpiresAtUtc` derived from a fixed maximum duration |
| Active workflow is simply rejected | Unexpired is rejected; expired is closed and replaced atomically |
| Workflow Realtime actor consumes Regime terminal events | New Regime Realtime actor owns terminal translation |
| Workflow Realtime parser fans later-stage Completed/Failed verbs into Regime event types | Parse one typed Workflow StateUpdated notification and dispatch by `CurrentStage` |
| No complete hard-timeout ownership rule | Regime outer handler races all work against the fixed deadline and owns the only terminal update |
| Entity-level Regime terminal state can collide with later workflows | Per-workflow execution stream isolates every attempt |
| Redispatch/timeout recovery contracts exist | No automatic redispatch/replay/resume; lazy expiry is authoritative |
| Configuration is resolved from PostgreSQL and embedded in Start | Keep PostgreSQL; freeze and embed it in Execute with identity/version/hash |

## 5. Gate sequence

The gates are intentionally ordered. A later gate must not be merged or enabled
before every dependency gate is green.

```text
RD-19A Baseline
   -> RD-19B Contracts and identity
   -> RD-19C Snapshot state and legacy safety
   -> RD-19D Workflow transitions
   -> RD-19E Workflow post-commit dispatch
   -> RD-19F Atomic Regime execution and timeout
   -> RD-19G Regime projection and Realtime translation
   -> RD-19H Observation
   -> RD-19I Test qualification
   -> RD-19J Controlled enablement
```

### RD-19A — Baseline and change isolation

Steps:

1. Record `git status` and preserve all unrelated/user-owned changes.
2. Record the current solution build and the Trade unit, BDD, and integrated
   test baselines.
3. Confirm the current feature is disabled in environments where a partial
   contract migration could receive live workflow traffic.
4. Inventory existing PostgreSQL workflow streams and determine whether any
   contain legacy events without the new snapshot contract.
5. Freeze the contract names, state values, timeout boundary, and composite
   identity encoding before changing MessagePack contracts.

Exit gate:

- baseline results and existing failures are recorded;
- live traffic cannot enter a mixed old/new route; and
- legacy stream count and migration disposition are known.

#### RD-19A execution record — 2026-08-27

Repository baseline:

| Item | Recorded result |
|---|---|
| Branch/commit | `main` at `37ecdd5057025cb140a4181b0bb4b2f10a389d4f` |
| SDK | .NET SDK `10.0.302` |
| Worktree | Pre-existing Regime command-file move and documentation edits recorded and preserved; no unrelated file was restored or overwritten. |
| Feature gate | `AppSettings:IntrinsicTimeStrategyWorkflow:Enabled` is `false`; startup also defaults the missing setting to `false`. |
| Solution build | Succeeded with 0 errors and 1 unrelated native DataBento concurrent-file warning. |
| Trade Unit baseline | 127 passed, 0 failed, 0 skipped. |
| Trade BDD baseline | 6 passed, 0 failed, 0 skipped. |
| Trade Integrated baseline | 39 passed, 0 failed, 2 unrelated TradePlan tests skipped. |
| Workflow storage baseline | 1 targeted test passed, 0 failed, 0 skipped. |
| Local PostgreSQL audit | 63 workflow streams, all 63 legacy-only, containing 894 stream events and no `WorkflowStrategyStateUpdatedEvent`. |

Disposition:

- the local test streams are retained as legacy migration/fail-closed test data
  until RD-19C decides whether the test environment is reset or migrated;
- no revised runtime may treat any of those 63 streams as Empty;
- deployed environments must repeat the same read-only inventory before
  RD-19J enablement because repository access cannot prove their contents; and
- the names, state values, exact `now >= ExpiresAtUtc` boundary, composite
  entity/workflow identity, and MessagePack append-only rule in sections 2 and
  3 are frozen for RD-19B.

RD-19A is complete for the repository and local test environment. Live
enablement remains blocked until each deployment database has an explicit
legacy-stream disposition.

### RD-19B — Shared contracts and execution identity

Steps:

1. Add the composite Regime Discovery execution identity/subject builder using
   `IntrinsicTimeStrategyWorkflowEntityId + StrategyWorkflowId`.
2. Replace the shared pipeline command with
   `ExecuteRegimeDiscoveryPipelineCommand`; use Actor
   `RegimeDiscoveryPipelineCommand` and Verb `Execute`.
3. Make `ExpiresAtUtc` mandatory and retain the immutable workflow view,
   trigger, parameter payload, parameter version/hash, correlation, and
   causation fields.
4. Add the workflow machine status, immutable workflow view, and
   `WorkflowStrategyStateUpdatedEvent` contracts.
5. Preserve MessagePack key compatibility: never reorder/reuse existing keys;
   a genuinely replacement contract gets a new type/name and append-only keys.
6. Add a single Regime execution option such as
   `MaximumExecutionDuration`; validate it as positive and bounded at startup.
7. Remove the old Start command from live routing. Do not allow old and new
   commands to execute the same calculation concurrently.

Exit gate:

- contract serialization round trips preserve every field;
- identity tests prove two workflow IDs for the same entity produce different
  actor subjects/streams; and
- all producers/consumers compile against Execute only.

#### RD-19B execution record — 2026-08-27

Implemented:

- added `RegimeDiscoveryExecutionEntityId`, combining the stable workflow
  entity and `StrategyWorkflowId` into an isolated execution subject/stream;
- replaced the live Regime Start contract, route, actor maps, handler, and
  projector metadata with `ExecuteRegimeDiscoveryPipelineCommand` using Actor
  `RegimeDiscoveryPipelineCommand` and Verb `Execute`;
- added mandatory `ExpiresAtUtc`, complete immutable workflow input,
  correlation/causation, trigger, and versioned parameter identity to Execute;
- added `WorkflowStrategyMachineStatus`,
  `IntrinsicTimeStrategyWorkflowView`, and
  `WorkflowStrategyStateUpdatedEvent` MessagePack contracts;
- extended each pipeline stage view append-only with its input revision,
  parameter version/hash, and execution deadline;
- added startup-validated `MaximumExecutionDuration`, defaulting to two
  minutes and bounded from one second through one hour; and
- retained the disabled workflow feature default so this contract-only gate
  cannot receive live traffic before snapshot migration and atomic-transition
  gates are complete.

Verification:

| Check | Recorded result |
|---|---|
| Execute contract inventory | The old shared Start type is absent; exactly one Regime Execute command is discoverable and routed. |
| MessagePack contracts | Sequential-key/serialization-constructor checks and populated byte-stable round trips passed for Execute, the immutable view, the snapshot event, execution identity, stage state, and machine status. |
| Execution isolation | Same workflow entity with two workflow IDs produces different composite IDs, actor subjects, and stream IDs. |
| Live reference audit | No `StartRegimeDiscoveryPipeline` reference remains in C# production code or runtime JSON; remaining mentions are migration history or the negative absence assertion. |
| Solution build | Succeeded with 0 warnings and 0 errors. |
| Trade Unit | 133 passed, 0 failed, 0 skipped. |
| Trade BDD regression | 6 passed, 0 failed, 0 skipped. No new business scenario is activated by this contract-only gate. |
| Trade Integrated | 39 passed, 0 failed, 2 unrelated TradePlan tests skipped; the runtime harness parses and identifies Execute. |
| Workflow storage contract | 2 passed, 0 failed, 0 skipped. |
| Diff validation | `git diff --check` passed; only line-ending conversion notices were emitted. |

RD-19B is complete. The new state-update event is a contract only at this
gate. Making it the sole authoritative reducer/load snapshot, and failing
closed on the 63 known legacy-only local streams, is intentionally deferred to
RD-19C so old and new recovery semantics are not mixed.

### RD-19C — Snapshot state and legacy-stream safety

Steps:

1. Refactor Workflow Command state so
   `WorkflowStrategyStateUpdatedEvent` is the authoritative apply/snapshot
   event and reconstructs the complete immutable view.
2. Change the repository runtime load path to select the latest state-update
   snapshot, not replay old events for dispatch or recovery.
3. Detect “stream exists but no state-update snapshot.” Return a hard migration
   error and keep the actor unavailable; never construct Empty state.
4. Choose and execute one pre-enable legacy action:
   - write one explicit migrated snapshot per legacy stream with a reviewed
     one-time migration, or
   - archive/reset confirmed non-production development streams.
5. Ensure the migration performs state reconstruction only. It must not project
   events, publish notifications, or dispatch pipeline commands.
6. Keep historical events immutable. The latest state-update snapshot becomes
   the only runtime state input after migration.

Exit gate:

- current state loads from one latest snapshot;
- a legacy-only non-empty stream fails closed;
- migration cannot emit live work; and
- restart tests reconstruct the exact view without redispatch.

#### RD-19C implementation record — 2026-08-27

Implemented:

- the Workflow Command state now accepts only
  `WorkflowStrategyStateUpdatedEvent` as an authoritative reducer input;
- the repository loads the latest snapshot and records its PostgreSQL stream
  version instead of rebuilding runtime state from legacy workflow events;
- a non-empty stream without a snapshot, or with an event after its latest
  snapshot, throws `LegacyWorkflowStreamException` and leaves the actor
  unavailable;
- snapshot load/restart performs no projection, notification, dispatch, replay,
  resume, or redispatch; and
- historical events remain immutable.

Legacy disposition:

- the 63 known legacy-only local development streams remain deliberately
  retained as fail-closed audit data;
- the selected pre-enable action is archive/reset after the environment owner
  confirms the database is disposable; no destructive database operation was
  performed as part of the code gate; and
- production enablement remains blocked until each target database repeats the
  inventory and completes an explicit archive/reset or reviewed migration.

Verification:

| Check | Recorded result |
|---|---|
| Latest snapshot recovery | Repository unit test selects the greatest stream version and reconstructs the complete view. |
| Legacy safety | A non-empty legacy-only stream throws `LegacyWorkflowStreamException`; it is never treated as Empty. |
| No live migration/recovery work | Load tests prove the projector is not invoked. |
| Restart safety | Unit, BDD, and real-actor integration checks reconstruct state without pipeline redispatch. |

RD-19C code paths and tests are complete. Its environment-specific destructive
legacy cleanup is intentionally an RD-19J enablement prerequisite.

### RD-19D — Strategy Workflow atomic transitions

Steps:

1. Inject/use `TimeProvider` in every time-sensitive transition.
2. On Start, compute and persist one fixed `ExpiresAtUtc` and append a Started
   snapshot before any pipeline dispatch can occur.
3. Reject a Start when the current Started snapshot is unexpired.
4. When Started is expired, append TimedOut(old) and Started(new) state-update
   snapshots in the same `SaveStateAsync` PostgreSQL event batch.
5. Validate Complete/Fail using entity ID, workflow ID, expected revision,
   current stage, and deadline.
6. Enforce timeout precedence inside the Command actor—not only in a Realtime
   actor or timer callback. A timeout-classified Regime failure closes the
   workflow as TimedOut even if clock skew makes command receipt appear just
   before the persisted deadline.
7. Make exact duplicates idempotent and reject stale/mismatched terminal input.
8. Remove/disable Regime paths that automatically redispatch, replay, resume,
   or separately mutate timeout outside this transition table.
9. Generate a later-pipeline command only from the committed valid completion
   state. No Failed/TimedOut branch may reach the next stage.
10. Retain actor serialization and PostgreSQL expected-revision/concurrency
    checks so two concurrent Starts cannot both commit Started.

Exit gate:

- the transition table in section 3.3 is fully covered by unit tests;
- expired-old/new-start snapshots commit atomically; and
- no state is visible in which both old and new workflows are active.

#### RD-19D execution record — 2026-08-27

Implemented:

- every accepted transition emits only a complete state-update snapshot and
  uses the injected `TimeProvider`;
- Start persists one fixed deadline, treats an unexpired Started state as Busy,
  and appends TimedOut(old) plus Started(new) in one pending event batch when
  lazy expiry permits replacement;
- Complete/Fail validates entity, workflow, revision, current stage, and fixed
  deadline, with `now >= ExpiresAtUtc` and timeout-classified failures taking
  timeout precedence;
- duplicates and stale/mismatched terminal inputs are no-ops;
- later-stage dispatch is reachable only from a valid completion snapshot; and
- PostgreSQL persistence now supports expected-stream-version atomic batches,
  rolling the complete batch back when the caller is stale.

Verification:

| Check | Recorded result |
|---|---|
| Transition matrix | Empty/terminal Start, Busy, lazy replacement, before/equal/after deadline, failure classification, duplicate, and stale cases pass. |
| PostgreSQL atomicity | A two-event expected-version batch commits at contiguous versions; a stale two-event batch commits nothing. |
| BDD behavior | 8 passed, covering D/W/M starts, valid completion, failure, timeout, lost notification, lazy replacement, and old late completion. |
| Trade Unit | 139 passed, 0 failed, 0 skipped. |

RD-19D is complete.

### RD-19E — Workflow projection and post-commit dispatch

Steps:

1. Update the Workflow EventProjector to project the complete immutable view
   from `WorkflowStrategyStateUpdatedEvent` to the ScyllaDB read model.
2. Publish the state-update notification only after the PostgreSQL commit.
3. In the Workflow Realtime actor, dispatch
   `ExecuteRegimeDiscoveryPipelineCommand` only for a committed Started state
   whose current stage is Regime Discovery.
4. Resolve the existing PostgreSQL Regime parameter set and place the frozen
   payload plus version/hash on Execute. Do not introduce new configuration
   storage in this gate.
5. Derive a deterministic Execute command ID from the workflow/stage/revision
   so duplicate notifications address the same execution.
6. Remove Regime Completed/Failed translation routes from the Workflow
   Realtime actor; those move to the Regime Realtime actor in RD-19G.
7. Remove the existing later-stage terminal-verb fan-in that deserializes
   Market Condition, Trade Selection, Order Composition, and Risk Management
   messages as Regime Discovery event types. Dispatch from the typed workflow
   view's `CurrentStage` instead.
8. Treat a projection/publish/dispatch failure as an observable stopped chain.
   Do not schedule replay.

Exit gate:

- no Execute is dispatched before the Started commit;
- only a Started/Regime state-update can dispatch Execute;
- duplicate delivery cannot create a second Regime execution stream; and
- configuration behavior remains unchanged except for the Execute rename.

#### RD-19E execution record — 2026-08-27

Implemented:

- the Workflow EventProjector projects only complete state-update snapshots to
  the Scylla detail, history, active, timeline, and start-attempt views;
- the repository invokes projection/publication only after the PostgreSQL
  expected-version batch returns committed events;
- the Workflow Realtime actor dispatches deterministic Regime Execute only for
  a Started/Regime snapshot, using the composite execution identity and frozen
  PostgreSQL parameter payload/version/hash/deadline;
- Regime Completed/Failed translation routes and the invalid later-pipeline
  Regime-type fan-in were removed from Workflow Realtime; and
- the projector exposes no workflow rebuild path, disables durable replay, and
  schedules no retry after projection/publication failure.

Verification:

| Check | Recorded result |
|---|---|
| Commit boundary | Repository tests prove commit occurs before projection; a concurrency exception invokes no projector. |
| Dispatch filter | Unit tests prove only Started/Regime builds Execute; terminal and later-stage snapshots do not. |
| Determinism/isolation | Duplicate snapshot input produces the same command ID and composite execution entity. |
| Real actor topology | 2 passed: three committed starts dispatch real isolated Regime executions without advancing; a Busy second trigger commits no state or later work. |
| Trade Integrated regression | 39 passed, 0 failed, 2 unrelated TradePlan tests skipped. |
| Workflow storage qualification | 4 passed, including schema/CQL contracts and the two expected-version PostgreSQL cases. |
| Solution build | Succeeded with 0 warnings and 0 errors. |

RD-19E is complete. The workflow intentionally remains Started after the real
Regime calculation finishes until RD-19F/G add atomic Regime terminal ownership
and the new Regime Realtime translator. The feature remains disabled.

### RD-19F — Atomic Regime execution and private timeout

Steps:

1. Route the Regime Command actor and repository through the composite
   execution identity from RD-19B.
2. Rename the command handler/receive-map route from Start to Execute.
3. Refactor the calculation body to return a pure result/failure outcome. It
   must not update actor state from worker tasks or specialist models.
4. Calculate remaining time from the persisted `ExpiresAtUtc`; never create a
   fresh deadline when Execute is received.
5. If Execute begins at or after expiry, commit the private timeout failure
   without starting snapshot/calculation work.
6. Race the entire snapshot-acquisition and calculation operation against the
   remaining time. Propagate cancellation where supported.
7. Let the outer handler select exactly one winner and perform exactly one
   state update: Completed, expected Failed, or timeout Failed.
8. Ensure work that ignores cancellation has no durable side effects and no
   reference capable of committing a late success after timeout.
9. Preserve the current rule for unexpected exceptions: return/log the error
   and append no fabricated terminal event.
10. Keep duplicate Execute handling idempotent for the same composite execution
    and reject conflicting payload hashes/revisions.

Exit gate:

- success, domain failure, timeout-before-start, timeout-during-snapshot, and
  timeout-during-calculation each commit no more than one terminal event;
- a late worker cannot overwrite timeout;
- process loss before commit leaves no terminal outcome; and
- a previous workflow terminal state cannot block a new workflow execution.

#### RD-19F execution record — 2026-08-27

Implemented:

- Execute is isolated by the composite workflow/execution identity;
- the snapshot-plus-calculation worker returns a pure outcome and cannot append
  state from a worker continuation;
- the outer handler races all work against the remaining persisted deadline,
  gives `now >= ExpiresAtUtc` timeout precedence, and appends one terminal
  private event;
- cancellation is propagated, while a worker that ignores it retains no
  durable commit capability; and
- unexpected exceptions append no fabricated terminal event. Matching
  duplicates are no-ops and conflicting revision/hash inputs cannot execute.

Verification:

| Check | Recorded result |
|---|---|
| Terminal ownership | Success, expected failure, timeout-before-work, timeout-during-work, and exact-deadline tests each append at most one terminal event. |
| Late worker | A controllable worker completed after timeout cannot overwrite the committed timeout. |
| Exception behavior | An unexpected worker exception propagates with no pending state event. |
| Isolation | Concurrent distinct workflow execution identities complete independently. |

RD-19F is complete.

### RD-19G — Regime projector and Realtime translation

Steps:

1. Keep the Regime EventProjector order: private terminal commit, ScyllaDB
   result/failure write, then public Completed/Failed notification.
2. Address the public event to `RegimeDiscoveryPipelineRealtime` with workflow
   identity, input revision, deadline, causation, and result/failure data.
3. Add `RegimeDiscoveryPipelineRealtimeContext` and the stateless
   `RegimeDiscoveryPipelineRealtimeActor`.
4. Route public Completed to `CompleteRegimeDiscoveryCommand` and public Failed
   to `FailRegimeDiscoveryCommand` on the Workflow Command actor.
5. Use deterministic command IDs so a duplicate public notification becomes an
   idempotent Workflow command.
6. Register both terminal routes at startup and add route/subject tests.
7. Do not add a Processing event, actor timer, durable inbox, retry queue,
   redelivery store, or replay endpoint.

Exit gate:

- the Workflow Realtime actor no longer consumes Regime terminal events;
- the Regime Realtime actor owns both translations and no state;
- projector failure/lost notification produces no workflow advancement; and
- duplicate/late terminal notification is harmless at the Workflow gate.

#### RD-19G execution record — 2026-08-27

Implemented:

- the private terminal projector writes the Regime ScyllaDB projection before
  best-effort publication of a public terminal notification;
- public Completed/Failed contracts target `RegimeDiscoveryPipelineRealtime`
  and carry workflow ID, input revision, fixed deadline, causation/correlation,
  and terminal data;
- the new Realtime actor is stateless, owns exactly the Completed and Failed
  routes, and produces deterministic guarded Workflow Complete/Fail commands;
- Workflow Realtime no longer consumes Regime terminal events; and
- no Processing route, timer, inbox, durable replay, retry queue, rebuild, or
  redispatch endpoint was added.

Verification:

| Check | Recorded result |
|---|---|
| Route ownership | Architecture tests prove only the Regime Realtime actor owns the two public terminal routes. |
| Deterministic translation | Duplicate Completed/Failed notifications produce the same Workflow command identity and preserve all guards. |
| Project-before-publish | Real topology observes the Regime projection before the matching Workflow snapshot/next-stage dispatch. |
| Fail closed | Failure and timeout terminal paths produce no next-pipeline command. |

RD-19G is complete.

### RD-19H — Observation and operational visibility

Steps:

1. Project every committed state-update snapshot to the workflow observation
   view, including status, stage, start, expiry, revision, workflow ID, and
   failure/timeout reason.
2. Show a Started workflow whose `ExpiresAtUtc` is in the past as an operational
   issue even if its authoritative snapshot has not yet been lazily closed.
   This is a derived UI observation, not an automatic state mutation.
3. Expose the last Regime private terminal result/failure and whether its public
   notification has produced a matching Workflow terminal snapshot where that
   correlation is available.
4. Add structured logs/metrics for busy rejection, lazy expiry, stale terminal,
   deadline precedence, projector failure, notification loss symptoms, and
   migration-blocked streams.
5. Do not add configuration UI/CRUD expansion in this gate.

Exit gate:

- operations can distinguish Running, expired-but-not-closed, Failed,
  TimedOut, Completed, and migration-blocked;
- observation never advances or repairs a workflow; and
- entity/workflow/correlation IDs link the Regime and Workflow views.

#### RD-19H execution record — 2026-08-27

Implemented:

- `GetIntrinsicTimeStrategyWorkflowObservationQuery` reads the authoritative
  snapshot without writing it and composes it with the latest Regime terminal
  projection;
- the observation exposes entity/workflow/correlation identity, machine
  status, stage, revision, start, fixed expiry, terminal time, and stop reason;
- Started at or beyond expiry is derived as `ExpiredNotClosed`; the query does
  not close, repair, redispatch, or otherwise mutate the workflow;
- Regime acceptance is correlated by workflow ID, input revision, and source
  event ID, with an expired unmatched terminal exposed as likely notification
  loss; and
- Running, Failed, TimedOut, Completed, Cancelled, NotStarted, and
  MigrationBlocked have distinct values. Structured logs cover Busy, lazy
  expiry, stale terminal, deadline precedence, expired/unclosed,
  notification-loss symptom, and migration-blocked conditions.

Verification:

| Check | Recorded result |
|---|---|
| Derived expiry | Exact-boundary tests classify Started as expired without changing serialized state. |
| Terminal classification | Failed, TimedOut, Completed, and migration-blocked are independently observable. |
| Correlation | Matching and mismatching Regime source-event cases are covered. |
| Transport | The operational query contract round-trips through MessagePack. |

RD-19H is complete. Configuration UI/CRUD remains deliberately out of scope.

### RD-19I — Test qualification

Steps:

1. Complete the unit, BDD, integration, storage, serialization, and
   architecture tests listed in section 7.
2. Run the Trade Shared/Trade builds and the Trade unit, BDD, and integrated
   test projects.
3. Run relevant Application.Storage integration tests for snapshot selection,
   event-batch atomicity, and Scylla projections.
4. Run repeatable timeout tests with a fake `TimeProvider` or controllable
   task gates; do not depend on wall-clock sleeps.
5. Record performance of the calculation inside the selected maximum duration
   and set the configured limit with explicit operational headroom.
6. Search the repository for old Regime Start and Processing live routes and
   verify no executable producer remains.

Exit gate:

- every mandatory scenario is green and deterministic;
- zero old live routes remain;
- build introduces no warnings; and
- the measured timeout is valid for the supported workload.

#### RD-19I execution record — 2026-08-27

| Check | Recorded result |
|---|---|
| Trade Unit | 157 passed, 0 failed, 0 skipped. |
| Trade BDD | 8 passed, 0 failed, 0 skipped. |
| Trade Integrated | 41 passed, 0 failed, 2 unrelated TradePlan tests skipped. |
| Storage qualification | 32 targeted workflow/snapshot/event-batch/Scylla tests passed. |
| Timeout determinism | Unit timeout races use controllable tasks and clocks; no wall-clock sleeps select a winner. |
| Controlled real topology | 4 passed: multi-entity success, Busy rejection, expected failure, and forced one-second timeout. |
| Supported workload headroom | Three concurrent real Regime successes reached their next committed stage within the 10-second polling envelope; the configured production maximum is 120 seconds (at least 12x observed end-to-end headroom). |
| Legacy route search | No `StartRegimeDiscoveryPipeline` executable contract/route and no production Regime Processing producer/route remain. The unused Processing contract is retained only as a compatibility type. |
| Solution build | `dotnet build TomasAI.IFM.sln --no-restore -m:1` succeeded with 0 warnings and 0 errors. |

The solution must currently be built serially because two pre-existing
Databento native targets configure the same CMake output directory; a parallel
solution build can race in `configure_file`. This is unrelated to RD-19.

RD-19I is complete.

### RD-19J — Controlled enablement

Steps:

1. Verify all legacy streams are migrated/reset or deliberately blocked.
2. Deploy shared contracts and all producers/consumers as one coordinated
   release; do not operate a mixed contract topology.
3. Start with workflow triggering disabled, validate actor route registration
   and read-model health, then enable for a controlled strategy/entity set.
4. Exercise one successful, one expected failure, and one forced-timeout run.
5. Verify only the successful, unexpired run creates the next-pipeline command.
6. Verify an expired Started run is closed and replaced by a later Start in one
   committed batch.
7. Expand enablement only after observation shows no stale-route, migration,
   duplicate-execution, or deadline anomalies.

Exit gate:

- the revised Regime flow is live only on the new contracts;
- fail-closed behavior is proven in the deployed topology; and
- rollback consists of disabling new Starts, not replaying old messages.

#### RD-19J controlled-readiness record — 2026-08-27

Completed in the controlled integration topology:

- production configuration remains disabled by default and no destructive
  legacy cleanup or external deployment was performed;
- the complete new contract topology starts together and actor route
  registration/read-model health are exercised before test-only enablement;
- successful, expected-failure, and forced-timeout executions ran through the
  real actors, NATS, PostgreSQL event source, and ScyllaDB projections;
- only successful, unexpired Regime completion dispatched Market Condition;
  failure and timeout dispatched no later pipeline;
- lazy expired replacement and old late-terminal rejection remain covered by
  atomic BDD/unit transition tests; and
- the 63 known local legacy-only streams remain deliberately blocked by
  `LegacyWorkflowStreamException`, satisfying fail-closed behavior without
  authorizing deletion.

Repository readiness for RD-19J is complete. The deployed-environment exit
gate remains an explicit operations action: inventory and archive/reset each
target environment, deploy all contracts/producers/consumers as one release,
enable only an approved strategy/entity cohort, observe it, and expand only if
no anomaly is present. Rollback is disabling new Starts; replay is not used.

## 6. File-level implementation map

Names below describe the intended ownership. Exact file grouping may follow
the existing project conventions, but responsibilities must not move across
the stated project boundaries.

| Project/area | Action | Responsibility |
|---|---|---|
| `Domain.Trade.Shared/.../Pipeline/Commands` | Replace | Add `ExecuteRegimeDiscoveryPipelineCommand`; retire live use of Start. |
| `Domain.Trade.Shared/.../Identity` | Add | Composite Regime execution identity and deterministic subject/stream encoding. |
| `Domain.Trade.Shared/.../Model` | Add/refactor | Workflow machine status and complete immutable workflow view. |
| `Domain.Trade.Shared/.../Events` | Add/replace | `WorkflowStrategyStateUpdatedEvent` snapshot contract. |
| `Domain.Trade/.../IntrinsicTime/Command/State` | Refactor | Apply latest snapshot and implement deadline-aware transition rules. |
| `Domain.Trade/.../IntrinsicTime/Command/Actor` and `Extensions` | Refactor | Start, Complete, Fail, lazy-expiry, idempotency, and next-stage gates. |
| `Domain.Trade/.../IntrinsicTime/Command/EventProjector` | Refactor | Project/publish state-update snapshots after commit. |
| `Domain.Trade/.../IntrinsicTime/Realtime` | Refactor | Dispatch Execute from committed Started; stop consuming Regime terminal events. |
| `Domain.Trade/.../RegimeDiscovery/Command` | Rename/refactor | Execute route and outer atomic outcome/timeout owner. |
| `Domain.Trade/.../RegimeDiscovery/Command/State` | Refactor | Per-workflow execution state and idempotency. |
| `Domain.Trade/.../RegimeDiscovery/Command/EventProjector` | Retain/refactor | Scylla write followed by best-effort terminal notification; no replay. |
| `Domain.Trade/.../RegimeDiscovery/Realtime/Actor` | Add | Stateless Completed/Failed to Workflow Complete/Fail translation. |
| `Domain.Trade/.../RegimeDiscovery/Options` | Add | Validated maximum execution duration if no existing options home is suitable. |
| `Application.Storage/TradeDb` | Refactor only as required | Workflow/Regime read models; no configuration authority. |
| `Application.Storage/ConfigurationDb` | Hold | Continue current PostgreSQL Regime parameter lookup; no general redesign. |
| `Domain.Trade.UnitTests` | Expand | Contracts, state table, identity, timeout race, actor ownership. |
| `Domain.Trade.BDDTests` | Expand | Business-level busy/free, timeout, stale result, and fail-closed scenarios. |
| `Domain.Trade.IntegratedTests` | Expand | Actor/message flow and lost-notification boundaries. |
| `Application.Storage.IntegrationTests` | Expand | Snapshot load, legacy blocking, atomic batch, and projections. |

## 7. Mandatory test matrix

### 7.1 Contract and identity tests

- Execute MessagePack round trip includes the full immutable view, deadline,
  parameter payload/version/hash, workflow identity, and routing fields.
- StateUpdated MessagePack round trip includes every state-machine and pipeline
  view field.
- MessagePack numeric keys are unique and append-only.
- Same entity + different workflow ID yields different Regime subjects/streams.
- Deterministic Execute and terminal-translation command IDs are stable.
- No executable receive-map/route produces or consumes the legacy Processing
  event.
- No later-stage Completed/Failed message is deserialized as a Regime Discovery
  event type.

### 7.2 Workflow state-machine unit tests

- Empty -> Started with a fixed persisted deadline.
- terminal -> Started for a new workflow.
- unexpired Started + Start -> Busy with no event.
- expired Started + Start -> TimedOut(old), Started(new), one event batch.
- valid completion before expiry -> result merged and next stage selected.
- completion exactly at expiry -> TimedOut and no result merge.
- failure before expiry -> Failed.
- timeout-classified failure, or any failure at/after expiry -> TimedOut.
- duplicate completion/failure -> no additional event.
- stale workflow/revision/stage terminal -> no state change and no dispatch.
- restart from latest snapshot reproduces status, deadline, and all pipeline
  outputs.
- a legacy-only stream is blocked, not loaded as Empty.
- concurrent Starts for one entity yield one Started commit and one Busy/stale
  result, never two active workflows.

### 7.3 Regime atomic-execution unit tests

- successful calculation appends one private Completed event.
- expected validation/data/quality failure appends one private Failed event.
- Execute received after expiry skips work and appends timeout Failed.
- snapshot provider exceeds remaining duration -> one timeout Failed.
- calculation exceeds remaining duration -> one timeout Failed.
- completion racing the deadline has exactly one winner under the boundary
  rule; never Completed after timeout.
- cancelled/late worker cannot update state.
- unexpected exception appends no terminal event and returns/logs failure.
- duplicate Execute with matching input is idempotent.
- duplicate Execute with conflicting workflow revision or parameter hash is
  rejected.
- terminal state for workflow A does not block workflow B on the same entity.

### 7.4 BDD scenarios

- Given no current workflow, when Start is accepted, then a Started snapshot is
  committed before Regime Execute is sent.
- Given Regime completes before the deadline, when its notification reaches the
  Regime Realtime actor, then Workflow accepts Complete and may continue.
- Given Regime fails, then Workflow becomes Failed and no next pipeline starts.
- Given Regime times out, then Workflow becomes TimedOut and no next pipeline
  starts.
- Given the timeout notification is lost, then Workflow stays Started and does
  not continue.
- Given that Started workflow is now expired, when a new Start arrives, then the
  old workflow is closed and the new workflow starts atomically.
- Given a late completion from the old workflow, then it cannot affect the new
  workflow.
- Given the Started notification or Execute dispatch is lost, then no Regime
  completion exists and the same lazy-expiry rule recovers the entity later.
- Given the process restarts at any pre-commit point, then no workflow advances
  without a valid committed completion.

### 7.5 Integrated/storage tests

- PostgreSQL commits the expired-old/new-start pair atomically.
- Latest StateUpdated snapshot is selected correctly after restart.
- A non-empty legacy stream without a StateUpdated snapshot fails closed.
- Workflow projector persists the complete immutable view to ScyllaDB.
- Regime projector persists terminal data before public publication.
- Regime Realtime maps Completed/Failed to the correct Workflow actor subject.
- Suppressing each best-effort notification independently proves no downstream
  stage is dispatched.
- Duplicate public terminal notification is harmless.
- A terminal notification delivered after replacement workflow Start is stale
  and harmless.

### 7.6 Architecture tests

- Specialist calculation models remain non-actor, non-persistent components.
- Regime Realtime actor has no repository/state/timer dependency.
- Workflow Realtime actor has no Regime terminal-event receive route.
- Regime Command actor is the only owner of private calculation terminal state.
- Workflow Command actor is the only owner of workflow machine transitions.
- No outbox/replay/redispatch/resume service is introduced for this flow.
- Configuration remains PostgreSQL-backed and Scylla is read-model-only.

### 7.7 Executable test ownership for RD-19

Tests are delivered with the gate that introduces the behavior. The suite must
not use skipped placeholders: each listed case becomes executable when its
production contract or seam exists, and RD-19I reruns the complete matrix.

| Case group | Owning test file | Delivery gate | Required cases |
|---|---|---|---|
| Execute contract and composite identity | `RegimeDiscoveryContractTests.cs`, `IntrinsicTimeStrategyWorkflowIdentityTests.cs`, `IntrinsicTimeStrategyPipelineBoundaryContractTests.cs` | RD-19B | MessagePack round trip and keys; Execute-only route; same entity/different workflow gives different stream; deterministic command IDs; deadline and parameter identity retained. |
| Workflow snapshot contract | `IntrinsicTimeStrategyWorkflowMessageContractTests.cs`, `IntrinsicTimeStrategyWorkflowStateTests.cs` | RD-19B/C | Complete immutable view; defensive copies; per-pipeline inputs/results/failures; latest snapshot reconstructs exactly. |
| Legacy snapshot safety and event-batch atomicity | `Application.Storage.IntegrationTests/TradeDb/IntrinsicTimeStrategyWorkflowStorageTests.cs` plus a workflow event-source storage test | RD-19C/D | Latest snapshot selected; non-empty legacy-only stream rejected; migration emits no dispatch; expired-old/new-start pair commits together; optimistic concurrency rejects a second writer. |
| Workflow transition table | `IntrinsicTimeStrategyWorkflowCommandStateTests.cs` or `IntrinsicTimeStrategyWorkflowAtomicStateTests.cs` | RD-19D | Empty/terminal Start; Busy rejection; lazy expiry; before/equal/after-deadline Complete and Fail; timeout reason precedence; duplicate and stale input; concurrent Starts; no next-stage event on failure. |
| Workflow projection and dispatch ownership | `IntrinsicTimeStrategyWorkflowGateQualificationTests.cs` and projector/Realtime unit tests | RD-19E | Dispatch only after committed Started/Regime snapshot; deterministic Execute; no Regime terminal route in Workflow Realtime; no later-stage event parsed as Regime. |
| Atomic Regime execution | `RegimeDiscoveryAtomicExecutionTests.cs` | RD-19F | Success; expected failure; expired before work; timeout during snapshot; timeout during calculation; exact-boundary race; cancellation ignored by worker; unexpected exception; duplicate Execute; conflicting hash/revision; workflow A cannot block B. |
| Regime projector and Realtime translation | `RegimeDiscoveryCommandArchitectureTests.cs` plus new projector/Realtime tests | RD-19G | Scylla write precedes publication; Completed/Failed mapping; deterministic Workflow commands; stateless actor; duplicate/late notification harmless; no Processing/timer/replay route. |
| Business behavior | `IntrinsicTimeStrategyWorkflowAtomicScenarios.cs` in Trade BDD tests | RD-19D–G | Happy Regime continuation; expected failure; hard timeout; lost Started/terminal notification; lazy replacement; old late completion; restart before commit; no downstream command on every failure. |
| Real actor topology | `IntrinsicTimeStrategyWorkflowRuntimeIntegrationTests.cs` | RD-19E–G | Real Regime success/failure/timeout; new Regime Realtime routes; Busy and expired replacement; duplicate/stale notification; isolated consecutive workflows; suppressed notification proves fail-closed. |
| Operational view | workflow/Regime query and storage tests | RD-19H | Running, expired-but-unclosed, Failed, TimedOut, Completed, and migration-blocked views; observation performs no state mutation. |

Existing tests remain the RD-0 through RD-18 regression baseline until their
old replay and multi-event-contract expectations are deliberately replaced by
the corresponding RD-19 tests above.

## 8. Known failure scenarios and result

| Scenario | Authoritative result | Can workflow advance? | Recovery/visibility |
|---|---|---:|---|
| Execute command throws before Regime commit | Workflow remains Started | No | Logged; expiry visible; later Start closes it. |
| Host dies during calculation | Workflow remains Started | No | Same lazy-expiry behavior. |
| Private timeout wins | Regime Failed commits | No | Public Failed may close Workflow; otherwise later Start does. |
| Regime projector/Scylla write fails | Regime terminal exists; Workflow remains Started | No | Projector error + expired workflow observation. |
| Public terminal notification is lost | Workflow remains Started | No | Later Start closes expired execution. |
| Workflow Complete/Fail command fails | Workflow remains Started | No | Command error; late retry is not automatic; expiry backstop. |
| Completion arrives after deadline | Workflow becomes TimedOut or remains newer state | No | Stale/timeout metric and observation. |
| Old completion arrives after replacement Start | New workflow unchanged | No | Composite identity + Workflow guard reject it. |
| Started state notification is lost | Regime never executes; Workflow remains Started | No | Later Start closes expired execution. |
| State snapshot cannot be loaded | Actor unavailable/fails closed | No | Migration-blocked alert; never assume Free. |

This table is the intended safety behavior. Some scenarios deliberately leave a
historical Started snapshot until another command closes it. That is not an
indeterminate permission to continue: it is an unexpired/expired fact with a
fixed deadline, and it always blocks forward progression.

## 9. Explicit non-goals and remaining boundaries

This plan does not:

- provide automatic replay, message redelivery, redispatch, or workflow resume;
- guarantee that best-effort projections/notifications are eventually sent;
- redesign PostgreSQL `ConfigurationDbContext` or add Scylla configuration;
- implement general versioned parameter CRUD for every domain actor;
- implement later Strategy Workflow pipeline calculations;
- implement Order Execution or its exactly-once/idempotency controls; or
- make arbitrary calculation code forcibly stoppable after timeout.

The last two boundaries matter. This design prevents Regime Discovery failure
from progressing toward an order, but the future Order Execution boundary must
still enforce its own idempotent command identity and validate the committed
workflow/revision. Also, .NET cancellation is cooperative. A calculation that
ignores cancellation may consume resources after the timeout, but it must be
pure with respect to durable state so it cannot commit or dispatch anything.

## 10. Definition of done

The atomic Regime Discovery revision is complete only when all of the following
are true:

- RD-19A through RD-19J exit criteria are recorded as green;
- Start has been replaced by Execute on every live Regime route;
- the Workflow actor loads one latest authoritative state snapshot and blocks
  legacy-only streams;
- every Started workflow has a fixed persisted deadline;
- timeout precedence and lazy expiry are proven at the exact boundary;
- Regime execution is isolated by workflow ID and has one terminal-state owner;
- the new Regime Realtime actor exclusively translates terminal notifications;
- all failure/loss/restart tests prove no downstream progression;
- observation exposes expired/stopped/migration-blocked work without mutating
  it; and
- no configuration-storage redesign or replay mechanism has entered scope.
