# MarketData Feed actor benchmark results

Summarized results and interpretation are maintained in [`../TomasAI.IFM.Domain.MarketData.Feed/Docs/Domain-Actor-Optimization-Details.md`](../TomasAI.IFM.Domain.MarketData.Feed/Docs/Domain-Actor-Optimization-Details.md).

Raw BenchmarkDotNet artifacts are intentionally ignored. Reproduce them with:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.MarketData.Feed.Benchmarks -- --filter *
```

## Tick aggregation active-prefix serialization

Full implementation context is in [`../TomasAI.IFM.Domain.MarketData.Feed/Docs/Tick-Aggregation-Implementation-Details.md`](../TomasAI.IFM.Domain.MarketData.Feed/Docs/Tick-Aggregation-Implementation-Details.md).

| Quotes | Before copy + serialize | After pooled-segment serialize | Before allocation | After allocation |
|---:|---:|---:|---:|---:|
| 8 | 1.773 us | 1.637 us | 1,488 B | 416 B |
| 64 | 13.676 us | 12.967 us | 10,608 B | 3,264 B |
