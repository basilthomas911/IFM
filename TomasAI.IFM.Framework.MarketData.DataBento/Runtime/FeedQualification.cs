using System.Diagnostics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedQualificationObservation
{
    public required double RecordsPerSecond { get; init; }
    public required TimeSpan P50Latency { get; init; }
    public required TimeSpan P99Latency { get; init; }
    public required TimeSpan P999Latency { get; init; }
    public long AllocatedBytesAfterWarmup { get; init; }
    public long LostRecords { get; init; }
    public long OutOfOrderRecords { get; init; }
    public TimeSpan Duration { get; init; }
    public long HandleCountDelta { get; init; }
    public FeedProcessorSelectionKind ProcessorSelection { get; init; }
    public LogicalProcessorLocation? ResolvedNativeProducer { get; init; }
    public LogicalProcessorLocation? AlternateNativeProducer { get; init; }
    public LogicalProcessorLocation? ObservedNativeProducer { get; init; }
    public LogicalProcessorLocation? ResolvedManagedDrain { get; init; }
    public LogicalProcessorLocation? AlternateManagedDrain { get; init; }
    public LogicalProcessorLocation? ObservedManagedDrain { get; init; }
    public bool NativeProducerAffinityVerified { get; init; }
    public bool ManagedDrainAffinityVerified { get; init; }
    public ulong NativeProducerProcessorSamples { get; init; }
    public ulong NativeProducerProcessorMigrations { get; init; }
    public uint NativeProducerUniqueProcessors { get; init; }
    public ulong NativeProducerOffAssignmentSamples { get; init; }
    public ulong ManagedDrainProcessorSamples { get; init; }
    public ulong ManagedDrainProcessorMigrations { get; init; }
    public uint ManagedDrainUniqueProcessors { get; init; }
    public ulong ManagedDrainOffAssignmentSamples { get; init; }
}

public sealed record FeedQualificationBaseline(
    double RecordsPerSecond,
    TimeSpan P99Latency);

public sealed record FeedQualificationResult
{
    public required bool Passed { get; init; }
    public required IReadOnlyList<string> Failures { get; init; }
}

public static class DatabentoQualificationGate
{
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

public static class DatabentoSyntheticQualificationProbe
{
    private const int MaximumLatencyMicroseconds = 10_000;

    public static FeedQualificationObservation Run(
        DatabentoFeedOptions options,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.DataSource != FeedDataSourceMode.Synthetic)
        {
            throw new ArgumentException(
                "The deterministic qualification probe requires the synthetic data source.",
                nameof(options));
        }
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var process = Process.GetCurrentProcess();
        var initialHandleCount = process.HandleCount;
        var histogram = new long[MaximumLatencyMicroseconds + 1];
        using var feed = new DatabentoFeedFactory().CreateTickerFeed(options);
        feed.Subscribe(
        [
            new TickerSubscription(
                "QUALIFICATION",
                DatabentoInputSymbology.RawSymbol,
                MarketDataKinds.Quote | MarketDataKinds.Trade
                    | MarketDataKinds.MboOrderUpdate | MarketDataKinds.Statistics)
        ], timeout);

