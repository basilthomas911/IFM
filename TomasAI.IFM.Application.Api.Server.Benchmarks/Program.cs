using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.ActorHealthJsonBenchmarks).Assembly).Run(args);
