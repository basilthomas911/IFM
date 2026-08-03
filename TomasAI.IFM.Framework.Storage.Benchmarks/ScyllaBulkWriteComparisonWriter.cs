using System.Globalization;
using System.Text;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

internal static class ScyllaBulkWriteComparisonWriter
{
    const string BenchmarkTypeName = nameof(ScyllaBulkWriteBenchmarks);
    const string LegacyMethodName = nameof(ScyllaBulkWriteBenchmarks.LegacyLoggedBatch);
    const string RedesignedMethodName = nameof(ScyllaBulkWriteBenchmarks.RedesignedBoundedConcurrency);

    public static void Write(IEnumerable<Summary> summaries)
    {
        foreach (var summary in summaries.Where(summary => summary.Title.Contains(BenchmarkTypeName, StringComparison.Ordinal)))
        {
            var measurements = summary.Reports
                .Where(report => report.ResultStatistics is not null)
                .Select(ToMeasurement)
                .ToArray();
            var comparisons = measurements
                .GroupBy(measurement => (measurement.RowCount, measurement.PartitionCount))
                .Select(group => Compare(
                    group.Single(measurement => measurement.Method == LegacyMethodName),
                    group.Single(measurement => measurement.Method == RedesignedMethodName)))
                .OrderBy(comparison => comparison.RowCount)
                .ThenBy(comparison => comparison.PartitionCount)
                .ToArray();
            if (comparisons.Length == 0)
                continue;

            var reportPath = Path.Combine(summary.ResultsDirectoryPath, "ScyllaBulkWriteComparison.md");
            Directory.CreateDirectory(summary.ResultsDirectoryPath);
            File.WriteAllText(reportPath, CreateMarkdown(comparisons));
            Console.WriteLine($"Scylla before/after percentage report: {reportPath}");
        }
    }

