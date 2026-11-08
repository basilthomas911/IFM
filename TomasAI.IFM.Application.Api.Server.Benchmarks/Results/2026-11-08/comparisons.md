# Final paired measurements — 2026-11-08

47 paired comparisons / 94 completed cases. Means and managed allocation per operation; confidence margins, GC collections and monitor counts are in [comparison.csv](comparison.csv). Full generated tables retain uncertainty and threading diagnostics. Negative reduction means a slower mean. Measurements overlap and must not be added.

| Workload | Parameters | Before | After | Time reduction | KiB/op before → after |
|---|---|---:|---:|---:|---:|
| Console | ConsumerDelayMilliseconds=0 | 2.145 us | 20.560 us | -858.6% | 0.00 → 0.56 |
| Console | ConsumerDelayMilliseconds=1 | 494.085 ms | 21.326 us | 100.0% | 0.00 → 0.56 |
| Diagnostic | Consumers=1 | 2.234 ms | 2.253 ms | -0.8% | 4.46 → 5.19 |
| Diagnostic | Consumers=32 | 2.691 ms | 2.079 ms | 22.7% | 121.20 → 12.32 |
| Diagnostic | Consumers=8 | 2.293 ms | 2.176 ms | 5.1% | 30.82 → 6.84 |
| EmaPersistence | Bars=21 | 368.429 ms | 129.878 ms | 64.7% | 2,030.05 → 620.28 |
| EmaPersistence | Bars=48 | 769.566 ms | 307.390 ms | 60.1% | 4,652.71 → 1,258.45 |
| EmaPersistence | Bars=64 | 949.382 ms | 398.899 ms | 58.0% | 6,213.31 → 1,655.77 |
| Expiry | Residents=1 | 19.764 ns | 14.512 ns | 26.6% | 0.00 → 0.00 |
| Expiry | Residents=1024 | 16.121 us | 14.082 ns | 99.9% | 0.00 → 0.00 |
| Expiry | Residents=4096 | 70.423 us | 13.885 ns | 100.0% | 0.00 → 0.00 |
| Expiry | Residents=64 | 990.700 ns | 13.759 ns | 98.6% | 0.00 → 0.00 |
| ExpiryBurst | Residents=4096 | 7.645 ms | 9.898 ms | -29.5% | 12,160.28 → 12,192.35 |
| ExpiryBurst | Residents=64 | 429.563 us | 478.647 us | -11.4% | 210.14 → 210.71 |
| HealthViews | default | 583.333 us | 399.320 ns | 99.9% | 253.05 → 0.28 |
| History | Rows=1000 | 6.267 ms | 894.441 ns | 100.0% | 1,891.06 → 1.34 |
| History | Rows=10080 | 61.744 ms | 881.332 ns | 100.0% | 19,055.71 → 0.00 |
| IndicatorCommand | default | 763.485 ms | 567.646 ms | 25.7% | 3,783.81 → 3,799.91 |
| IndicatorRead | default | 5.629 ms | 4.219 ms | 25.0% | 77.19 → 80.74 |
| Logging | InformationEnabled=False | 60.085 ns | 23.626 ns | 60.7% | 0.00 → 0.00 |
| Logging | InformationEnabled=True | 808.171 ns | 41.768 ns | 94.8% | 0.88 → 0.00 |
| ReceiveQualification | Bytes=256 | 76.290 ns | 50.756 ns | 33.5% | 0.27 → 0.00 |
| ReceiveQualification | Bytes=4096 | 824.955 ns | 190.991 ns | 76.8% | 4.02 → 0.00 |
| ReceiveQualification | Bytes=65536 | 12.469 us | 3.092 us | 75.2% | 64.02 → 0.00 |
| ReceiveQualification | Bytes=950000 | 438.678 us | 47.090 us | 89.3% | 927.74 → 0.00 |
| Redis | default | 2.879 ms | 2.798 ms | 2.8% | 32.59 → 35.46 |
| Rpc | Contracts=13 | 825.026 ns | 458.209 ns | 44.5% | 0.95 → 0.34 |
| Rpc | Contracts=218 | 4.965 us | 4.384 us | 11.7% | 8.15 → 3.95 |
| Rpc | Contracts=3640 | 75.775 us | 66.750 us | 11.9% | 128.46 → 64.10 |
| Rpc | Contracts=50000 | 1.952 ms | 1.190 ms | 39.0% | 2,637.20 → 879.02 |
| RpcDescriptor | default | 233.645 ns | 19.417 ns | 91.7% | 0.27 → 0.00 |
| Seed | Trades=1000 | 393.866 us | 251.105 us | 36.2% | 202.26 → 1,129.41 |
| Seed | Trades=100000 | 62.847 ms | 48.824 ms | 22.3% | 16,523.81 → 16,526.49 |
| SeedReader | Trades=1000 | 4.527 ms | 3.089 ms | 31.8% | 358.54 → 328.58 |
| SeedReader | Trades=100000 | 138.598 ms | 90.388 ms | 34.8% | 23,032.01 → 20,555.52 |
| SeedReader | Trades=300000 | 444.710 ms | 190.907 ms | 57.1% | 57,025.87 → 49,605.45 |
| TradeContention | Producers=1 | 107.400 us | 106.271 us | 1.1% | 221.50 → 221.50 |
| TradeContention | Producers=16 | 2.178 ms | 2.036 ms | 6.5% | 3,544.00 → 3,544.00 |
| TradeContention | Producers=4 | 506.352 us | 514.210 us | -1.6% | 886.01 → 886.00 |
| TradeRestore | Trades=1 | 30.256 ms | 23.590 ms | 22.0% | 183.75 → 170.31 |
| TradeRestore | Trades=128 | 1,834.326 ms | 318.198 ms | 82.7% | 13,400.27 → 10,690.47 |
| TradeRestore | Trades=32 | 449.092 ms | 80.862 ms | 82.0% | 2,279.15 → 1,678.38 |
| TradeRestore | Trades=4 | 67.595 ms | 27.010 ms | 60.0% | 360.63 → 281.16 |
| Warmup | Scenario=Independent | 17.397 ms | 14.729 ms | 15.3% | 1,728.42 → 2,005.88 |
| Warmup | Scenario=Retry | 4.042 ms | 4.347 ms | -7.5% | 400.39 → 466.40 |
| Warmup | Scenario=SameKey | 3.013 ms | 3.147 ms | -4.4% | 222.41 → 264.62 |
| Warmup | Scenario=Stale | 3.685 ms | 2.643 ms | 28.3% | 378.96 → 334.73 |
