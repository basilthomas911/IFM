# PostgreSQL Docker version benchmark results

Date: October 9, 2026 (America/Toronto).

## Results

All 15 BenchmarkDotNet cases produced valid measurements. The measured means show no overall latency improvement from PostgreSQL 18.6 for this workload. Changes between versions are small compared with the effect of grouping appends into one durable commit. These results do not establish a generally fastest PostgreSQL version.

Mean round-trip milliseconds; lower is better. `+/-` is BenchmarkDotNet's reported half-width of the 99.9% confidence interval of the retained iteration means. It is not request p95/p99 latency.

| Workload | 17.2 | 17.11 | 18.6 | 18.6 mean change vs 17.2 |
| --- | ---: | ---: | ---: | ---: |
| Single durable event | 5.579 +/- 0.159 | 5.394 +/- 0.178 | 5.638 +/- 0.223 | +1.1% |
| 32-event durable batch | 6.205 +/- 0.615 | 6.150 +/- 0.504 | 6.072 +/- 0.334 | -2.1% |
| Latest snapshot (1 KiB) | 0.941 +/- 0.079 | 0.952 +/- 0.056 | 1.052 +/- 0.082 | +11.9% |
| 100-event history (~100 KiB) | 1.407 +/- 0.095 | 1.416 +/- 0.071 | 1.474 +/- 0.067 | +4.8% |
| Eight concurrent durable appends (whole group) | 10.297 +/- 0.160 | 10.540 +/- 0.428 | 10.345 +/- 0.298 | +0.5% |

The single-event mean was 3.3% lower on 17.11 than 17.2 in this run; that is a descriptive observation, not proof of a consistent improvement. Confidence intervals overlap for the comparison. Batch means were lower on newer versions, while latest-snapshot and history read means were higher.

## Batching effect

A batch result is the total time for all 32 events, not the time for one event.

| Version | Single event (ms) | Batch amortized per event (ms) | Relative events per elapsed second |
| --- | ---: | ---: | ---: |
| 17.2 | 5.579 | 0.194 | 28.8x |
| 17.11 | 5.394 | 0.192 | 28.1x |
| 18.6 | 5.638 | 0.190 | 29.7x |

These ratios describe this fixed closed-loop workload. They are not maximum sustained throughput measurements and do not authorize batching financially critical operations.

## Environment and scope

- Windows 10 client on AMD Ryzen Threadripper 1950X (16 physical / 32 logical cores), .NET 10.0.10, SDK 10.0.302, BenchmarkDotNet 0.15.8, Npgsql 10.0.3.
- Official `postgres:<version>-bookworm` Docker images, Linux Docker volumes, one benchmark server active at a time, loopback ports 56417-56419. Exact image digests and server settings are recorded in each run's metadata.
- Each server: 4 CPU limit, 4 GiB RAM, 512 MiB shared memory, 256 MiB shared buffers, 2 GiB effective cache size, 16 MiB work memory, 2 GiB maximum WAL size, 15-minute checkpoint timeout. `fsync`, `synchronous_commit` and `full_page_writes` remained enabled and were checked before measurement. Version-specific defaults, including PostgreSQL 18's I/O method, were retained.
- Identical new schema before each case: 100 streams x 1,000 events, 1 KiB synthetic byte payloads, snapshot event every 100 rows. Stream/version, global event-version, command and event-name/version indexes match the representative IFM event-log structure. Prepared commands and pre-opened connections exclude connection establishment from measurement. Payloads, timestamps and command IDs are synthetic fixed inputs; these are SQL workload comparisons, not a full production event transaction benchmark.
- Durable writes use a CTE to update the stream version and insert rows atomically. Concurrent writes use eight independent connections and streams. Reads materialize payload bytes. Writes/read counts were validated in setup before warmup.
- One launch per case, three warmup iterations and eight measured iterations with a minimum 500 ms iteration duration. BenchmarkDotNet retained 6-8 iterations after its default outlier filtering. The first two versions and final version ran in sequential separate invocations. This was one comparison session in the order 17.2, 17.11, 18.6; background host activity and run order can affect small differences.
- The dataset fits in the configured cache. These measurements do not characterize cold reads, multi-gigabyte production histories, recovery, replica lag or IFM actor/serialization/end-to-end latency. Mean round-trip times include Windows-to-Docker networking and WAL flush cost.
- Docker CPU/memory/block-I/O samples include initialization, generated executable builds, seed work, warmup and measurement. They are diagnostic evidence and cannot be used as workload-specific normalized resource comparisons. WAL configuration was verified; per-operation WAL generation was not measured.

## Evidence and repeatability

Run `./scripts/Benchmarks/Compare-PostgresVersions.ps1` after pulling the three Bookworm images. The benchmark source is `PostgresVersionBenchmarks.cs`; its workload/setup contract is described in README.md. The script generates fresh credentials, labels resources with a unique run ID, validates measured results, exports summary.csv and cleans up only its own containers/volumes.

This session's raw evidence:

- PostgreSQL 17.2: `.artifacts/postgres-version-benchmarks/3fab0afcd8d4/17.2/results/`; image/settings: `3fab0afcd8d4/metadata.json`; Docker resource samples: `3fab0afcd8d4/17.2/docker-stats.json`.
- PostgreSQL 17.11: `.artifacts/postgres-version-benchmarks/36684c48a7c1/17.11/results/`; image/settings: `36684c48a7c1/metadata.json`; Docker resource samples: `36684c48a7c1/17.11/docker-stats.json`.
- PostgreSQL 18.6: `.artifacts/postgres-version-benchmarks/7dbf6163bf68/18.6/results/`; image/settings: `7dbf6163bf68/metadata.json`; Docker resource samples: `7dbf6163bf68/18.6/docker-stats.json`.

Combined machine-readable results: `.artifacts/postgres-version-benchmarks/comparison-2026-10-09.csv`.

The first two invocations encountered runner errors after BenchmarkDotNet had completed: a JSON filename assumption and PowerShell treating normal Docker log stderr as a terminating error. The final invocation exposed a Windows PowerShell process exit-code capture issue after all five measured cases completed. These wrapper errors did not invalidate the saved BenchmarkDotNet statistics. The runner was corrected to use the actual full-compressed JSON filename, capture Docker logs via redirected processes, retain the benchmark process handle, require five valid statistics records, and protect cleanup with exact run-label checks. Exit-code handling passed a separate process smoke check. All 15 raw report records were independently checked for positive means and at least six retained iterations.

No development database schemas, tables, volumes, or server settings were changed. No PostgreSQL upgrade was performed. The benchmark containers and labelled data volumes were removed; the original development `ifm_db` continues running PostgreSQL 17.2.
