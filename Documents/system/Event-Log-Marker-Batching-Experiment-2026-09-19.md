# Event-log marker-batching experiment results

Date: 2026-09-19
Decision: retain the candidate for production qualification. Strong local benchmark improvement for multi-marker transactions; do not replace the event-log schema. Production routing remains unchanged.

## Change evaluated

The existing binary COPY appender writes each required projection marker through an individually awaited SQL statement. The candidate collects the requirements for its physical transaction and inserts them with one parameterized, set-based statement.

The statement preserves:

- Event identity, stream version, event name, actor, and projector mapping.
- Processing, AlreadyCompleted, and Superseded checkpoint outcomes.
- ApplyProjection and PublishProcessingEvent initial stages.
- Atomic command audit, events, stream counters, and required markers.
- Conflict rejection and rollback when inserted count does not equal requested count.
- The existing financial fence and all incoming event-identity foreign keys.

No-marker batches issue no marker statement. The candidate remains gated by the internal benchmark layout; public appender constructors and application configuration still select the existing marker path. Three new observational marker metrics apply to both strategies.

Batched markers share one statement creation/update timestamp and one statement snapshot of checkpoints. Their ordering remains stream-version based. Concurrent checkpoint advancement requires separate qualification before production activation.

## Controlled comparison

All three variants retained the original four indexes and text timestamp representation:

1. Baseline: event_log, existing row-at-a-time marker writer.
2. V2Control: event_log_v2, same schema and existing writer.
3. V2BatchedMarkers: event_log_v2, same schema, set-based marker writer.

The earlier index-consolidation change was not included. All timed workloads used financial-stream names, LZ4, the same deterministic payload, and full durable command/event transactions. Matched no-marker cases separate a marker-specific benefit from general run variation.

Five workloads x three variants x six rotated repetitions = 90 successful samples:

- 78,912 measured commands.
- 369,216 measured events.
- 32 commands per stream per sample, after 8 seed commands per stream.
- Dedicated local PostgreSQL 17.2, fsync/synchronous_commit/full_page_writes enabled.
- .NET 10.0.10 Windows client, workstation GC.
- No builds or regression suites ran concurrently with the timed comparison.

A preceding 27-sample smoke run also passed. Its timings are not mixed into the main comparison.

## Results

Throughput and p99 columns are medians across six samples. The ratio column is the median of paired candidate/baseline ratios, not the ratio of independently computed medians.

| Workload | Baseline commands/s | Candidate commands/s | Paired throughput ratio | Baseline p99 ms | Candidate p99 ms |
|---|---:|---:|---:|---:|---:|
| One event, no markers, 64 streams | 2,077.2 | 2,081.3 | 1.001x | 37.90 | 35.07 |
| One event with marker, 64 streams | 597.3 | 2,060.5 | 3.443x | 165.19 | 35.62 |
| One event with marker, one stream | 59.9 | 61.3 | 0.982x | 29.37 | 29.84 |
| 64 events, no markers, four streams | 183.8 | 185.7 | 1.016x | 27.40 | 26.52 |
| 64 events with markers, four streams | 11.7 | 126.5 | 10.856x | 425.56 | 37.47 |

The marker-heavy improvement held against the identical-v2 control too: median paired ratios were 3.576x for concurrent one-event commands and 10.984x for 64-event commands.

For concurrent one-event marker writes, candidate/baseline gains ranged from +222.3% to +285.1%. For 64-event marker commands, they ranged from +912.8% to +1080.2%. These effects substantially exceed the control variation.

The one-stream case has only one marker per transaction, so there is no statement-count reduction. Its paired range (-8.8% to +11.3%) does not establish a benefit. No-marker changes also fall within observed variability.

## Measured mechanism and memory

| Marker workload | Rows/sample | Baseline statements/sample | Candidate statements/sample | Baseline cumulative marker ms | Candidate cumulative marker ms |
|---|---:|---:|---:|---:|---:|
| One event, 64 streams | 2,048 | 2,048 | 32 | 2,528.21 | 125.57 |
| One event, one stream | 32 | 32 | 32 | 47.04 | 52.79 |
| 64 events, four streams | 8,192 | 8,192 | 32 | 10,255.19 | 299.25 |

These are per-sample medians. Marker operation time includes client preparation and database await; it is not pure server CPU time. Successful statement rows can subsequently roll back, so these telemetry counters are not durable-commit counters. All measured samples completed successfully.

Allocated bytes per command:

- Concurrent one-event marker writes: 15,792 -> 10,683, a 32.4% reduction.
- 64-event marker commands: 584,008 -> 253,488, a 56.6% reduction.
- No-marker 64-event commands: effectively unchanged.

