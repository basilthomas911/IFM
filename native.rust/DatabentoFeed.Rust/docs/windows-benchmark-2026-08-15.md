# Windows C++/Rust native feed benchmark — 2026-08-15

This is the first Windows comparison of the canonical C++ implementation and the Rust
implementation of ABI v1. It is a development-workstation baseline, not a production
capacity claim.

## Workload

- Windows 10 22H2, AMD Ryzen Threadripper 1950X (16 cores/32 logical processors)
- .NET 10.0.10, BenchmarkDotNet 0.15.8, Release native builds
- One complete synthetic ticker-feed lifecycle per sample
- 10,000 mixed quote/trade/MBO records across two instruments
- P/Invoke create, subscribe, read-buffer allocation, start, mapping copy, ready, wait,
  batch drain, statistics, stop, buffer free, and destroy
- One warmup and five measured samples, in-process toolchain
- Reported time is normalized per record; managed allocations are also normalized per
  record and rounded by BenchmarkDotNet

| Implementation | Batch size | Mean per record | Median per record | Managed allocation |
|---|---:|---:|---:|---:|
| C++ | 64 | 300.0 ns | 369.6 ns | none detected |
| Rust | 64 | 374.2 ns | 432.1 ns | none detected |
| C++ | 512 | 151.6 ns | 149.7 ns | none detected |
| Rust | 512 | 288.5 ns | 281.5 ns | none detected |
| C++ | 4096 | 345.5 ns | 364.1 ns | none detected |
| Rust | 4096 | 349.0 ns | 354.3 ns | none detected |

The 4096-record result is effectively equal in this run. C++ was faster at 64 and 512,
with the largest observed difference at 512. Several confidence intervals are wide because
native producer scheduling dominates such a short lifecycle. These measurements are useful
as a regression baseline, but they do not justify selecting an implementation or predicting
live-feed capacity. Before changing the default implementation, rerun with processor
affinity, a longer sustained feed, and production-rate DBN input.

## Isolated native producer

The second benchmark removes feed creation, subscription, read-buffer allocation, producer
thread creation, draining, and shutdown from the timed section. The producer is created and
parked during iteration setup. The timed method releases it to publish 1,000,000 mixed
synthetic records into a ring large enough to hold the complete workload, then observes its
terminal state at 1 ms intervals. This measures record construction, timestamping, atomic
publication, statistics, and ring writes. It does not measure P/Invoke draining.

The original result and five successive optimization rounds used the same benchmark,
workload, and Release profiles. Each row is an independent C++/Rust run, so the comparison
column uses the C++ result from that same row. Negative differences mean Rust was faster.

| Round | Rust change | C++ mean | Rust mean | Rust vs C++ | Rust change from prior round |
|---|---|---:|---:|---:|---:|
| Baseline | Original implementation | 62.780 ns | 199.970 ns | 218.5% slower (3.18x) | - |
| 1 | Snapshot mappings and remove a mutex plus two `Vec` clones per record | 65.153 ns | 68.401 ns | 5.0% slower | 65.8% faster |
| 2 | Replace `OnceLock<Instant>`/`u128` timestamp conversion with a per-feed Windows QPC clock | 62.978 ns | 51.246 ns | 18.6% faster | 25.1% faster |
| 3 | Precompute each mapping's record-kind sequence and simplify record construction | 65.842 ns | 49.513 ns | 24.8% faster | 3.4% faster |
| 4 | Use single-producer monotonic statistic stores instead of redundant atomic RMW operations | 64.716 ns | 34.381 ns | 46.9% faster | 30.6% faster |
| 5 | Remove runtime mapping division, specialize record-kind selection, and inline hot functions | 65.140 ns | 33.288 ns | 48.9% faster | 3.2% faster |

The final Rust mean is approximately 30.0 million records/second, compared with 15.4
million records/second for C++ in the same run. Rust improved by 83.4% from its own
199.970 ns baseline. The requirement that Rust not trail C++ by more than 1% is therefore
met with substantial headroom; the implementations are not within 1% in the absolute
sense because Rust is now faster, and the implementation is not intentionally throttled
to manufacture equal results.

The largest problems were implementation-specific rather than inherent Rust overhead:
per-record heap cloning, an unnecessarily expensive timestamp conversion, and atomic
read/modify/write operations for statistics written by only one producer. The ABI and
consumer path remained unchanged. Both native libraries reported no managed allocation
in the timed method.

The workstation could not switch to BenchmarkDotNet's high-performance power plan, and
the C++ samples showed scheduling variance. These results establish that the earlier 3.18x
Rust deficit is removed; they are not a substitute for the deferred sustained live-market
benchmark.

