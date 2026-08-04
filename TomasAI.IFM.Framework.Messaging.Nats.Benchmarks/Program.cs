using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
