# Deferred Nerdbank.MessagePack Migration and ActorEventSourceDb Binary Payload Specification

**Document type:** Implementation specification

**Status:** Nerdbank no-go/deferred; current MessagePack-CSharp retained; event-log binary work approved for later implementation

**Created:** 2026-08-07

**Owner:** IFM engineering

**Decision scope:** Deferred serializer migration research plus active `ActorEventSourceDb` MessagePack-CSharp/`bytea` requirements

## 1. Executive decision

Nerdbank.MessagePack is a **no-go for the current development cycle**. IFM will retain MessagePack-CSharp for actor messages, NATS transport, TickAggregation quote/trade messages, and durable JetStream messages. No Nerdbank package, witness catalog, converter, registry, attribute removal, or dual-serializer production path is to be implemented now.

The decision is driven by three confirmed migration blockers:

1. the default Nerdbank quote-segment shape serializes the entire 64-slot physical buffer rather than logical `Count`;
2. `ActorEntityId` silently loses its value with the default shape; and
3. runtime event-type processing requires a generated registry instead of the existing MessagePack-CSharp runtime-`Type` path.

The remaining Nerdbank analysis is retained in this document as deferred research. Re-evaluate it after the supported .NET 11 release and its Zstandard implementation can be tested with the actual application. Elapsed calendar time alone does not authorize implementation; a new benchmark, compatibility audit, and explicit approval are required.

One independent improvement is approved for a later implementation: replace JSON event payloads in `ActorEventSourceDb` with the **current MessagePack-CSharp serializer** and store the binary payload in PostgreSQL `bytea`. This event-log change does not introduce Nerdbank and does not alter the current actor/NATS message serializer.

Current binding decisions:

- keep all existing MessagePack-CSharp contract attributes and formatters;
- keep the existing count-aware `FuturesTickQuoteDataSegmentFormatter`;
- keep existing actor transport and durable-queue serialization unchanged;
- implement event-log binary serialization as a separately planned change using MessagePack-CSharp;
- use `EventData bytea NOT NULL` in the recreated PostgreSQL event-log schema;
- do not retain a JSON shadow column or permanent dual reader; and
- do not generate production code from the deferred Nerdbank sections until a future approval.

## 2. Goals

1. Keep MessagePack-CSharp as IFM's active actor-message serializer.
2. Move `ActorEventSourceDb.EventData` from indented JSON text to MessagePack-CSharp binary bytes.
3. Store event payload bytes in PostgreSQL `bytea` without JSON/base64 conversion on the normal path.
4. Preserve event identity, ordering, replay, snapshot, last-N range, projector, and unknown-event semantics.
5. Benchmark JSON/text versus MessagePack-CSharp/`bytea` before accepting the event-log implementation.
6. Preserve the deferred Nerdbank research so it can be reassessed with .NET 11 and Zstandard later.

## 3. Non-goals

- Replacing MessagePack-CSharp with Nerdbank.MessagePack in the current development cycle.
- Changing actor/NATS/TickAggregation/durable-queue serialization.
- Removing existing MessagePack-CSharp attributes, unions, constructors, or formatters.
- Reading both JSON and binary event payloads after the event-log schema cutover.
- Changing actor command-success versus state-change semantics.
- Changing event ordering, actor mailbox ordering, event immutability, or replay selection semantics.
- Converting human-readable diagnostic logs, exception text, or UI JSON merely because event payloads become binary.
- Migrating any IFM contract to Nerdbank during the event-log work.
- Introducing .NET 11 before its use is separately approved.
- Writing production code as part of this documentation update.

## 4. Current-state evidence

The repository currently contains:

| Item | Observed count or behavior |
| --- | ---: |
| Projects with a direct MessagePack package reference | 15 |
| C# source files touching MessagePack | 754 |
| Active `[MessagePackObject]` contract types | 1,003 |
| `[Key(...)]` uses | 8,355 |
| `[IgnoreMember]` uses | 4,039 |
| `AllowPrivate = true` uses | 851 |
| Active `[SerializationConstructor]` uses | 791 |
| Writable (`set`/`init`) members currently marked `[IgnoreMember]` | 234 |
| Custom MessagePack formatter declarations | 1 |
| MessagePack union declarations | 5 attributes across 3 declared union roots |

The active-type and constructor counts above come from a syntax-aware Roslyn audit that excludes comments, generated output, `bin`, `obj`, and audit artifacts. Earlier text-search counts of 1,004 and 793 each included commented-out attributes. The remaining figures are source inventories rather than counts of distinct runtime wire roots. They establish that automated removal without contract-level validation would be unsafe.

### 4.1 Current actor transport

The active NATS typed serializer:

- uses MessagePack-CSharp `ContractlessStandardResolver`;
- applies built-in `Lz4BlockArray` compression;
- writes directly to NATS's `IBufferWriter<byte>`; and
- reads from `ReadOnlySequence<byte>`.

The direct writer shape is good and must be preserved. The library and compression implementation change; the transport must not regress to `byte[]` plus a second NATS copy.

### 4.2 Current event log

`ActorEventSourceDb` currently:

- serializes each `IEvent` with `JsonConvert.SerializeObject(..., Formatting.Indented)`;
- stores the result in `event_log.EventData text NOT NULL`;
- reads the payload as a UTF-16 .NET `string`;
- resolves an assembly-qualified runtime type with `Type.GetType`; and
- deserializes the string with Newtonsoft.Json.

This path pays for JSON formatting, UTF-16 strings, UTF-8 database encoding, textual property names, and runtime reflection during replay. Binary MessagePack is a better fit for an internal immutable event log.

Command/error diagnostic JSON is a separate concern. It may remain readable JSON unless a later specification deliberately changes it.

## 5. Deferred Nerdbank design decisions (inactive)

Sections 5 through 8 and 10 through 20 preserve the prior Nerdbank investigation for a future .NET 11 reassessment. They are not current implementation requirements and must not be used to generate production code without a new explicit approval. Section 9 is the active event-log requirement and uses MessagePack-CSharp.

### 5.1 No legacy compatibility

The production cutover is coordinated across all producers, consumers, durable queue workers, event projectors, and event-store readers/writers. Development/test databases and durable streams may be recreated or explicitly cleared as part of the cutover procedure.

There will be no:

- legacy MessagePack-CSharp decoder;
- JSON fallback for event payloads;
- payload sniffing based on the first byte;
- migration of released event history; or
- long-lived `Old`/`New` feature flag.

The TickAggregation pilot may coexist in source with the current serializer while it is evaluated, but each pilot subject must have exactly one configured serializer on both producer and consumer. Mixed-format auto-detection is prohibited.

### 5.2 Domain types will not be partial

Applying `[GenerateShape]` directly to a type requires that type, and any containing type, to be declared `partial`. IFM will not apply `[GenerateShape]` directly to domain messages.

Instead, each contract assembly will own an infrastructure-only witness/catalog class:

```csharp
[GenerateShapeFor<FuturesTickTradeDataChangedEvent>]
[GenerateShapeFor<FuturesTickQuoteDataChangedEvent>]
[GenerateShapeFor<InsertFuturesTickTradeDataCommand>]
[GenerateShapeFor<InsertFuturesTickQuoteDataCommand>]
internal sealed partial class MarketDataFeedMessageShapes;
```

Only the empty catalog is partial. The command/event/query types remain ordinary non-partial records, classes, record structs, or structs. A catalog can carry many `[GenerateShapeFor<T>]` attributes, and shapes for referenced child types are generated from the top-level roots.

This resolves the partial-type concern for the great majority of current IFM contracts.

### 5.3 Attribute-free maps are the default

General actor contracts will use Nerdbank's default property-name map representation. Therefore the following MessagePack-CSharp attributes are removed:

- `[MessagePackObject]`;
- `[Key(...)]`;
- `[IgnoreMember]`;
- `[SerializationConstructor]`;
- `[Union(...)]`;
- `[MessagePackFormatter(...)]`; and
- `using MessagePack` directives used only for those attributes.

Property-name maps are selected because:

- there is no old positional schema to preserve;
- property additions/removals are safer than numeric array positions;
- the domain model becomes substantially quieter;
- the event log will be easier to evolve before 1.0; and
- repeated property names compress well in buffered quote payloads.

This choice must be benchmarked with the actual attribute-free pilot contracts. The earlier exploratory Nerdbank numbers used indexed/keyed models and are not sufficient to approve property-name maps by themselves.

Numeric Nerdbank `[Key]` attributes are not the default fallback. If a measured hot contract cannot meet its budget as a map, prefer a centrally registered custom converter or dedicated transport surrogate before returning serialization annotations to the domain model.

### 5.4 Public member inclusion is deliberate

Nerdbank serializes public fields and properties by default. It ignores non-public members by default. A non-collection property is serializable only when it has a getter plus a setter/init accessor or a matching deserialization constructor parameter.

The detailed audit closes the apparent 234-member problem to two repeated query metadata patterns:

- 117 writable `ErrorCode` properties; and
- 117 writable `QueryParams` properties;