### Additional optimization rounds 6-10

After round 5, the producer workload was increased from 1,000,000 to 4,000,000 records per
iteration. This keeps the same benchmark and ABI workflow while raising iteration duration
above BenchmarkDotNet's recommended 100 ms minimum. A fresh round-5 baseline was measured
before changing the implementation. "Rejected" means the experiment was reverted before
the following round; comparisons against the retained result therefore remain cumulative.

| Round | Experiment | C++ mean | Rust mean | Rust vs C++ | Decision |
|---|---|---:|---:|---:|---|
| Stabilized baseline | Retained round-5 implementation at 4 million records | 54.15 ns | 31.94 ns | 41.0% faster | Baseline |
| 6 | Derive produced count from the monotonic ring head; remove duplicate per-record atomic store | 54.15 ns | 30.50 ns | 43.7% faster | Retained; 4.5% faster than baseline |
| 7 | Maintain per-mapping record-kind cursors instead of calculating sequence modulus | 54.04 ns | 29.81 ns | 44.8% faster | Retained; 2.3% faster than round 6 |
| 8 | Maintain a second mutable cursor for quote/trade/MBO size cycles | 55.02 ns | 30.98 ns | 43.7% faster | Rejected; 3.9% slower than round 7 |
| 9 | Increment synthetic price instead of calculating it independently from sequence | 54.31 ns | 30.75 ns | 43.4% faster | Rejected; 3.2% slower than round 7 |
| 10 | Compile the Windows DLL for `x86-64-v3` instead of portable x86-64 | 55.09 ns | 30.29 ns | 45.0% faster | Rejected; 1.6% slower than round 7 and less portable |

The final retained Rust implementation publishes approximately 33.5 million synthetic
records/second, compared with 18.5 million records/second for C++ in the corresponding
round-7 run. Rounds 6 and 7 improve Rust by 6.7% over the stabilized round-5 result. From
the original 199.970 ns implementation to the retained 29.81 ns implementation, Rust's
time per record fell by 85.1%, representing approximately 6.7 times the original Rust
throughput.

The rejected rounds are important results: simple stateful counters introduced dependency
chains, and architecture-specific code generation did not outperform LLVM's portable
release output. The shipped DLL therefore remains portable and contains only the two
additional changes with demonstrated value.

## Isolated P/Invoke wait and drain

Iteration setup creates the feed and allows the native producer to fill and close a
1,000,000-record ring. The timed method then repeatedly calls the canonical `dbf_feed_wait`
and `dbf_feed_read_batch64` functions until the ring is empty. Feed construction and native
production are outside the timed section.

| Implementation | Batch size | Mean per record | Managed allocation |
|---|---:|---:|---:|
| C++ | 64 | 6.060 ns | none detected |
| Rust | 64 | 5.663 ns | none detected |
| C++ | 512 | 5.684 ns | none detected |
| Rust | 512 | 5.708 ns | none detected |
| C++ | 4096 | 5.952 ns | none detected |
| Rust | 4096 | 5.600 ns | none detected |

These confidence intervals overlap. The two implementations should be treated as equivalent
on the managed/native wait-and-drain path. Both drain far faster than either implementation
currently publishes synthetic records. The unchanged ABI, P/Invoke delegates, wait logic,
and ring-copy implementation therefore do not explain Rust's producer deficit.

## Deferred live-market benchmark

The sustained live Databento comparison remains deferred until the market is open. It must
measure quote/trade/MBO traffic separately, records per second, end-to-end P50/P95/P99/max
latency, CPU, native memory, ring high-water mark, overruns, wakeup frequency, reconnects,
slow-reader behavior, and processor residency over a materially long session.

Run the benchmark from `native.rust/DatabentoFeed.Rust/dotnet`:

```powershell
$env:DBF_CPP_DLL = '<repo>\native\DatabentoFeed.Native\out\build\Release\databento_feed_native.dll'
$env:DBF_RUST_DLL = '<repo>\native.rust\DatabentoFeed.Rust\out\build\Release\databento_feed_native.dll'
dotnet run --project .\DatabentoFeed.Native.Benchmarks -c Release
```

Use `--filter '*NativeProducerBenchmarks*'` or
`--filter '*PInvokeDrainBenchmarks*'` to run either isolated suite.

BenchmarkDotNet writes the full CSV, HTML, log, and GitHub Markdown artifacts under the
working directory's `BenchmarkDotNet.Artifacts` folder. That generated folder is ignored;
this document is the reviewed, tracked baseline.
