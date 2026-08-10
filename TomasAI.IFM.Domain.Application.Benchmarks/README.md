# Domain.Application benchmarks

Run the deterministic Application actor benchmarks in Release mode:

```powershell
dotnet run --project TomasAI.IFM.Domain.Application.Benchmarks -c Release -- --filter '*' --join
```

The suite compares the pre-optimization implementations reconstructed from commit `0a44bba^` with the current state replay, two-verb command dispatch, and valid-command validation paths. Storage and NATS operations are excluded because their latency is environment-dependent.

See [RESULTS.md](RESULTS.md) for the captured measurements and interpretation.

Run the SWO-06 current-versus-bounded event-projector recovery CPU/allocation comparison independently:

```powershell
dotnet run --project TomasAI.IFM.Domain.Application.Benchmarks -c Release -- --filter '*EventProjectorRecoveryBaselineBenchmarks*'
```

Both paths use synchronous fake storage/queue completions and therefore exclude PostgreSQL and NATS latency. The
baseline retains full-set materialization, JSON deserialization, the state N+1 call shape, state write, and enqueue.
The bounded comparison uses 256-row joined keyset pages, eight cross-stream lanes, conditional claims, same-stream
ordering, deserialization, and enqueue. MemoryDiagnoser reports cumulative allocation, not peak retained recovery
inventory; the implementation bounds live inventory to one page plus its active stream groups.
