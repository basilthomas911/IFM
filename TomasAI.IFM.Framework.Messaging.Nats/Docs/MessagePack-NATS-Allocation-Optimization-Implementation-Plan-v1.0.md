# MessagePack and NATS Allocation Optimization Implementation Plan

**Work package:** Messaging transport allocation control
**Status:** Implemented and verified
**Version:** 1.0
**Created:** 2026-09-11
**Owner:** IFM engineering
**Primary projects:** `TomasAI.IFM.Framework.Messaging.Nats`, `TomasAI.IFM.Framework.Serialization`
**Packages verified:** MessagePack-CSharp 3.1.8, NATS.Net 3.0.1, .NET 10

## Implementation record

Completed on 2026-09-11. Command and function request replies now deserialize directly from the NATS receive
sequence, and legacy actor replies write typed results directly to the NATS output writer. Core realtime and
JetStream actor event delivery use one shared pooled ingress owner with reference-counted mailbox branches. Command
and query ownership failure paths dispose the wrapper that owns the lease, including cancellation before channel
handoff.

The diagnostic rollback switches and application-facing listener callback contracts remain available for staged
deployment. Their use is visible through managed-copy counters. Durable replay retains two managed arrays by design:
one for the polymorphic event payload and one independently owned outer envelope. This boundary survives process and
pool lifetimes and is counted separately.

No MessagePack key, contractless member name, resolver, or compression setting changed. Explicit formatter/schema
migration was rejected for this release because there was no measured improvement sufficient to justify a wire
version transition. The current resolver caches formatters on first use. The compression benchmark shows LZ4 costs
more CPU at the serializer boundary for every tested payload; the existing setting remains because mixed-version and
end-to-end network evidence is required before a wire-format change.

Verification evidence:

- 101 messaging unit tests passed.
- 3 allocation, ownership, and wire-compatibility verification tests passed.
- 61 broker-backed integration tests passed.
- 38 BenchmarkDotNet cases completed for request/reply, event fan-out, compression, and durable payloads.
- Typed reply serialization allocated 0 B/op at both 256-byte and 4,096-byte payload sizes.
- Shared event ownership reduced allocation in every fan-out case, from 39% for one 256-byte destination to 8% for
  seventeen 4,096-byte destinations in this run.

## 1. Purpose

Complete the direct integration between MessagePack-CSharp and NATS.Net so hot actor messages do not create an
intermediate GC-owned serialized payload. Outbound typed messages must serialize directly into the NATS-provided
`IBufferWriter<byte>`. Inbound actor messages must be deserialized from a `ReadOnlySequence<byte>` or an explicitly
owned pooled payload without first calling `ToArray()`.

This plan targets allocation and copy reduction at the transport boundary. It does not claim that complete message
processing is allocation-free: materializing commands, queries, events, replies, strings, arrays, collections, and
domain results still creates the object graph required by the actor.

## 2. Verified current implementation

### 2.1 Already optimized

1. `NatsMessagePackSerializer<T>` implements `INatsSerializer<T>`.
2. Outbound serialization calls `MessagePackSerializer.Serialize(IBufferWriter<byte>, T, options)` directly.
3. Typed inbound deserialization accepts `ReadOnlySequence<byte>` directly.
4. Core NATS command/event publication uses the typed serializer.
5. Core query request and reply use typed serializers in both directions.
6. JetStream actor publication uses the typed serializer.
7. Command and query consumers default to `NatsMemoryOwner<byte>` payload ownership.
8. Owned command/query messages defer deserialization until actor processing and release pooled memory after parsing.
9. JetStream event consumption defaults to one pooled root payload with reference-counted mailbox branches.
10. Existing benchmarks demonstrate zero additional outbound serialization allocation for the measured 256-byte and
    4,096-byte payloads.

### 2.2 Measured baseline

The checked-in benchmark evidence reports:

| Boundary | Legacy allocation | Current optimized allocation |
| --- | ---: | ---: |
| MessagePack plus NATS output, 256-byte payload | 400 B | 0 B |
| MessagePack plus NATS output, 4,096-byte payload | 4,256 B | 0 B |
| Command ingress, 256-byte payload | 816 B | 416 B |
| Command ingress, 4,096-byte payload | 8,512 B | 4,256 B |
| Query reply serialization, measured payload range | 408-4,264 B | 0 B |

