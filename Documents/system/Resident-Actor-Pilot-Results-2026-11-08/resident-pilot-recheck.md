# First five resident actor pilots — paired timings

Before: residency disabled. After: residency enabled. Same binary, actual actor and disposable PostgreSQL paths.
Both cold paths include stream resolution. Fixture setup and NATS transport are excluded from timed batches.
Uncertainty shown is one standard error of the measured batch mean; raw BDN reports retain confidence intervals.
Batch means are elapsed time for all commands in a batch; dividing by count is throughput normalization, not individual latency.

| Actor | Workload / streams | Commands | Before batch ms | After batch ms | Improvement | Before / after KiB per command |
|---|---|---:|---:|---:|---:|---:|
| IronCondorPlan | Cold / 1 | 1 | 20.565 ± 0.388 | 19.783 ± 0.270 | +3.8% | 182.70 / 207.16 |
| Vwap | Churn / 16 | 32 | 979.731 ± 2.821 | 981.626 ± 2.864 | -0.2% | 95.02 / 95.93 |
| Vwap | Cold / 1 | 1 | 33.892 ± 1.398 | 35.050 ± 0.903 | -3.4% | 111.26 / 112.40 |
