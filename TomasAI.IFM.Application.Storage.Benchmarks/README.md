# Application Storage benchmarks

Run the typed snapshot-range replay comparison in Release mode:

```powershell
dotnet run --project TomasAI.IFM.Application.Storage.Benchmarks -c Release -- --filter "*SnapshotRangeReplayBenchmarks*"
```

The benchmark isolates managed state reconstruction after row selection. PostgreSQL query correctness and ordering
are covered by `EventSourceActorSnapshotRangeTests`; database query plans should be measured separately with
`EXPLAIN (ANALYZE, BUFFERS)` against representative production-scale streams.
