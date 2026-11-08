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
| Method      | Producers | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0    | Completed Work Items | Lock Contentions | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------ |---------- |-----------:|---------:|---------:|------:|--------:|--------:|---------------------:|-----------------:|-------:|-------:|----------:|------------:|
| **BatchBefore** | **1**         |   **107.4 μs** |  **1.76 μs** |  **1.96 μs** |  **1.00** |    **0.03** |  **3.7842** |                    **-** |           **0.0007** |      **-** |      **-** |  **221.5 KB** |        **1.00** |
| BatchAfter  | 1         |   106.3 μs |  1.68 μs |  1.87 μs |  0.99 |    0.02 |  2.9297 |                    - |           0.0010 |      - |      - |  221.5 KB |        1.00 |
|             |           |            |          |          |       |         |         |                      |                  |        |        |           |             |
| **BatchBefore** | **4**         |   **506.4 μs** |  **8.09 μs** |  **8.99 μs** |  **1.00** |    **0.02** | **12.6953** |                    **-** |          **10.5479** | **0.9766** | **0.9766** | **886.01 KB** |        **1.00** |
| BatchAfter  | 4         |   514.2 μs | 15.42 μs | 17.76 μs |  1.02 |    0.04 | 11.7188 |                    - |          10.0938 |      - |      - |    886 KB |        1.00 |
|             |           |            |          |          |       |         |         |                      |                  |        |        |           |             |
| **BatchBefore** | **16**        | **2,178.3 μs** | **41.88 μs** | **48.23 μs** |  **1.00** |    **0.03** | **46.8750** |                    **-** |          **44.8242** |      **-** |      **-** |   **3544 KB** |        **1.00** |
| BatchAfter  | 16        | 2,036.4 μs | 64.18 μs | 71.34 μs |  0.94 |    0.04 | 46.8750 |                    - |          47.1875 |      - |      - |   3544 KB |        1.00 |
