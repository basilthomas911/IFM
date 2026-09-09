using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].StartsWith("--event-log-", StringComparison.Ordinal))
        {
            try {
                if (args[0] == "--event-log-reset-status") EventLogTestCutover.StatusAsync().GetAwaiter().GetResult();
                else if (args[0] == "--event-log-inspect-test") EventLogTestCutover.InspectAsync().GetAwaiter().GetResult();
                else if (args[0] == "--event-log-reset-test-to-binary") EventLogTestCutover.ResetAsync().GetAwaiter().GetResult();
                else EventLogSerializationBaseline.RunAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
            return;
        }
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        ScyllaBulkWriteComparisonWriter.Write(summaries);
        ScyllaItiQueryComparisonWriter.Write(summaries);
    }
}
