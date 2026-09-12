# System-Wide In-Memory Event-Sourced Command Actor Implementation Plan

**Version:** 1.1  
**Status:** Development implementation complete; operational soak and release acceptance pending  
**Pilot:** `OptionTradeCommandActor` / `ChangeOptionTradeLegDataCommand`  
**Prerequisite:** Uncompressed MessagePack command logging and windowed PostgreSQL deduplication  
**Future extraction:** `TradePositionCommandActor` during position-workflow monitoring work

The implementation and measured gate outcomes are recorded in
`System-Wide-In-Memory-Event-Sourced-Command-Actor-Verification-v1.0.md`. Gates 0-9 have development evidence. Gate 10
remains an operational release gate because it requires a 30-minute synthetic soak and a full trading-session run;
it is not represented as complete by a short automated test.

## 1. Purpose

This plan first replaces per-command JSON command logging with compatible, uncompressed MessagePack storage and an
event-driven PostgreSQL command-audit window. After that prerequisite is qualified, it introduces an opt-in, reusable
event-sourced CommandActor base that retains authoritative working state in memory for a bounded sequence of commands.
The pilot removes repeated PostgreSQL replay from the option-position market-update path and amortizes durable event
persistence across a short command window. It preserves the existing `BaseEventSourceCommandActor` path for every
command and actor that does not explicitly opt in.

Implementation is progressive. A reproducible BenchmarkDotNet result and correctness report are produced after every
material step. A later step proceeds only when the previous step preserves the required invariants and its measurements
identify the next useful change.

## 2. Verified current state

The following behavior exists as of 2026-09-12:

1. `OptionTradeCommandActor` derives from `BaseEventSourceCommandActor<OptionTradeCommandActor>` and owns fifteen
   command types through explicit parse, validation and receive maps.
2. Every accepted command performs a PostgreSQL-backed `command_log` reservation before domain validation and state
   loading.
3. Every command calls `OptionTradeStateRepository.LoadStateAsync`, which restores the most recent
   `OptionTradeSnapshotEvent` and replays later events.
4. Every command calls `OptionTradeStateRepository.SaveStateAsync`; event persistence and required projection complete
   before the command reply is sent.
5. `ActorThreadPoolV2` serializes one `ActorThreadId` and permits different actor thread IDs to execute concurrently.
   The worker awaits `HandleMessageAsync` before taking the next message from that actor-thread queue.
6. The production sequential/LZ4 event appender sustains approximately 200 one-event transactions per second on one
   stream. Sixty-four independent streams reached approximately 4,005 aggregate transactions per second through keyed
   parallel scheduling. Independent-stream parallelism does not improve one busy position stream.
7. `ChangeOptionTradeLegDataCommand` carries a complete option-leg observation and underlying price. Its event replaces
   the selected leg; it is not a relative price delta. This makes the hot update convergent when a newer observation for
   the same leg arrives.
8. The current caller awaits `ChangeOptionTradeLegDataAsync`. Actual batch occupancy therefore depends on the number of
   concurrently active upstream contract actor threads. The end-to-end benchmark must measure this topology and cannot
   infer production throughput from a storage-only benchmark.
9. `TradePosition.TradeValue` and `NetSpread` currently retain constructor values when leg data is replaced. That known
   OptionTrade valuation concern belongs to the later OptionTrade/TradePosition refactor and is not a prerequisite for
   measuring the generic state and persistence mechanism. Pilot correctness tests use the existing command/event
   outcome as the compatibility oracle.
10. `TryReserveAsync` serializes every new command with Newtonsoft JSON and performs one PostgreSQL
    `INSERT ... ON CONFLICT DO NOTHING` round trip. It does not issue a separate `SELECT`, but the insert remains a
    per-command durable bottleneck.
11. `command_log.CommandId` is the durable deduplication key. The process-local `CommandDuplicateCoordinator` retains
    up to 100,000 recent IDs by default, but PostgreSQL remains authoritative after restart and across processes.
12. New command-log rows are written as `InProgress`. An update API and status enum exist, but no production caller was
    found that advances ordinary command rows to `Completed`, `Failed`, or `DenormalizerFailed`.
13. The only production command-log payload reader found is the Parameter Sets startup actor, which validates duplicate
    identity and determines whether a reserved command has a committed event. Its legacy JSON behavior must remain
    compatible during the MessagePack migration.

## 3. Goals

1. Load an aggregate once on first use and retain its state by `ActorThreadId`/event stream.
2. Process consecutive eligible commands against that resident state in strict mailbox order.
3. Persist complete command event groups in one bounded PostgreSQL transaction.
4. Bound the uncommitted interval with a one-shot deadline that cannot be extended by continued traffic.
5. Delay command success until the command's events have committed and its required projection policy has completed.
6. Preserve the existing event-log schema and event contracts; evolve `command_log` only through an additive,
   backwards-compatible binary-payload migration.
7. Keep orders, executions, fills, lifecycle changes, snapshots, end-of-day work and other critical commands on the
   existing synchronous path.
8. Make eligibility, limits, eviction and failure behavior explicit and reusable by other CommandActors.
9. Demonstrate improvements with actor-level, database-level and end-to-end benchmarks, including allocation and GC
   evidence.
10. Prove restart recovery from the latest snapshot plus committed events and fast valuation convergence after a market
    data interruption.
11. Store new command payloads as versioned, uncompressed MessagePack and batch deduplication reservations without a
    PostgreSQL read or transaction for every command.

