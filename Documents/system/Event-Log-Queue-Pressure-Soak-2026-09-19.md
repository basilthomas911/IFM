# Event-log bounded queue-pressure / short-soak results

Date: 2026-09-19

## Decision

**All eight one-minute main samples passed.** Batching sustained a median paired **2.433x throughput improvement** under bounded producer pressure, with lower tail latency and allocation per command. Every sample proved blocked admission before measurement, completed its load window, drained outstanding queue/admission work to zero, and passed durable replay and failure checks.

Retain the candidate for end-to-end actor/projector qualification and controlled rollout design. No production configuration, queue capacity, persistence writer, event-log schema, or application service was changed. The smaller queue is a test device, not a recommended production setting.

This is an eight-minute aggregate persistence-layer experiment across fresh fixtures, **not a continuous multi-hour soak, production leak proof, or default-capacity test**.

## Workload and fairness

- Four paired repetitions, alternating Baseline then V2BatchedMarkers / V2BatchedMarkers then Baseline.
- 60 seconds of demand per sample; final completion times including outstanding requests were 60.03–60.13 seconds.
- 64 bounded producers, one outstanding audited command per stream.
- Eight events per command; every eighth stream version requires a marker: one marker per command, 12.5% event density.
- Same deterministic 1,024-character synthetic payload, LZ4 compression, financial-stream naming, command audit and stream-concurrency checks.
- Queue capacity **8**, maximum physical transaction size 256 events, batch delay 1 ms; all four event-log indexes and the financial fence retained.
- Eight seed commands per stream, followed by one untimed pressure-barrier command per stream.
- Dedicated local PostgreSQL 17.2, fsync/synchronous_commit/full_page_writes=on/on/on; .NET 10.0.10 Windows client with workstation GC.

Each writer received the same bounded concurrency and time budget, not the same completed command count. Batching consequently completed more work. CPU, allocation and WAL comparisons must be normalized accordingly.

This run compares the original event_log path with the benchmark-only batched event_log_v2 path. There is no identical-v2 control in this run; previous experiments separately measured naming/control variation.

Measured totals, excluding seeding and verification:

- **389,792 commands**
- **3,118,336 events**
- **389,792 required markers**
- 113,568 commands on the original writer; 276,224 on the batched writer.

## Results

Values below are medians across four samples per writer. Speedup is the median of matched candidate/baseline ratios, not a ratio of unrelated best runs.

| Measure | Original writer | Batched markers |
|---|---:|---:|
| Commands/second | 472.8 | **1,152.5** |
| p99 command latency | 164.81 ms | **79.80 ms** |
| Allocated bytes/command | 41,708 | **36,633** |
| Client CPU milliseconds/command | 0.340 | **0.201** |
| WAL bytes/event | 2,134 | 2,166 |
| Gen 2 collections, total across four timed windows | 4 | 4 |

Paired throughput improvements ranged from **+143.0% to +144.5%**, with median **+143.3%**. Allocated bytes/command fell approximately **12.2%**, and client CPU milliseconds/command approximately **40.9%**.

Individual p99 values varied: 163.91–200.72 ms on the original writer, 69.36–124.40 ms on batching. These are local observed percentiles, not latency guarantees.

The optimization still writes all required durable data. This run does not show a WAL reduction; median WAL/event was approximately 1.5% higher for the candidate, with background/checkpoint activity included in the measurement.

The original writer issues one marker statement per command. At this load, the candidate usually combines 32 commands into one physical transaction and marker statement. Median per-sample marker statements were 28,416 original versus 2,162 candidate, despite the candidate processing substantially more commands. Normalized statement reduction is approximately 32-to-1.

## Backpressure and drain

Before every timed window, the harness locks all fixture stream rows and starts all 64 producers. It observes the writer's database lock wait before releasing the barrier. The queue/admission metric, minus channel capacity and one possible consumer carry item, proves a conservative lower bound of blocked producers.

All eight main samples proved **at least 23 producers blocked on admission**; one proved at least 30. The lock is then released and all barrier writes must finish.

Important metric semantics:

- ifm.event_log.queue.depth includes buffered channel contents and producers awaiting channel admission.
- It excludes commands already dequeued into the transaction batch.
- It is **not channel occupancy**; values above the configured capacity do not mean the channel exceeded capacity.

During the main load, the median sampled queue/admission count was **32 in every sample**. Low-rate observations ranged from 0 to 32 including sampled drain periods; the higher-resolution metric recorded brief peaks of 35–47. The harness independently asserted a final zero count after complete drain for every sample, even where the last low-rate observation preceded drain.

There were no failed commands, early safety-cap exits, admission stalls that failed the barrier, or durable verification failures. Bounded producer count prevents unlimited accumulation outside the channel; this does not prove safety for an application that launches unlimited concurrent appends.

## Resource observations

The run retained **485 client/PostgreSQL observations** and **72 Docker resource snapshots**.

