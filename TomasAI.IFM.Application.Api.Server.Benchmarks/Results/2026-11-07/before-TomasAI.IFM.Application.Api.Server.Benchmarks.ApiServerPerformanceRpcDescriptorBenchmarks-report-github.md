```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen Threadripper 1950X 3.80GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]                              : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  ApiServerPerformance-Net10-ServerGC : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Job=ApiServerPerformance-Net10-ServerGC  Runtime=.NET 10.0  Server=True  
Arguments=/p:ArtifactsPath=E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107\generated  Toolchain=Pinned-ApiServerPerformance-Net10  IterationCount=10  
LaunchCount=2  WarmupCount=3  Categories=03-OperationDescriptor  

```
| Method           | Mean     | Error   | StdDev  | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|--------:|--------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| DescriptorBefore | 236.0 ns | 8.45 ns | 9.73 ns |  1.00 |    0.06 |                    - |                - | 0.0036 |     280 B |        1.00 |