## 4. Non-goals

- Creating `TradePositionCommandActor` in this implementation.
- Refactoring OptionTrade valuation formulas or its domain model.
- Applying this mechanism to EventActors, QueryActors, FunctionActors or RealtimeActors.
- Weakening durability for orders, executions, fills, cash, commissions, exercise, assignment or position lifecycle.
- Treating projections as authoritative position state.
- Changing `event_log`, `event_stream_id`, `event_name` or `event_projector_state` schemas, or destructively replacing
  existing `command_log` columns and JSON history.
- Automatically retrying a failed or commit-ambiguous window.
- Persisting every raw market tick in the PostgreSQL domain event log; raw tick ownership remains in the market-data
  storage path.

## 5. Core decisions

### 5.1 Two command paths remain available

`BaseEventSourceCommandActor<TActor>` remains the default. The new
`BaseInMemoryEventSourceCommandActor<TActor, TState>` is an opt-in specialization. A derived actor declares an exact
policy for every supported command type:

| Policy | State source | Persistence | Initial OptionTrade use |
| --- | --- | --- | --- |
| `StandardDurable` | Load/replay for this command | Existing synchronous save | All commands except leg market updates |
| `ResidentDurableWindow` | Actor-owned resident state | Bounded transactional window | `ChangeOptionTradeLegDataCommand` only |

An unmapped command fails closed. Assignable-type matching and a permissive fallback are prohibited.

### 5.2 One stream has one ordered resident-state slot

The actor remains a singleton that can serve many `ActorThreadId` values concurrently. It owns a concurrent slot table,
but a slot is mutated only by the already serialized actor-thread queue for its ID. Timer and persistence completions
return as internal control messages to the same actor-thread queue. They do not mutate state from a timer, database, or
thread-pool callback.

### 5.3 Only one speculative window may exist per stream

One stream may have one collecting or commit-in-flight window. Commands within that window observe earlier working-state
changes in mailbox order. A second window cannot advance until the first window commits. This prevents an unbounded
chain of state derived from an uncommitted predecessor.

### 5.4 Working state and durable state are explicit

Each slot records:

- the working `TState`;
- the last committed stream version;
- the next working stream version;
- the active window and its ordered command groups;
- pending reply handles;
- one-shot deadline generation and timestamp;
- lifecycle and last-use information.

The state is considered authoritative for command decisions only while its slot is `Ready` or `Collecting`. On commit
success, the working version becomes committed. On any failed or ambiguous commit, every command in the window fails,
the slot is evicted, and the next command must reload PostgreSQL. No rollback algorithm and no automatic write retry are
used.

### 5.5 A command remains the atomic logical unit

A window may contain multiple commands, and each command may contain multiple events. A command's events stay adjacent
and cannot be split across physical transactions. The physical transaction commits all command groups in the selected
window or none of them.

### 5.6 Reply and acknowledgement boundary

Domain execution may advance working state before commit, allowing later commands in the same window to execute. A
successful `ServiceResult<GuidResult>` is not sent until:

1. the complete physical window has committed;
2. the command's event IDs and stream versions are known; and
3. the command's configured required projection work has completed.

If transport delivery has a durable acknowledgement, it follows the same terminal result. A process crash before
commit therefore cannot lose a command that was reported as successful.

### 5.7 The one-shot deadline is a maximum pending age

The first eligible command added to an empty window schedules exactly one deadline. Later commands cannot move that
deadline. The window flushes at the earliest applicable trigger:

- maximum commands;
- maximum events;
- maximum serialized bytes;
- one-shot maximum age;
- a standard/durability-barrier command for the same stream;
- graceful actor shutdown.

There is no periodic polling loop. When another trigger wins, the deadline generation is invalidated. A late deadline
message is ignored through its slot and generation identity.

### 5.8 Market-update recovery semantics

The pilot command contains the complete current observation for one leg. After a restart, the actor restores the latest
snapshot and every subsequent committed event. If one or more uncommitted market updates disappeared with the process,
the next observation for each affected leg replaces its stale observation. A four-leg position is fully refreshed after
all stale required legs have received a sufficiently recent observation.

This convergence rule applies only to derived mark-to-market observations. Missing trade executions, fills, lifecycle
events or position definitions are never repaired by a later market price and must remain synchronously durable.

## 6. Command-log MessagePack and windowed-deduplication prerequisite

This prerequisite is implemented, tested, benchmarked and qualified before any resident event-sourced actor code. It is
independently useful to every standard CommandActor because it removes JSON serialization and lets concurrent command
reservations share a PostgreSQL transaction while retaining the existing request/response contract.

### 6.1 Compatible storage migration

Keep every existing `command_log` column and row. Add nullable binary metadata columns equivalent to:

```text
CommandPayload bytea
CommandPayloadFormat smallint
CommandPayloadVersion integer
CommandPayloadSha256 bytea
```

New rows use `CommandPayloadFormat = MessagePack`, a registered contract version, raw uncompressed MessagePack bytes and
a SHA-256 payload identity. `CommandData` remains present for legacy rows and compatibility; a new binary row may use the
smallest valid compatibility value allowed by the existing not-null constraint. No historical JSON row is rewritten.

Readers use the binary payload when present and fall back to `CommandData` JSON. The Parameter Sets startup duplicate
validator is migrated to a format-aware reader. If an old caller still requires JSON text, conversion happens on that
rare read path rather than on every command write.

### 6.2 Serialization policy

