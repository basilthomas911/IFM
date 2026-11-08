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
| Method     | Rows  | Mean            | Error           | StdDev          | Ratio | RatioSD | Gen0     | Completed Work Items | Lock Contentions | Gen1   | Gen2   | Allocated  | Alloc Ratio |
|----------- |------ |----------------:|----------------:|----------------:|------:|--------:|---------:|---------------------:|-----------------:|-------:|-------:|-----------:|------------:|
| **ReadBefore** | **1000**  |  **6,267,241.2 ns** |   **339,526.93 ns** |   **377,383.28 ns** | **1.003** |    **0.08** |  **31.2500** |             **242.0000** |                **-** |      **-** |      **-** |  **1936450 B** |       **1.000** |
| ReadAfter  | 1000  |        894.4 ns |        26.78 ns |        30.83 ns | 0.000 |    0.00 |   0.0172 |                    - |                - |      - |      - |     1368 B |       0.001 |
|            |       |                 |                 |                 |       |         |          |                      |                  |        |        |            |             |
| **ReadBefore** | **10080** | **61,744,113.9 ns** | **1,074,675.53 ns** | **1,194,499.00 ns** | **1.000** |    **0.03** | **250.0000** |            **2396.0000** |                **-** |      **-** |      **-** | **19513050 B** |        **1.00** |
| ReadAfter  | 10080 |        881.3 ns |        21.96 ns |        24.41 ns | 0.000 |    0.00 |   0.0267 |                    - |                - | 0.0010 | 0.0010 |          - |        0.00 |
