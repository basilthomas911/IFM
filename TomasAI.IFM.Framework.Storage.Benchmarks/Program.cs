using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
