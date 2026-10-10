# ScyllaDB version benchmarks

This standalone BenchmarkDotNet project compares **6.2.2** (the version currently deployed in development) with **2026.3.3** (the latest stable patch verified October 9, 2026). It uses the same ScyllaDBCSharpDriver 3.22.0.4 as the repository, plus BenchmarkDotNet 0.15.8 on .NET 10.

```powershell
scripts/Benchmarks/Compare-ScyllaVersions.ps1 -ManageWslAioLimit
```

The optional switch temporarily raises Docker/WSL's AIO ceiling to 1,048,576 and restores its original value after cleanup. The running development services currently use almost all of the default 65,536 slots, leaving insufficient capacity to launch another Scylla node. Without the switch, configure sufficient AIO capacity before running. No persistent sysctl file is modified. Use `-OnlyProfiles` to select profiles such as `6.2.2-periodic,2026.3.3-periodic`.

## Isolation and controls

Each profile gets its own labelled container and volume. Profiles run sequentially, with four CPUs, four shards, 4 GiB container memory, 3 GiB Scylla memory and an explicit 256 MiB reserve. Containers use developer mode, overprovisioning and the epoll reactor consistently. Ordinary CQL and shard-aware CQL ports are mapped identically inside/outside Docker (59142/59143); REST/metrics use localhost 59144/59145. Endpoint, actual server version and commit-log policy are checked before any schema writes.

The benchmark keyspace has replication factor one, LOCAL_QUORUM, durable_writes=true, tablets disabled and SizeTieredCompactionStrategy and classic LZ4 compression with 64 KiB chunks for all three tables. This retains vnode data placement on both versions and isolates the engine comparison. Replication factor one does not test distributed quorum or failover. A fresh upstream 6.2.2 image is compared with the latest upstream image; the development node's backup/manager agent is excluded from both.

Commit-log segment size is 32 MiB and the space budget is 256 MiB on both versions, bounding disk use and inducing memtable flushes. A 3 GiB free-space guard protects Docker's shared filesystem. The shared filesystem is approximately 98% used; ScyllaDB 2026.3.3 rejects writes at its default 98% critical-disk threshold. Only the disposable latest-version node uses a 99.5% threshold, with the stricter external 3 GiB guard remaining active. This override is for the bounded benchmark and is not an upgrade recommendation. Free storage before adopting the latest release with its normal safeguards. Cleanup verifies exact run labels and never removes development volumes or prunes unrelated Docker resources.

## Measurements

Nine cases per version use periodic commit-log synchronization every 10 seconds, matching the current node's policy. Three write cases per version additionally use batch commit-log synchronization with a 1 ms grouping window. The latter waits for commit-log synchronization; periodic acknowledgements are not comparable to PostgreSQL synchronous_commit=on. CQL batches are unlogged single-partition batches and must not be interpreted as general multi-partition transactions.

The workload uses a representative dated trade-plan composite partition key and descending revision history, with 500,000 seeded one-kilobyte payloads in 500 partitions and a separate current-snapshot table. Seed/prepare/readback/initial flush happen outside timed iterations. Seeding uses unlogged batches of up to 32 rows within one partition; the initial periodic baselines seeded the same data with individual inserts. Durable-write comparisons both use the batched setup with four concurrent seed writers. Periodic sustained setup uses 32 concurrent seed writers. The latest-version durable setup initially timed out with 32 writers; the failed attempt is excluded and preserved in evidence. Prepared CQL cases cover current-snapshot upserts, plan writes, batches of 8/32/128, latest reads, 100-row history, reads traversing the seed working set, and eight concurrent writes. Write revisions cycle over 65,536 keys to bound storage, so these measure append/upsert execution and not unbounded history growth. The seed remains unchanged between cases; write keys are in separate partitions.

The fixed deterministic payload is synthetic and compressible across rows; results do not predict a production dataset's compression ratio. Measured time includes driver binding, network, execution and row decoding, but excludes application snapshot calculation and serialization. BDN uses three warmups and eight measured iterations, with at least six accepted samples required. Confidence margins describe iteration means, not individual request p95/p99.

Two separate three-minute diagnostics offer 5,000 logical events/s across eight writers using batches of 32. They capture actual request p50/p95/p99, two explicitly requested memtable flushes, and before/after Prometheus metrics. These diagnostics use periodic sync on both versions and are not BenchmarkDotNet results. They are not maximum-throughput or crash-durability tests.

Raw evidence is retained under `.artifacts/scylla-version-benchmarks/<run-id>/`: settings, validation, full BDN JSON/CSV, client/server logs, Docker resource/free-space samples, sustained JSON and Prometheus snapshots. Latest release verification: https://forum.scylladb.com/t/release-scylladb-2026-3-3/5531