The remaining inbound allocation in those benchmarks is the deserialized application object. The eliminated portion
is the temporary transport `byte[]`.

### 2.3 Confirmed remaining allocation and copy paths

| Path | Current behavior | Required disposition |
| --- | --- | --- |
| Command request reply at requester | Reply is received as `byte[]`, then deserialized | Convert to typed direct reply |
| Function request reply at requester | Reply is received as `byte[]`, then deserialized | Convert to typed direct reply |
| Legacy command/query feature paths | `NatsByteArrayMessageSerializer.Deserialize` calls `ToArray()` | Retain only as bounded rollback path, then retire |
| Core event compatibility listeners | Receive `NatsMsg<byte[]>` | Migrate where ownership can be explicit |
| Some JetStream compatibility listeners | Receive `NatsMsg<byte[]>` | Migrate separately from actor-owned delivery |
| `NatsActorMessage.ReplyAsync` | Serializes reply to an intermediate array | Use typed NATS reply serializer |
| Durable replay envelope | Creates event and envelope byte arrays | Optimize temporary buffers; retain required durable ownership |
| Historical/digest compatibility helpers | Return exact `byte[]` representations | Preserve as explicit compatibility boundary |
| Content/encoded measurement | Uses a new `ArrayBufferWriter<byte>` | Pool or cache measurement writers only where concurrency-safe |

### 2.4 Documentation inconsistency

`PERFORMANCE.md` says events remain on the legacy byte-array adapter. Current JetStream actor delivery already uses
owned shared event payloads by default. Documentation and tests must be updated from verified code behavior during
Gate 0; the implementation must not repeat already completed work.

## 3. Definitions

### 3.1 Zero intermediate payload allocation

No GC-owned `byte[]`, `MemoryStream`, or temporary encoded copy is created between a typed message and the
NATS-provided output writer.

### 3.2 Allocation-controlled inbound processing

The transport payload is either consumed directly during the NATS callback or copied once into a pooled owner whose
lifetime is explicit. The payload is not copied into a GC-owned array before actor deserialization.

### 3.3 Required allocation

An allocation required to create the application message object, retain immutable durable bytes beyond a pool lease,
or preserve an existing byte-level digest/wire contract.

### 3.4 Legacy allocation

An intermediate transport array created only because an older interface accepts or returns `byte[]`.

## 4. Required invariants

### MP-NATS-INV-001: Direct outbound serialization

Every supported typed Core NATS and JetStream publish/request/reply path writes MessagePack directly to the NATS
`IBufferWriter<byte>`.

### MP-NATS-INV-002: No unowned receive memory

No actor mailbox may retain a `ReadOnlySequence<byte>` backed by NATS internal receive storage unless ownership has
been transferred through an explicit disposable owner.

### MP-NATS-INV-003: Actor scheduling remains authoritative

Serialization optimization must not move material domain deserialization or processing onto a NATS socket/read loop.
Owned payloads may cross dispatch and mailbox boundaries; typed object creation remains within the actor processing
boundary unless a measured, reviewed exception is documented.

### MP-NATS-INV-004: Exactly-once payload release

Every pooled inbound payload is released exactly once after successful deserialization, rejected admission,
cancellation, dispatch failure, actor failure, shutdown drain, or fan-out completion.

### MP-NATS-INV-005: Wire compatibility

Existing MessagePack keys, contractless member names, compression behavior, type resolution, historical byte digests,
and durable replay formats remain readable. A formatter or compression change requires explicit old/new compatibility
evidence.

### MP-NATS-INV-006: Durable ownership

Durable replay, persistence, hashing, or de-duplication may retain an exact owned byte representation. It must not
retain memory after returning its pool lease.

### MP-NATS-INV-007: Bounded pooling

Pool rentals have bounded size, deterministic return paths, and no references retained after return. Oversized
payloads follow a documented policy rather than expanding a process-wide pool without limit.

### MP-NATS-INV-008: No exception-driven normal flow

Malformed payloads produce one bounded transport failure result and the correct Core/JetStream disposition. Expected
compatibility selection and empty-payload handling do not throw as normal branching behavior.

### MP-NATS-INV-009: No per-message observability allocation

Production counters use allocation-free numeric instruments and cached tags. Detailed payload diagnostics are sampled
or enabled explicitly and never retain complete trading messages.

## 5. Scope and non-goals

### 5.1 In scope