Command-log MessagePack uses the registered concrete command type and its existing numeric-key contract. Compression is
always disabled for command logs. The event-log LZ4 option remains independent.

The initial implementation serializes the typed command once into a window-owned buffer. A benchmarked follow-up may
transfer or reuse the original NATS MessagePack payload when ownership, canonical bytes and lifetime can be proven. Raw
payload reuse is not allowed to extend a pooled NATS buffer beyond its owner or to make payload release ambiguous.

The SHA-256 value identifies reuse of one `CommandId` with different content. Equality requires the same command ID,
format, contract version and payload hash. Hash equality may be followed by byte comparison when resolving a conflict.

### 6.3 Event-driven command-audit batch writer

Add one bounded command-audit writer owned by `EventSourceActorDbContext`. It accepts complete command audit envelopes
and returns one terminal reservation result per command. Its queue waits for arrivals; it never polls PostgreSQL.

The first request in an empty window creates one non-extendable deadline. The writer flushes at the earliest of command
count, serialized bytes, maximum age, shutdown or explicit barrier. Later arrivals cannot move the original deadline.

The PostgreSQL operation is set based:

```text
INSERT INTO command_log (...)
SELECT ... FROM the supplied command window
ON CONFLICT (CommandId) DO NOTHING
RETURNING CommandId
```

It may use typed arrays, `UNNEST`, prepared batching or binary COPY according to benchmarks. The result set identifies
newly reserved commands without a preceding per-command read. Existing conflicts are resolved in one batch query only
when their stored payload or committed-event outcome is required.

### 6.4 Deduplication levels and guarantees

Deduplication has three levels:

1. A bounded completed-ID cache rejects recent duplicates without PostgreSQL.
2. An in-flight table coalesces duplicate IDs already waiting in the current or active audit window.
3. The PostgreSQL `CommandId` primary key remains authoritative across process restarts and application instances.

The batch writer must distinguish:

- newly reserved command;
- same-window duplicate;
- recently completed duplicate;
- durable duplicate with identical payload;
- command-ID conflict with different payload; and
- legacy reservation with no corresponding committed event.

A command-ID payload conflict fails explicitly. It is never acknowledged as a normal duplicate. The Parameter Sets
startup actor retains its explicit ability to resume an audited but uncommitted immutable operation. No other actor
inherits that exception implicitly.

### 6.5 Transaction boundary before and after resident state

Before resident state is implemented, the audit writer may batch reservations across concurrently executing standard
actors. Each caller still awaits its durable reservation before validation, replay and domain execution. This preserves
current command semantics while reducing command-log transactions.

After the resident-state window is introduced, eligible hot command logs and their event groups are written in the same
PostgreSQL transaction. This removes the crash gap between an `InProgress` audit row and its event. Standard commands
continue to use the independently qualified audit writer plus their current event-save path until a broader migration is
separately justified.

### 6.6 Configuration, lifecycle and rollback

Add an independently selectable command-audit mode:

```text
CommandAuditWriteMode
  SequentialJsonLegacy
  SequentialMessagePack
  WindowedMessagePack
```

Initial test values cover 1, 4, 16, 64 and 256 commands, 16 KiB through 1 MiB, and 0.25 through 5 millisecond maximum
age. Production defaults are selected from measured request latency and real concurrency.

On shutdown the writer stops admission, flushes its final partial window, completes every accepted reservation and then
releases buffers. A failed batch fails every reservation in that physical batch and is not retried automatically.
`SequentialJsonLegacy` remains the immediate rollback mode throughout the pilot.

### 6.7 Command-audit observability

Record queue depth, oldest age, commands and bytes per batch, flush trigger, serialization duration, PostgreSQL duration,
cache/in-flight/durable duplicate outcomes, payload conflicts, allocation per command and terminal failures. Successful
commands do not produce one information log each.

## 7. Required framework contracts and models

Names are implementation targets; minor naming adjustments are allowed when existing namespaces require them.

### 7.1 Base actor

Add under `TomasAI.IFM.Shared.EventModelActor`:

```text
BaseInMemoryEventSourceCommandActor<TActor, TState>
```

It owns resident slots, command-window assembly, internal control-message handling, lifecycle drain and metrics. It
inherits the common parse, audit, validation, exception, tracing and reply conventions from
`BaseEventSourceCommandActor<TActor>`. The existing base must first expose narrow protected template hooks so the new
base does not copy the ingress pipeline.

The existing actor base remains behaviorally identical when all commands use `StandardDurable`.

### 7.2 Execution policy

Add immutable framework types:

```text
EventSourceCommandExecutionMode
  StandardDurable
  ResidentDurableWindow

ResidentCommandSemantics
  StrictDeduplicated
  ConvergentAbsoluteUpdate

InMemoryEventSourceOptions
  MaximumCommandsPerWindow
  MaximumEventsPerWindow
  MaximumSerializedBytesPerWindow
  MaximumWindowAge
  MaximumResidentStreams
  IdleStateRetention
  MaximumPendingReplyCount
```

The command policy map is keyed by exact concrete `Type`. The convergent policy requires a documented domain reason and
focused duplicate/reordering tests. It is not a general shortcut around command deduplication.

### 7.3 Resident state

Add internal reusable models:

