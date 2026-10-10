# PostgreSQL 18.6 tuning results

Measured October 9, 2026 on the shared development workstation. Development database settings and data were not changed. All database profiles used four CPUs, 4 GiB memory and durable commits. See README.md for the workload and runner.

## Configuration and findings

| Parameter | Baseline | Sustained candidate |
|---|---|---|
| shared_buffers | 256 MiB | 1 GiB |
| wal_compression | off | lz4 |
| max_wal_size | 2 GiB | 4 GiB |
| checkpoint_timeout (stress only) | 30 seconds | 30 seconds |

The combined sustained candidate changed batch p95 from 35.84 to 16.11 ms (55% reduction), p99 from 58.72 to 19.32 ms (67% reduction) and WAL volume from 1580.7 to 1105.8 MiB (30% reduction). Throughput was 4249 versus 4272 events/s. The runs crossed 6 and 6 timed checkpoints. These combined results do not isolate the contribution of each setting.

Use the measurements below to compare batching cost per event, client placement and individual settings. No tuning profile was applied to the development database.

## BenchmarkDotNet measurements

Mean and confidence margin are milliseconds per invocation. Margins are BDN 99.9% confidence intervals of iteration means, not request p95/p99. Negative change means faster than the Windows baseline. Confidence overlap is descriptive; sequential profiles on a shared workstation do not establish causation.

| Profile | Method | Mean ms | Margin ms | N | Change | Baseline CI overlaps |
|---|---|---:|---:|---:|---:|---|
| Baseline | AppendEvent | 5.372 | 0.258 | 7 | 0.0% | True |
| Baseline | AppendBatch32 | 6.051 | 0.299 | 7 | 0.0% | True |
| Baseline | AppendBatch8 | 5.539 | 0.099 | 6 | 0.0% | True |
| Baseline | AppendBatch128 | 7.439 | 0.236 | 8 | 0.0% | True |
| Baseline | WorkingSetRead | 1.411 | 0.069 | 7 | 0.0% | True |
| Baseline | SortWindow | 37.792 | 1.432 | 8 | 0.0% | True |
| Baseline | LatestSnapshot | 1.249 | 0.114 | 8 | 0.0% | True |
| Baseline | History100 | 1.396 | 0.079 | 8 | 0.0% | True |
| Baseline | ConcurrentAppend8 | 10.304 | 0.185 | 8 | 0.0% | True |
| IoSync | WorkingSetRead | 1.429 | 0.153 | 7 | 1.3% | True |
| IoSync | History100 | 1.473 | 0.090 | 8 | 5.5% | True |
| JitOff | SortWindow | 38.786 | 1.409 | 7 | 2.6% | True |
| LinuxClient | AppendEvent | 4.804 | 0.143 | 8 | -10.6% | False |
| LinuxClient | AppendBatch32 | 5.371 | 0.361 | 8 | -11.2% | False |
| LinuxClient | AppendBatch8 | 4.811 | 0.206 | 8 | -13.1% | False |
| LinuxClient | AppendBatch128 | 6.760 | 0.291 | 8 | -9.1% | False |
| LinuxClient | WorkingSetRead | 0.969 | 0.042 | 8 | -31.3% | False |
| LinuxClient | SortWindow | 36.589 | 0.863 | 7 | -3.2% | True |
| LinuxClient | LatestSnapshot | 0.296 | 0.016 | 8 | -76.3% | False |
| LinuxClient | History100 | 0.694 | 0.034 | 8 | -50.3% | False |
| LinuxClient | ConcurrentAppend8 | 9.485 | 0.263 | 7 | -8.0% | False |
| Memory1GB | WorkingSetRead | 1.508 | 0.221 | 8 | 6.9% | True |
| Memory1GB | History100 | 1.460 | 0.110 | 8 | 4.6% | True |
| Wal4GB | AppendEvent | 5.375 | 0.178 | 7 | 0.0% | True |
| Wal4GB | AppendBatch128 | 7.431 | 0.172 | 8 | -0.1% | True |
| Wal4GB | ConcurrentAppend8 | 10.672 | 0.296 | 6 | 3.6% | True |
| WalLz4 | AppendEvent | 5.494 | 0.129 | 8 | 2.3% | True |
| WalLz4 | AppendBatch128 | 7.519 | 0.308 | 7 | 1.1% | True |
| WalLz4 | ConcurrentAppend8 | 10.352 | 0.219 | 7 | 0.5% | True |
| WorkMem32MB | SortWindow | 41.033 | 1.482 | 8 | 8.6% | False |