- Core NATS command, query, function, event, and reply serialization.
- JetStream actor event publishing and consumption.
- Compatibility listeners and event producer/consumer bases.
- Pooled receive-buffer ownership through dispatch and actor mailboxes.
- Durable replay serialization where temporary arrays can be removed safely.
- Shared MessagePack options and formatter initialization.
- Allocation benchmarks, broker-backed transport tests, GC soak tests, and documentation.

### 5.2 Non-goals

- Replacing MessagePack-CSharp.
- Changing NATS subject names or actor routing.
- Sharing mutable deserialized domain objects across actors.
- Removing allocations intrinsic to constructing message object graphs.
- Holding NATS internal receive buffers after their documented lifetime.
- Weakening JetStream ACK/NAK, replay, ordering, or idempotency behavior.
- Changing historical digest bytes or immutable persisted definitions without a versioned migration.
- Adding per-message logs to prove performance.

## 6. Target architecture

### 6.1 Outbound Core NATS and JetStream

```text
typed command/query/event/result
        |
        v
NatsMessagePackSerializer<T>
        |
        v
NATS IBufferWriter<byte>
        |
        v
NATS protocol/socket buffer
```

There is no application-owned encoded `byte[]` between the typed object and NATS.

### 6.2 Inbound command and query

```text
NATS receive sequence
        |
        v
NatsMemoryOwner<byte> (pooled owned copy)
        |
        v
dispatch stripe -> actor mailbox
        |
        v
MessagePack deserialize from ReadOnlySequence<byte>
        |
        +--> release owner immediately
        v
typed actor processing
```

The one pooled copy is retained because NATS receive memory cannot be kept across asynchronous mailbox processing.
It avoids a GC payload allocation while preserving actor-thread ownership.

### 6.3 Inbound event fan-out

```text
one pooled root payload
        |
        v
reference-counted branch per mailbox
        |
        v
independent typed event per actor
        |
        v
release branch; final branch returns root owner
```

Actors continue to receive independent event object graphs. Only immutable serialized bytes are shared.

### 6.4 Durable replay

Durable bytes must outlive the original serialization call. The target is allocation-controlled rather than falsely
zero-allocation:

```text
typed event -> pooled scratch/measurement as required -> one final owned durable representation
```

Any final GC array retained by the durable transport is an explicit boundary allocation. Intermediate event/envelope
arrays must be eliminated only when MessagePack format and replay compatibility can be preserved.

## 7. Work packages and implementation gates

## Gate 0 - Baseline, inventory, and contract freeze

### Deliverables

1. Build a route matrix covering every Core and JetStream producer, consumer, request, reply, fan-out, compatibility,
   and durable path.
2. Record for each path:
   - typed payload type;
   - serializer implementation;
   - receive buffer type;
   - ownership transfer points;
   - current copies and allocations;
   - compression option;
   - ACK/NAK or Core delivery semantics.
3. Reconcile `PERFORMANCE.md`, `Messaging-NATS-Implementation-Details.md`, benchmark results, and current code.
4. Freeze representative wire fixtures for commands, queries, events, results, malformed payloads, compressed payloads,
   and durable replay envelopes.
5. Capture baseline benchmarks on the same host/runtime for 128 B, 256 B, 1 KB, 4 KB, 16 KB, and 128 KB payloads.
6. Add a source scan/architecture test that identifies production hot paths calling:
   - `MessagePackSerializer.Serialize` returning `byte[]`;
   - `ReadOnlySequence<byte>.ToArray()`;
   - `MemoryStream.ToArray()`;
   - legacy `IDataSerializer.Serialize` from a NATS publish/reply path.

### Exit criteria

- Every transport path is classified.
- Existing wire fixtures round-trip with MessagePack 3.1.8.
- Existing benchmark results are reproducible or replaced with dated results explaining the variance.
- No implementation begins with an unknown ownership lifetime.

## Gate 1 - Complete typed Core request/reply

### Implementation

1. Replace command request reply handling in `NatsActorProducer` with
   `RequestAsync<TRequest, ServiceResult<TResult>>` and typed serializers in both directions.
2. Apply the same conversion to function request/reply.
3. Make legacy `NatsActorMessage.ReplyAsync` publish the typed result directly when that compatibility class remains
   reachable.
4. Remove `_messageSerializer` and `_dataSerializer` fields from the producer only after all production call sites no
   longer use them.
5. Preserve cancellation ownership, request timeouts, trace headers, correlation, and error propagation exactly.
6. Do not introduce a new actor, polling service, retry loop, or background serialization worker.