```text
ResidentActorStateSlot<TState>
ResidentActorStateStatus
  Cold, Loading, Ready, Collecting, CommitInFlight, Faulted, Evicting

ResidentStateLoad<TState>
  State, StreamId, CommittedStreamVersion, SnapshotStreamVersion

PendingCommandGroup
  Command, OrderedEvents, ExpectedStartVersion, ReplyHandle, AuditEnvelope

InMemoryCommandWindow
  WindowId, Generation, StartedTimestamp, CommandGroups, EventCount, ByteCount

InMemoryWindowCommitResult
  WindowId, FirstEventId, LastEventId, FinalStreamVersion, CommandResults

InMemoryWindowFlushTrigger
  CommandCount, EventCount, Bytes, MaximumAge, Barrier, Shutdown
```

Use `TimeProvider` and monotonic timestamps for deadline decisions. Wall-clock UTC remains metadata only.

### 7.4 Event-state draining and versioning

The resident base needs to detach events created by one command while retaining the mutated state. Extend the shared
state contract with an operation equivalent to `DrainPendingEvents()` that returns the current ordered events and
replaces the pending collection with an empty collection. Existing replay semantics remain unchanged.

Add a version-aware repository load result. The resident actor must never infer a committed version from event count,
because a snapshot may begin after version one and commands may produce multiple events.

### 7.5 Deferred reply ownership

The worker disposes `IActorMessage` after `HandleMessageAsync` returns. The resident path therefore must not retain the
message object. Add a transport-independent, single-completion reply handle that can be detached after parsing and
payload release. It owns only the reply destination, trace linkage and completion state. It must:

- survive source-message disposal;
- allow exactly one success or failure reply;
- release its resources after completion;
- report reply failure through `SupervisorRuntimeContext`;
- support messages with no reply destination without allocation where practical.

### 7.6 Internal control messages

Add actor-runtime-only messages for:

```text
ResidentWindowDeadlineElapsed
ResidentWindowCommitCompleted
ResidentWindowCommitFailed
ResidentStateEvictionRequested
```

They route to the original `ActorThreadId` and bypass domain command parsing, audit and validation. Their payloads contain
identities and immutable results, never mutable state references. They are not domain events and are not published to
NATS.

### 7.7 Persistence boundary

Extend `IEventSourceActorDbContext` through a compatibility-preserving method or focused companion interface:

```text
CommitCommandWindowAsync(
  eventStream,
  expectedStreamVersion,
  orderedCommandGroups,
  cancellationToken)
```

The implementation resides with the existing `EventSourceActorDbContext` PostgreSQL persistence code. It reuses the
current MessagePack codec, event-name registry, stream registry and configured LZ4 policy.

The transaction must:

1. resolve and lock the event stream;
2. verify the exact expected committed stream version;
3. reserve/validate command audit identities according to the selected policy;
4. assign contiguous stream versions in command and event order;
5. reserve event IDs;
6. write existing `event_log` rows;
7. write any existing required projector markers;
8. commit once; and
9. return terminal results for every command group.

No event-log table or column is added. Only the compatible command-log payload columns approved in section 6.1 are
introduced. The current one-command `SaveEventsAsync` and both existing event appender implementations remain available.

### 7.8 Command audit policy

The qualified windowed MessagePack audit writer from section 6 is the prerequisite command-audit implementation. The
resident window incorporates its audit envelopes into the event transaction. Strict commands must be identified as new
before their events affect working state. The pilot's absolute replacement command may use the explicit
`ConvergentAbsoluteUpdate` policy: an already committed duplicate is omitted from persistence, receives its existing
success outcome, and cannot introduce a different payload under the same command ID. The stored hash and, when needed,
byte comparison distinguish a true redelivery from command-ID reuse.

### 7.9 Projection policy

The first pilot retains existing ordered projection and completion behavior. Projection duration is measured separately
from event commit. If projection is the remaining bottleneck, a later gate may introduce a specific latest-value
projection policy for convergent market observations. That change requires its own correctness tests and must preserve
event-log order, durable projector status and terminal failure visibility. It cannot silently become eventual for
orders, fills or lifecycle commands.

## 8. OptionTrade pilot mapping

`OptionTradeCommandActor` changes its base to:

```text
BaseInMemoryEventSourceCommandActor<OptionTradeCommandActor, OptionTradeCommandState>
```

Its existing `_parseMap`, `_validationMap` and `_receiveMap` remain the complete command manifest. Add an exact-type
execution-policy map:

| Command | Mode | Reason |
| --- | --- | --- |
| `ChangeOptionTradeLegDataCommand` | `ResidentDurableWindow` | High-frequency complete leg observation; recoverable from a newer leg observation |
| Every other current command | `StandardDurable` | Changes durable trade identity, lifecycle, history, calculation policy or snapshot boundary |

When a standard command arrives for a stream with a pending resident window, it is a barrier:

1. close and commit the resident window;
2. wait for its terminal result;
3. execute the standard command through the existing load/save path; and
4. evict or reload the resident slot before another hot command.

`SnapshotOptionTradeCommand`, position open/close, trade open/close/delete, order placement, end-of-day processing and
all data-definition mutations are mandatory barriers.

## 9. State eviction and lifecycle

Resident state is bounded by count and retention settings. Eviction is allowed only when a slot has no collecting
window, no commit in flight and no pending replies. Idle eviction is event driven from actor activity or a bounded
supervisor maintenance action; it does not add a high-frequency polling service.

On graceful shutdown:

1. stop accepting new resident commands;
2. enqueue a barrier for every collecting slot;
3. await or terminally fail every accepted window within the configured shutdown bound;
4. complete every detached reply;
5. stop the OptionTrade projector; and
6. release resident slots and deadline registrations.

