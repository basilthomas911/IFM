# MarketData Feed actor benchmarks

Run from the repository root:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.MarketData.Feed.Benchmarks -- --filter *
```

The `Before` methods retain the reviewed implementations so both versions execute with identical inputs in the same BenchmarkDotNet process. Timer lifecycle, actor error propagation, database behavior, and broker safety are covered by deterministic tests rather than synthetic microbenchmarks.