### Tests

- Command and function typed reply round trips for success and failure results.
- Empty/null reply behavior.
- Malformed reply behavior with complete bounded exception details.
- Caller cancellation, producer shutdown cancellation, timeout, and reconnect.
- Exact command/result correlation after concurrent requests.
- Byte compatibility between legacy and typed serializers for representative contracts.
- Allocation benchmark proving zero intermediate reply array.

### Exit criteria

- Query, command, and function requester paths all use typed reply deserialization.
- Reply serialization allocates 0 B at the isolated serializer boundary for the representative fixed-writer benchmark.
- No request context or pooled payload survives terminal completion.

## Gate 2 - Complete Core event owned-payload migration

### Implementation

1. Separate actor-owned Core event delivery from compatibility listener callbacks.
2. Receive actor events through `NatsMemoryOwner<byte>` when deserialization must be deferred to an actor mailbox.
3. Reuse the reference-counted event-payload design for Core event routing where one incoming event fans out to
   multiple mailboxes.
4. Preserve one independently materialized typed event object per destination actor.
5. Convert `NatsActorEventListener` callback contracts to explicit ownership or typed deserialization only where all
   callers can comply with the lifetime contract.
6. Keep a named compatibility adapter for external/legacy callbacks that require `byte[]`; increment a legacy-copy
   counter whenever it is used.
7. Default all internal actor registrations to the owned path.

### Tests

- One, two, five, and seventeen-destination fan-out.
- Route rejection before ownership transfer.
- One destination fails while other destinations complete.
- Cancellation during dispatch and shutdown drain.
- Duplicate/replayed event behavior.
- Exactly-once root and branch release under every terminal path.
- No mailbox can deserialize after its payload is returned.
- Allocation comparison against the existing event fan-out baseline.

### Exit criteria

- No internal Core actor event path calls `ReadOnlySequence<byte>.ToArray()`.
- Shared bytes are returned only after every branch terminates.
- Fan-out ordering and actor isolation remain unchanged.

## Gate 3 - Consolidate JetStream owned delivery

### Implementation

1. Verify every `NatsJetStreamActorConsumer` path uses owned payloads when actor processing is asynchronous.
2. Remove duplicate byte-array construction when adapting JetStream messages to Core actor messages.
3. Keep ACK after successful mailbox handoff/processing according to the existing delivery contract.
4. Preserve NAK/redelivery and terminal handling for parsing, admission, dispatch, and actor failures.
5. Retain the legacy option only as a documented rollback switch during rollout.
6. Remove the rollback switch after soak acceptance and a full release interval without fallback use.

### Tests

- Broker-backed publish, consume, ACK, NAK, redelivery, and reconnect.
- Malformed MessagePack does not leak a pooled owner and is not incorrectly acknowledged.
- Admission rejection and mailbox shutdown return the owner exactly once.
- Fan-out delivery completion cannot ACK twice.
- Consumer restart leaves no retained pool leases or dispatch entries.

### Exit criteria

- Internal JetStream actor delivery has no GC-owned ingress payload copy.
- ACK/NAK evidence matches the pre-change delivery semantics.
- Legacy fallback count remains zero in integration and soak tests.

## Gate 4 - Durable replay allocation control

### Implementation

1. Classify each durable replay array as required final ownership or removable intermediate storage.
2. Preserve the currently readable MessagePack and legacy JSON envelope formats.
3. Use pooled writers for temporary envelope construction where ownership is bounded by one method call.
4. Retain one exact owned payload whenever required for JetStream publication, de-duplication hashing, retry storage, or
   legacy byte-digest compatibility.
5. Do not retain `ReadOnlyMemory<byte>` backed by a returned pool rental.
6. Avoid serializing the same event multiple times for payload, measurement, and hash where one canonical owned
   representation can serve all three.
7. If removing the inner event-payload array requires a new envelope schema, introduce an additive version and a dual
   reader. Never overwrite immutable stored envelopes.

### Tests

- Current compressed MessagePack envelope fixture remains readable.
- Legacy JSON envelope remains readable.
- Process/replay de-duplication IDs remain deterministic.
- Retry mutation retains the original event payload and bounded error details.
- ACK only follows successful replay publication or terminal disposition.
- Pool ownership survives asynchronous publish acknowledgement and is then released.
- Restart/replay tests prove no dependency on process-local pooled memory.

