# Domain.Application benchmarks

Run the deterministic Application actor benchmarks in Release mode:

```powershell
dotnet run --project TomasAI.IFM.Domain.Application.Benchmarks -c Release -- --filter '*' --join
```

The suite compares the pre-optimization implementations reconstructed from commit `0a44bba^` with the current state replay, two-verb command dispatch, and valid-command validation paths. Storage and NATS operations are excluded because their latency is environment-dependent.

See [RESULTS.md](RESULTS.md) for the captured measurements and interpretation.
