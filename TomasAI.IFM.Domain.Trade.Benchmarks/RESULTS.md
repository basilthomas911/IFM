# Domain.Trade actor benchmark results

## Regime Discovery execution-mode qualification (2026-08-26)

BenchmarkDotNet 0.15.8 on .NET 10.0.10 compared sequential specialist
calculation with awaited ordinary thread-pool fan-out. Three-workflow cases
schedule the workflows independently to model actor-dispatcher concurrency.

| Horizon | Workflows | Sequential | Thread-pool parallel | Ratio |
| --- | ---: | ---: | ---: | ---: |
| Daily | 1 | 35.11 us | 53.01 us | 1.51 |
| Daily | 3 | 81.14 us | 81.93 us | 1.02 |
| Weekly | 1 | 38.35 us | 50.83 us | 1.33 |
| Weekly | 3 | 84.67 us | 93.98 us | 1.11 |
| Monthly | 1 | 36.01 us | 52.09 us | 1.45 |
| Monthly | 3 | 81.34 us | 88.82 us | 1.09 |

Decision: keep `RegimeDiscoveryExecutionMode.Sequential`. Inner parallelism
was slower in every case and allocated about two percent more memory.

Summarized results and interpretation are maintained in [`../TomasAI.IFM.Domain.Trade/Docs/Domain-Actor-Optimization-Details.md`](../TomasAI.IFM.Domain.Trade/Docs/Domain-Actor-Optimization-Details.md).

Raw BenchmarkDotNet artifacts are intentionally ignored. Reproduce them with:

```powershell
dotnet run -c Release --project TomasAI.IFM.Domain.Trade.Benchmarks -- --filter *
```
