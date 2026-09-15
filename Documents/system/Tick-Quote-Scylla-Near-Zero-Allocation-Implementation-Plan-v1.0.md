# Tick Quote Scylla Write Near-Zero Allocation Implementation Plan

**Version:** 1.0

**Date:** 2026-09-15

**Status:** G0-G3 and G5 qualified; G4 quantitatively declined; native CQL writer is the default
**Scope:** Futures and futures-option quote batches written to `tick_quote_data`

## 1. Outcome and allocation boundary

Remove the per-quote heap objects and the per-batch projection array created by `TickQuoteStorageCollection.Resolve`, while retaining the current `tick_quote_data` table, `tick_quote_item` UDT, prepared CQL, quote order, actor route, and durable write semantics. The target for the application-owned quote conversion is **zero allocations per quote and at most one encoded `byte[]` per batch**. The installed driver's public collection-serializer registration rejects `ColumnTypeCode.List`, but its prepared-statement binder accepts the native CQL list payload as a pre-encoded `byte[]`. Actual Scylla readback verifies that compatible route.

The entire database write cannot honestly be called zero-allocation under `ScyllaDBCSharpDriver` 3.22.0.4. Its `PreparedStatement.Bind` accepts `object[]`, and the public `TypeSerializer<T>.Serialize` returns `byte[]`. The bound statement, boxed scalar values, driver request/response objects, and existing MessagePack quote-segment deserialization remain separate allocation boundaries. A fully allocation-free write would require a separately qualified driver/protocol writer and transport ownership change.

No Scylla schema or persisted MessagePack key changes are authorized by this plan. A `blob` column or a forked driver is an alternative design gate only if the compatible UDT route fails or a later end-to-end profile proves the remaining allocations are material. Binding native CQL list bytes does **not** change the stored column to `blob`.

## 2. Verified pre-change production path

1. `TickAggregationService.FlushAsync` publishes a `FuturesTickQuoteDataChangedEvent` containing a `FuturesTickQuoteDataSegment` of 1–64 quote value structs. The source `FuturesTickQuoteData[]` is leased from `ArrayPool<FuturesTickQuoteData>` and returned when publication has finished its handoff.
2. The NATS actor path receives and deserializes the quote segment. `FuturesTickQuoteDataSegmentFormatter.Deserialize` currently allocates a new array of exactly `count` quote structs; it does not own the source publisher's pooled lease.
3. `TickAggregationRealtimeActor` sends the changed event to `TickAggregationRealtimeProjector`, which calls `MarketDataDbContext.InsertTickQuoteDataAsync` with an inserted event. The business event identifiers, value date, definition date, contract ID, count and quote order are preserved.
4. `InsertTickQuoteDataAsync` creates a 21-slot `object?[]` with boxed scalar values and one `TickQuoteStorageCollection` wrapper. `InsertTickQuoteData.Bind()` returns that array.
5. `ScyllaDbObjectDataRepositoryProvider.Bind` clones the 21-slot array when it encounters an `IScyllaUdtValue`, invokes `Resolve(session)`, and creates a bound prepared statement.
6. `TickQuoteStorageCollection.Resolve` enters a static lock, registers the UDT map once per Scylla session, and records the registration with a `ConditionalWeakTable<ISession, object>` marker. For every batch it then creates `TickQuoteStorageItem[count]` and one class instance per quote, copying all twelve quote fields.
7. The installed driver encodes the value for `frozen<list<frozen<tick_quote_item>>>` and executes the insert. These projection objects are temporary, but their allocation rate scales with quote traffic.

Existing integration coverage checks row identity and `quote_count`, but it does not compare every UDT field after readback. That gap must close before changing the encoder.

## 3. Ownership and compatibility invariants

- The writer accepts only `1 <= QuoteCount <= 64`, and `QuoteCount` equals `QuoteData.Count`.
- Every quote's twelve UDT fields, null decimal values, signed/unsigned numeric conversions, nanosecond timestamps, header flags, and original order survive readback exactly.
- The row key, dataset, definition date, publisher ID, instrument ID, event IDs, emission reason, and schema version remain unchanged.
- A failed, cancelled, replayed, or duplicated write never returns or mutates a buffer while the driver can still use it.
- Concurrent writes cannot share a mutable parameter array, bound statement, quote buffer, or encoded payload.
- Session replacement validates the nested UDT-list prepared marker on the replacement session before its first native-CQL quote write. A failed validation never marks that statement as verified. The pre-change mapped path registered its UDT mapping on each replacement session.
- The stored CQL type stays `frozen<list<frozen<tick_quote_item>>>`; historical quote rows remain queryable using the existing APIs.
- No new actor, durable queue, retry timer, or database poll is introduced.

