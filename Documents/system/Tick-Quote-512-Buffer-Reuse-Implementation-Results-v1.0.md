# Tick quote 512-buffer reuse implementation and results

**Date:** 2026-09-15
**Status:** Isolated implementation verification complete; live batch sizes remain 64 pending feed-specific qualification

## Ownership and bounds

The active batch sizes are independently configurable for futures and futures options; both retain the existing 64-quote default. The 4,096-quote message ceiling is unchanged. The 512-quote experiment is an opt-in isolated Synthetic Development profile until qualification for a live feed. No Scylla schema or wire field changed.

For each Databento generation, `TickQuoteBufferPool` preallocates exact-sized producer arrays by asset batch capacity. One slot per resolved contract plus eight spare slots per capacity bounds producers; `RentAsync` waits instead of allocating or dropping observations when slots are in use. Each generation has a 512 MiB preallocation limit. Each lease is returned once after the publisher finishes its NATS send. Generation shutdown completes pending slot waits and retires the pool. A slot belonging to an old generation cannot be handed to a new one.

The live event publisher now has a bounded waiting channel. The already bounded Stage 3 publisher additionally accounts for retained quote items. At MessagePack ingress, the actor uses a process-wide set of 128 exact 512-element arrays. The deserializer rents a slot, the realtime actor awaits projection and returns it in `finally`; unexpected saturation falls back to an ordinary allocation rather than losing market data. The actor warms the slots at startup.

The native CQL encoder writes into an exact-length cached `byte[]` instead of creating a new array for every quote batch. A 64 MiB idle-cache limit bounds retained storage buffers. The Scylla bind parameter owns that buffer from encoding through prepared-statement execution. Because the Cassandra driver's pending request may retain a reference to bound arrays, cancellation of the caller does **not** return the buffer early; a background continuation drains the driver task and returns the buffer after actual completion. Preparation, connection, binding and submission failures release buffers before a driver request is sent.

This design removes the per-batch quote array and CQL array in steady state at or below 512 quotes. It does not claim literally zero allocation: each batch still creates a small event/lease/parameter owner, and NATS, actor, Scylla driver and readback allocations remain. Exact CQL lengths can vary with decimal representation, so a new length allocates once, and idle-cache pressure may evict an old length. Batches above 512 and more than 128 simultaneous decoder slots use the safe allocating path. CQL reads are unchanged.

A worst-case 512-quote CQL value, with full-size decimal fields on every item, is below the 85,000-byte large-object threshold including the array header. This property is covered by an automated storage test.

## Verification

The first 15-minute isolated 512-quote soak passed without the live IFM API or UI. It modeled 200 futures and 2,000 futures-option quotes per second through the realtime actor, Core NATS, native CQL projection and local Scylla. The producer submitted assembled batches, so publisher-to-row visibility does not include the dwell time of a partly filled live quote buffer. The producer stopped before the final drain; all 3,383 accepted batches were published and stored, with zero publisher failures, expiration, rejection, backlog, or outstanding leases. The test host exited with code 0.

Release builds passed for the API, isolated actor host, and BenchmarkDotNet project. Focused tests passed: four fixed quote-pool unit cases; five Scylla awaiter/cancellation unit cases; 18 real-Scylla storage cases; seven MessagePack quote-contract unit cases; 12 publisher integration cases; 29 Databento aggregation service unit cases; and two production epoch reset cases. The service project's native C++ `ZERO_CHECK` target hit a Visual Studio FileTracker access-denied error in the sandbox; the managed test assembly was produced and all 29 service cases passed via direct `--no-build` execution.

## Measurements

The 512 run processed 157,184 futures and 1,574,912 futures-option quotes. Total quote volume was within 0.02% of the earlier 64 run. Sustained samples were measured after warmup and before drain.

