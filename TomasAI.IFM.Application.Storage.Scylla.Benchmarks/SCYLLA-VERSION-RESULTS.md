# ScyllaDB 6.2.2 versus 2026.3.3

Measured October 9, 2026 with BenchmarkDotNet 0.15.8, .NET 10 and the repository's ScyllaDBCSharpDriver 3.22.0.4. Current deployment version was confirmed using scylla --version. [2026.3.3 release notes](https://forum.scylladb.com/t/release-scylladb-2026-3-3/5531) identify the latest stable patch released October 7.

## Findings

- periodic: latest had lower means in 8/9 cases. Confidence intervals overlapped in 9/9 cases; overlapping intervals limit claims of a decisive version advantage.
- batch: latest had lower means in 0/3 cases. Confidence intervals overlapped in 3/3 cases; overlapping intervals limit claims of a decisive version advantage.
- Sustained batch p95: 6.254 to 4.862 ms; p99: 8.396 to 7.724 ms. One three-minute diagnostic per version; this does not establish repeatability or production capacity.
- Valid measured BDN cases: 24. Cleanup verification for the completed comparison is retained in .artifacts/scylla-version-benchmarks/cleanup-verification.json.

## Controls

Separate disposable upstream containers; four CPUs/shards, 4 GiB container memory, 3 GiB Scylla memory, 256 MiB reserve, epoll reactor, developer mode and overprovisioning. RF=1, LOCAL_QUORUM, durable_writes=true, tablets=false, STCS and classic LZ4/64 KiB compression. Representative dated trade-plan history plus current snapshots; 500,000 seeded one-kilobyte payloads. Seed and readback are outside timing. Write keys cycle over a bounded range; this is an append/upsert test, not unbounded history growth. The synthetic payload compresses across rows. See README.md for full methodology.

The initial 6.2.2 baseline used its existing default STCS/classic LZ4 table settings. The subsequent runs explicitly pin those same settings because 2026.3.3 has changed compression and compaction defaults. These controls represent the existing table layout rather than a comparison of every new default.
The periodic baselines seeded with individual inserts. Follow-up setup uses unlogged batches of 32 rows and four concurrent seed writers for both durable profiles (32 for periodic sustained profiles). This setup is outside measurement; table content is unchanged. The initial latest-version durable seed with 32 concurrent writers timed out, so it is excluded and retained as a load-test finding.

Periodic commit-log sync uses 10 seconds, matching the current development node. Batch commit-log sync uses a 1 ms grouping window and waits for synchronization. These policies are reported separately. CQL batches are unlogged, single-partition batches. Do not compare periodic acknowledgements directly with PostgreSQL synchronous_commit=on.

## BenchmarkDotNet measurements

Milliseconds per invocation; margin is the BDN 99.9% confidence half-width for iteration means, not individual request tail latency. Three warmups/eight measured iterations, at least six accepted values. Negative change means latest is faster. Sequential runs on a shared workstation and overlapping confidence intervals limit claims about causation.

