# First five resident actor pilots — paired timings

Before: residency disabled. After: residency enabled. Same binary, actual actor and disposable PostgreSQL paths.
Both cold paths include stream resolution. Fixture setup and NATS transport are excluded from timed batches.
Uncertainty shown is one standard error of the measured batch mean; raw BDN reports retain confidence intervals.
Batch means are elapsed time for all commands in a batch; dividing by count is throughput normalization, not individual latency.

| Actor | Workload / streams | Commands | Before batch ms | After batch ms | Improvement | Before / after KiB per command |
|---|---|---:|---:|---:|---:|---:|
| FuturesPlan | Churn / 16 | 32 | 484.229 ± 7.309 | 467.180 ± 6.872 | +3.5% | 91.84 / 99.54 |
| FuturesPlan | Cold / 1 | 1 | 16.985 ± 0.358 | 17.559 ± 0.360 | -3.4% | 107.43 / 114.24 |
| FuturesPlan | Replay / 1 | 8 | 28.321 ± 0.632 | 10.905 ± 0.407 | +61.5% | 40.66 / 21.53 |
| FuturesPlan | Warm / 1 | 2 | 31.831 ± 0.419 | 25.847 ± 0.477 | +18.8% | 103.96 / 72.66 |
| FuturesPlan | Warm / 4 | 8 | 41.396 ± 0.459 | 33.861 ± 0.494 | +18.2% | 127.17 / 91.86 |
| FuturesPlan | Warm / 16 | 32 | 51.562 ± 0.597 | 41.754 ± 0.559 | +19.0% | 130.63 / 84.92 |
| FuturesPlan | Warm / 64 | 128 | 219.712 ± 16.095 | 151.600 ± 2.082 | +31.0% | 123.98 / 82.77 |
| IronCondorPlan | Churn / 16 | 32 | 607.119 ± 12.626 | 574.528 ± 8.830 | +5.4% | 175.89 / 201.12 |
| IronCondorPlan | Cold / 1 | 1 | 20.703 ± 0.444 | 21.755 ± 0.562 | -5.1% | 181.79 / 206.98 |
| IronCondorPlan | Replay / 1 | 8 | 35.580 ± 0.655 | 12.106 ± 0.311 | +66.0% | 61.28 / 27.54 |
| IronCondorPlan | Warm / 1 | 2 | 40.010 ± 0.735 | 39.958 ± 2.217 | +0.1% | 186.07 / 144.92 |
| IronCondorPlan | Warm / 4 | 8 | 52.585 ± 0.966 | 41.915 ± 0.500 | +20.3% | 208.31 / 159.50 |
| IronCondorPlan | Warm / 16 | 32 | 62.509 ± 1.020 | 49.595 ± 0.843 | +20.7% | 216.19 / 166.97 |
| IronCondorPlan | Warm / 64 | 128 | 230.419 ± 1.792 | 182.956 ± 2.073 | +20.6% | 206.01 / 157.61 |
| MarketOutlook | Churn / 16 | 32 | 986.756 ± 2.809 | 981.929 ± 2.735 | +0.5% | 88.53 / 88.95 |
| MarketOutlook | Cold / 1 | 1 | 33.909 ± 1.031 | 34.658 ± 0.652 | -2.2% | 103.33 / 104.16 |
| MarketOutlook | Replay / 1 | 8 | 129.334 ± 1.180 | 128.497 ± 0.787 | +0.6% | 52.07 / 30.92 |
| MarketOutlook | Warm / 1 | 2 | 65.817 ± 1.081 | 59.055 ± 1.361 | +10.3% | 99.73 / 64.29 |
| MarketOutlook | Warm / 4 | 8 | 71.000 ± 1.765 | 67.909 ± 0.984 | +4.4% | 106.42 / 78.22 |
| MarketOutlook | Warm / 16 | 32 | 86.978 ± 1.900 | 70.048 ± 2.082 | +19.5% | 109.24 / 62.62 |
| MarketOutlook | Warm / 64 | 128 | 321.631 ± 5.991 | 262.594 ± 2.488 | +18.4% | 109.10 / 59.14 |
| VerticalSpreadPlan | Churn / 16 | 32 | 483.110 ± 4.812 | 473.505 ± 6.546 | +2.0% | 96.05 / 107.15 |
| VerticalSpreadPlan | Cold / 1 | 1 | 16.810 ± 0.307 | 16.851 ± 0.285 | -0.2% | 111.23 / 122.50 |
| VerticalSpreadPlan | Replay / 1 | 8 | 28.193 ± 0.364 | 11.328 ± 0.296 | +59.8% | 41.18 / 21.94 |
| VerticalSpreadPlan | Warm / 1 | 2 | 31.894 ± 0.477 | 24.485 ± 0.242 | +23.2% | 108.16 / 78.64 |
| VerticalSpreadPlan | Warm / 4 | 8 | 42.830 ± 0.505 | 34.516 ± 0.818 | +19.4% | 125.31 / 88.53 |
| VerticalSpreadPlan | Warm / 16 | 32 | 50.966 ± 0.402 | 42.448 ± 0.601 | +16.7% | 125.44 / 95.72 |
| VerticalSpreadPlan | Warm / 64 | 128 | 185.824 ± 1.699 | 150.515 ± 1.330 | +19.0% | 126.24 / 90.90 |
| Vwap | Churn / 16 | 32 | 980.520 ± 3.408 | 982.712 ± 1.516 | -0.2% | 95.08 / 974.58 |
| Vwap | Cold / 1 | 1 | 34.585 ± 0.741 | 37.004 ± 0.557 | -7.0% | 111.08 / 112.27 |
| Vwap | Replay / 1 | 8 | 131.076 ± 0.820 | 127.881 ± 1.218 | +2.4% | 52.82 / 31.63 |
| Vwap | Warm / 1 | 2 | 66.267 ± 1.144 | 61.581 ± 1.086 | +7.1% | 107.11 / 71.46 |
| Vwap | Warm / 4 | 8 | 74.025 ± 1.718 | 69.488 ± 0.606 | +6.1% | 115.52 / 72.87 |
| Vwap | Warm / 16 | 32 | 88.078 ± 2.505 | 70.320 ± 1.046 | +20.2% | 124.02 / 69.53 |
| Vwap | Warm / 64 | 128 | 336.082 ± 5.406 | 263.184 ± 2.642 | +21.7% | 117.21 / 62.84 |

Allocation qualification: these are raw BDN memory diagnostics, measured separately from the timed iterations. The original VWAP churn candidate value was an outlier that did not recur. Separate long probes exposed non-monotonic default-policy allocation counters, so do not treat every allocation figure as a proven reduction. The [fixed-heap warm allocation control](resident-resources-fixed-heaps.md) provides an additional matched validation; it does not alter these original timings or silently replace their memory values.