Across the six 64-event marker samples, observed Gen 0 collections fell from 81 to 41. Two Gen 2 collections occurred in baseline samples and zero in candidate samples, but that small count is not evidence that production Gen 2 collections have been eliminated. Longer profiling is required.

WAL per event was approximately unchanged, as expected: the same durable rows and indexes are still maintained. The speedup comes from doing fewer SQL operations and less per-command client work, not from skipping durability.

## Correctness and regression evidence

Every sample ran the same existing durable-write gates and additional untimed marker checks:

- Full event deserialization/replay and contiguous stream counters.
- Event/audit/marker counts and measured marker statement/row counts.
- Stale-version rejection and durable duplicate rejection, including a separate appender.
- Changed command-payload rejection.
- Financial fence rejection with complete rollback.
- Checkpoint absent/behind/equal/ahead outcomes.
- Both allowed initial stages and mixed material/nonmaterial events.
- Actor, event-name, revision, retry, replay, and timestamp-pair fields.
- Missing event-name join causing the complete append to roll back.
- Missing event ID, existing marker conflict, and duplicate input causing the marker set to roll back.
- Empty marker set without a database statement.

Regression tests after measurement:

| Group | Passed |
|---|---:|
| Benchmark layout/guard/empty-set tests | 12 |
| Existing dual-appender integration tests | 14 |
| Existing snapshot/projector recovery tests | 30 |
| Existing command-audit persistence tests | 8 |
| Total | 64 |

Release benchmark build: zero warnings, zero errors.

These tests establish the observed semantics, not an exhaustive concurrency/crash proof. The benchmark does not simulate ambiguous commit acknowledgment, process termination, or concurrently advancing projector checkpoints.

## Next step and limits

Follow-up: the [fault/concurrency qualification](Event-Log-Marker-Fault-Qualification-2026-09-19.md)
passed 36/36 checks for both writer variants, including backend disconnect, lost COMMIT acknowledgment,
independent-connection contention, and a checkpoint advance. The original measurement's limitations above
still describe that measurement. Independent-process/full-server crash and longer representative load gates remain open.

Qualify this writer optimization for activation on the existing event_log table. No event_log_v2 migration, index removal, or global-ID change is necessary to obtain this algorithmic benefit.

Before activation:

1. Exercise crash/disconnect and commit-acknowledgment ambiguity with durable reconciliation.
2. Test concurrent projector/checkpoint progress and multiple writer processes.
3. Run a longer representative dataset/workload on the target host, including sparse marker batches and queue pressure.
4. Define the controlled rollout switch and rollback behavior while preserving the existing row writer.
5. Decide from measurements whether single-marker transactions should retain the original statement path.

Results are local, closed-loop persistence-boundary measurements, not end-to-end actor/NATS/API/UI latency or guaranteed market-hours throughput. Synthetic freshly seeded datasets, warm replay, and missing server CPU/wait/disk profiling remain limitations. Every scenario requiring a marker is intentionally marker-heavy; benefits will depend on the actual production mix.

## Files and evidence

Implementation:

- TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/BatchedProjectionMarkerWriter.cs
- TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/BinaryCopyEventLogAppender.cs
- TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/EventLogSqlLayout.cs
- TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/EventLogAppenderSupport.cs
- TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/EventLogPersistenceMetrics.cs
- TomasAI.IFM.Framework.Storage.Benchmarks/EventLogV2Benchmark.cs
- TomasAI.IFM.Framework.Storage.Benchmarks/EventLogMarkerVerification.cs

Artifacts:

- [Full paired summary](../../BenchmarkDotNet.Artifacts/event-log-v2/marker-paired-20260919/summary.md)
- [90 raw samples](../../BenchmarkDotNet.Artifacts/event-log-v2/marker-paired-20260919/samples.json)
- [Environment/options/source hashes](../../BenchmarkDotNet.Artifacts/event-log-v2/marker-paired-20260919/metadata.json)
- [Layout/appender test results](../../BenchmarkDotNet.Artifacts/event-log-v2/marker-regression-20260919/marker-layout-appenders.trx)
- [Snapshot/recovery test results](../../BenchmarkDotNet.Artifacts/event-log-v2/marker-regression-20260919/marker-snapshot-recovery.trx)
- [Command-audit test results](../../BenchmarkDotNet.Artifacts/event-log-v2/marker-regression-20260919/marker-command-audit.trx)
- [Reproduction instructions](Event-Log-V2-Benchmark-Harness-and-Verification.md#marker-batching-experiment-extension-2026-09-19)

The development application database still has its four original event_log indexes and no event_log_v2 table. Application settings and running IFM services were not changed. Disposable benchmark/regression databases are removed after verification; raw evidence is retained. The dedicated benchmark container is stopped afterward.
