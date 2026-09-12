# PostgreSQL Event Log Dual Appender Benchmark Results

**Version:** 1.0
**Measured:** 2026-09-11
**Runtime:** .NET 10.0.10, concurrent workstation GC
**Database:** local PostgreSQL test event store, `synchronous_commit=on` default

## Decision

Both appenders now accept the same `UseLz4Compression` Boolean. Production is configured as
`WriteMode=Sequential` and `UseLz4Compression=true`. Binary COPY remains available for integration and canary runs,
but it is not the production default because the concurrent single-event workload did not meet the performance gate.

The reader always uses the LZ4-aware MessagePack decoder. Compatibility tests prove that it reads uncompressed rows,
compressed rows, and streams that contain both. Switching compression therefore does not require a schema migration
or rewriting historical events.

## Durable append and replay matrix

The workload appends one command containing 1, 64, or 256 events, then reads and deserializes the latest matching
event count. Times are BenchmarkDotNet means and allocations are managed bytes per operation.

| Writer | LZ4 | Events | Write | Write allocation | Read | Read allocation |
|---|---:|---:|---:|---:|---:|---:|
| Sequential | Off | 1 | 6.393 ms | 21.07 KB | 1.621 ms | 19.74 KB |
| Sequential | On | 1 | 6.241 ms | 19.91 KB | 1.193 ms | 18.09 KB |
| Sequential | Off | 64 | 74.673 ms | 658.10 KB | 2.761 ms | 296.98 KB |
| Sequential | On | 64 | 45.420 ms | 458.39 KB | 1.571 ms | 227.93 KB |
| Sequential | Off | 256 | 189.520 ms | 2,562.94 KB | 4.178 ms | 1,129.77 KB |
| Sequential | On | 256 | 175.269 ms | 1,787.20 KB | 2.601 ms | 871.92 KB |
| Binary COPY | Off | 1 | 58.731 ms | 22.80 KB | 1.068 ms | 18.02 KB |
| Binary COPY | On | 1 | 55.241 ms | 20.06 KB | 1.159 ms | 15.98 KB |
| Binary COPY | Off | 64 | 22.724 ms | 473.05 KB | 1.358 ms | 289.19 KB |
| Binary COPY | On | 64 | 59.823 ms | 291.23 KB | 1.630 ms | 229.10 KB |
| Binary COPY | Off | 256 | 55.086 ms | 1,844.73 KB | 2.421 ms | 1,128.58 KB |
| Binary COPY | On | 256 | 60.445 ms | 1,122.52 KB | 2.494 ms | 871.59 KB |

For the sequential writer, LZ4 reduced allocation by about 30% for 64- and 256-event writes and by about 23% for the
matching reads. It also improved the measured 64-event write by 39% and the 256-event write by 8%. For binary COPY,
LZ4 reduced write allocation by 38-39% for the larger batches, while the measured write time varied by workload.

## Concurrent actor-command workload

This workload submits 64 independent one-event commands concurrently. It measures the intended cross-command
micro-batching case rather than placing 64 events in one command.

| Writer | LZ4 | Mean | Median | Allocation |
|---|---:|---:|---:|---:|
| Sequential | Off | 23.06 ms | 22.22 ms | 1,254.85 KB |
| Sequential | On | 24.74 ms | 23.32 ms | 1,079.29 KB |
| Binary COPY | Off | 47.48 ms | 61.64 ms | 547.02 KB |
| Binary COPY | On | 67.73 ms | 68.69 ms | 368.78 KB |

Binary COPY cut managed allocation by 56% without LZ4 and 66% with LZ4 compared with the corresponding sequential
run. Its acknowledgement time was slower on this host. The uncompressed binary result was also bimodal and had a very
wide confidence interval, so it is not suitable as rollout evidence.

## Interpretation and next gate

LZ4 is beneficial for production memory pressure and generally improves replay time. It remains enabled in production.
The retained sequential writer is currently the safer production choice because it has much lower single-event latency
and won the representative concurrent-command timing test.

The binary implementation should be profiled around batch-age timing, PostgreSQL stream locking, sequence reservation,
and COPY startup before its canary gate is approved. The current BenchmarkDotNet jobs intentionally use few iterations
to keep database mutation bounded; BenchmarkDotNet reports short-iteration and wide-confidence warnings. A rollout
decision needs a longer out-of-process run on the target PostgreSQL host, plus p95/p99 acknowledgement latency and WAL,
CPU, I/O, queue-depth, and backpressure evidence.

## Verification evidence

- Event-source integration tests: 42 passed.
- MessagePack codec compatibility tests: 6 passed.
- Storage, benchmark, and API server Release builds: succeeded with zero warnings and zero errors.
- Raw matrix: `BenchmarkDotNet.Artifacts/event-log-dual-appender-v2/results/`.
- Raw concurrent workload: `BenchmarkDotNet.Artifacts/event-log-concurrent-v1/results/`.
