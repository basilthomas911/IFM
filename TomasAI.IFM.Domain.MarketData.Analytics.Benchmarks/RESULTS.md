# MarketData Analytics actor benchmark results

The summarized BenchmarkDotNet results, environment, interpretation, and top-ten findings are maintained in [`../TomasAI.IFM.Domain.MarketData.Analytics/Docs/Domain-Actor-Optimization-Details.md`](../TomasAI.IFM.Domain.MarketData.Analytics/Docs/Domain-Actor-Optimization-Details.md).

Raw BenchmarkDotNet artifacts are intentionally ignored. Reproduce them with:

```powershell
dotnet run --project TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks -c Release -- --filter "*IndicatorBenchmarks*" "*ValidationBenchmarks*"
```

## Futures trade-session bar publication validation (2026-09-15)

BenchmarkDotNet 0.15.8, .NET 10.0.10, Windows x64, concurrent workstation GC;
five measured iterations after two warmups. The sample is one valid completed
ES 15-second bar with the Databento market timestamp 340 ms ahead of its true
host finalization timestamp. These methods run when a bar closes, never on
each trade tick.

| Method | Mean | Managed allocation per bar |
| --- | ---: | ---: |
| Shared bar-model validation | 5.651 µs | 2.82 KB |
| Full publication ingress validation | 8.846 µs | 4.57 KB |

The extra routing/completion checks cost about 3.2 µs and 1.75 KB per closed
bar in this run. The full-ingress result had a broad 99.9% interval (5.54–12.15
µs), so this is a sizing estimate rather than a tight speed claim. The Command
actor repeats validation as its authoritative safety boundary. No per-tick
allocation was added by the publication change.

Reproduce with `NuGetAudit=false` in restricted environments:

```powershell
$env:NuGetAudit = 'false'
dotnet run --project TomasAI.IFM.Domain.MarketData.Analytics.Benchmarks -c Release -- --filter "*FuturesBarPublicationValidationBenchmarks*"
```