On abrupt termination, PostgreSQL contains either the complete window or none of it. Callers without a success reply
must regard the outcome as unresolved and reconcile by command ID. Commit-ambiguous outcomes are never retried blindly.

## 10. Backpressure

Bounds apply at both mailbox and resident-window levels. A slot refuses additional hot commands when any of these are
exhausted:

- maximum pending reply handles;
- maximum command/event/byte capacity;
- commit-in-flight backlog;
- global resident stream count; or
- actor admission budget.

The response is an explicit overload/unavailable result with structured metrics. Dropping an accepted update silently is
forbidden. A future market-data caller may coalesce observations before command creation, but that is separate from the
event-sourced actor and must retain the latest observation per position leg.

## 11. Observability

Expose bounded metrics through the existing actor and supervisor runtime surfaces:

- resident stream count and status;
- resident-state hit, miss, load and eviction counts;
- replay duration and replayed event count;
- collecting and commit-in-flight window counts;
- commands, events and bytes per committed window;
- window age and flush trigger;
- command execution, audit, serialization, commit, projection and reply latency;
- pending reply count and oldest pending reply age;
- commit success, failure and ambiguous outcome counts;
- state evictions after failures;
- duplicate and command-ID payload-conflict counts;
- recovery-to-first-current-leg and recovery-to-all-legs-current duration;
- allocation per update and Gen0/Gen1/Gen2 collection counts in benchmark/soak reports.

Normal success does not log per event. Emit bounded window summaries at debug/trace level and complete structured detail
for failures.

## 12. Progressive implementation and benchmark gates

### Gate 0 - Freeze the current command-log baseline

**Implementation**

- Add no production behavior.
- Add focused BenchmarkDotNet command-audit workloads around the current `TryReserveAsync` path.
- Capture PostgreSQL round trips, JSON serialization cost and duplicate-cache behavior separately.

**Benchmark matrix**

- 1,000, 10,000 and sustained 100,000 unique commands;
- recent-cache hit, concurrent same-ID, durable duplicate and all-new command workloads;
- serial callers and 4, 16 and 64 concurrent actor callers;
- representative small, OptionTrade leg-update and large workflow command payloads.

**Exit gate**

The report includes reservations/second, p50/p95/p99/p99.9 latency, bytes allocated per command, all GC generations,
PostgreSQL transactions and serialized payload sizes. The host exits and removes its test rows.

### Gate 1 - Compatible uncompressed MessagePack command log

**Implementation**

- Apply the additive binary-payload migration from section 6.1 without rewriting legacy rows.
- Add the versioned, uncompressed command MessagePack codec and stable payload hash.
- Add format-aware command-log reads and legacy JSON fallback.
- Keep writes sequential for this isolated comparison.

**Benchmark comparison**

Run Gate 0 as `SequentialJsonLegacy` versus `SequentialMessagePack`. Separately compare serialization from the typed
command with safe reuse of the received NATS payload if the ownership prototype is valid.

**Exit gate**

- every supported pilot command round-trips byte-semantically;
- retained legacy JSON rows remain readable;
- Parameter Sets duplicate validation passes with both formats;
- compression is demonstrably disabled for command payloads;
- payload conflicts under one command ID are detected;
- schema verification reports only the approved additive columns;
- MessagePack performance and allocation evidence justify retaining the new path.

### Gate 2 - Event-driven windowed MessagePack deduplication

**Implementation**

- Add the bounded command-audit writer, non-extendable one-shot deadline and batch result fan-out.
- Add process-local completed and in-flight deduplication before admission.
- Add set-based PostgreSQL insertion and batch conflict resolution.
- Keep each standard CommandActor waiting for durable audit acceptance before domain execution.
- Retain `SequentialJsonLegacy` and `SequentialMessagePack` switches.

**Benchmark comparison**

Compare sequential and windowed MessagePack with command limits 1, 4, 16, 64 and 256; byte limits 16 KiB, 64 KiB,
256 KiB and 1 MiB; maximum ages 0.25, 0.5, 1, 2 and 5 milliseconds; and 1, 4, 16 and 64 concurrent actor callers.

**Exit gate**

- no PostgreSQL `SELECT` or transaction occurs for each unique command;
- one batch insert returns the exact accepted command-ID set;
- same-window, in-flight, cached, durable and conflicting duplicates are deterministic;
- the one-shot deadline cannot be extended by continuous arrivals;
- every caller receives exactly one terminal reservation result;
- failed or ambiguous batches are visible and never blindly retried;
- all command actors remain behaviorally compatible;
- the retained configuration materially reduces transaction count and allocation at realistic concurrency.

Resident event-sourced state implementation cannot start until Gates 0-2 pass.

### Gate 3 - Freeze the current OptionTrade end-to-end baseline

**Implementation**

- Add no production behavior.
- Add a realistic `OptionTradeCommandActor` BenchmarkDotNet fixture.
- Add deterministic fixture generation for one active option trade with four legs and a current snapshot/event history.
- Separate timers for parse, audit reservation, validation, replay, command execution, event serialization, PostgreSQL
  commit, projection and reply.

**Benchmark matrix**

- histories after snapshot: 0, 100, 1,000 and 10,000 events;
- one stream with round-robin four-leg updates;
- 1, 4, 16 and 64 concurrent streams;
- LZ4 enabled and disabled;
- 1,000, 10,000 and sustained 100,000 update runs where runtime permits;
- closed-loop request/reply and open-loop offered-load modes.