spread across 112 query source files. All audited `ErrorCode` constructor assignments restore the static query `ErrorId`. Only 11 files assign `QueryParams`; the remainder leave it null/default. The assigned values are either empty strings or deterministic strings derived from canonical query fields.

These members are operational metadata, not independent wire state. If the old attributes are merely removed, they would enter the wire schema. If the members are merely omitted without refactoring, deserialization can leave `ErrorCode` at zero and can lose derived query diagnostics. The required resolution is:

- change `ErrorCode` to a computed getter returning `ErrorId`;
- change `QueryParams` to a computed getter derived from the query's canonical fields, or to an explicit interface implementation when it is not part of the public contract;
- use an empty/null computed value for query types that intentionally have no query parameters;
- remove constructor assignments that merely cache those derived values; and
- assert both schema exclusion and correct values after round trip.

Consequences for the rest of IFM:

- computed getter-only members such as `EventName`, `CommandName`, `StreamId`, `UserName`, and `EventType` normally require no ignore attribute;
- static constants such as `Actor`, `Verb`, and `ErrorId` are not instance payload members;
- the 234 writable ignored members have a defined mechanical refactoring, but the refactoring and its tests are a full-cutover gate;
- a writable non-wire member must not silently enter the new schema; and
- private state formerly enabled by `AllowPrivate` will not be serialized unless deliberately modeled.

For each writable ignored member, use the first applicable option:

1. make it a computed getter-only member;
2. implement it explicitly through the actor interface;
3. remove it from the transport contract and derive it after deserialization;
4. use a dedicated transport surrogate; or
5. as a documented exception, apply `[PropertyShape(Ignore = true)]`.

The objective is minimal serialization noise, not an unsafe rule that no serialization-specific attribute may ever exist.

### 5.5 Constructor policy

The 791 active MessagePack-CSharp serialization-constructor attributes do not imply that 791 Nerdbank constructor annotations will be necessary.

Nerdbank can use:

- a public default constructor plus writable/init properties;
- a record primary constructor; or
- a constructor whose parameter names match serialized property names, ignoring normal casing differences.

The syntax-aware audit found:

- 975 contract types with public parameterless construction;
- 14 types with exactly one public constructor;
- 14 primary-record contracts;
- zero contract types with multiple public constructors and no public parameterless constructor;
- zero contract types without a public constructor;
- zero non-public keyed members, despite 851 uses of `AllowPrivate = true`;
- two keyed public properties without public `set`/`init`; and
- one keyed readonly field.

Therefore constructor ambiguity is not systemic. `[ConstructorShape]` is needed only when multiple viable constructors make selection ambiguous or when a particular immutable construction path must be selected. Migration must remove obsolete serializer-only constructors where the normal public object model is sufficient.

Three special shapes require explicit treatment:

1. `LookupTypeCollection.Items` is a private-set `List<LookupTypeReadModel>`. Refactor it to a getter-only initialized collection and verify Nerdbank populates it through `Add`.
2. `TradePositionReadModel.OptionLegData` is a private-set array. Arrays cannot be populated through `Add`; select the existing full constructor with `[ConstructorShape]` or use a dedicated surrogate. This is a documented exception to the otherwise attribute-free model.
3. `ActorEntityId.Value` is a readonly field and its string constructor parameter is named `entityId`, not `value`. Use one explicit generated converter/surrogate, or make a single canonical matching constructor and mark it deliberately. Preserve the existing empty-value normalization to `"none"`.

The contract audit must fail the build for an ambiguous type until it is refactored or explicitly annotated.

### 5.6 Polymorphism and interface-typed members

Polymorphism is not inferred from the runtime object by default. Without an explicit union, serializing through an interface/base declaration can lose derived members or fail to instantiate an abstract type.

The audit found 118 serialized `IActorEntityId EntityId` members across 113 files and 151 direct `IActorEntityId` implementations in the repository. The current single `IActorEntityId` union entry for `ActorEntityId` is not a complete representation of the repository. An executable Nerdbank probe also failed to deserialize a representative `GetFuturesRsiSignalQuery` when its entity ID remained interface-typed.

The full migration will therefore:

- replace each query's public wire member with its concrete query-parameter/entity-ID type;
- implement the interface `EntityId` explicitly when actor infrastructure still requires `IActorEntityId`;
- preferably compute the concrete entity ID from the query's canonical fields so it is not duplicate wire state;
- use explicit, stable integer discriminators only where a genuinely polymorphic member must remain; and
- configure Nerdbank runtime derived-type mappings centrally rather than placing large union lists on domain interfaces.

The other 15 interface-looking keyed members reported by the syntax audit are collection interfaces or concrete types whose names begin with `I`; they are not an unbounded domain-polymorphism family. Collection members still require ordinary round-trip tests. Duck typing and typeless/object deserialization are prohibited for actor contracts because their ambiguity, security profile, and slower deserialization are unnecessary here.

For the TickAggregation pilot, `EntityId` is already the concrete `TickDataEntityId`, so it does not have this problem.

### 5.7 Runtime event types use a generated allowlist

Event-log and durable-replay code currently receives `IEvent` and discovers its concrete CLR type at runtime. There are two concrete persistence/queue paths to replace:

1. `EventLogReadModel` calls `Type.GetType(EventTypeName)` and then Newtonsoft.Json runtime-type deserialization; and
2. `NatsJSDurableReplayQueue` persists `eventType.AssemblyQualifiedName`, invokes MessagePack-CSharp with a runtime `Type`, calls `Type.GetType` on replay, and retains a legacy JSON fallback.

`ActorEventSourceDb` also obtains/stores event-name IDs from assembly-qualified CLR identity. Nerdbank production code will not fall back to reflection.

Introduce an immutable allowlisted `EventContractRegistry` shared by `ActorEventSourceDb` and `NatsJSDurableReplayQueue`, with two lookup directions:

- CLR `Type` to a descriptor containing the stable contract name and a closed generated serialization delegate/type shape for writes; and
- stable event contract name to a descriptor containing a closed generated deserialization delegate/type shape for reads.

The registry is an allowlist. Startup validation must fail before message intake if:

- an event used by an actor has no generated shape;
- two event types claim the same stable contract name;
- a union discriminator is duplicated; or
- an event-log contract cannot round trip.

Assembly-qualified type names are not durable contract identifiers. Replace the event-log dependency on `Type.GetType(assemblyQualifiedName)` with a stable contract name owned by the registry. Initially this may be the full CLR namespace plus type name without assembly name/version. Later renames require an explicit alias in the registry.

The witness/registry catalog must be checked in or generated before normal C# compilation. One source generator must not depend on discovering output from another source generator in the same compilation because generator ordering is not a reliable contract. A build-time catalog tool or Codex-maintained checked-in catalog is acceptable, provided a test scans the intended contract assemblies and proves that every concrete persisted/published event has exactly one descriptor. Production startup uses only the immutable registry; it does not use the scan or reflection fallback.

### 5.8 Serializer lifetime

Create and configure one immutable `Nerdbank.MessagePack.MessagePackSerializer` instance per serialization profile and reuse it. Nerdbank documents the serializer as immutable and thread-safe after configuration.

Do not construct a serializer per message.

Use synchronous APIs for:

- NATS Core and JetStream `IBufferWriter<byte>` serialization;
- `ReadOnlySequence<byte>` deserialization;
- event-log payload encoding before the asynchronous database write; and
- event-log payload decoding after the asynchronous database read.

Use async serialization only for a true `Stream`/`PipeReader`/`PipeWriter` whose payload should not be materialized at once. Surrounding async I/O does not make CPU-only serialization asynchronous.

### 5.9 Package baseline

At implementation start, pin and record the current stable package versions rather than accepting floating versions. As of this specification:

- `Nerdbank.MessagePack` stable is 1.2.36; and
- `K4os.Compression.LZ4.Streams` stable is 1.3.8.

Recheck current stable versions, release notes, target-framework support, licenses, and NuGet vulnerability advisories immediately before code generation. Do not select a prerelease package for the production cutover without a separate approval.

## 6. Deferred Nerdbank payload framing and compression

### 6.1 Compression abstraction

Define an implementation-independent codec contract in the framework serialization layer. The exact API may evolve during implementation, but it must express:

- codec identifier;
- maximum accepted uncompressed length;
- synchronous compression from a sequence/span into `IBufferWriter<byte>`;
- synchronous decompression into a caller-owned pooled writer;
- exact completion/disposal semantics; and
- whether a dictionary is required and, if so, its stable dictionary identifier.

Initial codec identifiers:

| ID | Codec | Use |
| ---: | --- | --- |
| 0 | None | Small messages below the measured threshold |
| 1 | LZ4 Frame (K4os) | .NET 10 interim compression |
| 2 | Zstandard (.NET 11) | Planned replacement after .NET 11 qualification |

Codec IDs are transport metadata, not MessagePack contract fields.

### 6.2 Binary payload header

Every compressed-capable transport/storage payload uses a small fixed header followed by a dynamically sized body:

| Offset | Size | Field |
| ---: | ---: | --- |
| 0 | 4 | Magic value identifying an IFM binary payload |
| 4 | 1 | Envelope version |
| 5 | 1 | Serializer ID (`NerdbankMessagePack`) |
| 6 | 1 | Compression codec ID |
| 7 | 1 | Flags/reserved |
| 8 | 4 | Uncompressed MessagePack length, little-endian |
| 12 | variable | Raw or compressed MessagePack bytes |

