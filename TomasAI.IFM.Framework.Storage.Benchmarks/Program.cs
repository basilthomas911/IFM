using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].StartsWith("--event-log-", StringComparison.Ordinal))
        {
            try {
                if (args[0] == "--event-log-single-index-benchmark") EventLogSingleIndexBenchmark.RunAsync(args[1..]).GetAwaiter().GetResult();
                else if (args[0] == "--event-log-v2-benchmark") EventLogV2Benchmark.RunAsync(args[1..]).GetAwaiter().GetResult();
                else if (args[0] == "--event-log-marker-qualification") EventLogMarkerQualification.RunAsync(args[1..]).GetAwaiter().GetResult();
                else if (args[0] == "--event-log-index-migration-qualification") EventLogIndexMigrationQualification.RunAsync(args[1..]).GetAwaiter().GetResult();
                else if (args[0] == "--event-log-process-child") EventLogProcessQualification.ChildAsync().GetAwaiter().GetResult();
                else if (args[0] == "--event-log-reset-status") EventLogTestCutover.StatusAsync().GetAwaiter().GetResult();
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
