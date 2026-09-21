# Retained-history event-log load qualification

Date: 2026-09-19

## Result

**4/4 main samples passed**, following **4/4 retained-mode smoke samples** and **4/4 existing pressure-mode regressions**. Each main sample began with 262,144 events committed through the real writer and retained throughout a two-minute measurement window. The main run measured 390,594 additional commands and 3,124,752 additional events across four isolated fixtures.

The candidate retained its throughput advantage at this bounded history size. This is not production activation, a full-day soak, a leak-free certification or a demonstrated Gen 2 reduction.

| Metric | Original markers | Batched markers |
|---|---:|---:|
| Median commands/second | 465.0 | 1,161.5 |
| Median sample p99 latency | 187.25 ms | 68.66 ms |
| Median allocation/command | 41,636 bytes | 36,553 bytes |
| Aggregate client CPU/command | 0.3182 ms | 0.1925 ms |
| Gen 2 collections across two windows | 3 | 5 |

Median paired throughput speedup: **2.499x**, with two observed paired ratios ranging from **2.457x to 2.541x**. Allocation/command fell approximately 12.2%; client CPU/command fell approximately 39.5%. There are only two paired repetitions, so these ranges are descriptive, not confidence intervals.

The candidate completed approximately 2.5 times as much work in the same duration. It allocated more total bytes and had more total GC activity. In particular, five versus three Gen 2 collections is **not** evidence of fewer Gen 2 collections per unit time. The per-command allocation/CPU improvement is the clearer result.

## Workload and retained data

- 64 bounded closed-loop producers, at most one outstanding command per stream.
- Eight events per command; one required durable projection marker per command (every eighth stream version).
- Same synthetic 1,024-character payload and LZ4 compression as prior experiments.
- 512 seed rounds per stream: 32,768 audited seed commands / 262,144 retained events per fixture.
- Seed event table plus its four indexes occupied 466,731,008–466,886,656 bytes, approximately 445 MiB. These sizes exclude audit/marker tables and WAL.
- Existing 8,192-command queue capacity; 256-event / 1 MiB physical batch limits and 1 ms coalescing delay.
- Two 120-second measured windows per writer, order baseline/candidate then candidate/baseline. About eight minutes total measured duration, excluding preload and verification.
- Final event table/index sizes were approximately 1.25–1.27 GB for baseline fixtures and 2.45 GB for candidate fixtures, reflecting different completed work volumes rather than a matched final-size comparison.

Measured writes do not replace or truncate the seed history. Full verification replay covered 701,952 and 716,032 events in the baseline fixtures, and 1,378,560 and 1,376,784 in the candidate fixtures. Thus the same starting history is controlled; final history size is not.

The financial fence, atomic command audit, projection markers and original four-index layout remain in place. Durability settings are on/on/on. Baseline uses event_log and the candidate uses event_log_v2 with unchanged schema. This timed pair has no identical-table control; the prior mixed-density experiment separately characterized naming/control noise.

## Resources and queue behavior

The observer retained **475 client/PostgreSQL snapshots and 71 container snapshots**. Across measured windows, observed managed memory peaked at **13.88 MiB** and working set at **102.98 MiB**. These are sampled timed-window maxima, not whole-process lifetime high-water marks or peak memory during seed/replay.

| Window | Managed memory start/end (MiB) | Working set start/end (MiB) |
|---|---:|---:|
| Baseline 1 | 4.59 / 8.00 | 87.87 / 84.55 |
| Candidate 1 | 9.05 / 11.54 | 86.77 / 91.29 |
| Candidate 2 | 8.36 / 7.19 | 91.18 / 89.07 |
| Baseline 2 | 8.81 / 12.81 | 96.20 / 95.06 |

The same process runs successive fixtures, and GC timing affects observations. These short windows do not establish a steady-state retention plateau or diagnose a leak. No Gen 2 tuning change is justified by this result alone.

Maximum queue/admission metric values were 35, 36, 62 and 36 in run order. The metric includes buffered work and admission waiters but excludes the in-flight transaction batch; it is not channel occupancy. Every sample asserted zero outstanding queue/admission work after drain. With 64 producers and an 8,192-capacity channel, this mode does not claim saturation or backpressure. The separate pressure regression retained the eight-slot queue and verified blocked admission.

PostgreSQL wait snapshots and Docker resource records are available in per-fixture observation files. They are low-rate observations, not time-weighted waits or physical disk-latency measurements. Docker block I/O is cumulative. Observer overhead is included for both variants; the same fixed cost is amortized over different completed command counts.

## Implementation and checks

Only the benchmark harness changed: added --retained-experiment, independent timed-mode reporting, recorded verified seed event counts and table/index bytes, and bypassed the small-queue saturation assertion when the configured capacity exceeds producer count. Timed drain, safety caps, durability, schema, payload replay and failure checks remain active. Modes reject incompatible combinations.

Release build passed with zero warnings/errors. The 4-sample smoke run used 10-second windows and two seed rounds. The existing small-queue mode also passed four 10-second samples after the extension. No builds or regression runs overlapped the main measurement.

All main samples passed event/audit/marker counts, stream-version progression, exact marker placement, payload replay and existing duplicate/conflict/stale-version/fence/checkpoint failure checks. No safety-cap early exit occurred. The earlier actor payload-conflict fix and fault suite remain separate qualification evidence; this run did not inject crashes.

## Artifacts and reproduction

- [Main raw samples](../../BenchmarkDotNet.Artifacts/event-log-v2/retained-paired-20260919/samples.json)
- [Main metadata/source hashes](../../BenchmarkDotNet.Artifacts/event-log-v2/retained-paired-20260919/metadata.json)
- [Main numerical summary](../../BenchmarkDotNet.Artifacts/event-log-v2/retained-paired-20260919/summary.md)
- Per-fixture *-observations.json files in the same directory contain resource evidence.
- [Retained-mode smoke](../../BenchmarkDotNet.Artifacts/event-log-v2/retained-smoke-20260919/summary.md)
- [Small-queue regression](../../BenchmarkDotNet.Artifacts/event-log-v2/retained-pressure-regression-20260919/summary.md)

Use the existing isolated benchmark credentials/environment and the otherwise empty owned PostgreSQL container on 127.0.0.1:25432. Run:

~~~powershell
dotnet run --project TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release --no-build -- --event-log-v2-benchmark --retained-experiment --soak-seconds=120 --repeats=2 --seed-rounds=512 --output=BenchmarkDotNet.Artifacts/event-log-v2/new-retained-run
~~~

The output directory must be new. Retained mode validates the same labeled, loopback, exclusive-volume container required by the observer. Do not point this at an application database.

Successful synthetic fixture databases were removed by the harness; a final server check found only postgres. The owned benchmark container was stopped. Synthetic data is discarded without backup; raw results remain. No production data, settings, schema or running IFM service changed.

## Remaining gates

This establishes bounded retained-history performance at the explicitly tested size and synthetic event distribution. It is not actual production-sized history or a measured real trading mix. Full business aggregate/downstream integration, a deployment activation/rollback design and target-host verification remain before enabling marker batching. Longer-duration GC profiling should use a representative host/workload and assess retention after warmup; two-minute windows cannot certify trading-day behavior.
