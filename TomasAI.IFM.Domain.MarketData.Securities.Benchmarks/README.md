# MarketData Securities actor benchmarks

Run from the repository root:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.MarketData.Securities.Benchmarks -- --filter *
```

The `Before` implementations retain the reviewed behavior so both versions execute with identical inputs in the same BenchmarkDotNet process. Controlled asynchronous benchmarks model orchestration and call count; they do not claim to reproduce production database or broker latency.