**Evidence**

- throughput, mean/p50/p95/p99/p99.9 latency;
- bytes allocated per command;
- Gen0/Gen1/Gen2 collections;
- working set, managed heap and PostgreSQL transaction count;
- raw BenchmarkDotNet CSV, Markdown and HTML reports.

**Exit gate**

The baseline is repeatable, the test host exits, test streams are removed, and each reported result clearly states
whether it includes transport, audit, projection and PostgreSQL durability.

### Gate 4 - Resident state with unchanged synchronous persistence

**Implementation**

- Add version-aware resident-state loading and bounded slot ownership.
- Continue current per-command audit, save, projection and reply behavior.
- Enable resident state only for the pilot command.
- Add mandatory barriers for all other OptionTrade commands.

**Benchmark comparison**

Run the complete Gate 3 matrix as `CurrentReplayEachCommand` versus `ResidentStateSynchronousCommit`.

**Exit gate**

- live state and replayed state are byte-semantically equivalent after every tested sequence;
- standard OptionTrade commands retain current behavior;
- replay queries disappear on resident-state hits;
- no throughput or allocation regression is accepted without an identified downstream bottleneck;
- the report attributes remaining time to audit, commit, projection and reply.

### Gate 5 - Generic bounded window engine with an in-memory persistence double

**Implementation**

- Add window assembly, event draining, detached replies, one-shot deadline messages, barriers and slot failure eviction.
- Use a deterministic in-memory commit double; do not enable PostgreSQL window commits yet.
- Prove one in-flight window per stream and no cross-thread slot mutation.

**Benchmark matrix**

- command limits: 1, 4, 16, 64 and 256;
- maximum age: 0.25, 0.5, 1, 2 and 5 milliseconds where the platform timer permits reliable measurement;
- event payloads: representative leg update, 4 KiB and 64 KiB;
- continuous traffic, burst/idle traffic and barrier-heavy traffic;
- one and 64 resident streams.

**Exit gate**

- a continuous stream cannot extend the first command's deadline;
- every reply completes exactly once;
- a failed window evicts state and fails every dependent command;
- no unbounded task, timer, reply or payload retention is observed;
- actor-only processing demonstrates enough headroom that PostgreSQL remains the measured limiting component.

### Gate 6 - PostgreSQL command-and-event window transaction

**Implementation**

- Add the command-and-event window persistence operation without changing the event-log schema.
- Preserve command grouping, exact expected version, LZ4 choice and current event serialization.
- Incorporate the already-qualified uncompressed MessagePack audit envelopes from Gate 2 into the same transaction.
- Retain the independently selectable command-audit writer for standard commands.

**Benchmark comparison**

Run:

1. resident state plus one event transaction per command and the qualified audit writer;
2. resident state plus event windows while retaining a separate qualified audit window;
3. resident state plus atomic event and command-audit windowing;
4. each configuration with LZ4 on and off;
5. batch sizes 1, 4, 16, 64 and 256 with the one-shot age matrix.

Measure committed command TPS, events per transaction, WAL bytes, PostgreSQL commands per logical update, connection
wait, commit latency, allocation and GC.

**Exit gate**

- no event-log schema difference and no unapproved command-log schema difference before and after the test;
- one window produces one atomic transaction;
- stream versions are contiguous and deterministic;
- one invalid command/event rolls back the window;
- duplicate IDs and payload conflicts have deterministic results;
- no result is successful before commit;
- the chosen configuration materially improves same-stream durable throughput over the approximately 200 TPS baseline.

The initial target is at least 1,000 committed pilot updates per second on the same local benchmark environment, with a
stretch target of 5,000. If this is not reached, the report must identify audit, serialization, WAL, projection,
transport or offered-load concurrency as the limiting stage before any further optimization.

### Gate 7 - OptionTrade pilot integration

**Implementation**

- Switch `OptionTradeCommandActor` to the new base.
- Map only `ChangeOptionTradeLegDataCommand` to the resident window.
- Retain all other commands on the standard path with barriers.
- Wire options, dependency registration and supervisor metrics.

**Tests and benchmarks**

- run the full OptionTrade unit and integration suites;
- compare direct actor offered load with the real FuturesOptionTickData request/reply topology;
- measure batch occupancy with one iron condor's four contract actor callers;
- run 1, 16 and 64 simultaneous option trades;
- record projector throughput separately from event commit throughput.

**Exit gate**

- existing command maps remain equal and complete;
- standard commands cannot overtake a pending leg-data window;
- live and replayed OptionTrade state match after every committed window;
- real caller topology produces measurable benefit;
- any upstream await/concurrency cap is documented rather than hidden by direct actor benchmarks.

### Gate 8 - Failure, crash and recovery qualification

**Implementation/testing**

- terminate the process before window submission, during serialization, before commit, during commit and after commit
  before reply;
- inject connection loss with known rollback and ambiguous outcome;
- restart from snapshots with 0, 100, 1,000 and 10,000 later events;
- simulate market-data gaps of one minute, one hour and one trading session;
- resume one leg at a time and then all four legs;
- verify that lifecycle commands remain unavailable until the barrier/reconciliation result is known.

**Exit gate**

- PostgreSQL contains all or none of every window;
- acknowledged commands are always recoverable;
- ambiguous commits are visible and never automatically retried;
- recovery replays only committed events in exact order;
- newer full leg observations converge stale market state as each leg returns;
- every pending caller receives one terminal result or an explicitly documented connection-loss outcome.

