# Domain.Fund benchmarks

Run the deterministic Fund collection benchmarks in Release mode:

```powershell
dotnet run --project TomasAI.IFM.Domain.Fund.Benchmarks -c Release -- --filter "*FundCollectionBenchmarks*"
dotnet run --project TomasAI.IFM.Domain.Fund.Benchmarks -c Release -- --filter "*FundSharpeRatioBenchmarks*" "*FundQueryFanOutBenchmarks*" "*FundStateProbeBenchmarks*" "*FundBatchMaterializationBenchmarks*"
```

The suite uses BenchmarkDotNet memory and threading diagnostics for actor-state collections, financial calculations, async query fan-out, and batch transaction materialization. Raw artifacts are intentionally ignored; the reviewed before/after summary is stored in `RESULTS.md`.