The exact magic bytes will be selected during implementation and frozen by tests. Version 1 is the only supported production version at cutover.

Although there is no legacy reader, the header is still required because it:

- lets the decoder select LZ4 versus Zstandard;
- lets decompression rent an appropriately bounded destination;
- prevents compression choice from leaking into domain contracts;
- supports uncompressed small messages; and
- makes malformed/truncated payloads fail deterministically.

The header is written directly into the destination writer. It must not cause a separate envelope-array allocation.

### 6.3 Compression selection

Start with a configurable threshold, provisionally 1 KiB, and finalize it from the pilot benchmark. Payloads below the threshold remain uncompressed.

Expected initial behavior:

- individual trade messages: normally uncompressed;
- quote buffers: normally compressed when the buffer contains enough active quotes;
- small completion/failure messages: uncompressed; and
- large event-log events/snapshots: compressed when the threshold is met.

To choose based on actual serialized length, serialize once into a pooled staging writer, then either:

- copy the raw bytes to the final writer; or
- compress them into the final writer.

The staging storage is pooled and returned in `finally`. It is not a newly allocated worst-case byte array.

### 6.4 K4os implementation requirements

Use the standard interoperable LZ4 Frame APIs from `K4os.Compression.LZ4.Streams`, not `MemoryStream` wrappers or `LZ4Pickler`.

Use its span/`IBufferWriter<byte>` frame APIs where possible. The exploratory benchmark exposed a roughly 8,728-byte steady allocation per direct-writer operation. The interim codec is not accepted until that fixed allocation is eliminated or reduced below the acceptance budget.

Required approach:

- profile which K4os writer/session objects allocate;
- create a bounded pool of compression/decompression session state if reuse is supported safely;
- never share one mutable frame writer/encoder between concurrent operations;
- rent one session per operation and return it exactly once in `finally`;
- use dynamically growing pooled segments for data;
- dispose/complete a frame exactly once; and
- clear sensitive references before pooling state that can retain user buffers.

Initial steady-state target, excluding the caller-owned final output buffer:

- preferred: 0 B allocated per operation;
- acceptance ceiling: 256 B per operation; and
- rejection: a fixed multi-kilobyte allocation comparable to the exploratory 8,728 B result.

If K4os cannot meet this gate without brittle code, retain uncompressed Nerdbank for the pilot or defer compression until .NET 11 Zstandard. Do not hide the allocation with a larger object pool that introduces unsafe ownership.

### 6.5 .NET 11 Zstandard transition

.NET 11 Preview 1 introduces `ZstandardStream`, `ZstandardEncoder`, and `ZstandardDecoder`, including streaming, one-shot, and dictionary-based operation.

The future transition will:

- add codec ID 2 without changing any domain contract;
- benchmark `Fastest`/low-latency settings against LZ4 on actual IFM payloads;
- validate pooled encoder/decoder reuse and concurrency;
- consider a trained dictionary only for repetitive large tick/event payloads;
- include a dictionary ID in the header flags/extension only if dictionaries are adopted; and
- remove K4os after all persisted/queued pilot data has been drained or recreated.

Because there is no 1.0 compatibility requirement, production can standardize on Zstandard rather than retain LZ4 indefinitely.

## 7. Deferred Nerdbank allocation and concurrency design

### 7.1 Serialization write path

For NATS:

1. obtain the generated shape for compile-time `T`;
2. serialize synchronously into pooled staging storage;
3. choose raw or compressed representation;
4. write the header and body directly to NATS's `IBufferWriter<byte>`;
5. complete the codec session; and
6. return all staging/session resources in `finally`.

For `ActorEventSourceDb`, the same encoder creates one exact-length binary value for the PostgreSQL `bytea` parameter. A final exact-length managed value may remain necessary at the Npgsql binding boundary, but it must not be a constant oversized allocation and must not be copied through a JSON string.

### 7.2 Deserialization read path

1. validate header magic, version, serializer, flags, lengths, and configured maximum;
2. for raw payloads, deserialize directly from the source sequence;
3. for compressed payloads, rent a destination using the validated uncompressed length;
4. decompress exactly that length;
5. deserialize with the generated shape; and
6. return pooled memory/session state in `finally`.

Reject trailing/truncated frames, negative/overflowed lengths, unsupported codecs, and decompressed sizes beyond the configured maximum.

### 7.3 Thread safety

- The configured Nerdbank serializer is shared because it is immutable and thread-safe.
- Compression sessions and mutable staging writers are operation-scoped or exclusively rented.
- A pool is concurrency-safe, but a rented item is never used concurrently.
- The NATS serializer generic singleton contains no per-call mutable state.
- Event shape registries are immutable after startup.
- No global lock is taken for each message.
- Pool misses may allocate but must be measured and bounded under burst concurrency.

## 8. Deferred TickAggregation Nerdbank pilot

### 8.1 Pilot contract roots

The pilot shape catalog includes these top-level messages:

1. `FuturesTickTradeDataChangedEvent`;
2. `FuturesTickQuoteDataChangedEvent`;
3. `InsertFuturesTickTradeDataCommand`;
4. `InsertFuturesTickQuoteDataCommand`;
5. `FuturesTickTradeDataInsertedEvent`;
6. `FuturesTickQuoteDataInsertedEvent`;
7. `FuturesTickTradeDataInsertedCompleteEvent`;
8. `FuturesTickQuoteDataInsertedCompleteEvent`;
9. `FuturesTickTradeDataInsertedFailEvent`; and
10. `FuturesTickQuoteDataInsertedFailEvent`.

Referenced shapes include:

- `ActorSubject`;
- `TickDataEntityId`;
- `TickDataId`;
- `FuturesTickTradeData`;
- `FuturesTickQuoteData`;
- `FuturesTickQuoteDataSegment`;
- `AssetTypeId`;
- `QuoteEmissionReason`; and
- other enum/value types reached from the roots.

The concrete complete/fail event types are serialized as their concrete subject-selected types. Their abstract bases do not require union framing on that path. If code serializes a value declared as `TickAggregationCompleteEvent` or `TickAggregationFailEvent`, central integer-discriminator mappings are required and must be tested separately.

### 8.2 Quote segment special handling

`FuturesTickQuoteDataSegment` currently wraps a pooled array whose physical length can exceed its active `Count`. Its MessagePack-CSharp formatter writes exactly the active elements.

Nerdbank must preserve that rule. The default shape cannot serialize `Buffer` as-is because it would serialize the physical array length and potentially include stale pooled values.

An executable repository probe confirms the failure mode. A segment with a 64-slot physical buffer, `Count = 2`, two active quotes, and a stale third quote produced:

| Path | Encoded bytes | Decoded logical content |
| --- | ---: | --- |
| Default Nerdbank shape | 11,050 | All 64 physical slots, including the stale third quote |
| Count-aware Nerdbank converter | 441 | Exactly the two active quotes |

This is a correctness defect first and a size/latency defect second. The converter is mandatory for the pilot.

Register a central custom converter or surrogate for `FuturesTickQuoteDataSegment` that:

- writes one MessagePack array containing exactly `Count` quote records;
- rejects counts of 0 or greater than `MaximumCount`;
- never reads beyond `Buffer[Count - 1]`;
- allocates one exact-length receiver-owned array for the decoded logical count;
- constructs a segment whose `Buffer.Length` and `Count` are consistent after deserialization;
- does not serialize the `Items` span property; and
- never owns or returns the publisher's `ITickQuoteBufferLease`.

The publisher retains ownership of its quote lease until `_producer.SendAsync` has completed and returns it exactly once in `finally`, as it does today. Serialization sees a borrowed read-only view only.

The receiver must not rent the decoded array from `ArrayPool<T>` under the current actor-message contract. A deserialized actor event has no disposal/lifetime hook that can prove when the last consumer is finished, so pooling the receiver array would create either a leak or a use-after-return risk. Exact owned allocation is the safe v1 design. A future pooled receive model requires a separate reference-counted/disposable message-lifetime protocol.

Implement `FuturesTickQuoteDataSegmentConverter` as an immutable, thread-safe `MessagePackConverter<FuturesTickQuoteDataSegment>` and register its type centrally on the immutable TickAggregation serializer profile. Its only instance state is the generated child converter cached from `ConverterContext`; it has no per-call mutable state. It must:

1. call `SerializationContext.DepthStep()` before reading or writing the structure;
2. write/read exactly one MessagePack array structure;
3. validate `Buffer` and require `1 <= Count <= 64` and `Count <= Buffer.Length` before writing;
4. write an array header equal to `Count` and iterate only `Items`;
5. obtain/cache the generated child converter for `FuturesTickQuoteData` from the generated witness provider;
6. validate the input array header before allocating on read;
7. allocate exactly `count` receiver-owned elements and return a segment with `Buffer.Length == Count`; and
8. never retain, return, or dispose the publisher's buffer/lease.

