# Repeated resource probe

All 30 repeat samples are retained. Same binary and Server GC; three alternating before/after repetitions, 1,280 requests each. Descriptive averages of each metric are shown; these are not per-request CPU values or confidence intervals.

| Actor | CPU ms before / after | KiB/request before / after | GC pause ms before / after | Monitor contentions before / after | p50 ms before / after | p95 ms before / after | p99 ms before / after |
|---|---:|---:|---:|---:|---:|---:|---:|
| Vwap | 3984.38 / 2750.00 | 133.31 / 69.91 | 25.83 / 10.39 | 1639.33 / 1764.67 | 45.02 / 30.93 | 51.82 / 42.62 | 65.04 / 58.07 |
| MarketOutlook | 2833.33 / 1677.08 | 121.41 / invalid | 15.55 / 6.42 | 1764.33 / 1750.00 | 44.87 / 30.66 | 50.52 / 40.51 | 62.63 / 45.37 |
| FuturesPlan | 3239.58 / 1734.38 | 145.07 / 129.10 | 19.38 / 14.64 | 12.00 / 21.67 | 24.79 / 20.40 | 29.67 / 23.99 | 42.35 / 37.51 |
| VerticalSpreadPlan | 2703.12 / 2119.79 | invalid / 103.59 | 26.39 / 19.70 | 8.33 / 16.67 | 25.09 / 20.09 | 29.10 / 23.80 | 41.51 / 26.04 |
| IronCondorPlan | 4807.29 / 1927.08 | 237.45 / 176.25 | 33.51 / 19.75 | 2.33 / 5.00 | 30.73 / 23.55 | 37.22 / 28.91 | 58.73 / 51.29 |

The initial and repeated results must be read together. Monitor contention remains mixed. Thread-pool/semaphore waits are not captured by Monitor.LockContentionCount. PeakWorkingSet is cumulative within each process, not an isolated cache byte measurement. The probes force full GC before each timed sample, while measuring natural collection and pause deltas inside that sample.

Allocation validity: the precise process-lifetime allocation counter decreased in Market Outlook enabled repetition 2 (-45,011,904 bytes) and Vertical Spread disabled repetition 0 (-203,398,728 bytes). Their aggregate allocation figures are invalid, not savings. Do not clamp these to zero or drop them silently. Other long-probe allocation totals also varied; no repeatable process-wide allocation percentage is asserted from these probes. BDN allocation diagnostics are reported separately, including the VWAP churn outlier and its clean repeat. The root cause of the runtime counter discontinuity has not been established. The documented API returns a lifetime process allocation count: [Microsoft GC.GetTotalAllocatedBytes](https://learn.microsoft.com/en-us/dotnet/api/system.gc.gettotalallocatedbytes?view=net-10.0).
