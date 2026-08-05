# MarketData Analytics actor benchmarks

Run all benchmarks from the repository root:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks -- --filter *
```

The `Before` methods retain the pre-optimization implementations so the same BenchmarkDotNet process, runtime, and input data measure both versions. Database and NATS latency are intentionally excluded; deterministic unit tests cover concurrent fan-out and timer lifecycle behavior.
