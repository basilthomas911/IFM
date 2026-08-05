# MarketData Feed actor benchmark results

Summarized results and interpretation are maintained in [`../TomasAI.IFM.Domain.MarketData.Feed/Docs/Domain-Actor-Optimization-Details.md`](../TomasAI.IFM.Domain.MarketData.Feed/Docs/Domain-Actor-Optimization-Details.md).

Raw BenchmarkDotNet artifacts are intentionally ignored. Reproduce them with:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.MarketData.Feed.Benchmarks -- --filter *
```
