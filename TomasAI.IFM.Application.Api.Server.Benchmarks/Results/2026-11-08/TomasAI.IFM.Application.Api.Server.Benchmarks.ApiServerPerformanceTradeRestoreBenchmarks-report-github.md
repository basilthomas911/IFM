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
| Method       | Trades | Mean        | Error     | StdDev    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated   | Alloc Ratio |
|------------- |------- |------------:|----------:|----------:|------:|--------:|---------------------:|-----------------:|------------:|------------:|
| **SerialBefore** | **1**      |    **30.26 ms** |  **3.990 ms** |  **4.595 ms** |  **1.03** |    **0.24** |              **34.0000** |                **-** |   **183.75 KB** |        **1.00** |
| BoundedAfter | 1      |    23.59 ms |  5.598 ms |  6.447 ms |  0.80 |    0.26 |              39.0000 |                - |   170.31 KB |        0.93 |
|              |        |             |           |           |       |         |                      |                  |             |             |
| **SerialBefore** | **4**      |    **67.60 ms** |  **4.273 ms** |  **4.572 ms** |  **1.00** |    **0.09** |              **79.0000** |                **-** |   **360.63 KB** |        **1.00** |
| BoundedAfter | 4      |    27.01 ms |  6.038 ms |  6.461 ms |  0.40 |    0.10 |              63.0000 |                - |   281.16 KB |        0.78 |
|              |        |             |           |           |       |         |                      |                  |             |             |
| **SerialBefore** | **32**     |   **449.09 ms** | **17.826 ms** | **19.813 ms** |  **1.00** |    **0.06** |             **505.0000** |                **-** |  **2279.15 KB** |        **1.00** |
| BoundedAfter | 32     |    80.86 ms |  7.254 ms |  8.353 ms |  0.18 |    0.02 |             348.0000 |                - |  1678.38 KB |        0.74 |
|              |        |             |           |           |       |         |                      |                  |             |             |
| **SerialBefore** | **128**    | **1,834.33 ms** | **45.605 ms** | **52.519 ms** |  **1.00** |    **0.04** |            **2009.0000** |                **-** | **13400.27 KB** |        **1.00** |
| BoundedAfter | 128    |   318.20 ms | 13.753 ms | 15.838 ms |  0.17 |    0.01 |            1321.0000 |                - | 10690.47 KB |        0.80 |
