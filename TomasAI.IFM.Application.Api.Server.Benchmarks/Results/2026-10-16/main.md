```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen Threadripper 1950X 3.80GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  Job-VNQJGX : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  Server=True  IterationCount=8  
LaunchCount=1  WarmupCount=3  

```
| Method                 | Categories            | Mean          | Error         | StdDev      | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|----------------------- |---------------------- |--------------:|--------------:|------------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| IdleExpiryBefore       | 01_IdleExpiry         |  1,061.900 ns |    30.2065 ns |  15.7986 ns |  1.00 |    0.02 |                    - |                - | 0.0286 |    2320 B |        1.00 |
| IdleExpiryAfter        | 01_IdleExpiry         |     14.874 ns |     0.4102 ns |   0.1463 ns |  0.01 |    0.00 |                    - |                - |      - |         - |        0.00 |
|                        |                       |               |               |             |       |         |                      |                  |        |           |             |
| QualificationBefore    | 02_Qualification      |    989.564 ns |    75.5225 ns |  39.4997 ns |  1.00 |    0.05 |                    - |                - | 0.0286 |    2320 B |        1.00 |
| QualificationAfter     | 02_Qualification      |    129.402 ns |     4.6721 ns |   2.0744 ns |  0.13 |    0.01 |                    - |                - | 0.0048 |     368 B |        0.16 |
|                        |                       |               |               |             |       |         |                      |                  |        |           |             |
| SpreadMathBefore       | 03_SpreadMath         |    957.091 ns |    36.2167 ns |  18.9420 ns |  1.00 |    0.03 |                    - |                - | 0.0191 |    1568 B |        1.00 |
| SpreadMathAfter        | 03_SpreadMath         |    252.763 ns |     8.0483 ns |   4.2094 ns |  0.26 |    0.01 |                    - |                - | 0.0038 |     312 B |        0.20 |
|                        |                       |               |               |             |       |         |                      |                  |        |           |             |
| LegLookupBefore        | 04_LegLookup          |     26.148 ns |     0.6447 ns |   0.3372 ns |  1.00 |    0.02 |                    - |                - | 0.0005 |      40 B |        1.00 |
| LegLookupAfter         | 04_LegLookup          |      7.899 ns |     0.2195 ns |   0.1148 ns |  0.30 |    0.01 |                    - |           0.0000 |      - |         - |        0.00 |
|                        |                       |               |               |             |       |         |                      |                  |        |           |             |
| FanoutBefore           | 05_DuplicateFanout    |    453.695 ns |    13.2889 ns |   6.9503 ns |  1.00 |    0.02 |                    - |                - | 0.0057 |     456 B |        1.00 |
| FanoutAfter            | 05_DuplicateFanout    |    157.554 ns |    17.1612 ns |   8.9756 ns |  0.35 |    0.02 |                    - |                - |      - |         - |        0.00 |
|                        |                       |               |               |             |       |         |                      |                  |        |           |             |
| ConcurrentLookupBefore | 06_ConcurrentLookup   |    116.788 ns |     4.4589 ns |   2.3321 ns |  1.00 |    0.03 |                    - |           0.0005 |      - |         - |          NA |
| ConcurrentLookupAfter  | 06_ConcurrentLookup   |      2.967 ns |     0.5028 ns |   0.2630 ns |  0.03 |    0.00 |                    - |           0.0000 |      - |         - |          NA |
|                        |                       |               |               |             |       |         |                      |                  |        |           |             |
| RegistryExpiryBefore   | 07_RegistryExpiry1024 | 54,811.947 ns |   511.9392 ns | 227.3042 ns |  1.00 |    0.01 |                    - |                - | 0.0610 |    8320 B |        1.00 |
| RegistryExpiryAfter    | 07_RegistryExpiry1024 | 49,426.801 ns | 1,569.7477 ns | 821.0084 ns |  0.90 |    0.01 |                    - |                - |      - |         - |        0.00 |
|                        |                       |               |               |             |       |         |                      |                  |        |           |             |
| EvidenceBefore         | 08_ConcurrentEvidence |    105.499 ns |     7.7224 ns |   4.0389 ns |  1.00 |    0.05 |                    - |           0.0003 |      - |         - |          NA |
| EvidenceAfter          | 08_ConcurrentEvidence |      1.727 ns |     0.1188 ns |   0.0528 ns |  0.02 |    0.00 |                    - |           0.0000 |      - |         - |          NA |
|                        |                       |               |               |             |       |         |                      |                  |        |           |             |
| ReadinessBefore        | 09_ReadinessHealth    |     83.835 ns |     5.7711 ns |   2.5624 ns |  1.00 |    0.04 |                    - |                - | 0.0043 |     336 B |        1.00 |
| ReadinessAfter         | 09_ReadinessHealth    |      4.863 ns |     0.0814 ns |   0.0362 ns |  0.06 |    0.00 |                    - |                - |      - |         - |        0.00 |
|                        |                       |               |               |             |       |         |                      |                  |        |           |             |
| LifecycleBefore        | 10_LifecycleHealth    |    732.189 ns |    14.3136 ns |   7.4863 ns | 1.000 |    0.01 |                    - |           0.0000 | 0.0191 |    1520 B |        1.00 |
| LifecycleAfter         | 10_LifecycleHealth    |      6.950 ns |     0.2628 ns |   0.1375 ns | 0.009 |    0.00 |                    - |                - |      - |         - |        0.00 |