| Sync policy | Method | 6.2.2 mean +/- margin ms | 2026.3.3 mean +/- margin ms | Change | N old/new | CI overlaps |
|---|---|---:|---:|---:|---|---|
| periodic | WriteSnapshot | 1.218 +/- 0.319 | 0.921 +/- 0.114 | -24.4% | 7/8 | True |
| periodic | AppendPlan | 0.926 +/- 0.115 | 0.880 +/- 0.069 | -4.9% | 8/8 | True |
| periodic | SamePartitionBatch8 | 1.180 +/- 0.203 | 1.124 +/- 0.132 | -4.7% | 8/7 | True |
| periodic | SamePartitionBatch32 | 2.058 +/- 0.532 | 1.896 +/- 0.288 | -7.8% | 8/8 | True |
| periodic | SamePartitionBatch128 | 4.501 +/- 0.393 | 4.123 +/- 0.490 | -8.4% | 7/8 | True |
| periodic | LatestSnapshot | 1.099 +/- 0.089 | 1.019 +/- 0.055 | -7.3% | 7/8 | True |
| periodic | History100 | 1.828 +/- 0.220 | 1.604 +/- 0.163 | -12.2% | 8/8 | True |
| periodic | WorkingSetRead | 1.844 +/- 0.120 | 2.004 +/- 0.077 | 8.7% | 8/7 | True |
| periodic | ConcurrentWrite8 | 1.342 +/- 0.099 | 1.240 +/- 0.131 | -7.6% | 8/8 | True |
| batch | AppendPlan | 5.533 +/- 0.226 | 6.396 +/- 1.950 | 15.6% | 8/8 | True |
| batch | SamePartitionBatch32 | 6.203 +/- 0.307 | 6.562 +/- 0.906 | 5.8% | 6/7 | True |
| batch | ConcurrentWrite8 | 20.958 +/- 1.068 | 21.204 +/- 3.642 | 1.2% | 8/8 | True |

## Three-minute sustained diagnostics

Eight writers, 32 logical events per unlogged single-partition batch, offered target 5,000 events/s with pacing. Two explicit memtable flushes are requested around 60/120 seconds. Periodic commit-log sync is used. These are individual request samples, separate from BDN, and are not maximum-throughput, replicated quorum or crash-recovery tests.

| Version | Actual events/s | Batch p50 ms | Batch p95 ms | Batch p99 ms | Successful explicit flushes | Flush duration ms |
|---|---:|---:|---:|---:|---:|---|
| 2026.3.3-sustained | 4308 | 3.298 | 4.862 | 7.724 | 2 | 399.7, 391.9 |
| 6.2.2-sustained | 4252 | 4.649 | 6.254 | 8.396 | 2 | 420.5, 376.1 |

## Environment findings and limits

- Docker/WSL AIO slots were exhausted: 65,530 in use against a ceiling of 65,536. The runtime ceiling was temporarily raised to 1,048,576 for the tests. The runner offers an opt-in switch to raise/restore it; no persistent sysctl configuration is edited.
- Docker's shared filesystem is approximately 98% full. The latest node initially rejected all writes at its default critical-disk threshold. Only the isolated latest-version containers use a 99.5% threshold; a stricter external 3 GiB free-space guard remains active. Free capacity before upgrading with normal safeguards. [ScyllaDB disk guard documentation](https://docs.scylladb.com/manual/stable/troubleshooting/error-messages/critical-disk-utilization.html).
- The current development databases and their settings/data were not changed. Existing backup/manager-agent overhead is excluded from both benchmark nodes. No application snapshot calculation, serialization or actor workflow is included.
- Both versions use the same Windows client. Docker developer-mode measurements on this shared host are not production capacity or SLA guarantees.

## Evidence

- 2026.3.3-batch: `.artifacts\scylla-version-benchmarks\ef83da3447c3\2026.3.3-batch`
- 2026.3.3-periodic: `.artifacts\scylla-version-benchmarks\99b746ea181c\2026.3.3-periodic`
- 6.2.2-batch: `.artifacts\scylla-version-benchmarks\ef83da3447c3\6.2.2-batch`
- 6.2.2-periodic: `.artifacts\scylla-version-benchmarks\9b4753a5b277\6.2.2-periodic`
- Sustained: `.artifacts\scylla-version-benchmarks\ef83da3447c3\2026.3.3-sustained\2026.3.3-sustained-sustained.json`
- Sustained: `.artifacts\scylla-version-benchmarks\ef83da3447c3\6.2.2-sustained\6.2.2-sustained-sustained.json`

Folders retain settings, validated server identity, full BDN JSON/CSV, stdout/stderr, server logs and Docker resource/free-space samples. Sustained folders also retain before/after Prometheus metrics. Failed setup attempts have no valid measured statistics and are excluded.
