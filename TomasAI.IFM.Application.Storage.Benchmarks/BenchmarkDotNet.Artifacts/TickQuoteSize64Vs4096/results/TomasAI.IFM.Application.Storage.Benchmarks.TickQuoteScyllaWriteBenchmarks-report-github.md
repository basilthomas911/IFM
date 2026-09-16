```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
Unknown processor
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  Job-DOXDRP : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=8  WarmupCount=3

```
| Method            | QuoteCount | Mean        | Error       | StdDev      | Gen0     | Gen1     | Gen2     | Allocated |
|------------------ |----------- |------------:|------------:|------------:|---------:|---------:|---------:|----------:|
| **OneBufferCqlWrite** | **64**         |    **793.5 μs** |    **47.55 μs** |    **16.96 μs** |   **5.8594** |        **-** |        **-** |  **23.71 KB** |
| NativeListRead    | 64         |    748.8 μs |    63.89 μs |    33.42 μs |   5.8594 |        - |        - |     24 KB |
| **OneBufferCqlWrite** | **4096**       | **13,503.1 μs** | **6,005.86 μs** | **3,141.18 μs** | **156.2500** | **156.2500** | **156.2500** | **546.93 KB** |
| NativeListRead    | 4096       |  2,445.4 μs |   170.09 μs |    75.52 μs | 164.0625 | 164.0625 | 164.0625 | 579.05 KB |
