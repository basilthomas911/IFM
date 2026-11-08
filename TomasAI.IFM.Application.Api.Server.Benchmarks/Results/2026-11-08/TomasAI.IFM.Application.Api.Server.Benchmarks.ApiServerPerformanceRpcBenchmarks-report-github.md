```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen Threadripper 1950X 3.80GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]                              : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  ApiServerPerformance-Net10-ServerGC : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Job=ApiServerPerformance-Net10-ServerGC  Runtime=.NET 10.0  Server=True  
Arguments=/p:ArtifactsPath=E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107\acceptance-20261108-final\generated,/p:DatabentoNativeImplementation=Rust,/p:DatabentoNativeConfiguration=Debug,/m:2  Toolchain=Pinned-ApiServerPerformance-Net10  IterationCount=10  
LaunchCount=2  WarmupCount=3  Categories=04-RpcWirePayload  

```
| Method        | Contracts | Mean           | Error        | StdDev       | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|-------------- |---------- |---------------:|-------------:|-------------:|------:|--------:|---------------------:|-----------------:|-------:|-------:|-------:|----------:|------------:|
| **PayloadBefore** | **13**        |       **825.0 ns** |     **17.99 ns** |     **20.00 ns** |  **1.00** |    **0.03** |                    **-** |                **-** | **0.0124** |      **-** |      **-** |     **968 B** |        **1.00** |
| PayloadAfter  | 13        |       458.2 ns |     10.44 ns |     12.03 ns |  0.56 |    0.02 |                    - |                - | 0.0043 |      - |      - |     352 B |        0.36 |
|               |           |                |              |              |       |         |                      |                  |        |        |        |           |             |
| **PayloadBefore** | **218**       |     **4,965.2 ns** |    **122.77 ns** |    **131.36 ns** |  **1.00** |    **0.04** |                    **-** |                **-** | **0.1068** |      **-** |      **-** |    **8344 B** |        **1.00** |
| PayloadAfter  | 218       |     4,383.5 ns |     92.19 ns |    106.17 ns |  0.88 |    0.03 |                    - |                - | 0.0458 |      - |      - |    4040 B |        0.48 |
|               |           |                |              |              |       |         |                      |                  |        |        |        |           |             |
| **PayloadBefore** | **3640**      |    **75,775.5 ns** |  **1,659.73 ns** |  **1,844.78 ns** |  **1.00** |    **0.03** |                    **-** |                **-** | **2.1973** |      **-** |      **-** |  **131545 B** |        **1.00** |
| PayloadAfter  | 3640      |    66,749.9 ns |  1,791.10 ns |  1,990.81 ns |  0.88 |    0.03 |                    - |                - | 0.9766 |      - |      - |   65640 B |        0.50 |
|               |           |                |              |              |       |         |                      |                  |        |        |        |           |             |
| **PayloadBefore** | **50000**     | **1,951,631.3 ns** | **47,941.65 ns** | **55,209.67 ns** |  **1.00** |    **0.04** |                    **-** |                **-** | **7.8125** | **7.8125** | **7.8125** | **2700497 B** |        **1.00** |
| PayloadAfter  | 50000     | 1,189,678.4 ns | 20,950.83 ns | 23,286.79 ns |  0.61 |    0.02 |                    - |                - | 3.9063 | 3.9063 | 3.9063 |  900118 B |        0.33 |
