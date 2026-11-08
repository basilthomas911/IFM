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
| Method      | Residents | Mean       | Error       | StdDev      | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated   | Alloc Ratio |
|------------ |---------- |-----------:|------------:|------------:|------:|--------:|---------------------:|-----------------:|------------:|------------:|
| **BurstBefore** | **64**        |   **429.6 μs** |    **12.11 μs** |    **13.46 μs** |  **1.00** |    **0.04** |                    **-** |                **-** |   **210.14 KB** |        **1.00** |
| BurstAfter  | 64        |   478.6 μs |    35.54 μs |    39.50 μs |  1.12 |    0.10 |                    - |                - |   210.71 KB |        1.00 |
|             |           |            |             |             |       |         |                      |                  |             |             |
| **BurstBefore** | **4096**      | **7,644.8 μs** |   **527.04 μs** |   **541.23 μs** |  **1.00** |    **0.09** |                    **-** |                **-** | **12160.28 KB** |        **1.00** |
| BurstAfter  | 4096      | 9,898.0 μs | 1,785.16 μs | 1,910.10 μs |  1.30 |    0.26 |                    - |                - | 12192.35 KB |        1.00 |