The outer event/command validation must also require `QuoteCount == QuoteData.Count`. Lease ownership remains with the publisher and is released exactly once after asynchronous send completes, including serialization failure, send failure, and cancellation.

### 8.3 Attribute removal for the pilot

Remove MessagePack-CSharp attributes from the pilot contracts only after schema and round-trip tests pass.

The pilot event/command computed members are getter-only, so Nerdbank should naturally exclude them. Tests must prove that these fields are absent from the generated schema and serialized map:

- `UserName`;
- `EventName`;
- `EventType`;
- `CommandName`;
- `StreamId`;
- computed command `EventSource`; and
- static `Actor`, `Verb`, and `ErrorId` constants.

Do not add `[PropertyShape(Ignore = true)]` to these members unless the generated schema proves it is required.

### 8.4 Pilot transport integration

The pilot must exercise the real path:

```text
Databento TickAggregationService
  -> TickAggregationEventPublisher
  -> IJSActorProducer / JetStream
  -> TickAggregationEventActor
  -> InsertFuturesTick*Command
  -> TickAggregationCommandActor
  -> ActorEventSourceDb
  -> TickAggregationEventActor
  -> ScyllaDB tick_trade_data / tick_quote_data
```

Both producer and consumer select Nerdbank from the known concrete subject/message type. No receiver guesses the serializer from payload contents.

During source-level pilot development, a process-wide immutable profile may route only the explicit TickAggregation allowlist to the pilot serializer while all other types remain on the existing serializer. This is test scaffolding only. It must not become a permanent production dual-wire reader.

### 8.5 Pilot event-log scope

The pilot persists TickAggregation inserted events as Nerdbank MessagePack in a disposable/integration `ActorEventSourceDb` schema. It verifies save, full replay, snapshot replay where applicable, last-N replay, projector replay, and duplicate command audit behavior.

The pilot does not convert human-readable command fingerprints/error descriptions unless required for correctness.

## 9. Approved ActorEventSourceDb MessagePack-CSharp binary payload design

This section is independent of the deferred Nerdbank migration. It is the only currently approved implementation direction in this document.

### 9.1 Scope and invariants

Change only the serialized representation of `event_log.EventData`:

- serializer: the currently deployed MessagePack-CSharp library;
- database representation: PostgreSQL `bytea`;
- actor/NATS/durable-queue serialization: unchanged;
- event contract types and MessagePack-CSharp attributes: unchanged;
- event type identity: retain the existing `event_name_id.EventTypeName` and runtime `Type` resolution for this change;
- event ordering and version allocation: unchanged; and
- replay reducers, snapshot selection, last-N selection, command deduplication, and projector semantics: unchanged.

The event-log implementation must not add Nerdbank packages, generated witnesses, the deferred event registry, K4os, Zstandard, or a new compression envelope.

### 9.2 PostgreSQL schema

Replace the current column definition:

```sql
EventData text NOT NULL
```

with:

```sql
EventData bytea NOT NULL
```

Keep the column name `EventData` to minimize query and mapping churn. The complete table requirement is:

```sql
CREATE TABLE IF NOT EXISTS public.event_log (
    EventStreamId bigint NOT NULL,
    EventNameId integer NOT NULL,
    EventVersion bigint
        DEFAULT nextval('public.event_log_eventversion_seq'::regclass)
        NOT NULL,
    EventData bytea NOT NULL,
    CommandId uuid NOT NULL,
    EventTimestamp text NOT NULL,
    CONSTRAINT event_log_pkey
        PRIMARY KEY (EventStreamId, EventNameId, EventVersion)
);

CREATE INDEX IF NOT EXISTS ix_event_log_command_id
    ON public.event_log (CommandId);

CREATE UNIQUE INDEX IF NOT EXISTS ux_event_log_event_version
    ON public.event_log (EventVersion);
```

The existing `event_stream_id`, `event_name_id`, `event_projector_state`, indexes, sequences, and foreign-key behavior remain unchanged. All existing SELECT/CTE queries may continue to select/union `EventData`; only .NET parameter and row-mapping types change from text to binary.

Because IFM has no released 1.0 event history to preserve, the preferred cutover is to stop the application and recreate the event-source schema. Do not ship a permanent JSON/binary dual reader or retain a shadow JSON column. If durable production event history exists when implementation begins, stop and create a separate data-migration specification rather than assuming it may be discarded.

### 9.3 Serializer profile

Add a dedicated event-log payload abstraction, for example `IEventLogPayloadSerializer`, implemented with MessagePack-CSharp. It must use one shared immutable options instance consistent with the active serializer profile:

- `ContractlessStandardResolver.Instance`; and
- `MessagePackCompression.Lz4BlockArray`.

The runtime event type is already known from the concrete `IEvent` on write and from `EventTypeName` on read. Use MessagePack-CSharp's runtime-`Type` overloads rather than serializing through the `IEvent` interface:

```text
Serialize(domainEvent.GetType(), domainEvent, options) -> byte[]
Deserialize(domainEventType, eventDataBytes, options) -> IEvent
```

Do not use MessagePack-CSharp typeless payloads. The existing event type metadata remains outside the payload in `event_name_id`, so embedding CLR type information inside every `EventData` value is unnecessary.

The built-in MessagePack-CSharp LZ4 representation is stored directly in `bytea`. Do not wrap it in the deferred IFM/Nerdbank codec header.

### 9.4 Write path

`SaveEventsAsync` will eventually:

1. obtain the existing event-name ID from the concrete event type as it does today;
2. serialize the concrete event with the dedicated MessagePack-CSharp event-log serializer;
3. bind the resulting `byte[]` as PostgreSQL `bytea`, never text/base64;
4. insert it in the existing transaction with stream, command, and timestamp values; and
5. assign the returned event version to `IEvent.EventId` exactly as today.

Serialization is CPU-only and remains synchronous inside the asynchronous database operation. The database call remains asynchronous. Do not add `Task.Run`, a serialization lock, `MemoryStream`, JSON conversion, or a fixed maximum-sized intermediate array.

### 9.5 Read and replay path

Change `EventLogReadModel.EventData` and `EventStreamReadModel.EventData` from `string` to binary memory (`byte[]` initially, or `ReadOnlyMemory<byte>` if supported cleanly by the storage abstraction). Every event-log mapper must read the field as binary rather than call `GetString`.

Replay will:

1. resolve `EventTypeName` using the existing runtime type mechanism;
2. deserialize `EventData` with the runtime-`Type` MessagePack-CSharp overload and the same options used on write;
3. restore `EventId` from `EventVersion` exactly as today; and
4. pass events to existing reducers in the order returned by the current replay query.

Full replay, snapshot replay, last-N replay, snapshot-plus-last-N-range replay, and projector recovery must all consume the same binary read model. SQL range-selection logic does not deserialize payloads and therefore must remain otherwise unchanged.

Missing snapshots or missing requested range-event types retain their existing best-effort/empty-state behavior. An unresolved event type or MessagePack conversion failure retains the current `UnknownEvent` fallback. Database/query failures continue through the existing actor processing exception path.

### 9.6 Unknown-event behavior

Preserve the current best-effort `UnknownEvent` fallback when the stored CLR type cannot be resolved or the payload cannot be converted to that type. Because `UnknownEvent.EventData` is currently a string, encode the raw binary payload as Base64 only on this cold diagnostic path and identify it as binary MessagePack data. Do not perform Base64 conversion during normal save or replay.

A later event-contract registry can replace assembly-qualified resolution independently. It is not required for this MessagePack-CSharp event-log change and must not be coupled to it.

### 9.7 Storage mappings and affected surfaces

The implementation plan must include all of these surfaces together:

- `EventSourceSchemaSql.CreateEventLogTable`;
- `EventLogReadModel` and `EventStreamReadModel`;
- both current `ToEventData` JSON extension methods, replacing event-log use with the dedicated binary abstraction;
- `EventSourceActorDbContext` write binding and every binary row mapper;
- insert/update parameter binders and storage-provider value types (`Bytea`, not `Text`);
- event-source benchmarks and fixtures that currently construct JSON `EventData` strings;
- storage, projector, snapshot, range, and Fund integration tests; and
- operational schema recreation/startup validation.

Command-log `CommandData`, error/audit text, human-readable logs, REST JSON, and status-console JSON remain text and are outside this change.

### 9.8 Validation and BenchmarkDotNet requirements

Correctness tests must cover:

- every representative command/event/snapshot contract family;
- public/private/constructor-backed MessagePack-CSharp members already used by current contracts;
- empty, small, medium, and large event payloads;
- save followed by typed read and `EventId` restoration;
- full, snapshot, last-N, and snapshot-plus-last-N-range replay;
- projector recovery and command deduplication;
- unknown type and corrupt binary fallback/failure behavior;
- transaction rollback and cancellation; and
- schema recreation with `EventData bytea NOT NULL`.

BenchmarkDotNet must compare the existing indented Newtonsoft.Json/text representation with MessagePack-CSharp/LZ4 binary for small, medium, and large real event types. Report:

- serialized bytes;
- serialization and deserialization mean/p95 distribution where supported;
- operations per second;
- allocated bytes per operation;
- Gen0/Gen1/Gen2 collections; and
- end-to-end save/replay throughput in a separate integration measurement.

