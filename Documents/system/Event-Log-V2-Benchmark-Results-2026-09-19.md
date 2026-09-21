# Event-log v2 benchmark: first comparison results

Date: 2026-09-19
Decision: retain the current application schema and routing. Keep the three-index candidate for further qualification; do not migrate based on this screening run.

## Completed work

Implemented an isolated paired benchmark using the real sequential and binary COPY appenders. Atomic scenarios include command-audit reservation, optimistic stream-version checks, stream counters, global event IDs, and durable commit acknowledgment. Marker scenarios additionally include atomic projector-state insertion and the existing financial fence.

Compared:

- Baseline: current event_log, four indexes.
- Identical control: event_log_v2, four indexes.
- Index candidate: event_log_v2, stream/version primary key plus unique event ID and command-ID indexes.

Each sample used a fresh database on a dedicated PostgreSQL 17.2 container, separate from ifm_db. The original application event_log was not renamed, migrated, or written by the benchmark. Its four indexes remain present, and public.event_log_v2 is absent from event-source-dev-db.

No sequence caching, timestamp migration, financial-fence replacement, marker batching, production writer switch, or removal of durable command deduplication was introduced.

## Measurement

Six workloads x three variants x six repetitions = 108 successful samples.

- Measured commands: 115,776.
- Measured events: 848,448, excluding seed and correctness-check events.
- 32 commands per stream per measured sample; 8 seed commands per stream.
- Rotated variant order, balanced across six repetitions.
- Deterministic 1,024-character synthetic payload, LZ4 enabled.
- PostgreSQL fsync/synchronous_commit/full_page_writes all enabled.
- .NET 10.0.10, workstation GC, Windows client, local Linux PostgreSQL container.

The table reports the median percentage change across paired repetitions, not the percentage difference between independently calculated medians.

| Workload | Index candidate vs baseline: median paired throughput change | Paired range | Assessment |
|---|---:|---:|---|
| Regular sequential, one event, 64 streams | +0.3% | -7.1% to +8.5% | No reliable improvement |
| Regular COPY, one event, 64 streams | +2.0% | -4.9% to +19.4% | Inconclusive; substantial control variation |
| Atomic audited COPY, one event, 64 streams | +0.0% | -2.6% to +5.9% | Essentially unchanged |
| Atomic audited COPY, one event, one stream | +0.2% | -2.0% to +1.9% | Essentially unchanged |
| Atomic audited COPY, 64 events with markers, four financial streams | +2.0% | +0.4% to +6.5% | Small effect; not sufficient for promotion |
| Atomic audited COPY, 256 events, four streams | +6.1% | +3.2% to +15.2% | Most promising candidate workload |

The identical control itself differed from baseline: its median paired change was +1.9% for the 256-event workload, with a range up to +10.3%. Comparing the index candidate directly with that control gives a smaller median paired gain of +3.45% for large batches, positive in all six pairs (+2.06% to +7.11%). This is promising screening evidence, not production-scale proof.

For the main one-event atomic workload, median throughput was 2,096.2 commands/s baseline versus 2,097.1 candidate. Median per-run p99 latency was 35.16 ms baseline versus 37.71 ms candidate: there is no broad latency win.

For 256-event batches, median throughput was 52.8 commands/s baseline versus 56.0 candidate, and median per-run p99 was 84.22 ms versus 79.30 ms. Commands/sec must not be confused with events/sec.

The candidate reduced observed WAL bytes/event across these workloads; for example, the large-batch medians were 1,764 versus 1,657 bytes/event. That is useful evidence for reduced index maintenance, not proof that indexes dominate overall latency.

Full warm replay remained correct. For example, the atomic one-event workload's median replay was 25.31 ms baseline versus 25.18 ms candidate; the large-batch workload was 375.07 ms versus 365.50 ms. These are warm full-stream scans, not actor startup or snapshot-plus-tail recovery timings.

## Verification

Every one of the 108 samples passed its applicable checks:

- Durable event/audit/marker counts.
- Contiguous per-stream versions and matching stream counters.
- Full payload deserialization and reconstructed state.
- Stale-version rollback without partial effects.
- Exact duplicate and changed-payload rejection on audited writes.
- Deduplication from a separately constructed appender.
- Financial-fence rejection with transaction rollback.

Focused tests after benchmarking:

| Test group | Passed |
|---|---:|
| Benchmark routing safety | 10 |
| Existing dual-appender integration tests | 14 |
| Existing snapshot/projector recovery tests | 30 |
| Existing command-audit persistence tests | 8 |
| Total | 62 |

Release build passed with zero warnings and zero errors.

Initial harness runs exposed credential setup and exception-classification assumptions; they were corrected before the recorded paired run. The current atomic writer reports both exact duplicates and changed-payload conflicts as a duplicate exception. Both remain rejected; no production behavior was changed to make a test pass.

The first regression invocation had three cleanup failures because its direct database connections lacked credentials. After correcting only the process-scoped test connection, all 62 tests passed. Both original and corrected test logs are retained.

The paired run preceded final constructor-reuse safety guards and balanced-default-repeat hardening; those affect setup, not the timed write implementation. The final build was additionally smoke-tested separately. Do not mix that small smoke run into the six-repetition performance comparison.

## Limitations and next decision

This is a closed-loop, persistence-boundary screening benchmark with small freshly seeded synthetic datasets. It does not establish production TPS, open-loop overload behavior, application actor performance, financial journal throughput, or crash safety. It does not model complete business state in memory; it keeps a per-stream version cursor and verifies state reconstruction.

Before production migration: larger target-host datasets, event-type/snapshot query plans, concurrent replay, multi-process and crash/ack-loss tests, financial cutover races, detailed writer-phase timing, and server CPU/wait/disk measurements are still required.

Recommended next experiment: batch required projection-marker SQL in one transaction and compare it against the unchanged atomic writer using matching marker/no-marker workloads. Retain the same financial guard and durable command reservation. The current per-event marker statements are a concrete source-code opportunity; this run did not isolate their causal cost from all other workload differences.

Do not remove EventVersion, financial fencing, or database deduplication to obtain a misleading faster number. A complete schema redesign is not justified by these results.

## Evidence

- [Raw paired summary](../../BenchmarkDotNet.Artifacts/event-log-v2/paired-20260919/summary.md)
- [All 108 samples](../../BenchmarkDotNet.Artifacts/event-log-v2/paired-20260919/samples.json)
- [Environment and source hashes](../../BenchmarkDotNet.Artifacts/event-log-v2/paired-20260919/metadata.json)
- [Corrected appender/safety tests](../../BenchmarkDotNet.Artifacts/event-log-v2/regression-20260919/eventlog-v2-layout-and-appenders-corrected-env.trx)
- [Snapshot/recovery tests](../../BenchmarkDotNet.Artifacts/event-log-v2/regression-20260919/eventlog-v2-snapshot-recovery.trx)
- [Command-audit tests](../../BenchmarkDotNet.Artifacts/event-log-v2/regression-20260919/eventlog-v2-command-audit.trx)
- [Reproduction and harness scope](Event-Log-V2-Benchmark-Harness-and-Verification.md)

Temporary successful benchmark databases and the regression fixture are disposable and were removed; raw results remain in the workspace. They can be regenerated, not restored from a benchmark backup. The dedicated benchmark container is stopped after verification; existing IFM services remain untouched.