### Exit criteria

- Every remaining durable `byte[]` has a documented lifetime and correctness reason.
- No temporary array is created solely to pass bytes from MessagePack into NATS.
- Replay compatibility and idempotency tests pass against old fixtures.

## Gate 5 - Formatter and contract optimization

### Implementation

1. Inventory the highest-frequency concrete actor contracts and their resolved MessagePack formatters.
2. Prewarm formatter caches for the bounded startup actor-message catalog before trading admission opens.
3. Evaluate explicit indexed `[MessagePackObject]` contracts or generated formatters for high-frequency types.
4. Preserve existing contractless wire names until a versioned dual-reader migration is approved.
5. Never renumber an existing MessagePack key.
6. Prefer structs only when copying semantics, default values, boxing, and actor contracts have been measured and
   reviewed; do not convert message classes mechanically.
7. Keep shared `MessagePackBinarySerializer.Options` authoritative unless a versioned route-specific profile is
   introduced.

### Tests

- Cold-start and warm-path allocation measurements.
- Golden-byte fixtures for every migrated contract.
- Old writer/new reader and new writer/old reader where rolling deployment requires it.
- Null, default, unknown-field, added-field, and reordered-source-member compatibility.
- Analyzer coverage for duplicate keys and unsupported members.

### Exit criteria

- No unplanned formatter generation occurs after trading admission for the catalogued hot contracts.
- Wire compatibility gates pass before any formatter migration is enabled.
- Measured improvement justifies each schema change.

## Gate 6 - Compression policy verification

### Implementation

1. Benchmark `Lz4BlockArray` and `None` using representative commands, queries, signal events, workflow events, and
   durable replay envelopes.
2. Measure encoded size, CPU time, temporary pool use, end-to-end latency, and allocation at each payload size.
3. Keep the current compression setting unless evidence shows a material Ring 2 improvement and compatibility can be
   maintained.
4. If profiles differ by route, identify the profile in a versioned transport contract; never infer it from payload
   size after publication.
5. Validate mixed-version operation before changing production defaults.

### Exit criteria

- A documented threshold explains where compression helps or hurts.
- No compression change is made from microbenchmark latency alone.
- All existing compressed messages remain readable.

## Gate 7 - Allocation observability and safeguards

### Implementation

Add low-cost aggregate counters for:

- typed direct serializations;
- typed sequence deserializations;
- owned pooled ingress messages and bytes;
- pool rentals/returns and outstanding leases;
- legacy byte-array fallbacks;
- malformed payloads;
- oversize payloads;
- serialization/deserialization failures; and
- durable required allocations.

Metrics must use cached instruments and bounded dimensions. Actor type, transport kind, and operation kind are allowed;
subject, entity ID, command ID, thread ID, and exception text are prohibited metric dimensions.

Detailed allocation sampling must be opt-in and time-bounded. Production hot paths must not call
`GC.GetAllocatedBytesForCurrentThread()` per message.

### Exit criteria

- Supervisor/Operations health can distinguish direct, pooled, legacy, malformed, and leaking paths.
- Outstanding pooled leases return to zero after drain and shutdown.
- Metrics add no measurable allocation in the isolated hot-path benchmark.

## Gate 8 - System verification and rollout

### Verification suites

1. Serializer unit tests.
2. Actor-message ownership unit tests.
3. Core NATS broker-backed integration tests.
4. JetStream durable integration tests.
5. MessagePack wire-compatibility fixture tests.
6. Request/reply cancellation and reconnect tests.
7. Fan-out failure and shutdown-drain tests.
8. BenchmarkDotNet allocation/latency suite.
9. GC soak under representative Ring 2 message rates.
10. Full repository build and focused messaging test suite.

### Quantitative acceptance targets

| Measure | Target |
| --- | --- |
| Typed outbound serializer intermediate allocation | 0 B/op for benchmarked payloads |
| Typed query/command/function reply intermediate allocation | 0 B/op at serializer boundary |
| Internal inbound GC payload array | 0 B before typed object materialization |
| Pooled lease balance after test/shutdown | exactly zero outstanding |
| Legacy fallback in accepted production-like soak | zero invocations |
| Message loss, duplicate ACK, double release | zero |
| Throughput regression | no material regression; investigate any repeatable loss above 5% |
| P99 transport-boundary latency | no material regression at representative concurrency |
| Gen 2/LOH growth attributable to transport payload arrays | zero steady-state growth |

