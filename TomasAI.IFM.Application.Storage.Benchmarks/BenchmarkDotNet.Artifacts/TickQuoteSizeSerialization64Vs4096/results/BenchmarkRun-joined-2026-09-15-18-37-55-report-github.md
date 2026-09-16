```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
Unknown processor
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  Job-DOXDRP : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=8  WarmupCount=3

```
| Type                          | Method               | QuoteCount | Mean       | Error      | StdDev     | Gen0     | Gen1     | Gen2     | Allocated |
|------------------------------ |--------------------- |----------- |-----------:|-----------:|-----------:|---------:|---------:|---------:|----------:|
| **TickQuoteConversionBenchmarks** | **OneBufferCqlEncoding** | **64**         |   **8.298 μs** |  **0.2507 μs** |  **0.1311 μs** |   **2.0142** |        **-** |        **-** |   **8.28 KB** |
| TickQuoteIngressBenchmarks    | SegmentDeserialize   | 64         |  15.056 μs |  0.2866 μs |  0.1272 μs |   1.7090 |        - |        - |   7.02 KB |
| **TickQuoteConversionBenchmarks** | **OneBufferCqlEncoding** | **4096**       | **731.659 μs** | **41.4239 μs** | **18.3925 μs** | **166.0156** | **166.0156** | **166.0156** | **528.09 KB** |
| TickQuoteIngressBenchmarks    | SegmentDeserialize   | 4096       | 980.033 μs | 10.1090 μs |  5.2872 μs | 142.5781 | 142.5781 | 142.5781 | 448.07 KB |
