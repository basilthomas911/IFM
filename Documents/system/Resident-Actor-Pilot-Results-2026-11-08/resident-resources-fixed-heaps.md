# Fixed-heap server-GC resource validation

Separate quiet measurement process: `DOTNET_gcServer=1`, `DOTNET_GCDynamicAdaptationMode=0`, otherwise the accepted benchmark binary and fixtures. The heap count is left at the runtime default. No Development or production GC setting was changed. All 30 allocation deltas are positive; all repetitions remain published, including the higher first VWAP baseline sample. This sensitivity check does not retroactively repair the default-DATAS samples. Its CPU, GC and latency measurements belong to this collector policy and must not replace primary timings.

| Actor | KiB/request before / after | Allocation reduction | CPU ms before / after | GC pause ms before / after | Monitor contentions before / after | p50 ms before / after | p95 ms before / after | p99 ms before / after |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Vwap | 131.82 / 66.34 | 49.7% | 4005.21 / 2729.17 | 5.76 / 0.00 | 1746.67 / 1820.67 | 45.21 / 30.91 | 50.24 / 42.50 | 64.04 / 52.89 |
| MarketOutlook | 120.39 / 59.85 | 50.3% | 3031.25 / 1479.17 | 0.00 / 0.00 | 1776.67 / 1780.67 | 43.99 / 30.97 | 48.95 / 40.66 | 68.89 / 45.26 |
| FuturesPlan | 146.70 / 97.05 | 33.8% | 2880.21 / 1510.42 | 4.71 / 0.00 | 9.00 / 22.00 | 24.94 / 20.28 | 29.45 / 23.24 | 43.87 / 39.39 |
| VerticalSpreadPlan | 151.48 / 103.28 | 31.8% | 3109.38 / 1552.08 | 5.50 / 0.00 | 12.33 / 20.00 | 25.20 / 20.65 | 28.91 / 24.50 | 41.22 / 26.48 |
| IronCondorPlan | 239.71 / 176.12 | 26.5% | 3942.71 / 2333.33 | 5.70 / 11.49 | 4.00 / 9.67 | 31.03 / 24.76 | 37.37 / 29.43 | 58.88 / 56.69 |

This is consistent with an adaptive-GC-sensitive allocation-accounting problem, but does not establish its exact runtime cause. The fixed-heap control supports lower steady-state allocations for all five warm workloads. Monitor contention still does not improve consistently. Runtime configuration reference: [Microsoft GC settings](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector).

Zero pause/collection values mean no GC occurred inside that particular short sample, not that the path is GC-free. Iron Condor had higher GC pause time in this fixed-heap control despite lower allocation; the result does not support a universal pause reduction.