| Measurement | Earlier 64 | Earlier 4,096 | Pooled 512 |
|---|---:|---:|---:|
| Accepted and stored batches | 27,068 | 422 | 3,383 |
| Total quote items | 1,732,352 | 1,728,512 | 1,732,096 |
| Mean sustained managed allocation | 2.292 MB/s | 0.853 MB/s | 0.200 MB/s |
| Sustained Gen0 / Gen1 / Gen2 | 171 / 0 / 0 | 129 / 128 / 128 | 4 / 2 / 1 |
| Mean sampled LOH after last GC | 1.44 MB | 9.78 MB | 1.50 MB |
| Mean sustained working set | 221.84 MB | 220.88 MB | 248.23 MB |
| Row visibility p50 / p95 / p99 | 33.8 / 37.6 / 149.8 ms | 32.2 / 64.3 / 233.3 ms | 101.2 / 119.5 / 246.9 ms |

The pooled 512 path reduced whole-host managed allocation by about 91% relative to the earlier 64 path and 77% relative to 4,096. It also eliminated the repeated full collections seen with 4,096, but one sustained Gen2 collection occurred. Row visibility was higher than both earlier samples. The working set rose from 226 MB at the first sustained sample to 274 MB at the last, then held near 275 MB through the drain.

A separate five-minute diagnostic run published and stored 1,127 batches and exited cleanly. At the last sustained sample, live managed memory was 73.5 MB. A Gen0 collection during drain reduced it to 37.1 MB while working set stayed near 245.8 MB. An opt-in diagnostic full collection after traffic stopped reduced managed memory to 31.6 MB. Across the run, the CQL cache held approximately 141 KB, only two exact-length CQL arrays were created, and decoder overflow stayed at zero. These observations support committed but reclaimable GC heap pages, rather than accumulating quote buffers, as the principal reason for the working-set rise. They do not identify every remaining per-batch allocation source.

The [512 verdict](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit512Pooled/tick-quote-soak-20260915-233451.json) and [minute samples](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit512Pooled/tick-quote-soak-20260915-233451.csv) retain the raw observations. Compare with the [matched 64 and 4,096 baseline](Tick-Quote-64-vs-4096-Capacity-Experiment-Results-v1.0.md), which used the earlier allocating decoder/CQL path.

The [memory diagnostic verdict](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit512MemoryDiagnostic/tick-quote-soak-20260915-235421.json) and [counter samples](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit512MemoryDiagnostic/tick-quote-soak-20260915-235421.csv) include the extra live-memory and pool counters. The diagnostic full collection was isolated to this test host after workload drain; production code does not force GC.

## Direct allocation benchmarks

BenchmarkDotNet 0.15.8 ran on .NET 10.0.10 with concurrent Workstation GC, three warmups and eight measured iterations. The 512 input was prepared outside the measured method. The allocating and pooled CQL methods use the same native encoder; only the output buffer ownership differs.

| Operation | 64 quotes | 512 quotes | 4,096 quotes |
|---|---:|---:|---:|
| Allocating CQL encode: mean; allocated/op | 8.99 µs; 8,480 B | 67.08 µs; 67,616 B | 738.39 µs; 540,760 B |
| Pooled CQL encode: mean; allocated/op | 7.98 µs; 24 B | 64.12 µs; 24 B | 527.86 µs; 24 B |
| MessagePack decode and release: mean; allocated/op | 14.26 µs; 32 B | 120.58 µs; 32 B | 1,103.01 µs; 458,796 B |

At 512, the output CQL array fell from 67,616 B/op to a 24 B owner wrapper, while the encoder's mean time improved modestly. The decoder allocates a 32 B owner at 512 rather than a new roughly 57 KiB quote array. The decoder still allocates the full quote array above 512; its 4,096 case recorded Gen0, Gen1 and Gen2 collections. The pooled 4,096 CQL case retains a large exact-length buffer after warmup, so its low per-operation figure does not mean 4,096 is free of large-object retention.

The [joined encoding and ingress benchmark](../../TomasAI.IFM.Application.Storage.Benchmarks/BenchmarkDotNet.Artifacts/TickQuote512PooledConversionIngress/results/BenchmarkRun-joined-2026-09-15-20-03-44-report-github.md) contains all nine cases.

## Prepared Scylla benchmark and size boundary

