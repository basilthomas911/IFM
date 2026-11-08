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
| Method       | Mean     | Error     | StdDev    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------- |---------:|----------:|----------:|------:|--------:|---------------------:|-----------------:|----------:|------------:|
| SerialBefore | 5.629 ms | 0.1878 ms | 0.2087 ms |  1.00 |    0.05 |               8.0703 |                - |  77.19 KB |        1.00 |
| BoundedAfter | 4.219 ms | 0.3085 ms | 0.3552 ms |  0.75 |    0.07 |              11.0703 |                - |  80.74 KB |        1.05 |
