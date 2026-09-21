# Post-fix event-log performance requalification

Date: 2026-09-19

## Outcome

**108/108 samples passed verification**, measuring 193,536 commands, 3,096,576 events and 986,112 projection-marker rows. The marker-batching benefit remains after the atomic payload-conflict and mixed-batch isolation fix. No implementation or production configuration changed during this run.

Both compared writers contain the correctness fix. This run measures the marker-batching advantage, not the causal performance cost of the fix itself. The separate earlier run provides historical context, not a randomized old-binary/new-binary comparison.

## Paired results

Each speedup is the median of six candidate/control throughput ratios paired by repetition. V2Control and V2BatchedMarkers use the same table name and unchanged four-index schema; only the candidate batches marker statements. Baseline uses the original event_log name as an additional naming/noise control.

| Workload | Paired throughput speedup | Observed paired range | Control / candidate median p99 (ms) | Allocation reduction per command |
|---|---:|---:|---:|---:|
| No markers | 1.004x; inconclusive | 0.933–1.053x | 27.96 / 34.49 | -0.04% |
| One marker per 64 events | 1.154x | 1.090–1.207x | 33.25 / 30.98 | 1.67% |
| One marker per eight events | 2.601x | 2.537–2.714x | 78.81 / 30.08 | 14.14% |
| Half of events marked | 6.815x | 6.535–7.051x | 227.81 / 32.25 | 39.80% |
| Every event marked | 11.238x | 10.800–11.658x | 426.41 / 37.39 | 56.59% |
| Single-event commands, marker every eighth stream version | 1.296x | 1.233–1.312x | 112.84 / 37.98 | 5.78% |

Ranges describe observed samples, not statistical confidence intervals. p99 values are medians of per-sample percentiles, not a pooled global percentile. The no-marker tail latency varied and its throughput result is noise-sized; do not claim an across-the-board latency improvement.

For the all-marker workload, the control issued a median 32,768 marker statements per measured sample; batching used 130 statements for the same 32,768 rows. In the one-in-eight workload, 4,096 statements became 129 for the same 4,096 rows. This is the measured source of the large marker-dense improvement, without dropping durable evidence or indexes.

## Historical comparison

The candidate's median throughput relative to the preceding mixed-density run changed by -1.23% (no markers), +0.46% (one-in-64), +1.22% (one-in-eight), +1.90% (half), -0.09% (all) and +0.03% (burst). These descriptive changes are small; there is no observed material healthy-path throughput regression in this workload. They do not prove zero cost or eliminate host/run-order variability.

The changed-content rejection and audit-failure isolation paths are not throughput workloads here. They were separately verified by the [93 tests and 36 fault checks](Event-Log-Actor-Payload-Conflict-Fix-2026-09-19.md). A rejection-heavy workload would exercise the new singleton fallback and may have different performance.

## Method and verification

- Same workload settings as the previous mixed-density experiment: six scenarios, three variants, six rotated-order repetitions, 128 measured rounds per stream and 32 seed rounds.
- 64-event commands with four streams for the density scenarios; single-event commands with 64 streams for the burst scenario.
- Synthetic 1,024-character payload, LZ4 compression, atomic command audit, financial fence and four event-log indexes retained.
- Queue capacity 8,192; batch limit 256 events / 1 MiB; one-millisecond oldest-request delay.
- Dedicated PostgreSQL 17.2 on 127.0.0.1:25432; fsync, synchronous_commit and full_page_writes all on.
- Release build passed with zero warnings/errors before measurement. No builds or regression suites ran alongside the measured samples.
- Each sample passed the harness's durable counts, marker placement/identity, contiguous stream versions, payload replay, stale-version, duplicate/hash-conflict, financial-fence and checkpoint rollback checks.

This remains a closed-loop persistence-boundary benchmark with warm replay and synthetic data. It is not actor/UI latency, production-sized retained history, sustained mixed business load, server I/O profiling, or leak/Gen 2 reduction evidence. There is no schema-replacement recommendation from this result.

## Evidence and reproduction

- [Raw samples](../../BenchmarkDotNet.Artifacts/event-log-v2/post-fix-mixed-20260919/samples.json)
- [Metadata, settings and source hashes](../../BenchmarkDotNet.Artifacts/event-log-v2/post-fix-mixed-20260919/metadata.json)
- [Generated summary](../../BenchmarkDotNet.Artifacts/event-log-v2/post-fix-mixed-20260919/summary.md)
- [Earlier matched workload](../../BenchmarkDotNet.Artifacts/event-log-v2/mixed-paired-20260919/summary.md)

The generated summary expresses paired gains against Baseline. The table above deliberately compares against V2Control, holding the event table name constant; these are different denominators, not conflicting calculations.

With the guarded, otherwise empty benchmark server and test-only credentials configured as in the benchmark guide, run:

~~~powershell
dotnet run --project TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release --no-build -- --event-log-v2-benchmark --mixed-marker-experiment --repeats=6 --rounds=128 --seed-rounds=32 --output=BenchmarkDotNet.Artifacts/event-log-v2/new-post-fix-run
~~~

Use a new output directory. IFM_EVENTLOG_BENCH_ADMIN_CONNECTION must select postgres on the isolated nonapplication port; DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT are Test and POSTGRES_TEST_KEY contains only the benchmark credentials.

All generated fixture databases were removed by the harness. A final check found only postgres; the owned benchmark container was then stopped. Synthetic fixture data is discarded, not backed up; raw results remain on disk. No application data or running IFM service was changed.

## Next gate

Proceed to a longer retained-history load test with explicit event-size/marker-density assumptions, resource sampling and bounded producers. Production business-flow/downstream integration and an explicit activation/rollback plan remain separate gates. Optimized marker batching is still benchmark-only; the successful performance rerun does not activate it.
