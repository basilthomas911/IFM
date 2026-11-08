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
| Method         | Scenario    | Mean      | Error     | StdDev    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Allocated  | Alloc Ratio |
|--------------- |------------ |----------:|----------:|----------:|------:|--------:|---------------------:|-----------------:|-----------:|------------:|
| **OriginalBefore** | **Independent** | **17.397 ms** | **2.2864 ms** | **2.6330 ms** |  **1.02** |    **0.19** |              **47.0000** |                **-** | **1728.42 KB** |        **1.00** |
| CurrentAfter   | Independent | 14.729 ms | 1.0801 ms | 1.2439 ms |  0.86 |    0.12 |              68.0000 |                - | 2005.88 KB |        1.16 |
|                |             |           |           |           |       |         |                      |                  |            |             |
| **OriginalBefore** | **Retry**       |  **4.042 ms** | **0.3683 ms** | **0.3941 ms** |  **1.01** |    **0.13** |              **10.0000** |                **-** |  **400.39 KB** |        **1.00** |
| CurrentAfter   | Retry       |  4.347 ms | 0.0963 ms | 0.1071 ms |  1.08 |    0.09 |              11.0000 |                - |   466.4 KB |        1.16 |
|                |             |           |           |           |       |         |                      |                  |            |             |
| **OriginalBefore** | **SameKey**     |  **3.013 ms** | **0.3845 ms** | **0.4428 ms** |  **1.02** |    **0.21** |              **12.0000** |                **-** |  **222.41 KB** |        **1.00** |
| CurrentAfter   | SameKey     |  3.147 ms | 0.4689 ms | 0.5212 ms |  1.07 |    0.23 |              18.0000 |                - |  264.63 KB |        1.19 |
|                |             |           |           |           |       |         |                      |                  |            |             |
| **OriginalBefore** | **Stale**       |  **3.685 ms** | **0.6457 ms** | **0.7177 ms** |  **1.03** |    **0.26** |               **5.0000** |                **-** |  **378.96 KB** |        **1.00** |
| CurrentAfter   | Stale       |  2.643 ms | 0.0887 ms | 0.0949 ms |  0.74 |    0.12 |               9.0000 |                - |  334.73 KB |        0.88 |
