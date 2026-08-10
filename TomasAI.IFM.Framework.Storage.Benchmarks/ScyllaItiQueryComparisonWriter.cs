using System.Globalization;
using System.Text;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

internal static class ScyllaItiQueryComparisonWriter
{
    public static void Write(IEnumerable<Summary> summaries)
    {
        foreach (var summary in summaries.Where(summary =>
                     summary.Title.Contains(nameof(ScyllaItiQueryProjectionBenchmarks), StringComparison.Ordinal)))
        {
            var measurements = summary.Reports
                .Where(static report => report.ResultStatistics is not null)
                .Select(ToMeasurement)
                .ToArray();
            var comparisons = measurements
                .GroupBy(static measurement => measurement.RowCount)
                .Select(static group => Compare(
                    group.Single(measurement => measurement.Method == nameof(ScyllaItiQueryProjectionBenchmarks.LegacyFilteredTrendModeMaxSequence)),
                    group.Single(measurement => measurement.Method == nameof(ScyllaItiQueryProjectionBenchmarks.ProjectedTrendModeMaxSequence))))
                .OrderBy(static comparison => comparison.RowCount)
                .ToArray();
            if (comparisons.Length == 0)
                continue;

            var reportPath = Path.Combine(summary.ResultsDirectoryPath, "ScyllaItiQueryProjectionComparison.md");
            Directory.CreateDirectory(summary.ResultsDirectoryPath);
            File.WriteAllText(reportPath, CreateMarkdown(comparisons));
            Console.WriteLine($"ScyllaDB ITI query-projection before/after report: {reportPath}");
        }
    }

    static Measurement ToMeasurement(BenchmarkReport report)
    {
        var rowCount = Convert.ToInt32(
            report.BenchmarkCase.Parameters[nameof(ScyllaItiQueryProjectionBenchmarks.RowCount)],
            CultureInfo.InvariantCulture);
        var statistics = report.ResultStatistics!;
        return new Measurement(
            report.BenchmarkCase.Descriptor.WorkloadMethod.Name,
            rowCount,
            statistics.Mean,
            statistics.Percentiles.P50,
            statistics.Percentiles.P95,
            CalculatePercentile(statistics.OriginalValues, 0.99),
            report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase) ?? 0L);
    }

    static double CalculatePercentile(IReadOnlyList<double> source, double percentile)
    {
        if (source.Count == 0)
            return 0;

        var values = source.Order().ToArray();
        var position = (values.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
            return values[lower];
        return values[lower] + (values[upper] - values[lower]) * (position - lower);
    }

    static Comparison Compare(Measurement before, Measurement after)
        => new(
            before.RowCount,
            before.MeanNanoseconds / 1_000_000d,
            after.MeanNanoseconds / 1_000_000d,
            PercentChange(before.MeanNanoseconds, after.MeanNanoseconds),
            before.P50Nanoseconds / 1_000_000d,
            after.P50Nanoseconds / 1_000_000d,
            before.P95Nanoseconds / 1_000_000d,
            after.P95Nanoseconds / 1_000_000d,
            before.P99Nanoseconds / 1_000_000d,
            after.P99Nanoseconds / 1_000_000d,
            1_000_000_000d / before.MeanNanoseconds,
            1_000_000_000d / after.MeanNanoseconds,
            before.AllocatedBytes,
            after.AllocatedBytes,
            PercentChange(before.AllocatedBytes, after.AllocatedBytes));

    static string CreateMarkdown(IReadOnlyList<Comparison> comparisons)
    {
        var builder = new StringBuilder()
            .AppendLine("# ScyllaDB ITI query-projection before/after comparison")
            .AppendLine()
            .AppendLine("Both paths return the same logical latest row through the same Scylla session. Negative latency/allocation percentages and higher queries/second are improvements.")
            .AppendLine()
            .AppendLine("| Canonical rows | Before mean ms | After mean ms | Mean latency % | Before p50 ms | After p50 ms | Before p95 ms | After p95 ms | Before p99 ms | After p99 ms | Before queries/s | After queries/s | Before allocated | After allocated | Allocation % |")
            .AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var comparison in comparisons)
        {
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"| {comparison.RowCount:N0} | {comparison.BeforeMeanMilliseconds:N3} | {comparison.AfterMeanMilliseconds:N3} | {comparison.MeanLatencyPercent:+0.00;-0.00;0.00}% | {comparison.BeforeP50Milliseconds:N3} | {comparison.AfterP50Milliseconds:N3} | {comparison.BeforeP95Milliseconds:N3} | {comparison.AfterP95Milliseconds:N3} | {comparison.BeforeP99Milliseconds:N3} | {comparison.AfterP99Milliseconds:N3} | {comparison.BeforeQueriesPerSecond:N1} | {comparison.AfterQueriesPerSecond:N1} | {comparison.BeforeAllocatedBytes:N0} B | {comparison.AfterAllocatedBytes:N0} B | {comparison.AllocationPercent:+0.00;-0.00;0.00}% |");
        }

        return builder
            .AppendLine()
            .AppendLine("The percentiles are BenchmarkDotNet workload-sample percentiles. Network, driver, and local-cluster background work remain part of the measurement.")
            .ToString();
    }

    static double PercentChange(double before, double after)
        => before == 0 ? 0 : (after - before) / before * 100d;

    readonly record struct Measurement(
        string Method,
        int RowCount,
        double MeanNanoseconds,
        double P50Nanoseconds,
        double P95Nanoseconds,
        double P99Nanoseconds,
        long AllocatedBytes);

    readonly record struct Comparison(
        int RowCount,
        double BeforeMeanMilliseconds,
        double AfterMeanMilliseconds,
        double MeanLatencyPercent,
        double BeforeP50Milliseconds,
        double AfterP50Milliseconds,
        double BeforeP95Milliseconds,
        double AfterP95Milliseconds,
        double BeforeP99Milliseconds,
        double AfterP99Milliseconds,
        double BeforeQueriesPerSecond,
        double AfterQueriesPerSecond,
        long BeforeAllocatedBytes,
        long AfterAllocatedBytes,
        double AllocationPercent);
}
