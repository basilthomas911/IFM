# MarketData Analytics actor benchmark results

The summarized BenchmarkDotNet results, environment, interpretation, and top-ten findings are maintained in [`../TomasAI.IFM.Domain.MarketData.Analytics/Docs/Domain-Actor-Optimization-Details.md`](../TomasAI.IFM.Domain.MarketData.Analytics/Docs/Domain-Actor-Optimization-Details.md).

Raw BenchmarkDotNet artifacts are intentionally ignored. Reproduce them with:

```powershell
dotnet run --project TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks -c Release -- --filter "*IndicatorBenchmarks*" "*ValidationBenchmarks*"
```
