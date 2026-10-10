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
