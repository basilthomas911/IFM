using System.Diagnostics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public static class DatabentoQualificationGate
{
    /// <summary>Evaluates the supplied observations against the applicable feed health or qualification criteria.</summary>
    /// <param name="observation">The measured feed performance or health observations.</param>
    /// <param name="targetMillionsOfRecordsPerSecond">The qualification throughput target in millions of records per second.</param>
    /// <param name="baseline">The previously accepted performance baseline, when available.</param>
    /// <param name="requiredDuration">The minimum qualification run duration, when required.</param>
    /// <returns>The evaluate result.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep validation, native ownership and failure ordering in one method for this compatibility pass.")]
    public static FeedQualificationResult Evaluate(
        FeedQualificationObservation observation,
        int targetMillionsOfRecordsPerSecond,
        FeedQualificationBaseline? baseline = null,
        TimeSpan? requiredDuration = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (targetMillionsOfRecordsPerSecond is not (1 or 5 or 10))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetMillionsOfRecordsPerSecond),
                "Qualification targets are 1, 5, or 10 million records per second.");
        }
        var failures = new List<string>();
        var requiredRate = targetMillionsOfRecordsPerSecond * 1_000_000d;
        if (observation.RecordsPerSecond < requiredRate)
        {
            failures.Add(
                $"Throughput {observation.RecordsPerSecond:N0}/s is below {requiredRate:N0}/s.");
        }
        var limits = targetMillionsOfRecordsPerSecond switch
        {
            1 => (P50: TimeSpan.FromMicroseconds(50),
                P99: TimeSpan.FromMicroseconds(250),
                P999: TimeSpan.FromMilliseconds(1)),
            5 => (P50: TimeSpan.MaxValue,
                P99: TimeSpan.FromMicroseconds(500),
                P999: TimeSpan.FromMilliseconds(2)),
            _ => (P50: TimeSpan.MaxValue,
                P99: TimeSpan.FromMilliseconds(1),
                P999: TimeSpan.FromMilliseconds(5))
        };
        AddLatencyFailure(failures, "p50", observation.P50Latency, limits.P50);
        AddLatencyFailure(failures, "p99", observation.P99Latency, limits.P99);
        AddLatencyFailure(failures, "p99.9", observation.P999Latency, limits.P999);
        if (observation.AllocatedBytesAfterWarmup != 0)
        {
            failures.Add(
                $"Hot path allocated {observation.AllocatedBytesAfterWarmup:N0} bytes after warm-up.");
        }
        if (observation.LostRecords != 0 || observation.OutOfOrderRecords != 0)
        {
            failures.Add(
                $"Integrity failed: {observation.LostRecords} lost and "
                + $"{observation.OutOfOrderRecords} out-of-order records.");
        }
        if (observation.HandleCountDelta != 0)
        {
            failures.Add($"Native handle count changed by {observation.HandleCountDelta}.");
        }
        if (requiredDuration is { } duration && observation.Duration < duration)
        {
            failures.Add(
                $"Endurance duration {observation.Duration} is below required {duration}.");
        }
        if (baseline is not null)
        {
            if (observation.RecordsPerSecond < baseline.RecordsPerSecond * .90)
            {
                failures.Add("Throughput regressed by more than 10% from the saved baseline.");
            }
            if (observation.P99Latency > baseline.P99Latency * 1.20)
            {
                failures.Add("p99 latency regressed by more than 20% from the saved baseline.");
            }
        }
        return new FeedQualificationResult
        {
            Passed = failures.Count == 0,
            Failures = failures.AsReadOnly()
        };
    }

    /// <summary>Returns the required endurance duration: 24 hours for production or 30 minutes otherwise.</summary>
    /// <param name="production">Whether production endurance requirements apply.</param>
    /// <returns>The required soak duration result.</returns>
    public static TimeSpan GetRequiredSoakDuration(bool production) =>
        production ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(30);

    private static void AddLatencyFailure(
        List<string> failures,
        string percentile,
        TimeSpan observed,
        TimeSpan limit)
    {
        if (observed > limit)
        {
            failures.Add($"{percentile} latency {observed} exceeds {limit}.");
        }
    }
}