Final validation for this system-wide storage change must include the Fund integration suite. No production implementation is authorized by this documentation-only update; implementation requires a separate explicit request.

### 9.9 Event-log implementation acceptance checklist

- [x] MessagePack-CSharp remains the selected serializer.
- [x] The PostgreSQL payload column is specified as `EventData bytea NOT NULL`.
- [x] Actor/NATS/durable-queue serialization is outside the change.
- [x] Existing event type metadata/runtime resolution is retained.
- [x] JSON/text and binary/`bytea` benchmark coverage is required.
- [x] Full, snapshot, last-N, snapshot-plus-last-N, projector, and unknown-event behavior is specified.
- [x] Fund integration validation is required.
- [ ] Production implementation is explicitly authorized in a future request.

## 10. Deferred durable JetStream replay queue migration

`NatsJSDurableReplayQueue` currently stores a MessagePack-CSharp envelope, an assembly-qualified event type, a runtime-typed nested payload, and a legacy JSON fallback.

At full cutover:

- serialize the durable envelope with Nerdbank;
- store the stable event contract name, not an assembly-qualified type name;
- serialize the nested event with the generated registry entry;
- use the same binary header/compression codec abstraction;
- remove `LooksLikeJson`, the legacy JSON envelope, and JSON payload branching;
- retain projector name, enqueue/failure timestamps, and error metadata; and
- validate all queued messages are drained/recreated before deployment.

The durable queue may reuse the event-log registry. It must not introduce a second independently maintained type map.

## 11. Deferred Nerdbank full-solution migration sequence

### Phase 0 — approval and baselines

- Approve this specification.
- Freeze the exact pilot contract list.
- Capture MessagePack-CSharp baseline schema, size, latency, allocation, and throughput measurements.
- Add schema snapshots for the pilot contracts.

### Phase 1 — TickAggregation pilot

- Add Nerdbank and K4os only to the minimum pilot/framework projects.
- Add the non-domain partial witness catalog.
- Implement generated-shape serializer, payload header, codec abstraction, and count-aware quote converter.
- Integrate the explicit TickAggregation NATS paths.
- Integrate disposable event-log `bytea` persistence for pilot tests.
- Run unit, integration, concurrency, soak, corruption, and benchmarks.
- Produce a pilot decision report.

### Phase 2 — contract audit and registry generation

- Enumerate every top-level serialized command/event/query/result/read model.
- Enumerate every interface/base-typed member and union.
- Classify the 234 writable ignored members.
- Classify constructor ambiguity and genuine private-state requirements.
- Create per-assembly shape catalogs and the central stable event registry.
- Add a build/test gate proving registry completeness.

### Phase 3 — framework cutover preparation

- Replace `MessagePackBinarySerializer` behind a Nerdbank-specific implementation.
- Replace NATS Core, JetStream, owned-message, actor-extension, request/reply, and durable-replay serializers.
- Replace event-log JSON with the binary `bytea` path.
- Remove reflection fallback and runtime assembly-qualified type resolution.
- Update telemetry for serializer/codec/size/failure metrics.

### Phase 4 — domain attribute removal

- Migrate one contract assembly at a time in source.
- Remove MessagePack-CSharp attributes/usings/packages.
- Resolve writable ignored members and constructor ambiguity.
- Validate generated schema and round trip for every root.
- Keep production deployment blocked until every participating assembly is complete.

### Phase 5 — coordinated all-or-nothing cutover

- Stop message intake gracefully.
- Drain NATS/JetStream work that uses the old serializer.
- Recreate or explicitly clear non-production event/durable data as approved.
- Deploy all producers/consumers/projectors together.
- Recreate `ActorEventSourceDb` with the binary schema.
- Start consumers, then producers.
- Run health, replay, projection, and Fund integration gates.
- Roll back the deployment as a complete unit if validation fails; do not mix node versions.

### Phase 6 — cleanup

- Remove MessagePack-CSharp, annotations, analyzer, resolvers, formatters, and suppressions.
- Remove pilot routing/feature switches and legacy test fixtures.
- Verify no compiled project directly references MessagePack-CSharp.
- Update implementation-details and system optimization documents.

### Phase 7 — .NET 11 Zstandard qualification

- Add the Zstandard codec behind codec ID 2.
- Benchmark real IFM payload distributions.
- Run mixed concurrency/soak tests.
- Select compression level and optional dictionary.
- Coordinate a clean codec cutover and remove K4os when approved.

## 12. Deferred Nerdbank test specification

### 12.1 Contract/schema tests

For every pilot root:

- generated shape exists;
- no reflection provider is used;
- default, minimum, maximum, and representative values round trip;
- decimals, raw 64-bit prices, `DateOnly`, UTC `DateTime`, enums, nullable prices, GUIDs, and nested structs retain exact values;
- generated JSON schema is captured and reviewed;
- computed non-wire members do not appear;
- map property names are stable and case exact; and
- invalid/missing required fields produce the documented behavior.

### 12.2 Quote-buffer tests

- physical buffer length 64 with logical counts 1, middle, and 64;
- only logical elements appear in the payload;
- stale values after `Count` never appear;
- count 0 and count greater than 64 are rejected;
- lease returned once after successful publish;
- lease returned once after serialization failure;
- lease returned once after publish failure/cancellation;
- no use-after-return under concurrent publication; and
- decoded segment contains exactly the serialized logical quotes.

### 12.3 Compression tests

- raw, LZ4, and future Zstandard codec IDs;
- boundary sizes immediately below/at/above threshold;
- incompressible and highly repetitive payloads;
- truncated/corrupt frame;
- declared length mismatch;
- decompression bomb/max-length enforcement;
- pooled state returned after every exception;
- parallel operations never share mutable sessions; and
- steady-state allocation budget is met after warmup.

### 12.4 Event-log tests

- binary insert/read/round trip;
- event contract registry resolution;
- transaction rollback behavior unchanged;
- full stream replay;
- snapshot replay;
- snapshot plus last-N typed range replay in ascending order;
- missing snapshot/event types preserve empty/best-effort semantics;
- actual malformed payload fails through the existing pipeline;
- projector durable queue replay; and
- command retry/deduplication behavior unchanged.

### 12.5 End-to-end gates

- TickAggregation unit suite;
- MarketData.Feed integration suite for the pilot;
- DataBento synthetic/live-safe pipeline test as approved;
- NATS Core and JetStream serialization integration tests;
- `ActorEventSourceDb` integration tests;
- event-projector replay integration tests; and
- Fund integration suite for any full framework/event-source cutover.

## 13. Deferred Nerdbank BenchmarkDotNet specification

### 13.1 Payloads

Benchmark exact production contracts, not synthetic records only:

- one trade changed event;
- one trade insert command;
- one trade inserted event;
- quote changed/command/inserted payloads with 1, 8, 32, and 64 quotes;
- completion and failure events;
- representative small, medium, and large non-tick actor messages for the later full cutover; and
- representative event-log events and snapshots.

### 13.2 Compared paths

1. MessagePack-CSharp + built-in LZ4 (`IBufferWriter` baseline).
2. Nerdbank attribute-free map, uncompressed.
3. Nerdbank attribute-free map + K4os LZ4 Frame.
4. Nerdbank attribute-free map + K4os pooled codec.
5. Future Nerdbank + .NET 11 Zstandard.

Measure:

- serialize mean/P50/P95 where available;
- deserialize mean/P50/P95;
- full round trip;
- operations/second;
- output bytes;
- allocated bytes/op;
- Gen0/Gen1/Gen2;
- direct `IBufferWriter` path;
- exact-array/event-store path; and
- 1, 4, 16, and expected-production concurrent workers in a separate throughput harness.

### 13.3 Exploratory benchmark evidence already collected

These figures informed the design but are not pilot acceptance results because the Nerdbank model used indexed keys and the current K4os adapter still allocated fixed session state.

| Payload | MessagePack-CSharp LZ4 bytes | Nerdbank + K4os bytes | MPC serialize | NB + K4os serialize | MPC deserialize | NB + K4os deserialize |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 256 quotes | 3,916 | 3,379 | 53.42 us | 39.91 us | 58.60 us | 77.80 us |
| 2,048 quotes | 20,901 | 17,950 | 381.90 us | 271.68 us | 565.78 us | 641.76 us |

Round-trip evidence:

| Payload | MPC round trip | NB + K4os round trip |
| --- | ---: | ---: |
| 256 quotes | 112.02 us | 117.71 us |
| 2,048 quotes | 947.68 us | 913.44 us |

Direct reusable-writer serialization:

| Payload | MPC | MPC allocation | NB + K4os | NB + K4os allocation |
| --- | ---: | ---: | ---: | ---: |
| 256 quotes | 49.48 us | 0 B | 38.02 us | 8,728 B |
| 2,048 quotes | 364.61 us | 0 B | 261.87 us | 8,728 B |

Interpretation:

