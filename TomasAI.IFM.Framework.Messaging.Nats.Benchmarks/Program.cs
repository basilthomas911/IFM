using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="BenchmarkDotNet generates a separate assembly that invokes this public benchmark entry point.")]

public static class Program
{
    public static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