## 4. Progressive implementation gates

### Gate TQ-G0: allocation and write baseline

Add a `BenchmarkDotNet` benchmark in `TomasAI.IFM.Application.Storage.Benchmarks` for the current conversion and binding at quote counts 1, 32 and 64, with null and non-null prices. Separate measurements for `Resolve`, the 21-slot parameter build, binder clone, MessagePack deserialization, and full isolated Scylla write. Record allocated bytes per batch and per quote, operations/s, median and p95/p99 latency, CPU, Gen 0/1/2 collections and working-set trajectory. Run a production-like synthetic quote stream in an isolated test host; do not run it beside the live IFM process. Check in baseline commands, environment, sample size and results.

**Exit:** The baseline can distinguish our converter allocations from driver and transport allocations. The live after-hours allocation sample is context, not a benchmark substitute.

### Gate TQ-G1: remove incidental hot-path work

Move UDT mapping registration to the Scylla session initialization or a once-per-session helper used before quote binding. Replace the per-session `new object()` marker with a shared marker if a weak-key registry remains necessary. Preserve weak session ownership and failure retry. Ensure successful registration does not take a global lock on every batch.

Define an explicit exclusive-ownership contract for this insert's freshly created parameter array. Only then allow the binder to replace its UDT slot in place instead of cloning the array. The generic binder must continue cloning borrowed or reusable arrays. Do not reuse a bound statement across concurrent writes without proven driver ownership rules.

**Exit:** Session replacement, concurrent binding, cancellation and registration-failure tests pass. The benchmark isolates the bytes and latency saved by each change. This gate is useful even if the direct encoder fails.

### Gate TQ-G2: prove direct nested UDT-list encoding in isolation

Try the driver's public collection-serializer registration in an isolated session. If it refuses `ColumnTypeCode.List`, check whether the prepared binder can pass a correctly encoded native CQL list payload as `byte[]` for the existing `frozen<list<frozen<tick_quote_item>>>` marker. Do not infer that a custom UDT serializer replaces the enclosing list serializer.

Encode against prepared-statement type metadata and the actual `tick_quote_item` field order. Compare old/new inserts and read back every field for counts 1, 32 and 64, both nullable decimal states, boundary numeric values, and futures/futures-option identities. Reject a `blob` column substitution or a payload that does not follow native CQL list/UDT encoding.

**Exit:** The installed driver accepts the value, all readbacks match, and the prototype removes the per-quote projection objects. If neither public registration nor native raw-value binding works, record that blocker and keep the G1 path rather than silently changing the schema.

### Gate TQ-G3: integrate the encoded quote path

Place the typed encoder in the MarketDataDb storage model, not in the actor or feed algorithm. Keep `InsertTickQuoteDataAsync` and the existing projector contract; only replace the UDT parameter-conversion implementation. Preserve the mapped-object implementation behind a development diagnostic switch until the new path has passed all compatibility and performance gates. The switch must select one write path for a batch; it must never dual-write a quote row.

The native CQL-value encoder returns `byte[]`. Allocate an exactly sized per-batch payload unless a supported, documented ownership contract allows pooling safely through completion of asynchronous driver encoding. Never return a pooled byte array while a bound request may retain it. Do not attempt unsafe object pooling of mutable UDT item classes as a shortcut.

**Exit:** Application-owned quote conversion makes zero per-quote class/array allocations and at most one encoded buffer allocation per batch. Old and new paths have identical Scylla row values. Baseline and new benchmark results are recorded side by side.

### Gate TQ-G4: remove the separate MessagePack ingress array if justified

Measure the array allocated by `FuturesTickQuoteDataSegmentFormatter.Deserialize` after G3. If it is a material share of remaining quote-path allocation, introduce an explicit pooled ingress owner that stays alive through actor dispatch and Scylla projection and is released on success, failure, cancellation and replay disposal. Reuse the existing NATS pooled-ingress patterns; retain MessagePack keys and quote value layout.

This is a distinct gate because mailbox fan-out and durable replay can outlive a single callback. A pooled quote buffer cannot be returned merely because the producer's `PublishAsync` completed. If ownership cannot be proved in every branch, keep the current correctly owned deserialization array.

**Exit:** No use-after-return or stale quote data under fan-out, cancellation, restart, replay and concurrent sends; total allocations decrease in the full actor-to-Scylla benchmark.

