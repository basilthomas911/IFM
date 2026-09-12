# PostgreSQL Event Log Sequential Transaction-Rate Benchmark

**Measured:** 2026-09-11
**Runtime:** .NET 10.0.10, concurrent workstation GC
**Database:** local dedicated PostgreSQL event-source test database
**Writer:** `SequentialEventLogAppender`, LZ4 enabled

## Workload

Each operation was a real event-log command containing one event and one PostgreSQL transaction. Commands ran
sequentially against one event stream with an explicit expected stream version. Every append opened a pooled logical
Npgsql connection, updated the stream version, inserted the event payload, and waited for the transaction commit before
the next command began.

The benchmark executed complete runs of 1,000, 10,000, and 100,000 transactions. BenchmarkDotNet used one timed
iteration because repeating the 100,000-transaction workload would add more than nine minutes per iteration. The JIT
probe executed one representative transaction and was excluded from the timed count.

## Results

| Transactions | Elapsed | Transactions/second | Mean per transaction | Managed allocation | Allocation/transaction | GC collections |
|---:|---:|---:|---:|---:|---:|---:|
| 1,000 | 5.748 s | 173.98 | 5.748 ms | 11.91 MB | 12.20 KB | Gen0 2, Gen1 0, Gen2 0 |
| 10,000 | 56.235 s | 177.83 | 5.623 ms | 119.08 MB | 12.19 KB | Gen0 29, Gen1 2, Gen2 0 |
| 100,000 | 562.501 s | 177.78 | 5.625 ms | 1,190.46 MB | 12.19 KB | Gen0 298, Gen1 25, Gen2 2 |

## Interpretation

The sequential transaction rate stabilized at approximately **178 committed one-event transactions per second**. The
10,000 and 100,000 results differ by less than 0.03%, which indicates that throughput remained steady throughout the
nine-minute sustained run. There were no append, serialization, concurrency, or PostgreSQL failures.

The writer allocated about **12.2 KB per committed event**. Across 100,000 commands that produced approximately
1.16 GiB of managed allocation and caused 298 Gen0, 25 Gen1, and 2 Gen2 collections. The stable transaction rate shows
that those collections did not cause visible aggregate throughput decay, although latency percentiles require a
separate per-command latency benchmark.

Database cleanup occurred after BenchmarkDotNet stopped timing. Removing the dedicated 100,000-row test stream was
slow, but it did not affect the reported transaction rate. Event-log production storage is append-oriented; deletion
performance should be assessed separately if stream-retention or purge processing is introduced.

## Strong-consistency optimization

The sequential writer now uses a single PostgreSQL autocommit statement when a command contains exactly one event and
does not require a durable projection marker. The statement atomically advances the stream version and inserts the
event. The append does not return until PostgreSQL confirms completion of the implicit transaction. Multi-event
commands and commands with required projections continue to use an explicit transaction so their existing atomicity is
unchanged.

The writer also enables bounded Npgsql automatic statement preparation (`Max Auto Prepare = 16`, minimum two uses)
when the connection string has not already specified a preparation policy. It continues to obtain connections from the
process-wide cached Npgsql data source and preserves the existing credential-resolution path.

### Optimized sustained result

| Transactions | Baseline elapsed | Optimized elapsed | Baseline TPS | Optimized TPS | Throughput change | Optimized allocation | Optimized GC |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 10,000 | 56.235 s | 50.037 s | 177.83 | 199.85 | +12.4% | 142.34 MB | Gen0 35, Gen1 2, Gen2 0 |

The optimized path raises the same-stream sequential rate to approximately **200 committed transactions per second**.
Its managed allocation was about **14.6 KB per transaction**, roughly 19.5% above the baseline, so this change improves
database latency at the cost of more managed allocation in the current Npgsql autocommit path. This allocation result
is retained as a constraint for future tuning rather than hidden by the throughput gain.

Independent streams can make progress concurrently without weakening ordering within any one stream. A separate run
of 64 simultaneous one-event commands measured **19.941 ms** for the sequential LZ4 writer, or approximately **3,209
committed transactions per second** in aggregate. This is the applicable scale-out path for independent actors; commands
that depend on the immediately preceding state of the same actor remain ordered and await each commit.

## Artifacts

The raw BenchmarkDotNet CSV, HTML, and Markdown reports are under:

`BenchmarkDotNet.Artifacts/event-log-sequential-rate-v3/results/`

Optimized sustained and concurrent reports are under:

`BenchmarkDotNet.Artifacts/event-log-sequential-rate-optimized-v2/results/`

`BenchmarkDotNet.Artifacts/event-log-concurrent-v2/results/`
