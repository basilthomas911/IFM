using System.Diagnostics;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public static class DatabentoSyntheticQualificationProbe
{
    private const int MaximumLatencyMicroseconds = 10_000;

    /// <summary>Runs the synthetic feed qualification probe and collects its performance observations.</summary>
    /// <param name="options">The configuration governing provider or feed operation.</param>
    /// <param name="timeout">The maximum time allowed for the operation.</param>
    /// <returns>The run result.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Cleanup preserves the qualification failure already being propagated.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep validation, native ownership and failure ordering in one method for this compatibility pass.")]
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
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);


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
                    | MarketDataKinds.MboOrderUpdate | MarketDataKinds.Statistics
                    | MarketDataKinds.SessionVolume)
        ], timeout);

        var stopped = false;
        try
        {
            ISynchronousBatchReader<MarketDataBatch64>? reader = null;
            feed.Start(timeout, _ =>
                reader = feed.GetReader(feed.GetInstruments()[0].Instrument));
            var expectedSequence = checked((uint)options.Synthetic.StartSequence);
            var received = 0L;
            var lost = 0L;
            var outOfOrder = 0L;
            var started = 0L;
            var finished = 0L;
            var allocationBaseline = -1L;
            while (received < options.Synthetic.RecordCount)
            {
                using var batch = reader!.Read(timeout);
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
