```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
Unknown processor
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  Job-DOXDRP : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=8  WarmupCount=3

```
| Method         | QuoteCount | Mean        | Error      | StdDev      | Gen0   | Allocated |
|--------------- |----------- |------------:|-----------:|------------:|-------:|----------:|
| **PooledCqlWrite** | **64**         |    **912.1 μs** |   **163.5 μs** |    **85.49 μs** | **1.9531** |  **15.48 KB** |
| **PooledCqlWrite** | **512**        | **44,674.9 μs** | **1,051.9 μs** |   **550.16 μs** |      **-** |  **16.26 KB** |
| **PooledCqlWrite** | **4096**       |  **8,927.8 μs** | **2,884.0 μs** | **1,280.49 μs** |      **-** |  **18.19 KB** |
