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
| Method               | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Gen2   | Allocated | Alloc Ratio |
|--------------------- |-------------:|-------------:|-------------:|------:|--------:|-------:|---------------------:|-----------------:|-------:|-------:|----------:|------------:|
| FullResponseBefore   | 583,332.7 ns | 10,208.01 ns | 11,755.56 ns | 1.000 |    0.03 | 1.9531 |                    - |                - | 1.9531 | 1.9531 |  259122 B |       1.000 |
| SummaryResponseAfter |     399.3 ns |      6.48 ns |      7.46 ns | 0.001 |    0.00 | 0.0033 |                    - |                - |      - |      - |     288 B |       0.001 |
