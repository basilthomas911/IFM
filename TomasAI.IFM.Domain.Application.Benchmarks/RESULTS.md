# Domain.Application actor optimization results

Baseline source: the pre-optimization implementations reconstructed from repository commit `0a44bba^`. Current source includes commit `0a44bba` plus the later restoration of explicit repository awaits in `1a41397`.

Both implementations run in the same BenchmarkDotNet process, isolating the actor CPU paths from storage and NATS latency. Measurements used BenchmarkDotNet 0.15.8, .NET 10.0.10, X64 RyuJIT, Concurrent Workstation GC, three warmups, and the same Windows/AMD Ryzen Threadripper 1950X host.

## Before and after

| Benchmark | Input | Before mean | After mean | Latency reduction | Before allocated | After allocated |
|---|---:|---:|---:|---:|---:|---:|
| State replay | 32 events | 170.01 ns | 128.22 ns | 24.6% | 152 B | 152 B |
| State replay | 256 events | 1,190.09 ns | 699.78 ns | 41.2% | 152 B | 152 B |
| State replay | 2,048 events | 8,207.04 ns | 5,899.57 ns | 28.1% | 152 B | 152 B |
| Two-verb command dispatch | Per command | 11.39 ns | 4.41 ns | 61.3% | 0 B | 0 B |
| Valid command-ID validation | Per command | 8.32 ns | 0.42 ns | 95.0% | 32 B | 0 B |

The state benchmark's fixed 152-byte allocation is state/base collection construction, not per-event growth. No thread-pool work or monitor contention was recorded in either replay implementation. The direct validation result is close to the timer's practical resolution and should be interpreted primarily as removal of the 32-byte list allocation, not as a reason for further sub-nanosecond tuning.

## Scope limits

- Command-log persistence is not represented by a synthetic delay: the important change was moving its real asynchronous storage operation out of synchronous parsing and awaiting it in the actor pipeline.
- Repository, event-source database, NATS, and actor-mailbox latency are excluded.
- The Application domain processes lifecycle messages, not a continuous high-throughput feed. These measurements protect against regressions but do not justify additional micro-optimization without production evidence.

## Reproduce

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.Application.Benchmarks -- --filter '*' --join
```
