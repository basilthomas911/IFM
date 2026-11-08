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
| Method          | Trades | Mean        | Error       | StdDev      | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0     | Gen1     | Gen2     | Allocated   | Alloc Ratio |
|---------------- |------- |------------:|------------:|------------:|------:|--------:|---------------------:|-----------------:|---------:|---------:|---------:|------------:|------------:|
| **AggregateBefore** | **1000**   |    **393.9 μs** |     **6.75 μs** |     **7.77 μs** |  **1.00** |    **0.03** |                    **-** |                **-** |   **2.4414** |        **-** |        **-** |   **202.26 KB** |        **1.00** |
| AggregateAfter  | 1000   |    251.1 μs |     6.28 μs |     6.98 μs |  0.64 |    0.02 |                    - |                - |   2.9297 |   0.4883 |   0.4883 |  1129.41 KB |        5.58 |
|                 |        |             |             |             |       |         |                      |                  |          |          |          |             |             |
| **AggregateBefore** | **100000** | **62,847.4 μs** | **1,967.14 μs** | **2,186.48 μs** |  **1.00** |    **0.05** |                    **-** |                **-** | **111.1111** | **111.1111** | **111.1111** | **16523.81 KB** |        **1.00** |
| AggregateAfter  | 100000 | 48,823.6 μs | 2,086.35 μs | 2,402.64 μs |  0.78 |    0.05 |                    - |                - |  90.9091 |  90.9091 |  90.9091 | 16526.49 KB |        1.00 |
