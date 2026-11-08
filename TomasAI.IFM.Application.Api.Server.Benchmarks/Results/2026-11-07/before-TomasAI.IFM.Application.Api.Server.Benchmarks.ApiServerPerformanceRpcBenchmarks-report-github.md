```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen Threadripper 1950X 3.80GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]                              : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  ApiServerPerformance-Net10-ServerGC : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Job=ApiServerPerformance-Net10-ServerGC  Runtime=.NET 10.0  Server=True  
Arguments=/p:ArtifactsPath=E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107\generated  Toolchain=Pinned-ApiServerPerformance-Net10  IterationCount=10  
LaunchCount=2  WarmupCount=3  Categories=04-RpcWirePayload  

```
| Method        | Contracts | Mean           | Error        | StdDev       | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Gen2   | Allocated | Alloc Ratio |
|-------------- |---------- |---------------:|-------------:|-------------:|------:|--------:|-------:|---------------------:|-----------------:|-------:|-------:|----------:|------------:|
| **PayloadBefore** | **13**        |       **835.9 ns** |     **14.85 ns** |     **16.51 ns** |  **1.00** |    **0.03** | **0.0124** |                    **-** |                **-** |      **-** |      **-** |     **968 B** |        **1.00** |
|               |           |                |              |              |       |         |        |                      |                  |        |        |           |             |
| **PayloadBefore** | **218**       |     **5,114.7 ns** |    **106.91 ns** |    **123.12 ns** |  **1.00** |    **0.03** | **0.1068** |                    **-** |                **-** |      **-** |      **-** |    **8344 B** |        **1.00** |
|               |           |                |              |              |       |         |        |                      |                  |        |        |           |             |
| **PayloadBefore** | **3640**      |    **77,730.2 ns** |  **2,640.74 ns** |  **3,041.08 ns** |  **1.00** |    **0.05** | **2.1973** |                    **-** |                **-** |      **-** |      **-** |  **131545 B** |        **1.00** |
|               |           |                |              |              |       |         |        |                      |                  |        |        |           |             |
| **PayloadBefore** | **50000**     | **2,041,865.1 ns** | **47,648.44 ns** | **54,872.01 ns** |  **1.00** |    **0.04** | **7.8125** |                    **-** |                **-** | **7.8125** | **7.8125** | **2700495 B** |        **1.00** |
