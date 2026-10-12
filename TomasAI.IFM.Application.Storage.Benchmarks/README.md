# Application Storage benchmarks

Run the typed snapshot-range replay comparison in Release mode:

```powershell
dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks -c Release -- --filter "*SnapshotRangeReplayBenchmarks*"
```

Run the RSI, MACD, ADX, and ATR intraday/daily matrix:

```powershell
dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks -c Release -- --filter "*PeriodSignalReplayBenchmarks*"
```

The benchmark isolates managed state reconstruction after row selection. PostgreSQL query correctness and ordering
are covered by `EventSourceActorSnapshotRangeTests`; database query plans should be measured separately with
`EXPLAIN (ANALYZE, BUFFERS)` against representative production-scale streams.

## PostgreSQL Docker version comparison

Run the isolated real-database BenchmarkDotNet comparison:

```powershell
./scripts/Benchmarks/Compare-PostgresVersions.ps1
```

The runner requires Docker Desktop with Linux containers and pre-pulled
`postgres:17.2-bookworm`, `postgres:17.11-bookworm`, and `postgres:18.6-bookworm` images.
Each server runs alone with 4 CPUs, 4 GiB RAM, 512 MiB shared memory, and its own Docker
volume. Its port is bound to loopback (56417-56419). The existing `ifm_db` container and
its volume are never used. The runner generates temporary credentials and removes only
containers/volumes carrying its unique benchmark run label. It restores its process environment
when finished. If interrupted outside PowerShell's finally handling, any remaining resources
are named `ifm-pgbench-<run-id>-...` and labelled `ifm.postgres.benchmark=<run-id>`.

`PostgresVersionBenchmarks` uses Npgsql 10.0.3 and prepared commands against an event-log
schema representative of IFM: per-stream primary key, globally unique event version, command
index, event-name/version index, and an atomic stream-version update alongside appends.
Each case gets a new schema with 100 streams, 1,000 events per stream, 1 KiB payloads and a
snapshot every 100 events. VACUUM ANALYZE runs before measurement. Each workload is checked
for successful writes and expected read results before BenchmarkDotNet warmup.

| Method | One reported benchmark operation |
| --- | --- |
| AppendEvent | One event and stream-version update, one durable commit |
| AppendBatch32 | 32 events and stream-version update, one durable commit |
| LatestSnapshot | Read and materialize one indexed snapshot payload |
| History100 | Read and materialize 100 payloads, about 100 KiB |
| ConcurrentAppend8 | Eight simultaneous one-event commits on eight independent streams/connections; elapsed time until all complete |

Full durability is retained: `fsync`, `synchronous_commit`, and `full_page_writes` are on.
All versions use Bookworm packaging and the same shared-buffer, work-memory, checkpoint and
WAL-size settings. PostgreSQL version-specific defaults (including 18's I/O implementation)
are recorded in metadata rather than disabled.

BenchmarkDotNet performs one launch, three warmup iterations and eight measured iterations,
with a minimum iteration duration of 500 ms. Results contain mean, error, standard deviation
and managed allocations. These are database round-trip measurements from the Windows .NET
client through Docker Desktop; they do not include IFM actor handling, serialization or the
complete financial event transaction path. Mean iteration measurements are not individual
request p95/p99 latency or maximum sustainable transaction throughput.

Output is under `.artifacts/postgres-version-benchmarks/<run-id>/`: build log, exact image
digests/settings, per-version BenchmarkDotNet JSON/CSV/Markdown reports, server logs and
Docker CPU/memory/block-I/O samples. The first completed comparison and its scope are recorded
in `POSTGRES-VERSION-RESULTS.md`. Docker samples include setup, benchmark generation/warmup
and measurement; they are diagnostic samples, not workload-specific resource-normalized results.

## PostgreSQL event/projection durability comparison

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Benchmarks/Compare-PostgresDurability.ps1`.
The script runs full BenchmarkDotNet measurements (three warmups, eight measured iterations)
and two 120-second rate-controlled load modes per profile. Use `-LoadSeconds 1800` for
30 minutes per load mode. These load rates are initial comparison rates, not production capacity.

| Profile | Source fsync / synchronous_commit | Projection boundary |
| --- | --- | --- |
| AtomicDurable | on / on | Same durable transaction |
| AtomicAsyncCommit | on / off | Same transaction; acknowledged recent commits can be lost |
| AtomicNoFsync | off / on | Same transaction; crash corruption risk |
| AtomicNoFsyncAsyncCommit | off / off | Same transaction; crash corruption risk |
| SplitSameInstanceAsyncProjection | on / on | Separate transaction, fsync on, synchronous_commit off |
| SplitSeparateUnsafeProjection | on / on | Separate instance and transaction, fsync off, synchronous_commit off |

Five BenchmarkDotNet methods measure event append/commit, projection write/commit, complete
plan persistence, ledger persistence, and concurrent plan/ledger persistence. The component
benchmarks use independent fixtures; their means are not additive predictions for the full path.
The workloads execute actual source-event serialization/appends, Iron Condor plan projection
writes, ledger postings, and simultaneous plan/ledger writes. Financial transactions remain
fully durable in the split profiles. Each profile uses new isolated PostgreSQL 17.2 volumes;
only the active profile's instances run. The separate-instance case has an additional server
and resource budget, so its result also reflects that architectural difference.

`fsync` is instance-wide; it cannot differ between tables or schemas in one PostgreSQL instance.
`synchronous_commit` is transaction-wide; an atomic event/projection transaction cannot give its
source and projection different commit durability. The same-instance split uses `SET LOCAL
synchronous_commit = off` for its projection transaction only.

All profiles retain full_page_writes, verify effective settings, and run an injected constraint
failure/rollback probe. Transaction rollback does not prove power-loss durability. With fsync
on and asynchronous commit, a crash can discard acknowledged transactions while retaining a
consistent database. With fsync off, corruption is possible. See
[PostgreSQL WAL settings](https://www.postgresql.org/docs/current/runtime-config-wal.html).

The split profiles are experimental measurement paths only. They do not implement automatic
projection recovery, alter host configuration, or change the no-replay trade-plan policy.
FinancialHistoryProjection is an example of a rebuildable display projection. Ledger journals,
balances, and financial fences are authoritative state and remain durable; receipt-only completed
events do not contain all journal line data required to reconstruct those authoritative records.
A recoverable business projection requires a durable pending-projection marker committed with
the source event, replayable inputs, an idempotent projector, and checkpoint recovery. A source
commit followed by a failed projection leaves a gap; there is no cross-instance atomic commit.

Reports and verified settings are saved under `.artifacts/postgres-durability/<run-id>/`.
BenchmarkDotNet reports mean/error/standard deviation and allocation; load reports include
completed operations, errors, missed offered slots, and approximate p50/p95/p99 latency. The
script removes only its own generated containers/volumes after preserving reports. Application
databases and their settings remain unchanged.
