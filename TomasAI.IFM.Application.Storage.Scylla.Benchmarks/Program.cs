using BenchmarkDotNet.Running;
using TomasAI.IFM.Application.Storage.Scylla.Benchmarks;
if(args.Contains("--sustain")) await ScyllaSustainedWrites.RunAsync();
else BenchmarkSwitcher.FromAssembly(typeof(ScyllaVersionBenchmarks).Assembly).Run(args);
