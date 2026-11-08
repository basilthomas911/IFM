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
| Method       | Mean     | Error    | StdDev   | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|------:|--------:|---------------------:|-----------------:|----------:|------------:|
| SerialBefore | 763.5 ms | 24.98 ms | 27.76 ms |  1.00 |    0.05 |             642.0000 |                - |    3.7 MB |        1.00 |
| BoundedAfter | 567.6 ms | 38.29 ms | 44.09 ms |  0.74 |    0.06 |             645.0000 |                - |   3.71 MB |        1.00 |