- Nerdbank plus external LZ4 produced smaller large-buffer output and faster serialization.
- Deserialization was slower in these runs.
- End-to-end results were near parity for 256 quotes and modestly favorable for 2,048 quotes.
- The fixed K4os allocation is the clearest remaining defect.
- The real pilot uses a maximum of 64 quotes, so exact production-contract results may differ materially.
- Attribute-free maps must be measured because the exploratory keyed-array model does not represent the chosen schema policy.

### 13.4 Pilot acceptance gates

The pilot is accepted only if all correctness tests pass and:

- direct-writer steady-state serialization allocation is at or below 256 B/op excluding final output;
- there is no per-message allocation proportional to the configured maximum payload size;
- quote payload size is no worse than 15% above the current compressed path, unless throughput/GC evidence justifies it;
- serialization throughput is not worse than 10%;
- deserialization throughput is not worse than 20%, unless end-to-end throughput is neutral or better and replay remains within its budget;
- full TickAggregation end-to-end throughput is neutral or better;
- no regression appears in lease ownership, ordering, duplicate, or out-of-order behavior; and
- event-log replay latency/allocation is materially better than indented JSON.

Thresholds may be changed only in the benchmark decision record with the reason and measured evidence.

## 14. Deferred Nerdbank observability

Add low-cardinality metrics for:

- serialization/deserialization duration by serializer profile and broad message category;
- input, raw MessagePack, and final payload bytes;
- compression ratio and codec;
- compression skipped due to threshold or expansion;
- pool rent, miss, drop, and outstanding counts;
- malformed header/decompression/deserialization failures;
- event-registry misses;
- TickAggregation quote count and emission reason; and
- event-log encode/decode/replay duration.

Do not label metrics with contract ID, instrument ID, command ID, event ID, stream ID, subject, or full CLR type when that creates unbounded cardinality. Use a bounded contract-family identifier.

## 15. Deferred Nerdbank security and resilience

- Use generated allowlisted shapes only.
- Keep Nerdbank secure defaults unless a benchmarked change is explicitly approved.
- Bound maximum payload, collection count, nesting depth, string length, binary length, and uncompressed length.
- Validate the binary header before renting based on its declared size.
- Never instantiate an arbitrary type name supplied by a message or database row.
- Do not use typeless/object deserialization for actor messages.
- Reject duplicate/unknown union discriminators at startup.
- Return pooled buffers even when cancellation or exceptions occur.
- Do not log raw binary payloads or full market-data buffers.

## 16. Deferred Nerdbank risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Property maps are larger than indexed arrays | Compress measured large payloads; retain raw small payloads; use a central hot-type converter only if required. |
| Writable ignored properties silently enter payloads | Refactor the audited 117 `ErrorCode` and 117 `QueryParams` properties to computed/explicit-interface getters; schema snapshots and round-trip tests. |
| Interface-typed entity IDs lose concrete data | Refactor the audited 118 wire members to concrete/computed IDs; bounded central explicit unions only where unavoidable; round-trip every concrete case. |
| Constructor/member binding differs | Apply the defined `LookupTypeCollection`, `TradePositionReadModel`, and `ActorEntityId` resolutions; keep the syntax-aware ambiguity gate at zero unexplained cases. |
| Runtime event type has no generated shape | Immutable registry and fail-fast startup completeness test. |
| K4os state allocates per message | Use frame writer/span APIs and bounded session pooling; reject K4os if allocation gate cannot be met. |
| Compression state is used concurrently | Exclusive rent-per-operation ownership; no shared mutable codec instance. |
| Event-store binary data is hard to inspect | Provide diagnostic `ConvertToJson` tooling outside the hot path and retain stable contract names/metadata. |
| Coordinated deployment is incomplete | Stop/drain/deploy/start runbook; block mixed application versions; full rollback unit. |
| .NET 11 preview behavior changes | Keep codec abstraction; qualify the supported .NET 11 release before production. |

## 17. Deferred partial-type compatibility conclusion

Not liking partial domain types does not block this migration.

The current serialized model categories are compatible with the witness approach:

- records and sealed records: supported without making them partial;
- record structs and readonly record structs: supported through generated witness shapes;
- inherited event records: supported, with explicit union handling only when serialized through a base declaration;
- public init/set properties: supported;
- immutable getter-only properties: supported when a matching constructor is unambiguous;
- external/referenced graph types: shapes are generated from the top-level root or a witness;
- top-level arrays/closed generic roots: require explicit witness entries; and
- runtime-selected event types: require the generated registry described above.

The types that need actual migration work are not problematic because they are non-partial. The audit identified these bounded semantic dependencies:

- writable properties currently excluded with `[IgnoreMember]`;
- two private-set properties and one readonly field that require explicit construction/conversion behavior;
- obsolete serializer constructors/`AllowPrivate` configuration that must be removed only after schema validation;
- interface/base-typed values without a complete union;
- runtime `Type` serialization; or
- the custom pooled quote-segment formatter.

Therefore, the binding choice is: keep domain types non-partial, place `partial` only on infrastructure witness catalogs, and explicitly resolve the smaller set of semantic serialization cases.

## 18. Deferred Nerdbank migration-risk analysis

### 18.1 Overall verdict

The current product decision supersedes the earlier feasibility recommendation: Nerdbank is a no-go until a future .NET 11 reassessment. IFM treats these three findings as true migration blockers:

1. count-aware quote-buffer serialization;
2. lossless `ActorEntityId` construction; and
3. generated runtime event-type resolution.

The writable query metadata and broader interface-polymorphism work remain substantial migration gates, even though their remediation is mechanical. No blocker-removal work is authorized now. The designs and acceptance tests below are retained only to prevent the investigation from being repeated later.

| Risk | Audit result | Required disposition | Gate scope |
| --- | --- | --- | --- |
| 234 writable ignored members | Exactly 117 `ErrorCode` plus 117 `QueryParams` properties | Convert cached metadata to computed/explicit-interface getters and prove it is absent from the wire | Deferred migration gate |
| Incomplete interface polymorphism | 118 serialized `IActorEntityId` members; current union covers only one of 151 implementations | Replace wire-facing interface properties with concrete IDs; use bounded central unions only for genuine polymorphism | Deferred migration gate |
| Runtime event-type resolution | Two active persistence/queue paths depend on assembly-qualified names/runtime `Type` | Immutable generated-shape registry with stable names and closed delegates | True Nerdbank blocker |
| Constructor/private-member behavior | Default `ActorEntityId` probe silently changed `"ESM6"` to `"none"` | Lossless `ActorEntityId` converter/surrogate; defined handling for two other special shapes | True Nerdbank blocker for graphs using it |
| Count-aware quote buffer | Default Nerdbank shape serializes all 64 slots | Mandatory generated-child custom converter; exact owned receive array; publisher retains lease ownership | True TickAggregation Nerdbank blocker |

### 18.2 Writable ignored members

#### Evidence

The 234 members are not 234 unrelated design decisions. The audit found exactly two property names, each repeated 117 times across 112 query files:

- `ErrorCode`; and
- `QueryParams`.

Every observed constructor assignment to `ErrorCode` uses the query's static `ErrorId`. `QueryParams` is explicitly assigned in only 11 source files. Those assignments either produce an empty string or derive a diagnostic string from fields that are already the canonical query input. The other query contracts leave it at its default.

#### Failure mode

MessagePack-CSharp currently excludes these writable properties. Nerdbank's default public-property model would include them after a blind attribute removal. That would duplicate state and unnecessarily enlarge every query schema. Conversely, simply suppressing them without changing their object semantics could deserialize `ErrorCode` as zero or leave stale/default `QueryParams`, changing actor error routing or diagnostics.

#### Required solution

- `ErrorCode` becomes `public int ErrorCode => ErrorId;` or the equivalent explicit interface getter.
- `QueryParams` becomes a deterministic computed getter over canonical query fields.
- Parameterless queries return the currently intended empty/null diagnostic value without storing it.
- Constructors stop assigning either property.
- Neither member receives a wire key/property in the generated schema.

This preserves the distinction between canonical serialized query input and derived actor metadata. It also removes 234 old ignore annotations without replacing them with 234 Nerdbank ignore annotations.

#### Acceptance tests

For every affected query contract:

1. construct a representative query;
2. round trip it with its generated witness shape;
3. assert the canonical input fields are unchanged;
4. assert `ErrorCode == ErrorId` after round trip;
5. assert `QueryParams` equals its pre-serialization derived value; and
6. assert the generated schema/map contains neither `ErrorCode` nor `QueryParams`.

The migration tool/analyzer must fail if a writable `[IgnoreMember]` remains unexplained or if a replacement writable member is absent from the approved wire-schema snapshot.

#### Blocker status

Design closed; full-cutover implementation blocker until the query refactor and generated contract tests pass. It does not block the TickAggregation pilot because those messages do not use this query metadata pattern.

### 18.3 Interface polymorphism

#### Evidence

The audit found 118 keyed `IActorEntityId` members across 113 files and 151 direct implementations of `IActorEntityId`. The current MessagePack-CSharp union on `IActorEntityId` names only `ActorEntityId`; it is not a complete model of those possible runtime values. A repository probe of `GetFuturesRsiSignalQuery` confirmed that a default Nerdbank witness does not safely reconstruct the interface-typed entity ID.