### Gate 9 - Allocation and GC optimization

**Implementation**

Use profiler and BenchmarkDotNet evidence to consider, in order:

1. pooled pending-command/event arrays;
2. serialized payload reuse between size accounting and persistence;
3. value-type window metadata;
4. pooled completion sources;
5. safe reuse of already serialized MessagePack command payloads when profiling justifies it;
6. bounded state-slot pooling only if it does not retain large aggregate graphs.

**Exit gate**

- each retained optimization has an isolated before/after benchmark;
- no optimization weakens payload ownership, exact-once reply, failure or replay semantics;
- a sustained 100,000-update run reports allocation per update and all GC generations;
- no unexplained Gen2 growth, reply-handle retention or resident-slot leak remains.

### Gate 10 - Soak, configuration selection and controlled release

Run at least:

- a 30-minute synthetic burst/idle soak;
- a full trading-session-duration development soak when practical;
- forced feed interruption and recovery during the soak;
- standard OptionTrade lifecycle commands during and after hot traffic;
- clean host shutdown with partial windows.

Select production defaults from measured latency/throughput curves. Do not choose a maximum age solely from average TPS.
The selected one-shot age must bound p99 command completion appropriately while producing useful batch occupancy under
the real four-leg caller topology.

Release remains feature-switchable per actor and command type. Disabling the OptionTrade pilot returns every command to
the existing standard path without a data migration.

## 13. Unit-test requirements

- MessagePack command payload round-trip for every pilot command type;
- command-log compression is always disabled regardless of event-log LZ4 settings;
- legacy JSON and new binary command-log read compatibility;
- stable payload hashing and command-ID payload-conflict detection;
- completed-cache, in-flight and same-window duplicate outcomes;
- batch writer count, byte, age, barrier and shutdown triggers;
- one-shot audit deadline is non-extendable and stale generations are ignored;
- every accepted audit reservation completes exactly once;
- exact command-policy map coverage and fail-closed behavior;
- one initial load for repeated resident commands;
- deterministic command/event order within a window;
- multi-event command adjacency;
- pending-event draining without mutating current state;
- exact committed and working stream versions;
- one-shot deadline is created once and never extended;
- stale deadline generation is ignored;
- all flush triggers;
- standard-command barrier ordering;
- one commit in flight per stream;
- concurrent independent slots do not share state;
- window failure evicts state and completes all replies once;
- commit success advances the durable checkpoint once;
- detached reply survives source-message disposal;
- shutdown drains or explicitly fails all accepted work;
- strict and convergent duplicate policies;
- payload conflict under a reused command ID;
- state table and reply limits enforce backpressure;
- cancellation before admission versus after durable admission;
- supervisor metrics and failure records.

## 14. PostgreSQL integration-test requirements

- additive command-log migration preserves all legacy columns and rows;
- sequential and windowed MessagePack command reservations interoperate;
- one set-based insert returns the exact newly reserved command IDs;
- no per-command PostgreSQL read is issued for an all-new window;
- identical and conflicting durable duplicates resolve correctly;
- legacy audited-without-event rows retain their explicit reconciliation behavior;
- command-audit batch rollback and shutdown flush;
- one, four, sixteen and sixty-four command groups in one stream transaction;
- several events from one command mixed with single-event commands;
- exact expected-version success and conflict;
- atomic command-log/event-log persistence using existing tables;
- event ID and stream-version ordering;
- LZ4 on/off byte-semantic round trip;
- event/projector marker rollback;
- duplicate command ID with identical and conflicting payload;
- connection loss before commit and commit ambiguity;
- restart replay from latest snapshot;
- no schema mutation beyond the approved additive command-log payload columns;
- interoperability: standard writer reads window events and window writer follows standard events;
- graceful shutdown with a partial window.

## 15. Verification-test requirements

- full four-leg option update sequence through real actor routing;
- simultaneous updates for multiple OptionTrade aggregate IDs;
- resident-state result equals the existing replay-every-command oracle;
- standard command cannot observe or overtake an unresolved hot window;
- application restart reconstructs the final committed state;
- one-hour simulated feed gap followed by current observations for all legs;
- readiness transitions from recovered/stale to fully refreshed by leg;
- failure is visible in Supervisor actor metrics and detailed logs;
- feature switch returns the actor to the standard path.

## 16. Benchmark artifacts and reporting

Add the actor benchmark to `TomasAI.IFM.Domain.Trade.Benchmarks` and database-window benchmarks to
`TomasAI.IFM.Application.Storage.Benchmarks`. Each gate writes immutable, dated results under:

```text
BenchmarkDotNet.Artifacts/command-log-messagepack-gate-N/results/
BenchmarkDotNet.Artifacts/option-trade-resident-state-gate-N/results/
BenchmarkDotNet.Artifacts/event-log-command-window-gate-N/results/
```

After each gate, update a system-wide evidence document with:

- machine, runtime, GC mode and PostgreSQL settings;
- exact commit and configuration;
- scenario and inclusion boundaries;
- mean and percentile latency;
- logical commands/second and events/second;
- physical transactions/second and commands/transaction;
- allocation per command and GC counts;
- correctness test totals;
- comparison with the immediately preceding gate;
- decision to retain, revise or remove that stage's change.

Benchmark hosts must exit, database streams must be cleaned up, and no IFM application/test host may remain running
after a benchmark.

