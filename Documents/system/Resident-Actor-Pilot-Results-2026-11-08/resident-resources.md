# Separate resource probe

Three alternating before/after repetitions per actor; 1,280 requests per sample across 16 independent streams. CPU is process-wide across all threads. Setup is excluded; activity instrumentation is enabled on both paths. Means below are descriptive, not statistical significance claims.

| Actor | CPU ms before / after | KiB/request before / after | GC pause ms before / after | Monitor contentions before / after | p50 ms before / after | p95 ms before / after | p99 ms before / after |
|---|---:|---:|---:|---:|---:|---:|---:|
| Vwap | 4437.50 / 2932.29 | 132.09 / 67.52 | 24.84 / 11.41 | 1713.67 / 1786.33 | 44.81 / 31.16 | 50.44 / 43.32 | 60.70 / 53.77 |
| MarketOutlook | 2744.79 / 1588.54 | 122.74 / 61.59 | 24.87 / 9.34 | 1761.33 / 1733.67 | 45.07 / 33.80 | 52.48 / 45.52 | 65.73 / 50.31 |
| FuturesPlan | 3020.83 / 1750.00 | 144.96 / 97.23 | 16.91 / 10.96 | 10.00 / 24.33 | 24.83 / 19.88 | 28.44 / 23.54 | 75.05 / 46.52 |
| VerticalSpreadPlan | 2781.25 / 1744.79 | 149.75 / 103.12 | 14.69 / 9.36 | 16.00 / 20.00 | 24.34 / 20.78 | 27.91 / 24.34 | 60.67 / 27.14 |
| IronCondorPlan | 5265.62 / 2671.88 | 241.78 / 172.42 | 16.17 / 9.61 | 7.00 / 9.33 | 31.77 / 24.47 | 37.77 / 29.66 | 80.13 / 89.55 |

The three Iron Condor p99 samples are 105.03, 42.96, 92.39 ms before and 30.58, 83.21, 154.86 ms after. Tail latency is noisy; the initial probe does not establish a p99 improvement. Focused repeats are reported separately.

Warm state loads fall from 1,296 to 16 per sample (including 16 setup loads): 1,280 to zero additional loads during the measured workload. This does not eliminate audit, optimistic concurrency, event writes, projection writes or head validation. Npgsql command activities fall from approximately 7 to 5 per command for four pilots and 10 to 7 for Iron Condor. Activities can represent batched SQL; these are not exact SQL statement counts.

Raw per-repetition GC0/1/2, CPU, locks, working set, process peak, live managed heap, cache counters and latency percentiles are preserved in resident-resources.csv and the E: evidence root. Monitor contention does not improve consistently. Working-set samples are process-level observations across sequential workloads; process peak is cumulative and cannot be attributed to one cache.

The repeated probe exposed non-monotonic lifetime allocation counters. Treat these initial long-probe allocation averages as observed diagnostics rather than a confirmed allocation reduction. See [repeat validity analysis](resident-resources-repeat.md); do not combine invalid allocation samples into aggregate percentages.

CPU and working set cover the benchmark client process, which runs the actual actor code; PostgreSQL/NATS server processes are excluded. Per-request latency is direct actor-plus-DB invocation latency and excludes NATS transport and ActorRuntime mailbox scheduling.
