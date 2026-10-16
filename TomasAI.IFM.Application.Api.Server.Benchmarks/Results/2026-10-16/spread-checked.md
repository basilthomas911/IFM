```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen Threadripper 1950X 3.80GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  Job-VNQJGX : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  Server=True  IterationCount=8  
LaunchCount=1  WarmupCount=3  Categories=03_SpreadMath  

```
| Method           | Mean     | Error    | StdDev   | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|----------------- |---------:|---------:|---------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| SpreadMathBefore | 941.9 ns | 32.54 ns | 14.45 ns |  1.00 |    0.02 |                    - |                - | 0.0191 |    1568 B |        1.00 |
| SpreadMathAfter  | 260.5 ns |  7.56 ns |  3.96 ns |  0.28 |    0.01 |                    - |                - | 0.0038 |     312 B |        0.20 |