### Gate TQ-G5: cutover and qualification

Run all quote contract, realtime actor, projector, storage and real-Scylla integration suites. Add a 1/32/64-quote round trip that compares every UDT field and quote order. Verify failed writes, session replacement, replay, idempotent row identity, duplicate events, bounded publisher backpressure and buffer release. Run a synthetic feed soak in an isolated host with burst traffic and record write completion rate, storage lag, p95/p99 write latency, allocations/s, GC counts, LOH, working set and retained buffer counts.

Use repeated, same-environment BenchmarkDotNet runs to compare latency distributions rather than a single noisy mean. Cut over only if correctness is exact, application-owned conversion bytes fall substantially, and there is no material write-latency or backlog regression. Remove the old converter and diagnostic switch after the soak gate succeeds. Verify the live API with the synthetic host exited before the API runs.

**Exit:** Production uses one compatible quote write path, historical rows remain readable, and the measured allocation benefit is large enough to justify the encoder's complexity.

## 5. Test and evidence matrix

| Area | Happy path | Edge path |
| --- | --- | --- |
| Quote schema | 1, 32, 64 ordered quotes read back identically | zero/65 rejected; null decimal, numeric bounds, timestamp boundaries |
| Scylla session | UDT mapping or prepared-marker validation on the active session | session recreation, mismatched field order/type, concurrent first writes |
| Binding | owned parameters bind without an unnecessary copy | borrowed parameters remain unmodified; concurrent bind isolation |
| Durability | one inserted row per batch with same primary key and all metadata | cancellation, write failure, replay/duplicate identity, restart |
| Buffer ownership | producer and ingress leases released after last reader | backpressure, fan-out, cancellation, deserialization failure, replay |
| Performance | allocation and latency comparison for each batch size | bursts, sustained soak, queue lag, GC/LOH and working set |

Implementation evidence goes beside this plan or in the benchmark project's `RESULTS.md`. Include the exact driver version, Scylla version, package-lock state, hardware, GC mode, dataset, quote distribution, command lines and before/after results.

## 6. Stop and fallback rules

Stop the direct-encoding gate if prepared binding cannot accept the native CQL payload for the existing nested frozen-list UDT, if field ordering cannot be verified, or if the driver requires unsafe reuse of an asynchronous payload. Do not change `quote_data` to `blob` as an incidental workaround. Preserve the current mapped UDT writer and retain any independently verified G1 improvement.

If G3 reduces converter allocations but has no useful full-path benefit, keep the simpler implementation. A future fully zero-allocation design would be a new work package for a span/`IBufferWriter<byte>`-based Scylla protocol writer plus end-to-end quote-buffer ownership, with schema and compatibility decisions reviewed independently.

## 7. Completion definition

The near-zero allocation work is complete when G0–G3 and G5 pass, G4 is either completed or quantitatively declined, and the implementation evidence shows:

1. zero `TickQuoteStorageItem` objects allocated per quote;
2. zero `TickQuoteStorageItem[]` arrays allocated per batch;
3. no avoidable 21-slot binder clone on exclusively owned quote writes;
4. no global registration lock on established sessions;
5. no more than one application-owned quote encoding buffer per batch on the direct path;
6. unchanged schema, MessagePack compatibility, identifiers, metadata and all twelve quote fields;
7. correct ownership on all success and failure branches;
8. measured improvement in the full actor-to-Scylla path without material latency, backlog or durability regression; and
9. no claim of zero allocations for the entire Scylla driver write.

## 8. Implementation record (2026-09-15)

The compatible part of TQ-G1 is implemented. `TickQuoteStorageCollection` checks the weak session registry before entering the registration lock and uses a static marker. The quote insert now hands a fresh positional array to the binder under an explicit, one-time ownership contract; borrowed arrays still take the existing clone path. The new owner rejects a second take, preventing a resolved UDT array from being reused after a session changes.

The isolated BenchmarkDotNet handoff comparison on .NET 10.0.10 measured **408 B and 99.93 ns** for the cloned 21-slot array versus **240 B and 26.32 ns** for the exclusive handoff. These numbers include the benchmark's fresh positional array and a placeholder UDT value; they do **not** include Scylla encoding, network I/O, or the quote-item projection. They show a 168 B saving in that specific slice, not a 41% reduction in the complete write path.

