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
| Method        | ConsumerDelayMilliseconds | Mean           | Error        | StdDev       | Median         | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|-------------- |-------------------------- |---------------:|-------------:|-------------:|---------------:|------:|--------:|---------------------:|-----------------:|----------:|------------:|
| **DirectBefore**  | **0**                         |       **2.145 μs** |     **1.294 μs** |     **1.439 μs** |       **1.550 μs** |  **1.42** |    **1.30** |                    **-** |                **-** |         **-** |          **NA** |
| BufferedAfter | 0                         |      20.560 μs |     5.997 μs |     6.906 μs |      19.300 μs | 13.66 |    8.88 |                    - |                - |     576 B |          NA |
|               |                           |                |              |              |                |       |         |                      |                  |           |             |
| **DirectBefore**  | **1**                         | **494,085.437 μs** | **2,681.029 μs** | **2,979.957 μs** | **493,803.700 μs** | **1.000** |    **0.01** |                    **-** |                **-** |         **-** |          **NA** |
| BufferedAfter | 1                         |      21.326 μs |     5.534 μs |     6.151 μs |      19.000 μs | 0.000 |    0.00 |                    - |                - |     576 B |          NA |
