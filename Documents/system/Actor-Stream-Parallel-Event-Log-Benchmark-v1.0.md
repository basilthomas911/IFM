# Actor Stream-Parallel Event-Log Benchmark

**Measured:** 2026-09-11
**Runtime:** .NET 10.0.10, concurrent workstation GC
**Database:** local dedicated PostgreSQL event-source test database
**Production writer:** `SequentialEventLogAppender`, LZ4 enabled

## Production scheduling path

The production `ActorSupervisor` initializes `ActorThreadPoolV2` with twice the logical processor count. The scheduler
routes each message to an `ActorThreadQueueV2` keyed by `ActorThreadId`. An atomic scheduling flag allows only one worker
to process a particular actor thread ID at a time, preserving FIFO state transitions and event-stream version ordering.
Different actor thread IDs can be processed by different workers concurrently.

The application registers one singleton `IEventSourceActorDbContext`. Its production sequential appender is safe for
concurrent callers and obtains pooled connections from the process-wide Npgsql data source. Consequently, concurrent
actor thread IDs reach PostgreSQL as independent event streams without adding a second persistence scheduler.

## Matched database benchmark

The benchmark uses 64 independent streams, one event per command, explicit expected stream versions, the same payload,
the same appender instance, and the same PostgreSQL database in both cases. The only workload difference is dispatch:

- **Before** awaits each independent-stream append serially, matching a single serialized mailbox.
- **After** submits all independent-stream appends concurrently, matching keyed actor-thread scheduling.

The database benchmark deliberately invokes the appender at the persistence boundary so NATS transport, domain logic,
and UI work do not obscure the event-log scheduling effect. The active actor scheduler is verified separately by its
focused unit tests.

## Results

| Configuration | Dispatch | Mean for 64 commands | Aggregate TPS | Managed allocation | Relative time |
|---|---|---:|---:|---:|---:|
| Sequential, LZ4 (production) | Serial | 342.467 ms | 186.88 | 1,153.89 KB | 1.00 |
| Sequential, LZ4 (production) | Keyed parallel | 15.981 ms | 4,004.76 | 1,185.79 KB | 0.05 |
| Sequential, no compression | Serial | 359.684 ms | 177.93 | 1,330.51 KB | 1.00 |
| Sequential, no compression | Keyed parallel | 16.056 ms | 3,986.05 | 1,365.57 KB | 0.04 |
| Binary copy, LZ4 | Serial | 3,999.020 ms | 16.00 | 979.82 KB | 1.00 |
| Binary copy, LZ4 | Keyed parallel | 59.650 ms | 1,072.93 | 357.35 KB | 0.01 |
| Binary copy, no compression | Serial | 3,997.010 ms | 16.01 | 1,160.59 KB | 1.00 |
| Binary copy, no compression | Keyed parallel | 44.697 ms | 1,431.86 | 537.12 KB | 0.01 |

For the production sequential/LZ4 writer, keyed parallel dispatch completed the same 64 committed commands about
**21.4 times faster** and raised aggregate throughput from approximately **187 TPS to 4,005 TPS**. Managed allocation
increased by 31.90 KB for the 64-command burst, approximately 2.8%. Each stream retained its own expected-version
check, and every append waited for PostgreSQL commit confirmation.

The result is aggregate throughput across independent actor streams. It does not change the approximately 200 TPS
limit for dependent commands on one stream, which remain deliberately sequential.

## Verification

- 31 focused `ActorThreadPoolV2` and `ActorThreadQueueV2` tests passed. These cover concurrency across different IDs,
  FIFO and single-consumer execution within one ID, admission, draining, retirement, and failure recovery.
- Event-source integration tests exercise concurrent single-event commits through the sequential/LZ4 appender and
  verify stream version 1 for every independent stream.
- The benchmark project builds in Release with no warnings or errors.
- The BenchmarkDotNet host exited and the benchmark streams were removed during global cleanup.

Raw BenchmarkDotNet CSV, Markdown, and HTML reports are under:

`BenchmarkDotNet.Artifacts/event-log-stream-scheduling-v1/results/`
