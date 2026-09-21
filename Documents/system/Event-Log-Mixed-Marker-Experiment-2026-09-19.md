# Event-log mixed marker-density experiment

Date: 2026-09-19

## Decision

Retain marker batching for further production qualification on the existing schema. The benefit survives sparse and mixed marker workloads: the lowest nonzero density tested improved paired throughput by 15%, and the burst workload improved 28%. No-marker performance is inconclusive/essentially unchanged.

**No event-log migration, index removal, application cache change, or production activation was performed.** This experiment extends only the isolated benchmark and its verification. Durable command audit, stream locking, global event identities, the financial fence, and all four indexes remain intact.

## Method and verification

Six scenarios × three variants × six balanced, rotated repetitions = **108 passing main samples**:

- Baseline: event_log with the original per-row marker writer.
- V2Control: identical event_log_v2 schema with the original writer.
- V2BatchedMarkers: identical event_log_v2 schema with the set-based writer.

All scenarios use audited commands, financial-stream names, LZ4 payloads, and the same seeded synthetic payload. The dedicated local PostgreSQL 17.2 instance retained fsync/synchronous_commit/full_page_writes=on/on/on. The Windows client used .NET 10.0.10 and workstation GC.

Each stream had 32 seed commands and 128 measured commands—four times the measured rounds of the earlier marker experiment. Five scenarios use four streams and 64 events per command, with marker selection by stream-version modulo 64, 8, 2, or 1, plus a no-marker control. The sixth uses 64 streams and one event per command, with markers every eighth version. That final pattern models bursts, not random production traffic.

Measured totals, excluding seeding and post-measurement checks:

- **193,536 commands**
- **3,096,576 events**
- **986,112 marker rows**
- Individual timed samples: 2.65–44.61 seconds

A preceding 36-sample smoke run passed; its timings are not included below. Its short burst case never reached the eighth version, exercising the zero-marker interval; the main run covers 16 marked versions per stream.

Every main sample verified exact durable event/audit/marker counts, exact marker placement and identities, contiguous stream versions, payload replay, and the persisted requires-projection flag. The existing stale-version, duplicate/hash-conflict/restarted-writer rejection, financial-fence, and checkpoint/marker rollback checks also ran after each sample. Measured marker telemetry excludes seeding and supports transactions containing no markers.

Release build: zero warnings and zero errors. No builds or regression suites ran concurrently with measurement.

## Throughput and tail latency

Throughput and p99 are medians of six samples. The speedup is the median of six paired candidate/baseline ratios, **not** the ratio of the independently calculated median throughput columns. Percentiles are descriptive, not production SLAs.

| Workload | Baseline commands/s | Batched commands/s | Paired speedup | Baseline p99 ms | Batched p99 ms |
|---|---:|---:|---:|---:|---:|
| 64 events/command, no markers | 183.3 | 184.7 | 1.019×; inconclusive | 29.18 | 27.66 |
| 64 events/command, 1/64 marked (1.5625%) | 147.5 | 172.2 | **1.150×** | 38.14 | 30.05 |
| 64 events/command, 1/8 marked (12.5%) | 63.0 | 165.4 | **2.622×** | 81.10 | 33.27 |
| 64 events/command, half marked | 21.5 | 146.6 | **6.771×** | 232.93 | 33.08 |
| 64 events/command, all marked | 11.8 | 129.9 | **10.953×** | 405.15 | 38.37 |
| Single-event commands, marker every eighth version | 1,591.1 | 2,068.5 | **1.281×** | 115.69 | 38.33 |

All nonzero-marker workloads improved in every paired comparison:

| Workload | Candidate/baseline paired gain range | Median candidate/control speedup |
|---|---:|---:|
| 1/64 marked | +14.0% to +21.9% | 1.171× |
| 1/8 marked | +148.9% to +200.7% | 2.628× |
| Half marked | +535.1% to +586.9% | 6.855× |
| All marked | +988.6% to +1034.5% | 11.204× |
| Single-event bursts | +24.9% to +32.0% | 1.261× |

