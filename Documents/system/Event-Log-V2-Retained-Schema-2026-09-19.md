# Event-log v2: retained-history index qualification

Date: 2026-09-19. Status: benchmark only; production unchanged.

## Result

All eight measured samples and four smoke samples passed verification. The same single schema change from [step 1](Event-Log-V2-Schema-Step-1-2026-09-19.md) was tested: consolidate four indexes into three by replacing the old primary key with the existing stream/version unique index. Both variants use event_log_v2 and batched projection markers.

| Metric | Four-index batched control | Three-index batched candidate |
|---|---:|---:|
| Median commands/second | 1,141.0 | 1,193.0 |
| Median sample p99 latency | 75.02 ms | 67.68 ms |
| Median allocated bytes/command | 36,557 | 36,555 |
| Median WAL bytes/event | 2,180 | 2,070 |
| Gen 2 collections across four windows | 4 | 3 |

Median paired throughput improvement: **4.9%**, range **4.0% to 5.4%**. All four pairs favored the candidate. The paired median differs from the ratio of independent throughput medians. Median sample p99 improved about 9.8%; WAL/event fell about 5.0%. Allocations were effectively unchanged. The small GC count difference is not evidence of a reliable Gen 2 improvement.

## Controlled workload

- Four alternating-order paired repetitions; eight one-minute measured windows, plus seed writes and full verification.
- Every fixture starts with 262,144 events (512 seed rounds per stream), persisted through the real audited writer and retained during measurement.
- 64 bounded producers; eight events per command; one required marker per command; queue capacity 8,192; unchanged 256-event/1 MiB batch limits and 1 ms delay.
- Identical synthetic payload, compression and durability on/on/on. Dedicated PostgreSQL 17.2 server on loopback port 25432.
- Measured totals: 558,272 commands, 4,466,176 events and 558,272 markers, excluding seed writes.
- Equal seed history occupies 466,722,816–466,845,696 bytes for control and 447,782,912–448,045,056 bytes for candidate event tables/indexes: approximately 4% less space. Final sizes are not a matched comparison because timed runs complete unequal work.
- Resource observers run in both variants. No builds or other test suites overlapped measurement.

Global event identity/sequence, command lookup, durable audit, financial fencing, incoming identity foreign keys, payload validation and timestamp format are retained. This is not a test of removing those protections.

## Implementation and checks

Only the benchmark harness was extended to allow --schema-batched-experiment together with --retained-experiment. Other incompatible experiment combinations remain rejected. Retained schema mode selects the two already-batched variants, uses timed execution and emits accurate metadata. Existing fixed-round schema mode remains available.

Release build passed with zero warnings/errors. Four short smoke samples passed before the main run. Verification covers durable counts, replay, stream versions, marker placement, duplicates/conflicts, stale versions and financial fence/checkpoint checks. Schema assertions verify the primary key, index count and financial dependencies. Timed execution also checks that outstanding queue/admission work drains to zero.

All eight successful fixture databases were dropped automatically; the dedicated server was verified to contain only postgres afterward. No production migrations or settings were applied.

## Decision

The incremental throughput gain reproduced under this larger retained history. Keep the three-index layout as a qualified performance candidate; batching remains the main optimization. These four short pairs are descriptive evidence, not a production-wide guarantee or statistical confidence interval.

Next gate before deployment: qualify crash/restart, projector/financial identity compatibility and the actual migration/rollback path against the candidate schema. Further schema optimizations must remain separate experiments. This run does not justify removing the global ID, command audit or fence, and does not establish full-day memory stability or production-sized history performance.

## Reproduction and artifacts

Configure the guarded isolated benchmark connection, then run:

```text
dotnet run --project TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release --no-build -- --event-log-v2-benchmark --schema-batched-experiment --retained-experiment --repeats=4 --soak-seconds=60 --seed-rounds=512 --output=BenchmarkDotNet.Artifacts/event-log-v2/schema-retained-paired-20260919
```

Main artifacts: BenchmarkDotNet.Artifacts/event-log-v2/schema-retained-paired-20260919 (raw samples, summary, source-hashed metadata, exact index definitions and per-fixture observations). Smoke artifacts: schema-retained-smoke-20260919 under the same parent, using two repetitions, 10-second windows and two seed rounds.
