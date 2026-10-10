using BenchmarkDotNet.Running;
using TomasAI.IFM.Application.Storage.Postgres.Benchmarks;
if (args.Contains("--sustain")) await PostgresSustainedWrites.RunAsync();
else BenchmarkSwitcher.FromAssembly(typeof(PostgresTuningBenchmarks).Assembly).Run(args);
