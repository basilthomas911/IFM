using BenchmarkDotNet.Running;

if (args.Contains("--verify-api-optimizations", StringComparer.Ordinal))
{
    var verification = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiHotPathOptimizationBenchmarks();
    try { verification.Setup(); verification.VerifyEquivalence(); Console.WriteLine("Optimization equivalence checks passed (1,000 randomized snapshots)."); }
    finally { verification.Cleanup(); }
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.ActorHealthJsonBenchmarks).Assembly).Run(args);
