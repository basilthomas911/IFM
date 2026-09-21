# Post-retry-fix paired index benchmark

## Result

All four measured samples passed verification: 277,152 commands and 2,217,216 events, excluding seed history. The three-index candidate retained a modest throughput benefit over the four-index control with marker batching enabled in both.

| Pair | Four-index commands/s | Three-index commands/s | Candidate gain |
| --- | ---: | ---: | ---: |
| 1: control first | 1,126.2 | 1,171.9 | 4.05% |
| 2: candidate first | 1,144.0 | 1,174.2 | 2.64% |

Mean/median of the two paired gains: **3.35%**. This is descriptive evidence from two short pairs, not a statistical confidence interval or production guarantee. The earlier four-pair run measured 4.9%; the new results still favor the candidate but do not establish that the difference between campaigns is caused by the retry fix.

| Resource | Four-index control | Three-index candidate |
| --- | --- | --- |
| WAL bytes/event, pair 1 / pair 2 | 2178.9 / 2178.7 | 2067.0 / 2069.1 |
| Allocated bytes/command | about 36,614 | about 36,612 |
| p99 latency, pair 1 / pair 2 | 115.21 / 67.68 ms | 68.94 / 68.42 ms |
| Gen 2 collections across both windows | 3 | 2 |

WAL/event fell approximately 5.1%. Allocation is effectively unchanged. The first control sample's high tail latency was not reproduced in the second pair; no consistent p99 improvement claim is warranted. The tiny GC count difference does not demonstrate a reliable GC reduction.

## Controlled scope

- Current Release build, zero warnings/errors, with the retry correction present in source and binaries.
- Both writers batch markers on `event_log_v2`; only primary-key/index consolidation differs.
- Each fresh database begins with 262,144 verified retained events through the real writer. Seed table/index size is about 445 MiB for control versus 427 MiB for candidate.
- Four 60-second closed-loop windows, alternating order, 64 producers, eight events and one marker per command; unchanged 8192 queue, 256-event/1 MiB batch limits and 1 ms delay.
- PostgreSQL 17.2, isolated loopback port 25432, durability on/on/on; resource observer enabled equally for both variants. No builds or other test suites ran during measurement.
- The workload uses the persistence-boundary benchmark command type, not Portfolio's opted-in normalized retry envelope. Thus this compares index layouts on the repaired build; it does not measure the cost of a Portfolio retry storm. Separate live-host acceptance covers those retries.

Integrity checks include persisted counts, replay, stream versions, markers, duplicate/conflict/stale-version behavior and financial fence/checkpoint checks. All successful per-sample databases were removed automatically. They contained synthetic benchmark data only and are not backed up; reports are retained.

## Acceptance fixture preservation and restore

The existing synthetic acceptance database was backed up before temporarily removing it to satisfy the benchmark's empty-server guard. Its full backup is retained at:

`BenchmarkDotNet.Artifacts/event-log-v2/postretry-paired-20260920/acceptance-backup.dump`

SHA-256: `30E7D8CC3EE7B83404E23FDC73B59FFB0CC82A235400845421D9A93D02A005F8`.

The database was restored in the runner's finally block. Before/after event payload and command-audit hash aggregates matched exactly: 274 event rows and 530 command rows. The server was verified to contain only `postgres` and the restored `ifm_eventlog_bench_092020260032_synthetic_host` database (excluding templates). Its three intended indexes and `financial_legacy_event_fence` remain intact.

A newly launched real API then reached Healthy bootstrap with 185 actors and passed all 256 cold-cache retries from the saved load manifest; each Portfolio still had business revision 1. The owned API child was stopped afterward. No normal application database was modified.

## Artifacts and reproduction

- `BenchmarkDotNet.Artifacts/event-log-v2/postretry-paired-20260920/paired/samples.json`
- Same directory: `metadata.json`, `summary.md`, exact index DDL and per-sample observations.
- Parent directory: `run.log` and acceptance backup.
- Restore acceptance: `BenchmarkDotNet.Artifacts/event-log-v2/acceptance-092020260032/LoadRestart-20260920021406.trx`.

On an empty, validated benchmark server:

```text
dotnet TomasAI.IFM.Framework.Storage.Benchmarks/bin/Release/net10.0/TomasAI.IFM.Framework.Storage.Benchmarks.dll --event-log-v2-benchmark --schema-batched-experiment --retained-experiment --soak-seconds=60 --repeats=2 --seed-rounds=512 --output=<new-output-directory>
```

## Decision

Retain the three-index layout as the performance candidate: its incremental throughput and WAL benefit reproduced. Batching remains the larger optimization. This does not justify removing global identity, command lookup/audit or financial fencing.

No production cutover was performed. The remaining full current-policy trading workflow and UI gates, plus longer-duration/representative workload qualification, are still open. This campaign is not a full-day soak and does not qualify the original fully stripped-down schema.
