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
| Method              | InformationEnabled | Mean      | Error     | StdDev    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|-------------------- |------------------- |----------:|----------:|----------:|------:|--------:|---------------------:|-----------------:|-------:|-------:|-------:|----------:|------------:|
| **EverySuccessBefore**  | **False**              |  **60.09 ns** |  **0.518 ns** |  **0.576 ns** |  **1.00** |    **0.01** |                    **-** |                **-** |      **-** |      **-** |      **-** |         **-** |          **NA** |
| SampledSuccessAfter | False              |  23.63 ns |  0.075 ns |  0.077 ns |  0.39 |    0.00 |                    - |                - |      - |      - |      - |         - |          NA |
|                     |                    |           |           |           |       |         |                      |                  |        |        |        |           |             |
| **EverySuccessBefore**  | **True**               | **808.17 ns** | **11.481 ns** | **13.221 ns** |  **1.00** |    **0.02** |                    **-** |                **-** | **0.0114** |      **-** |      **-** |     **896 B** |        **1.00** |
| SampledSuccessAfter | True               |  41.77 ns |  0.451 ns |  0.463 ns |  0.05 |    0.00 |                    - |                - | 0.0002 | 0.0001 | 0.0001 |         - |        0.00 |