Most affected queries already carry the primitive fields needed to construct a specific query-parameter ID. In 107 of the 113 audited files, the constructor directly creates a concrete entity-ID/parameter instance. This means the interface member is normally duplicated derived state, not genuine open polymorphism.

#### Failure mode

Serializing a property declared as an interface without a complete derived mapping can:

- fail during shape generation or deserialization;
- serialize only the interface-visible surface;
- lose the concrete entity-ID fields; or
- require an unsafe/unbounded runtime-type mechanism.

A global union containing all 151 implementations would be difficult to own, would admit types irrelevant to a given query, and would make stable discriminator management unnecessarily broad.

#### Required solution

For queries and other contracts where the concrete ID is known:

1. expose a concrete entity-ID/query-parameter property to the serializer;
2. compute it from canonical query fields where possible;
3. implement `IQuery.EntityId` explicitly to return that concrete value if infrastructure requires the interface; and
4. do not serialize a second interface-typed copy.

For a truly polymorphic member that survives this refactor:

- define a bounded allowlist scoped to that base/interface and transport family;
- assign explicit stable integer tags;
- configure `DerivedShapeMapping<T>` centrally;
- reject duplicate tags and unregistered runtime types at startup; and
- round trip every registered derived type through its declared base.

Do not enable duck typing, the optional `object` converter, typeless deserialization, or a reflection fallback.

#### Acceptance tests

- A repository audit finds no wire-facing `IActorEntityId` property unless it is listed in an approved bounded union manifest.
- Each refactored query round trips and produces the same concrete `EntityId.Format()` value.
- Each genuine union has unique stable tags and a round-trip test for every member.
- Unknown runtime implementations fail before message intake, not after partial production processing.

#### Blocker status

Design closed; full-cutover implementation blocker. The TickAggregation pilot is not blocked because its contracts expose concrete `TickDataEntityId` values.

### 18.4 Runtime event-type resolution

#### Evidence

The runtime-type dependency is concentrated in two active mechanisms:

- `EventLogReadModel` resolves `EventTypeName` with `Type.GetType` for JSON replay; and
- `NatsJSDurableReplayQueue` writes `AssemblyQualifiedName`, serializes/deserializes through a runtime `Type`, and includes legacy JSON detection/fallback.

The event-source database event-name mapping is also fed by CLR assembly-qualified identity. Assembly version, assembly rename, namespace rename, and deployment loading behavior can therefore affect replay independently of the actual payload schema.

#### Failure mode

Nerdbank is intentionally compile-time-shape oriented. Replacing MessagePack-CSharp while retaining `Type.GetType` would either force reflection into production, encourage an unsafe arbitrary-type converter, or leave a replay path that cannot obtain its generated shape. Assembly-qualified names are also poor durable contract IDs.

#### Required solution

Create one `EventContractRegistry` abstraction used by both durable paths. Conceptually, each immutable descriptor contains:

- a stable `ContractName`;
- the exact CLR `EventType` for in-process write lookup;
- a closed generated-shape serializer delegate;
- a closed generated-shape deserializer delegate returning `IEvent`;
- the bounded contract-family metric identifier; and
- optional historical name aliases added only by an explicit future rename decision.

Build two immutable/frozen dictionaries:

- `Type -> EventContractDescriptor` for writes; and
- `ContractName/alias -> EventContractDescriptor` for reads.

The delegate is closed over a compile-time generic event type and its generated shape, so the hot path does not use `MakeGenericType`, `MethodInfo.Invoke`, `dynamic`, or `Type.GetType`.

The stable v1 name is the full namespace plus type name without assembly identity, unless a contract declares a more durable explicit constant before implementation. `event_name_id` and the JetStream durable envelope store this stable name. Event payloads are binary `bytea`/MessagePack as described earlier. The old JSON sniff/fallback is removed at the coordinated cutover.

Catalog generation rules:

- one checked-in witness/descriptor catalog per contract assembly or bounded family;
- generated/updated before normal compilation, not by relying on one source generator to observe another generator's output in the same compilation;
- a test-only reflection scan compares concrete events in the intended assemblies with the checked-in manifest;
- production never scans assemblies to choose a type and never falls back to reflection; and
- startup validates missing descriptors, duplicate CLR types, duplicate canonical names, duplicate aliases, and shape creation.

The all-or-nothing deployment means no legacy type names need to be read. Development event tables and durable streams are drained/recreated as already specified.

#### Acceptance tests

1. Every concrete event eligible for event-log or durable-queue use appears exactly once in the registry completeness test.
2. Every descriptor round trips a representative event through its closed delegate.
3. Registry write lookup and stable-name read lookup return the same descriptor.
4. Missing/duplicate names fail startup before subscriptions begin.
5. Static analysis finds no active `Type.GetType`, `AssemblyQualifiedName`, runtime-Type MessagePack call, or JSON payload sniff in the two migrated paths.
6. Event save/replay, durable enqueue/replay, snapshot replay, and last-N replay pass using the registry.

#### Blocker status

Design closed; implementation blocker for the pilot event-log path and the eventual production cutover. The registry must be implemented before event-log MessagePack is considered complete.

### 18.5 Constructors and private members

#### Evidence

The syntax-aware audit of 1,003 active MessagePack contract types found no broad constructor-ambiguity problem:

| Category | Count |
| --- | ---: |
| Public parameterless construction | 975 |
| Exactly one public constructor | 14 |
| Primary-record construction | 14 |
| Multiple public constructors with no parameterless constructor | 0 |
| No public constructor | 0 |
| Non-public keyed members | 0 |
| Public keyed properties without public `set`/`init` | 2 |
| Readonly keyed fields | 1 |

All 791 active `[SerializationConstructor]` uses are public. Of those, 786 occur on types that also have a public parameterless constructor. The 851 `AllowPrivate = true` flags do not correspond to non-public keyed payload members and can generally be removed as obsolete boilerplate.

The three special shapes are:

- `LookupTypeCollection.Items`: initialized `List<LookupTypeReadModel>` with a private setter;
- `TradePositionReadModel.OptionLegData`: private-set `OptionTradeLegDataReadModel[]`; and
- `ActorEntityId.Value`: readonly string field with multiple constructors and no matching `value` constructor parameter.

The executable probe serialized `ActorEntityId("ESM6")` but deserialized it as its normalized default value `"none"`. That is silent data loss and confirms that it cannot use the default witness shape unchanged.

#### Required solution

- `LookupTypeCollection.Items`: remove the private setter and retain an initialized getter-only list. Nerdbank documents support for populating initialized getter-only collections through `Add`; verify this behavior with empty and populated cases.
- `TradePositionReadModel.OptionLegData`: select the existing full constructor with `[ConstructorShape]`, or introduce a dedicated transport surrogate. The recommended minimal change is the one deliberate constructor annotation plus a full read-model round trip, because making the setter public weakens the domain model and receiver-side pooling cannot populate an immutable array.
- `ActorEntityId`: register a central converter/surrogate that writes the canonical string and constructs through the existing normalization rules. Test default, null/empty, simple string, escaped/special text, and `string[]` construction. Do not depend on constructor-parameter-name inference.

All other types follow the normal policy: public parameterless plus public set/init, a single public constructor, or a primary record constructor. Remove old `[SerializationConstructor]` and `AllowPrivate` only after the type's generated schema and round trip pass.

#### Acceptance tests

- The syntax-aware ambiguity audit remains at zero unexplained types.
- `ActorEntityId("ESM6")` round trips as `"ESM6"`, not `"none"`.
- `LookupTypeCollection` round trips empty and populated lists without replacing an inaccessible property.
- `TradePositionReadModel` round trips empty and populated option-leg arrays via the selected construction path.
- A schema snapshot proves no newly exposed private/internal state.

#### Blocker status

Design closed. `ActorEntityId` and `TradePositionReadModel` are implementation blockers for any enabled graph that reaches them. `LookupTypeCollection` is a test/refactor gate. Non-partial domain types are not a blocker.

### 18.6 Count-aware quote-buffer formatter

#### Evidence and failure mode

The quote segment owns a logical `Count` over a physical pooled array. The default generated shape sees `Buffer` and `Count` as ordinary public members and cannot infer that the tail is inactive. The probe encoded all 64 physical elements, including a deliberately stale third element, producing 11,050 bytes for a logical count of two. The count-aware converter encoded exactly two quotes in 441 bytes and reconstructed `Buffer.Length == Count == 2`.

Serializing the tail can leak old pooled quote data into another event, distort stored market data, increase compression work, and violate `QuoteCount`. This is therefore a correctness/security blocker, not merely a micro-optimization.

#### Required solution

Use a centrally registered immutable converter type with a `ConverterContext` constructor that caches the generated `FuturesTickQuoteData` child converter. The converter class may be an infrastructure `partial` witness provider; the domain segment remains non-partial and attribute-free.

Write behavior:

1. call `DepthStep()`;
2. validate non-null buffer, `1..64` count, and `Count <= Buffer.Length`;
3. write one array header equal to logical `Count`; and
4. write only `Items[0..Count)` through the cached generated child converter.

Read behavior:

1. call `DepthStep()`;
2. read and validate one array header before allocation;
3. reject zero, values above 64, overflow, truncation, and trailing malformed content;
4. allocate an exact receiver-owned array;
5. read exactly that many quotes through the cached child converter; and
6. construct a segment with matching `Buffer.Length` and `Count`.

Ownership behavior:

- the converter borrows the publisher's span and never retains or returns it;
- the publisher owns the `ITickQuoteBufferLease` until `SendAsync` completes and returns it exactly once in `finally`;
- the receiver array is not rented because actor messages currently have no disposal/lifetime contract; and
- outer TickAggregation messages reject `QuoteCount != QuoteData.Count`.

#### Acceptance tests

- logical counts 1, 2, 8, 32, and 64 round trip exactly;
- stale data at `Buffer[Count]` never appears in bytes or the decoded event;
- counts 0, 65, and greater than physical length fail deterministically before out-of-range access;
- truncated and excessive array headers fail within configured bounds;
- publisher lease is returned exactly once on success, serialization failure, send failure, and cancellation;
- concurrent sends do not share mutable converter/buffer state; and
- BenchmarkDotNet compares current formatter, default Nerdbank shape, and count-aware Nerdbank converter for latency, throughput, bytes, and allocation.

#### Blocker status

Design and API feasibility proven by the executable probe; mandatory TickAggregation pilot implementation blocker until the above tests pass.

## 19. Deferred Nerdbank blocker-removal implementation plan

This plan is ordered to resolve correctness before performance tuning. Each phase produces a reviewable commit and cannot be marked complete solely from compilation.

### Phase R0 — Freeze the audit and acceptance manifests

1. Move the syntax-aware audit into a maintained test/tool location.
2. Check in machine-readable manifests for writable ignored members, interface/base wire members, constructor exceptions, and event registry roots.
3. Add CI checks that report additions/removals as intentional schema changes.
4. Freeze representative round-trip fixtures for the affected contracts.

Exit gate: the repository inventory is reproducible and no category depends on regex/comment counts.

### Phase R1 — Implement the TickAggregation quote converter

1. Add the infrastructure witness catalog for the pilot roots and quote child type.
2. Add the immutable count-aware converter and register its type in the pilot serializer profile.
3. Add count/bounds/stale-tail/ownership/concurrency tests.
4. Run the focused executable probe as a permanent unit/integration test.
5. Run BenchmarkDotNet for active counts 1, 8, 32, and 64.

Exit gate: no inactive slot is serialized, all leases are returned exactly once, and pilot performance gates pass.

### Phase R2 — Resolve special construction shapes

1. Add and test the central `ActorEntityId` converter/surrogate.
2. Refactor/test `LookupTypeCollection.Items` as a getter-only initialized collection.
3. Select and test `TradePositionReadModel` construction using the existing full constructor with `[ConstructorShape]` or an approved surrogate.
4. Run the ambiguity audit and schema snapshots.

Exit gate: all three special shapes preserve data and there are zero unexplained constructor/private-member cases.

### Phase R3 — Refactor query metadata and entity IDs

1. Convert all 117 `ErrorCode` properties to derived getters.
2. Convert all 117 `QueryParams` properties to deterministic derived/explicit-interface getters.
3. Replace the 118 wire-facing `IActorEntityId` properties with concrete/computed entity IDs where the concrete type is known.
4. Create bounded stable-tag unions only for the small genuine-polymorphism remainder.
5. Generate schema and round-trip tests for every changed query family.

Exit gate: no unapproved writable ignored metadata or wire-facing open `IActorEntityId` remains, and every query retains the same canonical behavior.

### Phase R4 — Implement stable runtime event resolution

1. Define the stable contract-name convention and descriptor API.
2. Add checked-in per-assembly witness/descriptor catalogs.
3. Build frozen bidirectional registry maps and fail-fast validation.
4. Replace `ActorEventSourceDb`/`EventLogReadModel` assembly-qualified resolution and JSON payloads.
5. Replace `NatsJSDurableReplayQueue` runtime-Type calls and remove legacy JSON sniffing.
6. Add completeness, duplicate, unknown, round-trip, event-log replay, durable replay, snapshot, and last-N tests.

Exit gate: migrated paths contain no runtime type resolution/fallback and every eligible event has exactly one validated descriptor.

### Phase R5 — Pilot and system validation

1. Run TickAggregation unit/integration suites and its BenchmarkDotNet matrix.
2. Run event-log replay and durable-queue fault/restart tests.
3. Run the Fund integration suite as the system-wide actor regression gate.
4. Publish a decision record with payload bytes, latency, throughput, allocation, and GC results.
5. Resolve K4os's fixed allocation or approve uncompressed pilot/deferred compression as specified in section 6.4.

Exit gate: all correctness gates pass, measured performance meets section 13.4, and no open severity-one/two serialization defect remains.

### Phase R6 — Full-cutover authorization

1. Review the completed evidence against this specification.
2. Approve the coordinated stop/drain/recreate/deploy/start runbook.
3. Generate the full contract migration only after explicit authorization.
4. Remove MessagePack-CSharp references and attributes after each assembly's witness/schema tests pass.

Exit gate: explicit cutover approval. Until then, the existing serializer remains the production default outside the bounded pilot.

## 20. Future Nerdbank reconsideration checklist

This checklist is intentionally unapproved. It may be revisited only after the supported .NET 11/Zstandard environment is available and a new explicit migration decision is made.

Before code generation begins, approve or amend these decisions:

- [ ] Nerdbank.MessagePack is the target serializer.
- [ ] Event-log domain payloads become binary Nerdbank MessagePack in `bytea`.
- [ ] Production cutover has no legacy/dual reader.
- [ ] Domain message types remain non-partial via witness catalogs.
- [ ] Attribute-free property-name maps are the default schema.
- [ ] Exceptional ignores/converters/unions may be configured centrally or minimally annotated when refactoring cannot express the contract safely.
- [ ] TickAggregation is the first pilot family.
- [ ] K4os LZ4 Frame is interim only and must meet the allocation gate.
- [ ] .NET 11 Zstandard is the planned codec after qualification.
- [ ] Stable event contract names replace assembly-qualified type resolution.
- [ ] The quote segment receives a count-aware custom converter/surrogate.
- [ ] The 234 query metadata members use the computed/explicit-interface design in section 18.2.
- [ ] Wire-facing `IActorEntityId` members are removed or covered by approved bounded unions.
- [ ] `ActorEntityId`, `LookupTypeCollection`, and `TradePositionReadModel` pass their special-shape gates.
- [ ] The event registry completeness test covers every persisted/published concrete event.
- [ ] The quote converter passes stale-tail, bounds, concurrency, and exact lease-ownership tests.
- [ ] Full cutover includes Fund integration validation.

## 21. Source references

- Nerdbank.MessagePack getting started and `partial` requirement: <https://aarnott.github.io/Nerdbank.MessagePack/docs/getting-started.html>
- Nerdbank.MessagePack witness classes/type shapes: <https://aarnott.github.io/Nerdbank.MessagePack/docs/type-shapes.html>
- Nerdbank.MessagePack customization, member inclusion, constructors, and map/indexed schemas: <https://aarnott.github.io/Nerdbank.MessagePack/docs/customizing-serialization.html>
- Nerdbank.MessagePack polymorphism and runtime derived-type registration: <https://aarnott.github.io/Nerdbank.MessagePack/docs/unions.html>
- Nerdbank.MessagePack custom converters, depth validation, child converters, and registration: <https://aarnott.github.io/Nerdbank.MessagePack/docs/custom-converters.html>
- Nerdbank.MessagePack security model and bounded deserialization: <https://aarnott.github.io/Nerdbank.MessagePack/docs/security.html>
- Nerdbank.MessagePack performance guidance: <https://aarnott.github.io/Nerdbank.MessagePack/docs/performance.html>
- Nerdbank.MessagePack feature comparison: <https://aarnott.github.io/Nerdbank.MessagePack/docs/features.html>
- Nerdbank.MessagePack NuGet package: <https://www.nuget.org/packages/Nerdbank.MessagePack>
- K4os.Compression.LZ4 project and frame APIs: <https://github.com/MiloszKrajewski/K4os.Compression.LZ4>
- K4os.Compression.LZ4.Streams NuGet package: <https://www.nuget.org/packages/K4os.Compression.LZ4.Streams>
- .NET 11 Preview 1 Zstandard support: <https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview1/libraries.md#zstandard-compression-support>

## 22. Revision log

| Date | Change |
| --- | --- |
| 2026-08-07 | Initial design-only specification based on repository audit, exploratory benchmarks, no-legacy decision, TickAggregation pilot scope, binary event-log requirement, K4os allocation constraint, and future .NET 11 Zstandard adoption. |
| 2026-08-07 | Added syntax-aware closure analysis for the five principal migration risks, executable Nerdbank probe evidence, concrete blocker resolutions, acceptance tests, and ordered blocker-removal plan. |
| 2026-08-07 | Recorded Nerdbank no-go/defer decision and retained MessagePack-CSharp. Replaced the active event-log design with a standalone MessagePack-CSharp binary payload requirement using PostgreSQL `EventData bytea`; no implementation change was made. |
