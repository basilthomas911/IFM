# Event-log v2: schema step 1 on the batched writer

Date: 2026-09-19. Status: isolated benchmark completed; no production migration.

## Single change tested

Both control and candidate use event_log_v2 and the real binary COPY appender with projection-marker batching enabled. The candidate replaces the old primary key (eventstreamid, eventnameid, eventversion) with the existing unique stream/version index as its primary key. This reduces four indexes to three. It does not test a table rename or combine schema gains with the earlier batching gains.

Candidate fixture DDL:

```sql
ALTER TABLE event_log_v2 DROP CONSTRAINT event_log_pkey;
ALTER TABLE event_log_v2 ADD CONSTRAINT event_log_v2_pkey
    PRIMARY KEY USING INDEX ux_event_log_stream_version_v3;
```

Global eventversion identity and sequence, its unique index, command-ID B-tree, timestamp representation, durable command audit, payload checks, financial fence and incoming financial foreign keys remain intact. In-memory stream versions advance only after durable acknowledgement. No production SQL layout or application configuration was changed.

## Method and verification

- PostgreSQL 17.2 in the dedicated loopback benchmark container on port 25432; durability on/on/on; .NET 10 Release.
- Three workloads, two variants, six alternating-order repetitions: 36 measured samples. Each has 128 rounds per stream following 32 seed rounds. Both variants use identical payloads and queue/batch settings.
- No-marker and all-marker cases: four streams, 64 events per command. Mixed case: 64 streams, eight events per command, one marker every eight stream versions.
- All 36 measured samples passed: 110,592 commands, 1,572,864 measured events and 491,520 measured markers. Seed events are additional. Initial smoke run passed 12/12 samples.
- Checks cover durable counts, replay, contiguous versions, duplicate/stale rejection, marker correctness and financial fencing. Fixture checks assert the exact primary key, index count, enabled fence and incoming identity foreign keys. Exact index DDL is saved per fixture.
- Release build passed with zero warnings/errors. This is storage-boundary verification, not end-to-end application or crash qualification of the new schema.

## Results

Throughput change is the median of six paired candidate/control ratios, not the ratio of the independently calculated medians.

| Workload | Paired throughput change | Paired range | Median p99 control / candidate | WAL/event reduction | Event table + indexes reduction |
|---|---:|---:|---:|---:|---:|
| No markers | +4.4% | -1.6% to +10.1% | 26.03 / 26.54 ms | 5.9% | 3.7% |
| One marker per eight events | +3.7% | +0.0% to +5.6% | 63.85 / 60.19 ms | 5.3% | 4.0% |
| Every event marked | +3.6% | -1.7% to +4.6% | 40.82 / 38.26 ms | 3.9% | 3.7% |

Client allocations per command were effectively unchanged. Do not claim a GC improvement from this index change. Table sizes include seed data and indexes, not the whole database. WAL measurements can include background server activity.

## Decision and next gate

Keep index consolidation as a benchmark candidate. The incremental improvement is modest and some pairs regress; these short, synthetic, closed-loop measurements do not establish a statistically reliable production latency gain. The previously measured batching improvement remains the main performance result and is a separate change.

Before promoting this schema, repeat with longer retained-history workloads and qualify recovery/projectors and migration dependencies. Any next schema experiment must change only one additional factor and retain this control. Timestamp storage is a possible next candidate, but requires checking reader/writer compatibility first. Do not drop global event identity, durable audit or financial fencing to chase throughput: their dependencies and correctness requirements remain.

## Reproduction and artifacts

With the existing guarded isolated benchmark connection configured, run:

```text
dotnet run --project TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release --no-build -- --event-log-v2-benchmark --schema-batched-experiment --repeats=6 --rounds=128 --seed-rounds=32 --output=BenchmarkDotNet.Artifacts/event-log-v2/schema-batched-paired-20260919
```

Results: `BenchmarkDotNet.Artifacts/event-log-v2/schema-batched-paired-20260919/` contains samples.json, summary.md, metadata.json (including source hashes) and per-fixture index definitions. Smoke results: `schema-batched-smoke-20260919/` under the same parent. Successful fixture databases were automatically dropped; the isolated server was checked to contain only postgres afterward.
