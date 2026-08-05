# Snapshot plus last typed range benchmark results

Run on 2026-08-05 with BenchmarkDotNet 0.15.8, .NET 10.0.10 X64 RyuJIT, concurrent workstation GC, and an AMD Ryzen Threadripper 1950X. Each case used three warmups and eight measured iterations. `LastNRange` was 60; the baseline replayed every matching event plus one interleaved nonmatching event after the snapshot, while the optimized case replayed the snapshot plus only the last 60 matching events.

| Matching events after snapshot | Before mean | After mean | Speedup | Before allocated | After allocated | Allocation reduction |
|---:|---:|---:|---:|---:|---:|---:|
| 256 | 7.468 ms | 990.3 us | 7.54x | 3,707.57 KB | 462.87 KB | 87.5% |
| 4,096 | 119.246 ms | 973.7 us | 122.47x | 59,213.60 KB | 462.87 KB | 99.2% |
| 32,768 | 958.839 ms | 998.2 us | 960.57x | 473,657.38 KB | 462.87 KB | 99.9% |

The optimized replay cost remains approximately constant once the stream contains at least 60 matching range events. In the largest case it also removed the baseline's measured Gen2 collections; the optimized case recorded Gen0 only.

These are managed deserialization and state-replay measurements after row selection. They intentionally exclude PostgreSQL and network latency. PostgreSQL selection, type filtering, snapshot boundaries, ascending output, missing-data behavior, and exact/fewer/more-than-N cases are covered by the integration suite.

Reproduce with:

```powershell
dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks -c Release -- --filter "*SnapshotRangeReplayBenchmarks*"
```

## RSI, MACD, ADX, and ATR variant matrix

This controlled comparison uses 4,096 matching events interleaved with 4,096 unrelated events as the before input and the database-selected last 60 typed events as the after input. RSI intraday also includes its `Started` snapshot in both inputs. The workload isolates managed deserialization and state reconstruction consistently across all eight variants.

| Variant | Before mean | After mean | Speedup | Before allocated | After allocated | Allocation reduction |
|---|---:|---:|---:|---:|---:|---:|
| RSI | 122.332 ms | 965.7 us | 126.68x | 59,213.60 KB | 462.87 KB | 99.2% |
| RSI daily | 123.115 ms | 953.9 us | 129.06x | 58,950.74 KB | 452.26 KB | 99.2% |
| MACD | 119.015 ms | 940.6 us | 126.53x | 58,541.08 KB | 446.22 KB | 99.2% |
| MACD daily | 111.749 ms | 912.6 us | 122.45x | 58,317.08 KB | 442.94 KB | 99.2% |
| ADX | 117.626 ms | 923.5 us | 127.37x | 58,540.79 KB | 445.93 KB | 99.2% |
| ADX daily | 113.549 ms | 891.3 us | 127.40x | 58,284.79 KB | 442.18 KB | 99.2% |
| ATR | 117.352 ms | 907.1 us | 129.37x | 58,511.95 KB | 445.48 KB | 99.2% |
| ATR daily | 115.578 ms | 880.3 us | 131.29x | 58,027.95 KB | 438.43 KB | 99.2% |

The old repository implementations were not identical: they combined snapshot-tail recovery, client-side range limiting, missing-snapshot fallbacks, and incomplete daily routing. This matrix deliberately normalizes the before workload to the unbounded mixed-history shape and the after workload to the typed SQL result. It demonstrates the managed scaling difference, not that every old repository replayed 8,192 rows on every invocation. Database round-trip and query-plan measurements remain separate.

Reproduce with:

```powershell
dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks -c Release -- --filter "*PeriodSignalReplayBenchmarks*"
```