Targets are evaluated against repeated runs on the same host and runtime. Variance and confidence intervals must be
reported; a single benchmark run is not acceptance evidence.

### Rollout sequence

1. Enable typed command/function replies in Development.
2. Soak Core request/reply and compare legacy-fallback counters.
3. Enable owned Core event delivery in Development.
4. Soak event fan-out and verify lease balance.
5. Verify JetStream owned delivery and replay under disconnect/reconnect.
6. Run one full trading-day development soak including market open, active session, and close.
7. Enable in paper trading with rollback switches retained.
8. Remove legacy switches only after one accepted release interval.

## 8. File-level implementation map

| Area | Expected files |
| --- | --- |
| Typed serializer | `Serializers/NatsMessagePackSerializer.cs` |
| Legacy adapters | `Serializers/NatsByteArrayMessageSerializer.cs`, `Serializers/NatsMessagePackDataSerializer.cs` |
| Core producer/requester | `NatsActorProducer.cs` |
| Core consumer ownership | `NatsActorConsumer.cs`, `NatsOwnedCommandMessage.cs`, `NatsOwnedQueryMessage.cs` |
| Core event ownership | `NatsActorEventListener.cs`, `NatsEventConsumer.cs`, new/reused owned event adapter as justified |
| JetStream producer | `NatsJetStreamActorProducer.cs` |
| JetStream consumer/fan-out | `NatsJetStreamActorConsumer.cs`, `NatsOwnedEventMessage.cs`, `NatsJetStreamEventListener.cs` |
| Durable replay | `NatsJSDurableReplayQueue.cs`, `NatsJSDurableQueueTransport.cs` |
| Shared serializer policy | `MessagePackBinarySerializer.cs`, `IBinarySerializer.cs` only if compatibility permits |
| Options/rollout | consumer option interfaces and concrete options |
| Unit/integration tests | Messaging.Nats UnitTests and IntegratedTests projects |
| Benchmarks | Messaging.Nats.Benchmarks serialization, query, event, and durable suites |
| Documentation | `PERFORMANCE.md`, benchmark `RESULTS.md`, Messaging NATS implementation details |

## 9. Required failure behavior

| Failure | Required behavior |
| --- | --- |
| Serialization failure before publish | Fail the operation; publish no partial frame; record bounded evidence |
| Core malformed inbound payload | Record one failure, release ownership, continue subscription |
| JetStream malformed inbound payload | Release ownership and apply defined NAK/terminal policy; never ACK as success |
| Mailbox admission rejection | Release at sender and preserve delivery semantics |
| Actor throws after deserialization | Payload already released; actor containment records processing failure |
| Reply serialization failure | Preserve primary actor result separately from reply failure |
| Cancellation before ownership transfer | Receiver disposes payload |
| Cancellation after ownership transfer | Mailbox/actor disposes payload |
| Fan-out branch failure | Complete/release that branch without invalidating other branches |
| Pool exhaustion/oversize | Follow bounded oversize policy and expose a counter; do not spin or retry forever |

## 10. Review checklist

- [ ] No new `byte[]` serialization convenience call appears in a hot NATS path.
- [ ] Every `NatsMemoryOwner<byte>` has a documented owner at each transfer.
- [ ] Every success, failure, cancellation, rejection, and shutdown path returns ownership exactly once.
- [ ] No pooled memory is referenced after return.
- [ ] No deserialization work is moved onto a socket/read loop without explicit latency evidence.
- [ ] Core and JetStream delivery semantics are unchanged.
- [ ] Durable replay retains restart-safe owned data.
- [ ] Wire fixtures remain readable.
- [ ] Metrics have bounded cardinality and no per-message logging.
- [ ] Benchmarks measure allocations as well as time.
- [ ] Live broker tests terminate all hosts and subscriptions.
- [ ] Full solution build passes.

## 11. Completion definition

The work is complete when every internal actor transport path is either typed direct-to-NATS or uses an explicitly
owned pooled receive payload; command, query, and function request/reply have no intermediate transport arrays; Core
and JetStream event fan-out release one shared serialized payload correctly; every remaining durable or compatibility
array is documented as required; legacy fallback usage is zero in the accepted soak; allocation and ownership gates
pass; and the full solution plus focused broker-backed suites pass without changing actor routing, delivery, replay,
or immutable wire contracts.
