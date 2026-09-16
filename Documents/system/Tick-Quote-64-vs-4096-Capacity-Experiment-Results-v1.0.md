# Tick quote capacity experiment: 64 versus 4,096

**Date:** 2026-09-15
**Status:** Completed isolated experiment; 4,096 is a supported hard ceiling, while active Databento batches default to 64.

## Workload and method

Two sequential 15-minute Release runs used the same isolated TickAggregation realtime actor, Core NATS handoff, native CQL quote encoder, and localhost Scylla `market_data_test_db` keyspace. No live IFM API or Databento subscription ran alongside either host. The synthetic producer modeled 200 futures quotes/second and 2,000 futures-option quotes/second, a 10:1 quote-volume ratio. It emitted full batches at the running build's 64 or 4,096 quote limit. The first and last eighths of each run were warmup and drain; twelve sustained-minute samples were compared. Both runs verified exact Scylla row counts, zero publisher faults/backlog, zero retained leases, and clean process exit.

The producer submits already assembled segments. Its measured row-visibility lag starts at publisher admission and includes NATS, actor projection, Scylla execution, and readback polling. It does **not** measure real Databento accumulation or the time since the first quote entered a partly filled buffer. At these modeled rates, a full buffer would take 0.32 versus 20.48 seconds for futures, and 0.032 versus 2.048 seconds for futures options, at 64 versus 4,096 respectively. Trades and feed transitions can flush partial buffers sooner. Those dwell times are rate-model calculations, not observations from a live feed.

| Measurement | 64 quotes | 4,096 quotes |
|---|---:|---:|
| Total quote items | 1,732,352 | 1,728,512 |
| Futures / futures-option quote items | 157,440 / 1,574,912 | 155,648 / 1,572,864 |
| Accepted, published, and stored batches | 27,068 | 422 |
| Mean sustained managed allocation | 2.292 MB/s | 0.853 MB/s |
| Sustained Gen0 / Gen1 / Gen2 collections | 171 / 0 / 0 | 129 / 128 / 128 |
| Mean sustained sampled LOH size | 1.44 MB | 9.78 MB |
| Mean sustained working set | 221.84 MB | 220.88 MB |
| Row visibility p50 / p95 / p99 | 33.8 / 37.6 / 149.8 ms | 32.2 / 64.3 / 233.3 ms |

The quote totals differed by only 0.22%. The 4,096 path used about 64 times fewer Scylla requests and reduced measured whole-host allocation rate by 62.8%, while p95 row visibility increased by 26.7 ms. Working set stayed approximately flat, but large ingress arrays and CQL values drove about 10.7 full collections per sustained minute. The run artifacts are the [64-quote verdict](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit64/tick-quote-soak-20260915-215233.json), [64-quote samples](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit64/tick-quote-soak-20260915-215233.csv), [4,096-quote verdict](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit4096/tick-quote-soak-20260915-221549.json), and [4,096-quote samples](../../TomasAI.IFM.Application.Actor.IntegrationTests/SoakResults/CapacityComparison/Limit4096/tick-quote-soak-20260915-221549.csv).

## Direct BenchmarkDotNet measurements

BenchmarkDotNet 0.15.8 ran on .NET 10.0.10 with concurrent Workstation GC, three warmups and eight measured iterations. Prepared write/read cases connected to the same local Scylla test keyspace. Write includes application encoding, driver serialization, execution, and response handling. Read fetches and decodes one stored nested-UDT-list value. Input quote arrays were prepared outside the measured write method.

| Operation | 64 mean; allocated/op | 4,096 mean; allocated/op |
|---|---:|---:|
| Prepared CQL write | 0.794 ms; 23.71 KB | 13.503 ms; 546.93 KB |
| Nested UDT-list read | 0.749 ms; 24.00 KB | 2.445 ms; 579.05 KB |
| Application CQL encoding alone | 8.298 µs; 8.28 KB | 731.659 µs; 528.09 KB |
| MessagePack segment deserialization alone | 15.056 µs; 7.02 KB | 980.033 µs; 448.07 KB |

The prepared 4,096 write mean was variable across measured iterations (9.07–18.28 ms; 13.50 ms mean), so it should not be treated as a precise live write-latency forecast. Both 4,096 Scylla cases, encoding, and ingress recorded Gen2 collections; none of their 64-quote counterparts did. The [write/read benchmark report](../../TomasAI.IFM.Application.Storage.Benchmarks/BenchmarkDotNet.Artifacts/TickQuoteSize64Vs4096/results/TomasAI.IFM.Application.Storage.Benchmarks.TickQuoteScyllaWriteBenchmarks-report-github.md) and [joined encoding/ingress report](../../TomasAI.IFM.Application.Storage.Benchmarks/BenchmarkDotNet.Artifacts/TickQuoteSizeSerialization64Vs4096/results/BenchmarkRun-joined-2026-09-15-18-37-55-report-github.md) retain the complete BenchmarkDotNet summaries. The host did not capture processor name or Scylla server version.

## Implementation and verification

`FuturesTickQuoteDataSegment.MaximumCount` is now 4,096. The pooled producer lease, MessagePack formatter, event publisher, and CQL encoder enforce that ceiling without changing the existing Scylla schema or the quote message keys. `TickAggregationOptions` and `DatabentoMarketDataRuntimeOptions` select active batch capacities per asset; both default to 64. The API reads `AppSettings:Databento:FuturesQuoteBatchCapacity` and `AppSettings:Databento:FuturesOptionQuoteBatchCapacity`, validates 1–4,096, and currently permits values above 64 only in isolated Synthetic Development with bounded Stage 3 publishing. The normal live-provider startup therefore retains 64.

Graceful TickAggregation shutdown now flushes a partial quote lease after the feed worker drains and before stopping its publisher. The bounded realtime publisher enforces a 65,536 retained-quote-item limit in addition to its message-count limit, so enabling a large experimental batch does not permit 4,096 large leases to accumulate in that queue. The normal live provider currently uses the separate legacy publisher path; its larger-batch promotion requires a qualified byte/item-bounded path.

Verification passed: 29 focused TickAggregation service unit cases, five quote-contract unit cases, 14 real-Scylla storage cases, eight bounded-publisher integration cases, production API and isolated-host Release builds, the two 15-minute matched soaks, and a final 27/27-batch 4,096 smoke with clean exit. The real-Scylla tests checked every nested UDT field and quote order at 1, 32, 64, and 4,096 for both futures and futures options.

## Decision

Keep 64 as the active default. The 4,096 ceiling is useful for isolated futures-option experiments and sharply lowers request count, but its repeated full GCs and longer potential persistence dwell need further work before live-provider promotion. The next performance gate should reduce or retain the large ingress and CQL buffers without premature reuse, bound the normal live publisher by retained bytes/items, and then measure actual futures-option quote distribution and GC pauses under a supervised feed run. No live Databento quote data was required for this isolated capacity comparison.

**Subsequent work:** The [512-buffer reuse implementation](Tick-Quote-512-Buffer-Reuse-Implementation-Results-v1.0.md) made the normal live publisher a bounded waiting channel and introduced privately retained producer, decoder, and CQL buffers. The historical measurements and decision above refer to the earlier build.