## 17. Initial benchmark parameters

These values form the test matrix and are not production defaults:

| Setting | Values |
| --- | --- |
| Commands/window | 1, 4, 16, 64, 256 |
| Events/window | 4, 16, 64, 256, 1,024 |
| Serialized bytes/window | 64 KiB, 256 KiB, 1 MiB |
| One-shot maximum age | 0.25 ms, 0.5 ms, 1 ms, 2 ms, 5 ms |
| Resident streams | 1, 16, 64 |
| Events after snapshot | 0, 100, 1,000, 10,000 |
| Command-log compression | Disabled in every configuration |
| Event-log compression | LZ4 enabled, disabled |
| Workload | burst, sustained, burst/idle, barrier mixed, failure injection |

Unsupported timer resolutions are reported and removed from runtime selection rather than approximated silently.

## 18. Expected file impact

### Shared actor framework

```text
TomasAI.IFM.Shared.EventModelActor/
  BaseEventSourceCommandActor.cs
  BaseInMemoryEventSourceCommandActor.cs
  InMemoryEventSourcing/
    InMemoryEventSourceOptions.cs
    ResidentActorStateSlot.cs
    InMemoryCommandWindow.cs
    InMemoryWindowControlMessages.cs
    DeferredActorReply.cs

TomasAI.IFM.Shared.EventModelActor/Contracts/
  IEventSourceActorState.cs
  IDeferredActorReply.cs
```

### Storage

```text
TomasAI.IFM.Application.Storage/
  IEventSourceActorDbContext.cs
  CommandAudit/
    CommandAuditWriteMode.cs
    CommandAuditMessagePackCodec.cs
    CommandAuditEnvelope.cs
    CommandAuditWindowOptions.cs
    WindowedCommandAuditWriter.cs
  EventSourceDb/EventSourceActorDbContext.cs
  EventSourceDb/Schema/EventSourceSchemaSql.cs
  EventSourceDb/Persistence/
    EventSourceCommandWindowContracts.cs
    PostgresEventSourceCommandWindowWriter.cs
```

### OptionTrade pilot

```text
TomasAI.IFM.Domain.Trade/Option/Command/Actor/OptionTradeCommandActor.cs
TomasAI.IFM.Domain.Trade/Option/Command/State/OptionTradeStateRepository.cs
TomasAI.IFM.Domain.Trade/Option/Command/Actor/OptionTradeCommandContext.cs
```

### Tests and benchmarks

```text
TomasAI.IFM.Shared.UnitTests/EventModelActor/InMemoryEventSourcing/
TomasAI.IFM.Application.Storage.UnitTests/CommandAudit/
TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/CommandWindows/
TomasAI.IFM.Application.Storage.IntegrationTests/EventSourceDb/CommandAudit/
TomasAI.IFM.Application.Storage.Benchmarks/EventLogCommandWindows/
TomasAI.IFM.Application.Storage.Benchmarks/CommandAudit/
TomasAI.IFM.Domain.Trade.UnitTests/Option/InMemoryEventSourcing/
TomasAI.IFM.Domain.Trade.IntegratedTests/Option/InMemoryEventSourcing/
TomasAI.IFM.Domain.Trade.VerificationTests/Option/InMemoryEventSourcing/
TomasAI.IFM.Domain.Trade.Benchmarks/OptionTradeResidentStateBenchmarks.cs
```

## 19. Completion criteria

The pilot is complete only when:

1. legacy JSON command history and new uncompressed MessagePack command history are both readable;
2. windowed command deduplication is qualified before resident actor implementation begins;
3. PostgreSQL remains the durable cross-process authority without a per-command read or transaction;
4. the standard event-sourced command path remains available and behaviorally compatible;
5. only `ChangeOptionTradeLegDataCommand` uses resident windowing;
6. the resident state is loaded once per active slot and bounded by eviction policy;
7. same-stream command and event ordering is deterministic;
8. every window is atomically committed with exact stream-version checks;
9. the one-shot deadline is event driven, non-extendable and verified under continuous load;
10. no command reports success before its durability boundary;
11. failure or ambiguity evicts the slot and never triggers a blind retry;
12. standard commands form a durable barrier;
13. restart recovery from snapshot plus committed events equals live committed state;
14. resumed full observations refresh every stale leg without replaying missed market ticks;
15. unit, integration and verification suites pass;
16. each progressive gate has a before/after BenchmarkDotNet report;
17. the final end-to-end result includes real upstream request/reply topology, allocation and GC evidence;
18. production settings and rollback switches are documented from measured results; and
19. no benchmark or test host remains running after execution.

## 20. Relationship to existing documents

This plan specializes, and does not replace:

- `PostgreSQL-Event-Sourcing-Optimization-Design.md` for event envelopes, durability, batching and recovery;
- `PostgreSQL-Event-Log-Dual-Appender-Implementation-Plan-v1.0.md` for current appender compatibility;
- `PostgreSQL-Event-Log-Sequential-Transaction-Rate-Benchmark-v1.0.md` for the one-stream baseline;
- `Actor-Stream-Parallel-Event-Log-Benchmark-v1.0.md` for independent-stream scheduling evidence; and
- `Actor-Implementation-Conventions.md` for CommandActor maps, handlers, state, projection and exception conventions.

The key additional capability is bounded working-state progression across several same-stream commands before one
durable commit, with delayed terminal replies, strict barriers and actor-thread-routed completion handling.
