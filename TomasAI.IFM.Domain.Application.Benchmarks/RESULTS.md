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

## SWO-06 Tranche E projector benchmarks

Tranche E used BenchmarkDotNet 0.15.8, .NET 10 Release, MemoryDiagnoser, two warmups, and five measured iterations on
the same Windows host. A 256-stage batch measured the final low-cardinality metrics implementation:

| Metrics path | Mean per batch | Approximate mean per stage | Allocated |
| --- | ---: | ---: | ---: |
| Meter dormant | 226.410 ns | 0.884 ns | 0 B |
| Meter observed | 14.9913 us | 58.56 ns | 0 B |
| Incremental observed cost | 14.7649 us | 57.68 ns | 0 B |

Replacing enum `ToString()` stage tags with bounded string literals removed the measured per-stage allocation. The
dormant path checks instrument enablement and does not read the elapsed-time clock.

The CPU-only outbox envelope benchmark serializes and deserializes 256 concrete messages per operation:

| Metrics listener | Mean per batch | Approximate per message | Allocated per batch |
| --- | ---: | ---: | ---: |
| Disabled | 1.4163 ms | 5.532 us | 546,744 B |
| Enabled | 1.4408 ms | 5.628 us | 546,744 B |

The disabled-listener result is approximately 180,750 messages/second on one benchmark thread and 2,136 allocated
bytes/message. This isolates MessagePack serialization/deserialization; it excludes PostgreSQL, NATS, network,
acknowledgement, dispatcher scheduling, and consumer cost and is not an end-to-end throughput estimate.

The same-host recovery rerun compared the legacy full-set/state-N+1 call shape with 256-row joined keyset pages and
eight independent-stream lanes:

| Pending events | Current mean | Bounded mean | Current allocated | Bounded allocated |
| ---: | ---: | ---: | ---: | ---: |
| 1,000 | 50.33 ms | 11.90 ms | 6.52 MB | 6.68 MB |
| 10,000 | 117.22 ms | 141.54 ms | 65.16 MB | 66.93 MB |
| 100,000 | 1,040.23 ms | 291.27 ms | 651.58 MB | 669.95 MB |

The bounded path is 76.4% faster at 1,000 and 72.0% faster at 100,000. Its 10,000 result is 20.7% slower but has a
42.6 ms standard deviation and is inconclusive. MemoryDiagnoser reports cumulative fixture allocation: both
benchmarks construct/deserialise the whole synthetic event population. It does not report the live coordinator's peak
retained inventory, which is bounded to one page plus active stream groups. PostgreSQL round trips, NATS publication,
readiness latency, and peak live memory require the production-like canary.

## Reproduce

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.Application.Benchmarks -- --filter '*' --join

dotnet run -c Release --project TomasAI.IFM.Domain.Application.Benchmarks -- --filter '*EventProjectorRecoveryBaselineBenchmarks*'

dotnet run -c Release --project TomasAI.IFM.Domain.Application.Benchmarks -- --filter '*EventProjectorTrancheEBenchmarks*'
```