The public custom-serializer route stopped at an installed-driver boundary. A serializer for `ColumnTypeCode.List` was registered in an isolated prototype, and `GenericSerializer` rejected it with `Cassandra.DriverInternalError: TypeSerializer defined for unsupported CQL type List`. That is a client API restriction, not a Scylla rejection. The installed driver also accepts a pre-encoded `byte[]` as the native value of the prepared nested UDT-list marker. A real-Scylla test first wrote an empty list payload, then a one-buffer encoder wrote 1, 32 and 64 items; Scylla read back every UDT field and nullable/negative/boundary decimal correctly. The stored column and prepared CQL remain unchanged.

The encoded path is now available behind the development switch `IFM_TICK_QUOTE_RAW_CQL_WRITE=true`, defaulting to the mapped writer. `TickQuoteEncodedStorageCollection` checks the prepared marker's list/UDT type and all twelve UDT field names and types in order once per prepared statement before returning an exactly sized payload. `InsertTickQuoteData` transfers exclusive ownership of its 21-slot array so the binder resolves the wrapper without cloning. The payload is a newly allocated array for each batch and is never returned to a pool while asynchronous execution can retain it. This establishes the application conversion's one-buffer, zero-per-quote-object design, but it is not yet a full-path performance qualification or a production cutover.

The isolated application-conversion benchmark measured the following old/new results: **176 B / 30.12 ns versus 160 B / 149.06 ns** for one quote; **4,888 B / 650.78 ns versus 4,256 B / 4,254.31 ns** for 32; and **9,752 B / 1,314.64 ns versus 8,480 B / 8,264.40 ns** for 64. Thus one-buffer encoding saves about 9–13% in this conversion slice but is approximately five to seven times slower than merely constructing the mapped item objects. The mapped route still pays the driver's nested-list encoding cost after this slice, so these results alone do not determine the full prepared-write comparison or authorize cutover. They establish a reason to measure the whole write before turning on the diagnostic switch.

Two repeated BenchmarkDotNet prepared-write runs to the local Scylla **test keyspace** include conversion, driver serialization, request execution and response handling. At 32 quotes, the mapped writer allocated **84.96 KB** per write, while raw binding allocated **19.61 KB** in both runs; mean latency was **859.8/861.6 us** mapped and **775.1/814.8 us** raw. At 64, allocation was **154.13 KB** mapped and **23.71/23.72 KB** raw; mean latency was **990.5/1,037.7 us** mapped and **872.0/870.7 us** raw. At one quote, allocation was **17.92 KB** versus **15.6 KB**, with latency varying more between runs. The large 32/64 allocation saving and absence of a write-latency regression repeat, but this test-keyspace benchmark excludes NATS ingress, actor dispatch, live feed backpressure and a sustained soak. It is evidence for the encoded path, not proof of its whole-system cutover gate.

Reproduce with `dotnet run -c Release --project TomasAI.IFM.Application.Storage.Benchmarks/TomasAI.IFM.Application.Storage.Benchmarks.csproj -- --filter '*TickQuoteConversionBenchmarks*'` and the same command with `'*TickQuoteScyllaWriteBenchmarks*'`. The generated harness used locally cached NuGet packages through a temporary root `NuGet.Config`, which was removed after each run. Environment: Windows 10, .NET 10.0.10, BenchmarkDotNet 0.15.8, concurrent Workstation GC, `ScyllaDBCSharpDriver` 3.22.0.4, localhost Scylla test keyspace, 1/32/64 ordered quotes with mixed nullable decimals. Scylla server version and CPU identification were not captured by the benchmark harness. Results reside in ignored `BenchmarkDotNet.Artifacts/TickQuoteConversionLocalFeed`, `TickQuoteScyllaWriteLocalFeedRetry` and `TickQuoteScyllaWriteLocalFeedRepeat` folders.

A compatible mapped value-type UDT item was also prototyped against real Scylla storage. Counts 1, 32 and 64 read back all twelve fields, null decimals and quote order correctly. Its projection benchmark saved 16% of projection allocation for 32 and 64 quotes, but took 27% and 39% longer respectively: **4,888 B / 653 ns** versus **4,120 B / 831 ns** at 32, and **9,752 B / 1,253 ns** versus **8,216 B / 1,745 ns** at 64. It was reverted because the projection latency regression and modest byte saving did not meet the cutover rule. The later prepared-write benchmark measured the complete mapped versus raw driver path.

At this stage of implementation, TQ-G0's actor-to-Scylla baseline, TQ-G4's ingress-ownership decision and TQ-G5's isolated synthetic-feed soak and cutover were still pending. The direct encoder had been **implemented, storage-verified and measurably beneficial in repeated prepared-write benchmarks**, while the mapped writer remained the default. The qualification and cutover record below supersedes this interim status.