- Client managed-memory observations peaked at **18.97 MiB**; observed client working set peaked at approximately **106.77 MiB**.
- The process footprint rose across successive fresh fixtures. Some individual windows ended below their starting managed-memory sample, others above. These measurements do **not** establish a leak-free steady-state plateau, nor do they by themselves diagnose a leak.
- The original writer recorded 885 Gen 0, 877 Gen 1 and 4 Gen 2 collections across its four timed windows. The candidate recorded 1,936 Gen 0, 1,911 Gen 1 and 4 Gen 2 collections while completing 2.43x as many commands.
- Therefore do not claim fewer collections per second or a demonstrated Gen 2 reduction. The clearer findings are lower allocation and client CPU cost per completed command.
- Per-sample median container CPU was approximately 49% for the original writer and 58% for batching, in Docker's CPU accounting. More work per second used more server CPU; these snapshots are not a full CPU profile.
- PostgreSQL active-backend snapshots included CPU-or-unreported activity, WAL sync/write, data-file I/O, and autovacuum delay. They include background activity and are not time-weighted wait-duration measurements.
- Docker block-I/O values are cumulative container accounting, including activity outside the timed writer itself. They are retained for inspection, not presented as event-log IOPS or physical disk latency.

The observer targets roughly one-second snapshots and invokes docker stats less often. Sampling can be delayed by the stats command. Observer overhead is included in both writers' measured allocation/CPU/latency; fixed observer cost is amortized over different completed command counts.

No builds or regression suites ran concurrently with the main measured comparison.

## Verification and reporting

Each sample verifies exact event/audit/marker counts, marker placement and identities, contiguous stream versions, persisted projection flags and payload replay. Existing stale-version, duplicate/hash-conflict/restarted-writer rejection, financial-fence and checkpoint/marker rollback checks run after measurement.

Completed checks:

- Initial short pressure smoke: 4/4 samples passed.
- Main one-minute paired pressure test: **8/8 passed**.
- Existing fixed-round marker benchmark regression: 18/18 passed.
- Final short pressure/reporting regression: 4/4 passed.
- Release builds: zero warnings and zero errors.

After measurement, an inherited generic metadata label and summary footer were found not to describe the new pressure mode accurately. The workload itself and all marker assertions used the intended one-in-eight pattern. Pressure-specific wording was fixed for future runs and verified in the final regression. Original measured artifacts remain unchanged; the [reporting clarification](../../BenchmarkDotNet.Artifacts/event-log-v2/pressure-paired-20260919/report-notes.json) records this explicitly.

All disposable fixture databases were removed; only postgres remained on the dedicated instance, which was then stopped. No application data was removed.

## Remaining work

The bounded small-queue pressure and short-soak gate is covered for this synthetic workload. Remaining promotion work:

1. End-to-end actor/projector/checkpoint progression and recovery under load, including the application's handling of duplicate/unknown commit outcomes.
2. A production-like longer run with actual event-size/marker distributions, retained history and the intended queue capacity; inspect memory retention and storage latency.
3. Controlled activation/rollback configuration, followed by target-host verification.

No schema replacement, durable command-audit removal, or production queue-size change is justified by this test. Earlier process/restart qualification remains separate evidence; this performance run did not repeat crash injection.

Follow-up: [storage-to-projector pipeline qualification](Event-Log-Projector-Pipeline-Qualification-2026-09-19.md)
passed 14/14 paired cases using the real PostgreSQL context, projector engine, recovery coordinator and outbox
dispatcher. Transport remains substituted; the complete actor/NATS end-to-end gate is still open.

## Evidence and reproduction

- [Main raw samples](../../BenchmarkDotNet.Artifacts/event-log-v2/pressure-paired-20260919/samples.json)
- [Main environment/options/source hashes](../../BenchmarkDotNet.Artifacts/event-log-v2/pressure-paired-20260919/metadata.json)
- [Generated numerical summary](../../BenchmarkDotNet.Artifacts/event-log-v2/pressure-paired-20260919/summary.md)
- [Reporting clarification](../../BenchmarkDotNet.Artifacts/event-log-v2/pressure-paired-20260919/report-notes.json)
- Per-fixture *-observations.json files beside those artifacts contain memory, queue/admission, commit, PostgreSQL-wait and container observations.
- [Fixed-round regression results](../../BenchmarkDotNet.Artifacts/event-log-v2/pressure-legacy-regression-20260919/summary.md)
- [Final pressure/report regression results](../../BenchmarkDotNet.Artifacts/event-log-v2/pressure-report-regression-20260919/summary.md)
- [Reproduction instructions](Event-Log-V2-Benchmark-Harness-and-Verification.md#bounded-queue-pressure--timed-soak-experiment-2026-09-19)
- [Prior process/restart qualification](Event-Log-Process-and-Restart-Qualification-2026-09-19.md)
