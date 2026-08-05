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
