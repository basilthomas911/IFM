using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Domain.Application.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