        var stopped = false;
        try
        {
            feed.Start(timeout);
            var reader = feed.GetReader(feed.GetInstruments()[0].Instrument);
            var expectedSequence = checked((uint)options.Synthetic.StartSequence);
            var received = 0L;
            var lost = 0L;
            var outOfOrder = 0L;
            var started = 0L;
            var finished = 0L;
            var allocationBaseline = -1L;
            while (received < options.Synthetic.RecordCount)
            {
                using var batch = reader.Read(timeout);
                if (started == 0)
                {
                    started = Stopwatch.GetTimestamp();
                }
                var records = batch.Records;
                for (var index = 0; index < records.Length; index++)
                {
                    ref readonly var record = ref records[index];
                    var sequence = record.Header.Sequence;
                    if (sequence > expectedSequence)
                    {
                        lost += sequence - expectedSequence;
                        expectedSequence = sequence + 1;
                    }
                    else if (sequence < expectedSequence)
                    {
                        outOfOrder++;
                    }
                    else
                    {
                        expectedSequence++;
                    }
                    histogram[LatencyBucket(record.Header.ReceiveTimestampNanoseconds)]++;
                    received++;
                }
                if (allocationBaseline < 0)
                {
                    allocationBaseline = GC.GetAllocatedBytesForCurrentThread();
                }
            }
            finished = Stopwatch.GetTimestamp();
            var allocated = allocationBaseline < 0
                ? 0
                : GC.GetAllocatedBytesForCurrentThread() - allocationBaseline;
            feed.Stop(timeout);
            stopped = true;
            var duration = Stopwatch.GetElapsedTime(started, finished);
            var health = feed.GetHealth();
            return new FeedQualificationObservation
            {
                RecordsPerSecond = received / duration.TotalSeconds,
                P50Latency = Percentile(histogram, received, .50),
                P99Latency = Percentile(histogram, received, .99),
                P999Latency = Percentile(histogram, received, .999),
                AllocatedBytesAfterWarmup = allocated + health.DrainAllocatedBytes,
                LostRecords = lost,
                OutOfOrderRecords = outOfOrder,
                Duration = duration,
                HandleCountDelta = process.HandleCount - initialHandleCount,
                ProcessorSelection = health.ProcessorSelection,
                ResolvedNativeProducer = health.ResolvedNativeProducer,
                AlternateNativeProducer = health.AlternateNativeProducer,
                ObservedNativeProducer = health.ObservedNativeProducer,
                ResolvedManagedDrain = health.ResolvedManagedDrain,
                AlternateManagedDrain = health.AlternateManagedDrain,
                ObservedManagedDrain = health.ObservedManagedDrain,
                NativeProducerAffinityVerified = health.NativeProducerAffinityVerified,
                ManagedDrainAffinityVerified = health.ManagedDrainAffinityVerified,
                NativeProducerProcessorSamples = health.NativeProducerProcessorSamples,
                NativeProducerProcessorMigrations = health.NativeProducerProcessorMigrations,
                NativeProducerUniqueProcessors = health.NativeProducerUniqueProcessors,
                NativeProducerOffAssignmentSamples = health.NativeProducerOffAssignmentSamples,
                ManagedDrainProcessorSamples = health.ManagedDrainProcessorSamples,
                ManagedDrainProcessorMigrations = health.ManagedDrainProcessorMigrations,
                ManagedDrainUniqueProcessors = health.ManagedDrainUniqueProcessors,
                ManagedDrainOffAssignmentSamples = health.ManagedDrainOffAssignmentSamples
            };
        }
        finally
        {
            if (!stopped)
            {
                try
                {
                    feed.Stop(timeout);
                    stopped = true;
                }
                catch
                {
                    // Preserve the qualification failure that caused cleanup.
                }
            }
        }
    }

    private static int LatencyBucket(long receiveTimestampNanoseconds)
    {
        var nowNanoseconds = Stopwatch.GetTimestamp()
                             * (1_000_000_000d / Stopwatch.Frequency);
        var latencyMicroseconds = (long)Math.Max(
            0,
            (nowNanoseconds - receiveTimestampNanoseconds) / 1_000d);
        return checked((int)Math.Min(MaximumLatencyMicroseconds, latencyMicroseconds));
    }

    private static TimeSpan Percentile(
        long[] histogram,
        long count,
        double percentile)
    {
        var target = Math.Max(1L, (long)Math.Ceiling(count * percentile));
        var cumulative = 0L;
        for (var microseconds = 0; microseconds < histogram.Length; microseconds++)
        {
            cumulative += histogram[microseconds];
            if (cumulative >= target)
            {
                return TimeSpan.FromMicroseconds(microseconds);
            }
        }
        return TimeSpan.FromMicroseconds(MaximumLatencyMicroseconds);
    }
}
