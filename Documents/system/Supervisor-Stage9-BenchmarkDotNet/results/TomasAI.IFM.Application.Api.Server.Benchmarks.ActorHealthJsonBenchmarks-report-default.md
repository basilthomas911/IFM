
BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen Threadripper 1950X 3.80GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  Job-DOXDRP : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=8  WarmupCount=3  

 Method                  | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
------------------------ |---------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
 ReflectionMetadata      | 766.5 μs | 19.97 μs | 10.45 μs |  1.00 |    0.02 | 142.5781 | 142.5781 | 142.5781 | 472.79 KB |        1.00 |
 SourceGeneratedMetadata | 602.8 μs | 20.78 μs | 10.87 μs |  0.79 |    0.02 | 142.5781 | 142.5781 | 142.5781 | 449.87 KB |        0.95 |