For no markers, candidate/baseline changes ranged from -3.8% to +5.6%, while candidate/control median throughput was -0.6%. This does not establish either an improvement or a regression.

## Mechanism and allocation

Median statement counts per measured sample:

| Workload | Required marker rows | Original marker statements | Batched marker statements | Allocated bytes/command reduction |
|---|---:|---:|---:|---:|
| No markers | 0 | 0 | 0 | Approximately unchanged |
| 1/64 marked | 512 | 512 | 129 | 1.7% |
| 1/8 marked | 4,096 | 4,096 | 129 | 14.1% |
| Half marked | 16,384 | 16,384 | 129 | 39.8% |
| All marked | 32,768 | 32,768 | 129 | 56.6% |
| Single-event bursts | 1,024 | 1,024 | 37 | 5.7% |

Even one marker per 64-event command can benefit because several commands share a physical transaction. These results do **not** establish a benefit for a transaction containing exactly one marker.

The allocation reduction is substantial at higher densities: median bytes/command fell from 584,127 to 253,544 at full density. WAL/event was broadly similar; the optimization reduces SQL statement work and client allocation, not the amount of required durable business data.

Do not infer a reliable Gen 2 GC improvement from this run. Those counts are small, vary across samples, and the same client process also performs seeding and verification between measured windows. Allocation measurements are more useful here; UI/API GC profiling remains separate work.

## Scope and remaining gates

This was a longer, repeated, closed-loop persistence-boundary experiment—not a time-based soak, open-loop saturation test, or measured production event distribution. Candidate queue depth peaked at four for multi-event cases and 64 for bursts, far below the configured capacity of 8,192. It does not demonstrate behavior at queue saturation.

Other limits:

- Fresh, modest-sized databases and warm replay; no production-sized retained history.
- Synthetic deterministic marker placement; no real actor/NATS/API/UI/ledger end-to-end workload.
- No server CPU, disk-latency, or wait sampler in this run.
- No new crash injection in this performance experiment. The separate [fault qualification](Event-Log-Marker-Fault-Qualification-2026-09-19.md) passed backend-disconnect, lost-acknowledgment, cancellation and independent-connection contention checks.
- Independent application-process death/contention, full database restart/crash recovery, end-to-end projector progress, and a controlled activation/rollback switch remain open gates.

Recommended next step: independent-process and full database-restart qualification on the disposable instance, before enabling batching. Then perform a target-host soak with representative marker density and queue pressure. There is still no benchmark justification for replacing the schema or removing durable deduplication.

Follow-up completed: [independent-process and server-restart qualification](Event-Log-Process-and-Restart-Qualification-2026-09-19.md)
passed 30/30 cases, including whole-server SIGKILL recovery. The earlier 36-case connection-fault suite also
passed again. Queue-pressure/soak, end-to-end projector and controlled-rollout work remain open.

## Evidence and reproduction

- [108 raw samples](../../BenchmarkDotNet.Artifacts/event-log-v2/mixed-paired-20260919/samples.json)
- [Generated comparisons and statement counts](../../BenchmarkDotNet.Artifacts/event-log-v2/mixed-paired-20260919/summary.md)
- [Environment, options and source hashes](../../BenchmarkDotNet.Artifacts/event-log-v2/mixed-paired-20260919/metadata.json)
- [Smoke results](../../BenchmarkDotNet.Artifacts/event-log-v2/mixed-smoke-20260919/summary.md)
- [Reproduction guide](Event-Log-V2-Benchmark-Harness-and-Verification.md#mixed-marker-density-experiment-2026-09-19)
- [Earlier marker-heavy performance experiment](Event-Log-Marker-Batching-Experiment-2026-09-19.md)

All successful disposable fixture databases were removed; only postgres remained on the benchmark instance, which was then stopped. No application data was deleted. Raw results are retained.
