```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
Unknown processor
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  Job-DOXDRP : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=8  WarmupCount=3

```
| Type                          | Method               | QuoteCount | Mean         | Error      | StdDev     | Gen0     | Gen1     | Gen2     | Allocated |
|------------------------------ |--------------------- |----------- |-------------:|-----------:|-----------:|---------:|---------:|---------:|----------:|
| **TickQuoteConversionBenchmarks** | **OneBufferCqlEncoding** | **64**         |     **8.993 μs** |  **0.1893 μs** |  **0.0841 μs** |   **2.0142** |        **-** |        **-** |    **8480 B** |
| TickQuoteIngressBenchmarks    | SegmentDeserialize   | 64         |    14.260 μs |  0.0754 μs |  0.0395 μs |        - |        - |        - |      32 B |
| TickQuoteConversionBenchmarks | PooledCqlEncoding    | 64         |     7.978 μs |  0.0897 μs |  0.0469 μs |        - |        - |        - |      24 B |
| **TickQuoteConversionBenchmarks** | **OneBufferCqlEncoding** | **512**        |    **67.083 μs** |  **0.6780 μs** |  **0.3010 μs** |  **15.8691** |        **-** |        **-** |   **67616 B** |
| TickQuoteIngressBenchmarks    | SegmentDeserialize   | 512        |   120.575 μs |  1.2046 μs |  0.6300 μs |        - |        - |        - |      32 B |
| TickQuoteConversionBenchmarks | PooledCqlEncoding    | 512        |    64.122 μs |  1.5410 μs |  0.8060 μs |        - |        - |        - |      24 B |
| **TickQuoteConversionBenchmarks** | **OneBufferCqlEncoding** | **4096**       |   **738.388 μs** | **26.8492 μs** | **14.0427 μs** | **166.0156** | **166.0156** | **166.0156** |  **540760 B** |
| TickQuoteIngressBenchmarks    | SegmentDeserialize   | 4096       | 1,103.010 μs | 16.2152 μs |  7.1996 μs |  62.5000 |  62.5000 |  62.5000 |  458796 B |
| TickQuoteConversionBenchmarks | PooledCqlEncoding    | 4096       |   527.860 μs | 29.5874 μs | 15.4748 μs |        - |        - |        - |      24 B |
