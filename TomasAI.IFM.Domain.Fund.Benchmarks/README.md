# Domain.Fund benchmarks

Run the deterministic Fund collection benchmarks in Release mode:

```powershell
dotnet run --project TomasAI.IFM.Domain.Fund.Benchmarks -c Release -- --filter "*FundCollectionBenchmarks*"
```

The suite uses BenchmarkDotNet memory and threading diagnostics with 32- and 256-item actor-state collections. Raw artifacts are intentionally ignored; the reviewed before/after summary is stored in `RESULTS.md`.