The prepared-write benchmark includes pooled CQL encoding, DataStax binding, local Scylla execution and response. It used the same isolated test keyspace as the soak. The driver and server still allocate about 15–18 KB per write, despite removal of the per-batch CQL output array. Reading the UDT list is unchanged.

| Operation | 64 quotes | 512 quotes | 4,096 quotes |
|---|---:|---:|---:|
| Pooled prepared write: mean; allocated/op | 0.918 ms; 15.48 KB | 44.67 ms; 16.22 KB | 10.80 ms; 18.21 KB |
| Native UDT-list read: mean; allocated/op | 0.784 ms; 24.00 KB | 0.922 ms; 85.63 KB | 2.232 ms; 579.71 KB |

The 512 write latency was reproduced in a fresh-key, write-only run: 44.67 ms, versus 0.912 ms at 64 and 8.93 ms at 4,096. A size sweep found a repeatable local latency band around 44–45 ms at 128, 256, 384, 496 and 512 quotes; 1,024 and 4,096 were faster but more variable. A narrower run measured 64 at 0.841 ms, 96 at 1.180 ms, 112 at 0.956 ms, 120 at 1.099 ms, 124 at 44.43 ms, 128 at 44.46 ms and 512 at 44.46 ms. This places the local transition between 120 and 124 quotes **for the benchmark's field values**. The underlying driver, network or Scylla mechanism has not been isolated, and different decimal lengths may move a byte-size threshold. No count near the transition should be promoted to live trading solely on these synthetic timings.

The [prepared write/read report](../../TomasAI.IFM.Application.Storage.Benchmarks/BenchmarkDotNet.Artifacts/TickQuote512PooledScylla/results/TomasAI.IFM.Application.Storage.Benchmarks.TickQuoteScyllaWriteBenchmarks-report-github.md), [512 write repeat](../../TomasAI.IFM.Application.Storage.Benchmarks/BenchmarkDotNet.Artifacts/TickQuote512PooledScyllaWriteRepeat/results/TomasAI.IFM.Application.Storage.Benchmarks.TickQuoteScyllaWriteBenchmarks-report-github.md), [large size sweep](../../TomasAI.IFM.Application.Storage.Benchmarks/BenchmarkDotNet.Artifacts/TickQuoteTransportBandPooledWrites/results/TomasAI.IFM.Application.Storage.Benchmarks.TickQuoteScyllaWriteBenchmarks-report-github.md) and [small boundary sweep](../../TomasAI.IFM.Application.Storage.Benchmarks/BenchmarkDotNet.Artifacts/TickQuoteSmallBoundaryPooledWrites/results/TomasAI.IFM.Application.Storage.Benchmarks.TickQuoteScyllaWriteBenchmarks-report-github.md) retain iteration error and GC statistics.

The 112-quote isolated follow-up passed after 15 minutes. It submitted 15,468 batches with 157,472 futures and 1,574,944 futures-option quote items. All accepted batches were published and the per-contract Scylla row counts matched; there were no publisher failures, rejected or expired batches, backlog, outstanding leases, decoder overflows, or new CQL arrays after warmup. The test host exited with code 0. Across 12 sustained minute samples, managed allocation averaged 864,282 B/s (0.864 MB/s), with Gen0/Gen1/Gen2 deltas of 47/2/1. Mean working set was 240.69 MB and mean sampled LOH was 1.64 MB. At drain, the working set was 226.6 MB. Row visibility p50/p95/p99 was 34.6/37.8/148.5 ms. Compared with the earlier 64 run at nearly the same quote volume, 112 used about 62% less whole-host managed allocation and about 43% fewer storage batches. The 112 soak validates row counts and sentinel visibility; separate real-Scylla integration tests validate the encoded quote fields and order.

The [112 verdict](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit112Pooled/tick-quote-soak-20260916-002531.json) and [minute samples](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit112Pooled/tick-quote-soak-20260916-002531.csv) retain the raw observations. These were synthetic assembled batches, not live Databento quotes. The boundary seen in prepared-write benchmarks may shift with real quote payload sizes and server conditions. Keep 64 as the live default. The 512 array-reuse implementation remains available for controlled experiments, but its observed local write latency does not justify live promotion.
