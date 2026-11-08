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
| Method       | Residents | Mean         | Error        | StdDev       | Median       | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------- |---------- |-------------:|-------------:|-------------:|-------------:|------:|--------:|---------------------:|-----------------:|----------:|------------:|
| **ExpireBefore** | **1**         |     **19.76 ns** |     **0.242 ns** |     **0.269 ns** |     **19.72 ns** |  **1.00** |    **0.02** |                    **-** |                **-** |         **-** |          **NA** |
| ExpireAfter  | 1         |     14.51 ns |     0.677 ns |     0.724 ns |     14.39 ns |  0.73 |    0.04 |                    - |                - |         - |          NA |
|              |           |              |              |              |              |       |         |                      |                  |           |             |
| **ExpireBefore** | **64**        |    **990.70 ns** |    **10.170 ns** |    **11.712 ns** |    **993.22 ns** |  **1.00** |    **0.02** |                    **-** |                **-** |         **-** |          **NA** |
| ExpireAfter  | 64        |     13.76 ns |     0.255 ns |     0.284 ns |     13.78 ns |  0.01 |    0.00 |                    - |                - |         - |          NA |
|              |           |              |              |              |              |       |         |                      |                  |           |             |
| **ExpireBefore** | **1024**      | **16,121.41 ns** |   **175.142 ns** |   **194.670 ns** | **16,069.53 ns** | **1.000** |    **0.02** |                    **-** |                **-** |         **-** |          **NA** |
| ExpireAfter  | 1024      |     14.08 ns |     0.473 ns |     0.545 ns |     14.11 ns | 0.001 |    0.00 |                    - |                - |         - |          NA |
|              |           |              |              |              |              |       |         |                      |                  |           |             |
| **ExpireBefore** | **4096**      | **70,423.44 ns** | **4,090.551 ns** | **4,546.636 ns** | **67,340.02 ns** | **1.004** |    **0.09** |                    **-** |                **-** |         **-** |          **NA** |
| ExpireAfter  | 4096      |     13.89 ns |     0.205 ns |     0.236 ns |     13.86 ns | 0.000 |    0.00 |                    - |                - |         - |          NA |
