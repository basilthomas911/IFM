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
| Method           | Bars | Mean     | Error    | StdDev   | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated  | Alloc Ratio |
|----------------- |----- |---------:|---------:|---------:|------:|--------:|---------------------:|-----------------:|-----------:|------------:|
| **PerBarBefore**     | **21**   | **368.4 ms** | **19.68 ms** | **21.88 ms** |  **1.00** |    **0.08** |             **220.0000** |                **-** | **2030.05 KB** |        **1.00** |
| AtomicBatchAfter | 21   | 129.9 ms | 16.31 ms | 18.78 ms |  0.35 |    0.05 |              92.0000 |                - |  620.28 KB |        0.31 |
|                  |      |          |          |          |       |         |                      |                  |            |             |
| **PerBarBefore**     | **48**   | **769.6 ms** | **70.75 ms** | **81.48 ms** |  **1.01** |    **0.14** |             **489.0000** |                **-** | **4652.71 KB** |        **1.00** |
| AtomicBatchAfter | 48   | 307.4 ms | 44.75 ms | 51.54 ms |  0.40 |    0.08 |             200.0000 |                - | 1258.45 KB |        0.27 |
|                  |      |          |          |          |       |         |                      |                  |            |             |
| **PerBarBefore**     | **64**   | **949.4 ms** | **41.57 ms** | **47.87 ms** |  **1.00** |    **0.07** |             **672.0000** |                **-** | **6213.31 KB** |        **1.00** |
| AtomicBatchAfter | 64   | 398.9 ms | 48.95 ms | 56.37 ms |  0.42 |    0.06 |             264.0000 |                - | 1655.77 KB |        0.27 |
