using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>
/// Stateful, per-generation causal watchdog. A quiet provider is healthy; a pipeline is
/// suspect only when upstream work or buffered work exists without downstream progress.
/// </summary>
public sealed class DatabentoDatasetHealthEvaluator(TimeSpan hardStallTimeout)
{
    readonly Dictionary<string, ProgressState> _progress = new(StringComparer.Ordinal);

    public DatabentoDatasetEvaluation Evaluate(
        DatabentoFeedWatchdogStatus feed,
        DateTime observedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(feed);
        if (hardStallTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(hardStallTimeout));

        if (!_progress.TryGetValue(feed.Dataset, out var previous)
            || previous.GenerationId != feed.GenerationId)
        {
            previous = ProgressState.Create(feed, observedOnUtc);
            _progress[feed.Dataset] = previous;
        }

        var immediate = ImmediateFailure(feed);
        if (immediate != DatabentoDatasetFailureReason.None)
            return Store(feed, observedOnUtc, previous, immediate, true);

        var reason = CausalFailure(feed, previous);
        if (reason == DatabentoDatasetFailureReason.None)
        {
            _progress[feed.Dataset] = ProgressState.Create(feed, observedOnUtc);
            return new(feed.Dataset, feed.GenerationId, DatabentoDatasetState.Up,
                DatabentoDatasetFailureReason.None, null, "Dataset pipeline is making progress or is causally quiet.");
        }

        var sameEpisode = previous.Reason == reason;
        var suspectSince = sameEpisode ? previous.SuspectSinceUtc : observedOnUtc;
        var down = observedOnUtc - suspectSince >= hardStallTimeout
            || reason == DatabentoDatasetFailureReason.AggregationRecordStalled;
        var next = ProgressState.Create(feed, observedOnUtc) with
        {
            Reason = reason,
            SuspectSinceUtc = suspectSince
        };
        _progress[feed.Dataset] = next;
        return new(feed.Dataset, feed.GenerationId,
            down ? DatabentoDatasetState.Down : DatabentoDatasetState.Suspect,
            reason, suspectSince,
            down
                ? $"Dataset pipeline remained stalled for at least {hardStallTimeout}."
                : $"Dataset pipeline has causal evidence of {reason}; confirmation timer is running.");
    }

    public void Forget(string dataset) => _progress.Remove(dataset);

    DatabentoDatasetEvaluation Store(
        DatabentoFeedWatchdogStatus feed,
        DateTime observedOnUtc,
        ProgressState previous,
        DatabentoDatasetFailureReason reason,
        bool down)
    {
        var suspectSince = previous.Reason == reason
            ? previous.SuspectSinceUtc ?? observedOnUtc
            : observedOnUtc;
        _progress[feed.Dataset] = ProgressState.Create(feed, observedOnUtc) with
        {
            Reason = reason,
            SuspectSinceUtc = suspectSince
        };
        return new(feed.Dataset, feed.GenerationId,
            down ? DatabentoDatasetState.Down : DatabentoDatasetState.Suspect,
            reason, suspectSince, feed.FailureDetail);
    }

    DatabentoDatasetFailureReason ImmediateFailure(DatabentoFeedWatchdogStatus feed)
    {
        if (feed.TerminalStatus != 0 || feed.MajorStatus == DatabentoMajorStatus.Down)
            return DatabentoDatasetFailureReason.NativeTerminalFailure;
        if (!feed.ProducerAlive || !feed.TransportRunning)
            return DatabentoDatasetFailureReason.NativeProducerStopped;
        if (feed.RingOverruns != 0)
            return DatabentoDatasetFailureReason.NativeRingOverrun;
        if (!feed.AggregationWorkerRunning)
            return DatabentoDatasetFailureReason.AggregationWorkerStopped;
        if (feed.ReceivedSubscriptions < feed.ExpectedSubscriptions)
            return DatabentoDatasetFailureReason.SubscriptionIncomplete;
        if (feed.AggregationMetrics is { } metrics
            && metrics.InFlightRecord is not null
            && TimeSpan.FromTicks(Math.Max(0, metrics.CurrentProcessingDurationTicks)) >= hardStallTimeout)
            return DatabentoDatasetFailureReason.AggregationRecordStalled;
        return DatabentoDatasetFailureReason.None;
    }

    static DatabentoDatasetFailureReason CausalFailure(
        DatabentoFeedWatchdogStatus feed,
        ProgressState previous)
    {
        var health = feed.DrainDiagnostics;
        var metrics = feed.AggregationMetrics;
        var producerAdvanced = feed.RecordsProduced > previous.RecordsProduced;
        var nativeConsumerAdvanced = feed.RecordsConsumed > previous.RecordsConsumed;
        var batchesAdvanced = feed.BatchesPublished > previous.BatchesPublished;
        var aggregationAdvanced = metrics is { } current
            && current.RecordsCompleted > previous.RecordsCompleted;

        if (producerAdvanced && !nativeConsumerAdvanced && feed.RingUsed > 0)
            return DatabentoDatasetFailureReason.NativeDrainStalled;
        if (previous.Reason == DatabentoDatasetFailureReason.NativeDrainStalled
            && !nativeConsumerAdvanced && feed.RingUsed > 0)
            return DatabentoDatasetFailureReason.NativeDrainStalled;
        if (feed.RingUsed > 0 && health?.Stage == FeedDrainStage.WaitingForNativeSignal)
            return DatabentoDatasetFailureReason.NativeDrainStalled;
        if (health is { ManagedBatchPublishActive: true }
            && !aggregationAdvanced)
            return DatabentoDatasetFailureReason.ManagedChannelBlocked;
        if (previous.Reason == DatabentoDatasetFailureReason.ManagedChannelBlocked
            && !aggregationAdvanced && feed.ChannelBatchCount > 0)
            return DatabentoDatasetFailureReason.ManagedChannelBlocked;
        if (feed.ChannelFullCount > previous.ChannelFullCount
            && !aggregationAdvanced)
            return DatabentoDatasetFailureReason.ManagedChannelBlocked;
        if (batchesAdvanced && !aggregationAdvanced
            && metrics is { RecordsStarted: > 0 })
            return DatabentoDatasetFailureReason.ManagedChannelBlocked;
        return DatabentoDatasetFailureReason.None;
    }

    sealed record ProgressState(
        Guid GenerationId,
        ulong RecordsProduced,
        ulong RecordsConsumed,
        ulong BatchesPublished,
        ulong ChannelFullCount,
        long RecordsCompleted,
        DateTime ObservedOnUtc,
        DatabentoDatasetFailureReason Reason,
        DateTime? SuspectSinceUtc)
    {
        public static ProgressState Create(DatabentoFeedWatchdogStatus feed, DateTime observedOnUtc) => new(
            feed.GenerationId,
            feed.RecordsProduced,
            feed.RecordsConsumed,
            feed.BatchesPublished,
            feed.ChannelFullCount,
            feed.AggregationMetrics?.RecordsCompleted ?? 0,
            observedOnUtc,
            DatabentoDatasetFailureReason.None,
            null);
    }
}

public sealed record DatabentoDatasetEvaluation(
    string Dataset,
    Guid GenerationId,
    DatabentoDatasetState State,
    DatabentoDatasetFailureReason Reason,
    DateTime? SuspectSinceUtc,
    string Detail);
