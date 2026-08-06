# Domain.Fund Benchmark Results

BenchmarkDotNet 0.15.8, .NET 10.0.10, x64 RyuJIT, AMD Ryzen Threadripper 1950X.
Each job used three warmup iterations and eight measurement iterations with both memory and threading diagnostics.

The baseline was captured from production `Domain.Fund` source at commit `0a44bba`. The optimized run used the identical benchmark source, runtime, parameters, and machine.

## Collection lookup results

| Operation | Items | Before | After | Improvement | Allocation before | Allocation after |
|---|---:|---:|---:|---:|---:|---:|
| Order lookup | 32 | 39.72 ns | 6.743 ns | 83.0% | 88 B | 0 B |
| Order exists | 32 | 87.64 ns | 4.898 ns | 94.4% | 24 B | 0 B |
| Trade lookup | 32 | 158.12 ns | 4.872 ns | 96.9% | 160 B | 0 B |
| Trade exists | 32 | 88.97 ns | 4.889 ns | 94.5% | 24 B | 0 B |
| Transaction exists | 32 | 38.77 ns | 5.595 ns | 85.6% | 24 B | 0 B |
| Transaction lookup | 32 | 39.33 ns | 7.845 ns | 80.1% | 168 B | 0 B |
| Order lookup | 256 | 237.26 ns | 5.947 ns | 97.5% | 88 B | 0 B |
| Order exists | 256 | 589.40 ns | 4.870 ns | 99.2% | 24 B | 0 B |
| Trade lookup | 256 | 865.67 ns | 4.812 ns | 99.4% | 160 B | 0 B |
| Trade exists | 256 | 593.74 ns | 4.890 ns | 99.2% | 24 B | 0 B |
| Transaction exists | 256 | 241.73 ns | 5.567 ns | 97.7% | 24 B | 0 B |
| Transaction lookup | 256 | 37.54 ns | 7.843 ns | 79.1% | 168 B | 0 B |

No benchmark recorded lock contention or thread-pool work items. Dictionary-backed indexes trade modest retained memory and slightly more replay/write work for constant-time, allocation-free actor-state reads. State replay writes each item once, while commands can perform several existence and lookup operations per event.

These microbenchmarks intentionally exclude PostgreSQL, NATS, logging, and serialization latency so collection CPU and GC costs remain visible. Integration tests cover the complete actor pipeline separately.

## 2026-08-05 actor hot-path optimization

The following before/after implementations were measured together using BenchmarkDotNet 0.15.8, .NET 10.0.10, x64 RyuJIT, three warmups, and eight measurement iterations on the same machine described above.

### Sharpe ratio calculation

| Daily balances | Before: list + MathNet | After: single-pass moments | Improvement | Allocation before | Allocation after |
|---:|---:|---:|---:|---:|---:|
| 32 | 484.6 ns | 338.1 ns | 30.2% | 608 B | 0 B |
| 256 | 3,523.9 ns | 2,658.8 ns | 24.6% | 4,264 B | 0 B |
| 2,048 | 26,684.1 ns | 21,200.6 ns | 20.5% | 33,008 B | 0 B |

The optimized calculation explicitly returns zero for an undefined return caused by a zero previous balance. It uses sample variance, matching the prior estimator semantics, without materializing a daily-return collection.

### Query I/O fan-out

| Operation | Before | After | Improvement | Allocation before | Allocation after |
|---|---:|---:|---:|---:|---:|
| P&L-style independent reads | 108.38 ms | 15.53 ms | 85.7% | 2.04 KB | 2.03 KB |

This benchmark uses controlled `Task.Delay(1)` storage operations so it measures async critical-path composition rather than database performance. The before path performs seven sequential reads, including the duplicate Sharpe read; the after path performs six independent reads concurrently and consumes daily balances once. Windows timer resolution makes the absolute delay larger than one millisecond, but both implementations use the same simulated operation.

### Batch materialization

| Transactions | Before: iterator + collection expression | After: exact array loop | Improvement | Allocation before | Allocation after |
|---:|---:|---:|---:|---:|---:|
| 32 | 622.5 ns | 221.0 ns | 64.5% | 632 B | 536 B |
| 256 | 4,653.6 ns | 1,584.0 ns | 66.0% | 4,216 B | 4,120 B |

### Indexed state probe

| Items | Before: contains + indexer | After: `TryGetValue` | Improvement | Allocation |
|---:|---:|---:|---:|---:|
| 32 | 6.043 ns | 3.923 ns | 35.1% | 0 B |
| 256 | 5.999 ns | 3.908 ns | 34.9% | 0 B |
| 2,048 | 5.982 ns | 3.934 ns | 34.2% | 0 B |

No measured benchmark recorded monitor lock contention. Actor state remains mailbox-owned; the implementation does not add locks or `Task.Run` work.