    static Measurement ToMeasurement(BenchmarkReport report)
    {
        var parameters = report.BenchmarkCase.Parameters.Items.ToDictionary(
            parameter => parameter.Name,
            parameter => Convert.ToInt32(parameter.Value, CultureInfo.InvariantCulture),
            StringComparer.Ordinal);
        var meanNanoseconds = report.ResultStatistics!.Mean;
        return new Measurement(
            report.BenchmarkCase.Descriptor.WorkloadMethod.Name,
            parameters[nameof(ScyllaBulkWriteBenchmarks.RowCount)],
            parameters[nameof(ScyllaBulkWriteBenchmarks.PartitionCount)],
            meanNanoseconds,
            report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase) ?? 0L,
            report.GcStats.Gen0Collections,
            report.GcStats.Gen1Collections,
            report.GcStats.Gen2Collections);
    }

    static Comparison Compare(Measurement before, Measurement after)
        => new(
            before.RowCount,
            before.PartitionCount,
            before.MeanNanoseconds / 1_000_000d,
            after.MeanNanoseconds / 1_000_000d,
            PercentChange(before.MeanNanoseconds, after.MeanNanoseconds),
            before.RowCount / (before.MeanNanoseconds / 1_000_000_000d),
            after.RowCount / (after.MeanNanoseconds / 1_000_000_000d),
            PercentChange(
                before.RowCount / (before.MeanNanoseconds / 1_000_000_000d),
                after.RowCount / (after.MeanNanoseconds / 1_000_000_000d)),
            before.AllocatedBytes,
            after.AllocatedBytes,
            PercentChange(before.AllocatedBytes, after.AllocatedBytes),
            before.Gen0Collections,
            after.Gen0Collections,
            before.Gen1Collections,
            after.Gen1Collections,
            before.Gen2Collections,
            after.Gen2Collections);

    static string CreateMarkdown(IReadOnlyList<Comparison> comparisons)
    {
        var builder = new StringBuilder()
            .AppendLine("# ScyllaDB bulk-write BenchmarkDotNet comparison")
            .AppendLine()
            .AppendLine("Positive throughput percentages are improvements. Negative latency and allocation percentages are improvements.")
            .AppendLine()
            .AppendLine("| Rows | Partitions | Before ms | After ms | Latency % | Before rows/s | After rows/s | Throughput % | Before allocated | After allocated | Allocation % |")
            .AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var comparison in comparisons)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"| {comparison.RowCount:N0} | {comparison.PartitionCount:N0} | {comparison.BeforeMilliseconds:N3} | {comparison.AfterMilliseconds:N3} | {comparison.LatencyPercent:+0.00;-0.00;0.00}% | {comparison.BeforeRowsPerSecond:N0} | {comparison.AfterRowsPerSecond:N0} | {comparison.ThroughputPercent:+0.00;-0.00;0.00}% | {comparison.BeforeAllocatedBytes:N0} B | {comparison.AfterAllocatedBytes:N0} B | {comparison.AllocationPercent:+0.00;-0.00;0.00}% |");
        }

        builder
            .AppendLine()
            .AppendLine("## Garbage collections")
            .AppendLine()
            .AppendLine("| Rows | Partitions | Before Gen0 | After Gen0 | Gen0 % | Before Gen1 | After Gen1 | Gen1 % | Before Gen2 | After Gen2 | Gen2 % |")
            .AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var comparison in comparisons)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"| {comparison.RowCount:N0} | {comparison.PartitionCount:N0} | {comparison.BeforeGen0:N0} | {comparison.AfterGen0:N0} | {FormatPercent(comparison.BeforeGen0, comparison.AfterGen0)} | {comparison.BeforeGen1:N0} | {comparison.AfterGen1:N0} | {FormatPercent(comparison.BeforeGen1, comparison.AfterGen1)} | {comparison.BeforeGen2:N0} | {comparison.AfterGen2:N0} | {FormatPercent(comparison.BeforeGen2, comparison.AfterGen2)} |");
        }

        var totalRows = comparisons.Sum(comparison => comparison.RowCount);
        var beforeTotalSeconds = comparisons.Sum(comparison => comparison.BeforeMilliseconds) / 1000d;
        var afterTotalSeconds = comparisons.Sum(comparison => comparison.AfterMilliseconds) / 1000d;
        var beforeOverallThroughput = totalRows / beforeTotalSeconds;
        var afterOverallThroughput = totalRows / afterTotalSeconds;
        var beforeMeanLatency = comparisons.Average(comparison => comparison.BeforeMilliseconds);
        var afterMeanLatency = comparisons.Average(comparison => comparison.AfterMilliseconds);
        var beforeAllocated = comparisons.Sum(comparison => comparison.BeforeAllocatedBytes);
        var afterAllocated = comparisons.Sum(comparison => comparison.AfterAllocatedBytes);
        var beforeGen0 = comparisons.Sum(comparison => comparison.BeforeGen0);
        var afterGen0 = comparisons.Sum(comparison => comparison.AfterGen0);
        var beforeGen1 = comparisons.Sum(comparison => comparison.BeforeGen1);
        var afterGen1 = comparisons.Sum(comparison => comparison.AfterGen1);
        var beforeGen2 = comparisons.Sum(comparison => comparison.BeforeGen2);
        var afterGen2 = comparisons.Sum(comparison => comparison.AfterGen2);

        return builder
            .AppendLine()
            .AppendLine("## Overall")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"- Weighted throughput: {beforeOverallThroughput:N0} to {afterOverallThroughput:N0} rows/s ({PercentChange(beforeOverallThroughput, afterOverallThroughput):+0.00;-0.00;0.00}%).")
            .AppendLine(CultureInfo.InvariantCulture, $"- Mean scenario latency: {beforeMeanLatency:N3} to {afterMeanLatency:N3} ms ({PercentChange(beforeMeanLatency, afterMeanLatency):+0.00;-0.00;0.00}%).")
            .AppendLine(CultureInfo.InvariantCulture, $"- Total allocated bytes across scenarios: {beforeAllocated:N0} to {afterAllocated:N0} ({PercentChange(beforeAllocated, afterAllocated):+0.00;-0.00;0.00}%).")
            .AppendLine(CultureInfo.InvariantCulture, $"- Total Gen0 collections: {beforeGen0:N0} to {afterGen0:N0} ({FormatPercent(beforeGen0, afterGen0)}).")
            .AppendLine(CultureInfo.InvariantCulture, $"- Total Gen1 collections: {beforeGen1:N0} to {afterGen1:N0} ({FormatPercent(beforeGen1, afterGen1)}).")
            .AppendLine(CultureInfo.InvariantCulture, $"- Total Gen2 collections: {beforeGen2:N0} to {afterGen2:N0} ({FormatPercent(beforeGen2, afterGen2)}).")
            .AppendLine()
            .AppendLine("Overall throughput is calculated as total rows divided by the sum of scenario mean durations. Overall latency is the arithmetic mean of the scenario means.")
            .ToString();
    }

    static double PercentChange(double before, double after)
        => before == 0 ? 0 : (after - before) / before * 100d;

    static string FormatPercent(double before, double after)
        => before == 0
            ? after == 0 ? "0.00%" : "n/a (zero baseline)"
            : PercentChange(before, after).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";

    readonly record struct Measurement(
        string Method,
        int RowCount,
        int PartitionCount,
        double MeanNanoseconds,
        long AllocatedBytes,
        long Gen0Collections,
        long Gen1Collections,
        long Gen2Collections);

    readonly record struct Comparison(
        int RowCount,
        int PartitionCount,
        double BeforeMilliseconds,
        double AfterMilliseconds,
        double LatencyPercent,
        double BeforeRowsPerSecond,
        double AfterRowsPerSecond,
        double ThroughputPercent,
        long BeforeAllocatedBytes,
        long AfterAllocatedBytes,
        double AllocationPercent,
        long BeforeGen0,
        long AfterGen0,
        long BeforeGen1,
        long AfterGen1,
        long BeforeGen2,
        long AfterGen2);
}