After reverting the value-type prototype, the mapped and direct-encoding Scylla round-trip suite passed **13/13** targeted tests (including futures and futures-option identities, 1/32/64 quote arrays, all twelve UDT fields, nullable decimals, `decimal.MinValue`, maximum unsigned source sequence, borrowed-parameter immutability, prepared marker validation and native raw-value binding). The framework storage unit suite passed **398/398** after the prepared-metadata check was added. Both filtered `dotnet test` processes exited. No application-wide synthetic feed soak is claimed.

Benchmark artifacts were generated under `TomasAI.IFM.Application.Storage.Benchmarks/BenchmarkDotNet.Artifacts/TickQuoteBindRootLocalFeed` and `TickQuoteProjectionLocalFeed`. A temporary local-package NuGet configuration used to build the generated harness was removed afterward.

### Isolated actor-to-Scylla qualification and cutover

The integration host now supports `IFM_TICK_QUOTE_SOAK=true`, which registers and starts **only** `TickAggregationRealtimeActor` and excludes unrelated hosted background services. The synthetic producer uses the production bounded `TickAggregationEventPublisher` and pooled quote leases to send 1/32/64-item futures and futures-option changed events over Core NATS. The actor and its realtime projector write to `market_data_test_db` through the configured MarketDataDbContext. The host does not subscribe to Databento, place orders, or start strategy actors. It shuts down the publisher, consumer, actor supervisor and process at completion.

The runner records one minute of host allocation rate, working set, GC generations, heap/LOH, queue depth, published count, faults and outstanding quote leases. A separate low-rate probe samples end-to-end **row visibility lag** from publisher acceptance to readback; its p95/p99 values are visibility measurements, not pure Scylla write latency. Final count queries compare stored row totals with accepted batches for both assets. The one-minute Debug-path rehearsal passed **448/448** quote batches with no failure, queue backlog or retained lease and an exit code of zero. A first Release attempt, started at **2026-09-15 09:56:28 UTC**, was deliberately stopped after 23 healthy minutes when a runner supervision defect was found: a readback-probe failure could have remained hidden until generation ended. Its partial CSV is diagnostic evidence, not a qualification pass. After correcting task supervision, an injected readback failure produced a failed verdict after the first batch in 0.12 seconds, and a clean one-minute Release rehearsal passed **454/454** sent, published and stored batches with zero retained leases and process exit code zero.

The restarted 120-minute Release qualification ran from **2026-09-15 10:24:44 to 12:24:47 UTC** and **passed with process exit code zero**. It sent and published **60,234/60,234** batches, and exact Scylla counts matched **30,117 futures** plus **30,117 futures-option** rows. There were zero failed, rejected, expired or discarded batches, zero outstanding quote leases and at most one queued batch in any sustained minute sample. Across 626 readback probes, visibility lag was **32.49 ms p50, 33.43 ms p95 and 33.58 ms p99**. The 90 sustained-minute samples averaged **0.609 MB/s allocated** including six 64-batch/s bursts; the highest sample was **3.33 MB/s**. They recorded **418 Gen0, 5 Gen1 and 2 Gen2** collections. Sustained working set ranged **203.4–231.9 MiB** and ended at **207.3 MiB**; the 15-minute drain ended at **207.6 MiB**. Sampled LOH size stayed at about **1.43 MiB**. These are isolated host measurements, not whole-application live-feed measurements.

Sequential 15-minute actor-to-Scylla runs used the same synthetic schedule and no concurrent live IFM host. Both passed exact stored-row counts and clean process exit: **7,493 mapped** versus **7,499 raw** batches, zero queue backlog or failed work, and **33.3 versus 33.4 ms p95 visibility lag**. The twelve sustained-minute samples averaged **1.310 MB/s mapped** versus **0.573 MB/s raw**, a **56% allocation-rate reduction** at nearly identical batch counts. Gen0 collections were **82 mapped** versus **19 raw**; Gen1/Gen2 totals were **2/1** for both. The separate prepared-write BenchmarkDotNet results above show no latency regression for 32 or 64 quotes. These figures support the native encoding cutover; working-set endpoints from the short sequential runs are too noisy to establish a mapped/raw memory difference.

