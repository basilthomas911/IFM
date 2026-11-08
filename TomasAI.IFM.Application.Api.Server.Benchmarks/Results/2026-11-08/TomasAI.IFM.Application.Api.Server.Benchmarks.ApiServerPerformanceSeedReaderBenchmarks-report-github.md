```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen Threadripper 1950X 3.80GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]                              : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  ApiServerPerformance-Net10-ServerGC : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Job=ApiServerPerformance-Net10-ServerGC  Runtime=.NET 10.0  Server=True  
Arguments=/p:ArtifactsPath=E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107\acceptance-20261108-final\generated,/p:DatabentoNativeImplementation=Rust,/p:DatabentoNativeConfiguration=Debug,/m:2  Toolchain=Pinned-ApiServerPerformance-Net10  InvocationCount=1  
IterationCount=10  LaunchCount=2  UnrollFactor=1  
WarmupCount=3  

```
| Method     | Trades | Mean       | Error      | StdDev     | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated   | Alloc Ratio |
|----------- |------- |-----------:|-----------:|-----------:|------:|--------:|---------------------:|-----------------:|------------:|------------:|
| **ReadBefore** | **1000**   |   **4.527 ms** |  **0.3504 ms** |  **0.3895 ms** |  **1.01** |    **0.12** |                    **-** |                **-** |   **358.54 KB** |        **1.00** |
| ReadAfter  | 1000   |   3.089 ms |  0.2635 ms |  0.2706 ms |  0.69 |    0.08 |                    - |                - |   328.58 KB |        0.92 |
|            |        |            |            |            |       |         |                      |                  |             |             |
| **ReadBefore** | **100000** | **138.598 ms** |  **7.3946 ms** |  **7.9121 ms** |  **1.00** |    **0.08** |                    **-** |                **-** | **23032.01 KB** |        **1.00** |
| ReadAfter  | 100000 |  90.388 ms | 34.5484 ms | 39.7860 ms |  0.65 |    0.28 |                    - |                - | 20555.52 KB |        0.89 |
|            |        |            |            |            |       |         |                      |                  |             |             |
| **ReadBefore** | **300000** | **444.710 ms** | **12.9823 ms** | **14.4298 ms** |  **1.00** |    **0.04** |                    **-** |                **-** | **57025.87 KB** |        **1.00** |
| ReadAfter  | 300000 | 190.907 ms |  6.3930 ms |  7.1058 ms |  0.43 |    0.02 |                    - |                - | 49605.45 KB |        0.87 |
