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