## Sustained checkpoint diagnostics

Separate three-minute request samples, eight writers, 32 events per commit, target 5,000 events/s and stress-only checkpoint_timeout=30s. These are not BDN results or a recommended checkpoint configuration.

| Profile | Events/s | Batch p50 ms | Batch p95 ms | Batch p99 ms | Timed/requested checkpoints | WAL MiB | WAL bytes/event | Full-page images |
|---|---:|---:|---:|---:|---|---:|---:|---:|
| SustainedBaseline | 4249 | 12.549 | 35.836 | 58.723 | 6/0 | 1580.7 | 2167 | 67501 |
| SustainedCandidate | 4272 | 12.279 | 16.108 | 19.318 | 6/0 | 1105.8 | 1508 | 65766 |

## Evidence locations

- Baseline: `.artifacts\postgres-tuning\e42b885bd2d4\Baseline`
- IoSync: `.artifacts\postgres-tuning\d254144494a1\IoSync`
- JitOff: `.artifacts\postgres-tuning\d254144494a1\JitOff`
- LinuxClient: `.artifacts\postgres-tuning\ecf4eb3f434b\LinuxClient`
- Memory1GB: `.artifacts\postgres-tuning\d254144494a1\Memory1GB`
- Wal4GB: `.artifacts\postgres-tuning\d254144494a1\Wal4GB`
- WalLz4: `.artifacts\postgres-tuning\d254144494a1\WalLz4`
- WorkMem32MB: `.artifacts\postgres-tuning\d254144494a1\WorkMem32MB`

- Sustained evidence: `.artifacts\postgres-tuning\9ce1c8bc5091\SustainedBaseline\SustainedBaseline-sustained.json`
- Sustained evidence: `.artifacts\postgres-tuning\9ce1c8bc5091\SustainedCandidate\SustainedCandidate-sustained.json`
- IoUring startup evidence: `.artifacts\postgres-tuning\6cbbc838b5f3\IoUring\server.stdout.log`

Raw folders also contain settings, client/server logs, Docker resource and free-space samples, and sort EXPLAIN plans. All benchmark containers and volumes are labelled and removed by the runner; raw files remain.

## Interpretation

- Batching amortizes durable commit cost. Compare per-event cost without treating a larger batch as lower latency for a single financial event. No application batching policy was changed.
- The working set is approximately 500 MiB of payload plus indexes. Operating-system cache remains available; these are warm runs, not a proof of benefits on a larger cold dataset.
- Sort plans use parallel in-memory quicksort under the baseline. No temp spill and no JIT execution were observed. Increasing work_mem or disabling JIT is not supported as an optimization for this workload.
- LinuxClient also changes client OS, runtime build and CPU allocation (two client CPUs), so its difference cannot be attributed to networking alone. Database configuration remains the baseline.
- io_uring startup failed with Operation not permitted under the existing Docker/kernel security configuration. No security settings were weakened. See the IoUring server.stdout.log for the exact failure.
- WAL compression affects full-page images, not all event payload WAL. Larger max_wal_size is a checkpoint budget, not a direct per-commit latency switch. The combined sustained profile cannot isolate the contribution of its three changes.

PostgreSQL parameter semantics: [resource consumption](https://www.postgresql.org/docs/18/runtime-config-resource.html), [WAL configuration](https://www.postgresql.org/docs/18/runtime-config-wal.html).