TQ-G4 was measured and declined. The MessagePack quote-segment ingress benchmark allocated **136 B, 3,608 B and 7,192 B** for 1, 32 and 64 quotes. With the equal-size distribution in the matched actor run, that is about **3.6 KB per batch**, roughly **7%** of the raw actor path's approximate **55 KB per batch**. Pooling it would require ownership through mailbox dispatch, projector completion, failure and replay, while leaving most allocation in driver and transport work. The current distinct ingress array retains simple, correct ownership. The benchmark evidence is archived in `TickQuoteIngressLocal`.

After qualification, `MarketDataDbContext.InsertTickQuoteDataAsync` uses only `TickQuoteEncodedStorageCollection`; the mapped UDT-item writer and `IFM_TICK_QUOTE_RAW_CQL_WRITE` diagnostic switch were removed. A one-minute post-cutover actor smoke with no writer switch passed **464/464** sent, published and stored batches, zero failures or retained leases, and process exit code zero. The focused real-Scylla storage suite passed **13/13**, the tick-aggregation feed unit subset passed **10/10**, and the benchmark project and production API server built with zero errors. The schema, prepared CQL and historical UDT-list column remain unchanged. The post-cutover smoke's **146.5 ms p95 visibility lag** is a short startup-scale measurement, so the two-hour and matched-run lag figures are the sustained comparison evidence. The test's sampled lag includes NATS handoff, actor projection, Scylla execution and readback polling; it does not isolate pure database write completion p95/p99 or CPU time.

The first broader tick-aggregation feed integration filter returned **18 passed, one skipped and one failed**. `NormalizedVxTradeAndQuote_RouteThroughRealtimeActors_AndPersistEod` failed at the stream-start command before sending a synthetic trade or quote: error **5005** requires a qualified value-date runtime before a futures route attaches. The test stopped any previous `DatabentoMarketDataApi` epoch but omitted its own `StartAsync(valueDate)`. It now starts that application runtime with the integration host's deterministic feed factory before requesting the stream, and its existing `finally` block stops the epoch. The case passed **1/1** alone; the complete tick-aggregation integration filter then passed **19/19**, with one existing skip. The isolated host was also hardened so an injected probe failure writes its failed verdict, stops publisher and actors, and exits with **code 1** instead of lingering after an unhandled top-level exception; a repeat injection verified that exit in about five seconds including startup and shutdown.

Run from `TomasAI.IFM.Application.Actor.IntegrationTests` after a Release build with `ASPNETCORE_ENVIRONMENT=Development`, `IFM_TICK_QUOTE_SOAK=true`, `IFM_TICK_QUOTE_SOAK_DURATION_MINUTES=120`, `IFM_TICK_QUOTE_SOAK_OUTPUT=<workspace output directory>` and a private local `Kestrel__EndPoints__Http__Url`, then execute `dotnet .\bin\Release\net10.0\TomasAI.IFM.Application.Actor.IntegrationTests.dll`. The output directory contains minute CSV samples and a JSON verdict. The historic mapped/raw comparison used `IFM_TICK_QUOTE_RAW_CQL_WRITE=false/true` in the pre-cutover binary; that switch is no longer present in current code. Do not run a live IFM API or another actor/feed host on the same NATS subjects during this test.

## References

- `TomasAI.IFM.Application.Storage/MarketDataDb/TickAggregationStorage.cs`
- `TomasAI.IFM.Framework.Storage/ScyllaDb/ScyllaDbObjectDataRepositoryProvider.cs`
- `TomasAI.IFM.Domain.MarketData.Feed.Shared/TickAggregation/TickAggregationContracts.cs`
- `TomasAI.IFM.Application.Storage/MarketDataDb/Schema/MarketDataSchemaCql.cs`
- `TomasAI.IFM.Application.Storage.IntegrationTests/MarketDataDb/TickAggregationStorageTests.cs`
- `TomasAI.IFM.Framework.Messaging.Nats/Docs/MessagePack-NATS-Allocation-Optimization-Implementation-Plan-v1.0.md`
- [Scylla C# driver UDT mapping](https://csharp-driver.docs.scylladb.com/stable/features/udts/index.html)
- [Scylla C# driver prepared statements](https://csharp-driver.docs.scylladb.com/stable/features/components/core/statements/prepared/)
- [Scylla C# driver custom serializer registration](https://csharp-driver.docs.scylladb.com/master/api-docs/api/Cassandra.Builder.html)
- [C# driver GenericSerializer registration and native byte-array binding](https://github.com/datastax/csharp-driver/blob/master/src/Cassandra/Serialization/GenericSerializer.cs)
