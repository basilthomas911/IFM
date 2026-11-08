```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen Threadripper 1950X 3.80GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]                              : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  ApiServerPerformance-Net10-ServerGC : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Job=ApiServerPerformance-Net10-ServerGC  Runtime=.NET 10.0  Server=True  
Arguments=/p:ArtifactsPath=E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107\acceptance-nats-final\generated,/p:DatabentoNativeImplementation=Rust,/p:DatabentoNativeConfiguration=Debug,/m:2  Toolchain=Pinned-ApiServerPerformance-Net10  IterationCount=10  
LaunchCount=2  WarmupCount=3  

```
| Method                   | Bytes  | Mean          | Error        | StdDev       | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------------------- |------- |--------------:|-------------:|-------------:|------:|--------:|---------------------:|-----------------:|-------:|-------:|-------:|----------:|------------:|
| **LegacyCompatibilityArray** | **256**    |      **76.29 ns** |     **2.806 ns** |     **3.232 ns** |  **1.00** |    **0.06** |                    **-** |                **-** | **0.0036** |      **-** |      **-** |     **280 B** |        **1.00** |
| ExistingPooledDefault    | 256    |      50.76 ns |     0.826 ns |     0.918 ns |  0.67 |    0.03 |                    - |                - |      - |      - |      - |         - |        0.00 |
|                          |        |               |              |              |       |         |                      |                  |        |        |        |           |             |
| **LegacyCompatibilityArray** | **4096**   |     **824.95 ns** |    **47.353 ns** |    **48.628 ns** |  **1.00** |    **0.08** |                    **-** |                **-** | **0.0544** |      **-** |      **-** |    **4120 B** |        **1.00** |
| ExistingPooledDefault    | 4096   |     190.99 ns |     2.383 ns |     2.649 ns |  0.23 |    0.01 |                    - |                - |      - |      - |      - |         - |        0.00 |
|                          |        |               |              |              |       |         |                      |                  |        |        |        |           |             |
| **LegacyCompatibilityArray** | **65536**  |  **12,468.64 ns** |   **921.563 ns** | **1,024.315 ns** |  **1.01** |    **0.11** |                    **-** |                **-** | **1.0681** |      **-** |      **-** |   **65560 B** |        **1.00** |
| ExistingPooledDefault    | 65536  |   3,092.21 ns |    16.920 ns |    18.105 ns |  0.25 |    0.02 |                    - |                - |      - |      - |      - |         - |        0.00 |
|                          |        |               |              |              |       |         |                      |                  |        |        |        |           |             |
| **LegacyCompatibilityArray** | **950000** | **438,677.59 ns** | **7,826.215 ns** | **9,012.681 ns** |  **1.00** |    **0.03** |                    **-** |                **-** | **1.4648** | **1.4648** | **1.4648** |  **950004 B** |        **1.00** |
| ExistingPooledDefault    | 950000 |  47,089.77 ns |   467.417 ns |   500.131 ns |  0.11 |    0.00 |                    - |                - |      - |      - |      - |         - |        0.00 |
