```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen Threadripper 1950X 3.80GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]                              : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  ApiServerPerformance-Net10-ServerGC : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Job=ApiServerPerformance-Net10-ServerGC  Runtime=.NET 10.0  Server=True  
Arguments=/p:ArtifactsPath=E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107\acceptance-20261108-final\generated,/p:DatabentoNativeImplementation=Rust,/p:DatabentoNativeConfiguration=Debug,/m:2  Toolchain=Pinned-ApiServerPerformance-Net10  IterationCount=10  
LaunchCount=2  WarmupCount=3  

```
| Method            | Consumers | Mean     | Error     | StdDev    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------------ |---------- |---------:|----------:|----------:|------:|--------:|---------------------:|-----------------:|----------:|------------:|
| **IndependentBefore** | **1**         | **2.234 ms** | **0.2343 ms** | **0.2699 ms** |  **1.01** |    **0.16** |               **6.0078** |                **-** |   **4.46 KB** |        **1.00** |
| CoalescedAfter    | 1         | 2.253 ms | 0.1895 ms | 0.2182 ms |  1.02 |    0.15 |               7.0195 |                - |   5.19 KB |        1.16 |
|                   |           |          |           |           |       |         |                      |                  |           |             |
| **IndependentBefore** | **8**         | **2.293 ms** | **0.1178 ms** | **0.1310 ms** |  **1.00** |    **0.08** |              **13.0352** |                **-** |  **30.82 KB** |        **1.00** |
| CoalescedAfter    | 8         | 2.176 ms | 0.1477 ms | 0.1642 ms |  0.95 |    0.09 |              14.0156 |                - |   6.84 KB |        0.22 |
|                   |           |          |           |           |       |         |                      |                  |           |             |
| **IndependentBefore** | **32**        | **2.691 ms** | **0.0960 ms** | **0.1105 ms** |  **1.00** |    **0.06** |              **37.1445** |                **-** |  **121.2 KB** |        **1.00** |
| CoalescedAfter    | 32        | 2.079 ms | 0.0782 ms | 0.0901 ms |  0.77 |    0.05 |              38.0352 |                - |  12.32 KB |        0.10 |
