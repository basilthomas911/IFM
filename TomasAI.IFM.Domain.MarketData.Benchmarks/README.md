# Domain.MarketData benchmarks

Run the deterministic MarketData actor-state and validation benchmarks in Release mode:

```powershell
dotnet run --project TomasAI.IFM.Domain.MarketData.Benchmarks -c Release -- --filter "*YieldCurveRate*"
```

The suite uses BenchmarkDotNet memory and threading diagnostics. Database and NATS latency are excluded so actor CPU and garbage-collection costs remain measurable; integration and concurrency tests cover the complete asynchronous pipeline.

See [RESULTS.md](RESULTS.md) for the captured before-and-after measurements and findings from the optimization pass.

Run only the command parse-dispatch experiment with:

```powershell
dotnet run --project TomasAI.IFM.Domain.MarketData.Benchmarks -c Release -- --filter "*YieldCurveRateParseDispatchBenchmarks*"
```
