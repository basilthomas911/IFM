using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// A bounded observation captured inside the dataset process. Complete means native and managed
/// evidence was obtained, not that the pipeline is healthy. Missing evidence must never become Up.
/// </summary>
[MessagePackObject]
public sealed record DatasetWorkerDiagnostics
{
    [Key(0)] public required string Dataset { get; init; }
    [Key(1)] public required Guid GenerationId { get; init; }
    [Key(2)] public DateTime ObservedOnUtc { get; init; }
    [Key(3)] public bool Complete { get; init; }
    [Key(4)] public ulong FeedInstanceId { get; init; }
    [Key(5)] public uint NativeMajorStatus { get; init; }
    [Key(6)] public FeedState NativeState { get; init; }
    [Key(7)] public int TerminalStatus { get; init; }
    [Key(8)] public bool ProducerAlive { get; init; }
    [Key(9)] public bool AggregationRunning { get; init; }
    [Key(10)] public bool TransportReady { get; init; }
    [Key(11)] public int ExpectedSubscriptions { get; init; }
    [Key(12)] public int ReceivedSubscriptions { get; init; }
    [Key(13)] public ulong HeartbeatCount { get; init; }
    [Key(14)] public ulong ProviderMessageCount { get; init; }
    [Key(15)] public long LastHeartbeatAgeTicks { get; init; }
    [Key(16)] public long LastProviderMessageAgeTicks { get; init; }
    [Key(17)] public ulong RecordsProduced { get; init; }
    [Key(18)] public ulong RecordsConsumed { get; init; }
    [Key(19)] public ulong RingCapacity { get; init; }
    [Key(20)] public ulong RingUsed { get; init; }
    [Key(21)] public ulong RingHighWater { get; init; }
    [Key(22)] public ulong RingOverruns { get; init; }
    [Key(23)] public ulong BatchesPublished { get; init; }
    [Key(24)] public ulong ChannelFullCount { get; init; }
    [Key(25)] public ulong PoolMissCount { get; init; }
    [Key(26)] public int ChannelBatchCount { get; init; }
    [Key(27)] public int ChannelBatchCapacity { get; init; }
    [Key(28)] public string FailureDetail { get; init; } = string.Empty;
    [Key(29)] public DatasetWorkerDrainDiagnostics? Drain { get; init; }
    [Key(30)] public DatasetWorkerAggregationDiagnostics? Aggregation { get; init; }

    [IgnoreMember] public bool Operational => Complete && NativeMajorStatus == 1
        && TerminalStatus == 0 && ProducerAlive && AggregationRunning && TransportReady
        && ReceivedSubscriptions >= ExpectedSubscriptions && RingOverruns == 0;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Dataset) || Dataset.Length > 64
            || GenerationId == Guid.Empty || ObservedOnUtc == default
            || FailureDetail is null || FailureDetail.Length > 4096
            || LastHeartbeatAgeTicks < 0 || LastProviderMessageAgeTicks < 0
            || ExpectedSubscriptions < 0 || ReceivedSubscriptions < 0
            || ChannelBatchCount < 0 || ChannelBatchCapacity < 0
            || Complete && (FeedInstanceId == 0 || Drain is null || Aggregation is null))
            throw new InvalidDataException("Dataset worker diagnostic identity or bounds are invalid.");
        Drain?.Validate();
        Aggregation?.Validate();
    }

    public static DatasetWorkerDiagnostics Unavailable(string dataset, Guid generation, string detail,
        DateTime observedOnUtc) => new()
    {
        Dataset = dataset, GenerationId = generation, ObservedOnUtc = observedOnUtc,
        LastHeartbeatAgeTicks = long.MaxValue, LastProviderMessageAgeTicks = long.MaxValue,
        FailureDetail = Bound(detail, 4096)
    };

    public static DatasetWorkerDiagnostics Capture(DatasetSubscriptionManifest manifest,
        DatabentoMarketDataEpochHealth epoch, DatabentoNativeWatchdogSnapshot? native,
        string failureDetail, DateTime observedOnUtc)
    {
        var managed = epoch.DatasetFeedStatuses?.Where(value => value.Dataset == manifest.Dataset).ToArray() ?? [];
        if (managed.Length != 1 || managed[0].GenerationId == Guid.Empty)
            throw new InvalidOperationException("The worker has no unique managed dataset generation.");
        var generation = managed[0].GenerationId;
        var feeds = native?.Feeds.Where(value => value.Dataset == manifest.Dataset && value.FeedKind == 1).ToArray() ?? [];
        if (feeds.Length != 1 || managed[0].Health.DrainDiagnostics is null)
            return Unavailable(manifest.Dataset, generation,
                string.IsNullOrEmpty(failureDetail) ? "Native or managed dataset diagnostics are incomplete." : failureDetail,
                observedOnUtc);
        var feed = feeds[0];
        var health = managed[0].Health;
        var statuses = epoch.ContractStatuses ?? [];
        var managedReady = epoch.Running && epoch.LastPriceStoreActive
            && manifest.Contracts.All(contract => statuses.Any(status => status.ContractId == contract.DomainContractId
                && status.ServiceRunning && status.ContractConfigured && status.ContractRunning));
        return new()
        {
            Dataset = manifest.Dataset, GenerationId = generation, ObservedOnUtc = observedOnUtc,
            Complete = true, FeedInstanceId = feed.FeedInstanceId, NativeMajorStatus = feed.MajorStatus,
            NativeState = feed.State, TerminalStatus = (int)feed.TerminalStatus,
            ProducerAlive = feed.ProducerAlive, AggregationRunning = managedReady,
            TransportReady = health.TransportReady, ExpectedSubscriptions = checked((int)feed.ExpectedSubscriptions),
            ReceivedSubscriptions = checked((int)feed.ReceivedSubscriptions), HeartbeatCount = feed.HeartbeatCount,
            ProviderMessageCount = feed.ProviderMessageCount,
            LastHeartbeatAgeTicks = Age(native!.ObservedMonotonicNanoseconds, feed.LastHeartbeatMonotonicNanoseconds),
            LastProviderMessageAgeTicks = Age(native.ObservedMonotonicNanoseconds, feed.LastProviderMessageMonotonicNanoseconds),
            RecordsProduced = feed.RecordsProduced, RecordsConsumed = feed.RecordsConsumed,
            RingCapacity = feed.RingCapacityRecords, RingUsed = feed.RingUsedRecords,
            RingHighWater = feed.RingHighWaterRecords, RingOverruns = feed.RingOverruns,
            BatchesPublished = health.BatchesPublished, ChannelFullCount = health.ChannelFullCount,
            PoolMissCount = health.PoolMissCount, ChannelBatchCount = health.ChannelBatchCount,
            ChannelBatchCapacity = health.ChannelBatchCapacity,
            FailureDetail = Bound(string.IsNullOrEmpty(feed.FailureDetail) ? health.Warning ?? string.Empty : feed.FailureDetail, 4096),
            Drain = DatasetWorkerDrainDiagnostics.From(health.DrainDiagnostics!),
            Aggregation = DatasetWorkerAggregationDiagnostics.From(managed[0].AggregationMetrics)
        };
    }

    public DatabentoFeedWatchdogStatus ToWatchdogStatus(IReadOnlyList<DatabentoContractRegistration> contracts,
        bool processHealthy)
    {
        Validate();
        var roles = contracts.Select(Role).OfType<DatabentoContractRole>().Distinct().ToArray();
        return new()
        {
            FeedInstanceId = FeedInstanceId, GenerationId = GenerationId, Dataset = Dataset, FeedKind = "Ticker",
            Criticality = roles.Length == 0 ? DatabentoFeedCriticality.Optional : DatabentoFeedCriticality.Core,
            MajorStatus = !Complete || !processHealthy ? DatabentoMajorStatus.Down : NativeMajorStatus switch
            {
                1 => DatabentoMajorStatus.Up, 2 => DatabentoMajorStatus.Resetting, _ => DatabentoMajorStatus.Down
            },
            NativeState = Complete ? NativeState.ToString() : "DiagnosticsUnavailable", TerminalStatus = TerminalStatus,
            ProducerAlive = Complete && processHealthy && ProducerAlive,
            AggregationWorkerRunning = Complete && processHealthy && AggregationRunning,
            TransportRunning = Complete && processHealthy && TransportReady,
            ExpectedSubscriptions = ExpectedSubscriptions, ReceivedSubscriptions = ReceivedSubscriptions,
            HeartbeatCount = HeartbeatCount, ProviderMessageCount = ProviderMessageCount,
            LastHeartbeatAge = TimeSpan.FromTicks(LastHeartbeatAgeTicks),
            LastProviderMessageAge = TimeSpan.FromTicks(LastProviderMessageAgeTicks),
            RecordsProduced = RecordsProduced, RecordsConsumed = RecordsConsumed, RingCapacity = RingCapacity,
            RingUsed = RingUsed, RingHighWater = RingHighWater, RingOverruns = RingOverruns,
            BatchesPublished = BatchesPublished, ChannelFullCount = ChannelFullCount, PoolMissCount = PoolMissCount,
            ChannelBatchCount = ChannelBatchCount, ChannelBatchCapacity = ChannelBatchCapacity,
            FailureDetail = FailureDetail, ContractRoles = roles,
            ContractIds = contracts.Select(contract => contract.DomainContractId).ToArray(),
            DrainDiagnostics = Drain?.ToDiagnostics(), AggregationMetrics = Aggregation?.ToMetrics()
        };
    }

    static long Age(ulong observed, ulong last) => last == 0 ? long.MaxValue
        : checked((long)Math.Min((observed - Math.Min(observed, last)) / 100, (ulong)long.MaxValue));
    internal static string Bound(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
    static DatabentoContractRole? Role(DatabentoContractRegistration value) => !value.Rollover ? null
        : value.RootSymbol?.ToUpperInvariant() switch
        {
            "ES" => DatabentoContractRole.EsQuarterly,
            "VX" => value.OnTheRun ? DatabentoContractRole.VxFrontMonth : DatabentoContractRole.VxSecondMonth,
            _ => null
        };
}

[MessagePackObject]
public sealed record DatasetWorkerDrainDiagnostics(
    [property: Key(0)] FeedDrainStage Stage,
    [property: Key(1)] long NativeReadCallCount,
    [property: Key(2)] uint LastNativeReadRecordCount,
    [property: Key(3)] ulong LastNativeReadFirstSequence,
    [property: Key(4)] ulong LastNativeReadLastSequence,
    [property: Key(5)] uint LastNativeReadRecordsRouted,
    [property: Key(6)] int CurrentNativeReadRecordIndex,
    [property: Key(7)] string CurrentRecordKind,
    [property: Key(8)] ushort CurrentPublisherId,
    [property: Key(9)] uint CurrentInstrumentId,
    [property: Key(10)] uint CurrentSourceSequence,
    [property: Key(11)] bool ManagedBatchPublishActive,
    [property: Key(12)] int ManagedBatchPublishRecordCount,
    [property: Key(13)] ushort ManagedBatchPublisherId,
    [property: Key(14)] uint ManagedBatchInstrumentId)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Stage) || CurrentRecordKind is null || CurrentRecordKind.Length > 64)
            throw new InvalidDataException("Worker drain diagnostic bounds are invalid.");
    }
    public static DatasetWorkerDrainDiagnostics From(FeedDrainDiagnostics value) => new(value.Stage,
        value.NativeReadCallCount, value.LastNativeReadRecordCount, value.LastNativeReadFirstSequence,
        value.LastNativeReadLastSequence, value.LastNativeReadRecordsRouted, value.CurrentNativeReadRecordIndex,
        DatasetWorkerDiagnostics.Bound(value.CurrentRecordKind, 64), value.CurrentPublisherId,
        value.CurrentInstrumentId, value.CurrentSourceSequence, value.ManagedBatchPublishActive,
        value.ManagedBatchPublishRecordCount, value.ManagedBatchPublisherId, value.ManagedBatchInstrumentId);
    public FeedDrainDiagnostics ToDiagnostics() => new()
    {
        Stage = Stage, NativeReadCallCount = NativeReadCallCount, LastNativeReadRecordCount = LastNativeReadRecordCount,
        LastNativeReadFirstSequence = LastNativeReadFirstSequence, LastNativeReadLastSequence = LastNativeReadLastSequence,
        LastNativeReadRecordsRouted = LastNativeReadRecordsRouted, CurrentNativeReadRecordIndex = CurrentNativeReadRecordIndex,
        CurrentRecordKind = CurrentRecordKind, CurrentPublisherId = CurrentPublisherId,
        CurrentInstrumentId = CurrentInstrumentId, CurrentSourceSequence = CurrentSourceSequence,
        ManagedBatchPublishActive = ManagedBatchPublishActive, ManagedBatchPublishRecordCount = ManagedBatchPublishRecordCount,
        ManagedBatchPublisherId = ManagedBatchPublisherId, ManagedBatchInstrumentId = ManagedBatchInstrumentId
    };
}

/// <summary>Explicit wire representation of aggregation counters and its causal in-flight record.</summary>
[MessagePackObject]
public sealed record DatasetWorkerAggregationDiagnostics(
    [property: Key(0)] long SourceQuoteRecords, [property: Key(1)] long SourceTradeRecords,
    [property: Key(2)] long EmittedQuoteBatches, [property: Key(3)] long EmittedQuoteItems,
    [property: Key(4)] long EmittedTradeEvents, [property: Key(5)] long BufferFullFlushes,
    [property: Key(6)] long PartialQuoteFlushes, [property: Key(7)] long DuplicateSourceSequences,
    [property: Key(8)] long OutOfOrderSourceSequences, [property: Key(9)] long SourceSequenceGaps,
    [property: Key(10)] long PublicationFailures, [property: Key(11)] long ProcessingFailures,
    [property: Key(12)] int ActiveTickers, [property: Key(13)] int ServiceOwnedQuoteBuffers,
    [property: Key(14)] long RecordsStarted, [property: Key(15)] long RecordsCompleted,
    [property: Key(16)] long SourceMboRecords, [property: Key(17)] long SourceStatisticsRecords,
    [property: Key(18)] long StatisticsReplayCompleteRecords, [property: Key(19)] long TradeReplayCompleteRecords,
    [property: Key(20)] long UnsupportedRecords, [property: Key(21)] long CurrentProcessingDurationTicks,
    [property: Key(22)] long TotalProcessingDurationTicks, [property: Key(23)] long MaximumProcessingDurationTicks,
    [property: Key(24)] DateTimeOffset? LastRecordStartedAtUtc, [property: Key(25)] DateTimeOffset? LastRecordCompletedAtUtc,
    [property: Key(26)] DateTimeOffset? LastRecordFailedAtUtc, [property: Key(27)] TickAggregationProcessingStage CurrentStage,
    [property: Key(28)] DatasetWorkerRecordProgress? InFlightRecord)
{
    public void Validate()
    {
        if (!Enum.IsDefined(CurrentStage) || CurrentProcessingDurationTicks < 0)
            throw new InvalidDataException("Worker aggregation diagnostic bounds are invalid.");
        InFlightRecord?.Validate();
    }
    public static DatasetWorkerAggregationDiagnostics From(TickAggregationMetricsSnapshot value) => new(
        value.SourceQuoteRecords, value.SourceTradeRecords, value.EmittedQuoteBatches, value.EmittedQuoteItems,
        value.EmittedTradeEvents, value.BufferFullFlushes, value.PartialQuoteFlushes, value.DuplicateSourceSequences,
        value.OutOfOrderSourceSequences, value.SourceSequenceGaps, value.PublicationFailures, value.ProcessingFailures,
        value.ActiveTickers, value.ServiceOwnedQuoteBuffers, value.RecordsStarted, value.RecordsCompleted,
        value.SourceMboRecords, value.SourceStatisticsRecords, value.StatisticsReplayCompleteRecords,
        value.TradeReplayCompleteRecords, value.UnsupportedRecords, value.CurrentProcessingDurationTicks,
        value.TotalProcessingDurationTicks, value.MaximumProcessingDurationTicks, value.LastRecordStartedAtUtc,
        value.LastRecordCompletedAtUtc, value.LastRecordFailedAtUtc, value.CurrentStage,
        value.InFlightRecord is { } record ? DatasetWorkerRecordProgress.From(record) : null);
    public TickAggregationMetricsSnapshot ToMetrics() => new(SourceQuoteRecords, SourceTradeRecords,
        EmittedQuoteBatches, EmittedQuoteItems, EmittedTradeEvents, BufferFullFlushes, PartialQuoteFlushes,
        DuplicateSourceSequences, OutOfOrderSourceSequences, SourceSequenceGaps, PublicationFailures,
        ProcessingFailures, ActiveTickers, ServiceOwnedQuoteBuffers)
    {
        RecordsStarted = RecordsStarted, RecordsCompleted = RecordsCompleted, SourceMboRecords = SourceMboRecords,
        SourceStatisticsRecords = SourceStatisticsRecords, StatisticsReplayCompleteRecords = StatisticsReplayCompleteRecords,
        TradeReplayCompleteRecords = TradeReplayCompleteRecords, UnsupportedRecords = UnsupportedRecords,
        CurrentProcessingDurationTicks = CurrentProcessingDurationTicks, TotalProcessingDurationTicks = TotalProcessingDurationTicks,
        MaximumProcessingDurationTicks = MaximumProcessingDurationTicks, LastRecordStartedAtUtc = LastRecordStartedAtUtc,
        LastRecordCompletedAtUtc = LastRecordCompletedAtUtc, LastRecordFailedAtUtc = LastRecordFailedAtUtc,
        CurrentStage = CurrentStage, InFlightRecord = InFlightRecord?.ToProgress()
    };
}

[MessagePackObject]
public sealed record DatasetWorkerRecordProgress(
    [property: Key(0)] string Dataset, [property: Key(1)] string ContractId, [property: Key(2)] string RecordKind,
    [property: Key(3)] ushort PublisherId, [property: Key(4)] uint InstrumentId,
    [property: Key(5)] uint SourceSequence, [property: Key(6)] DateTimeOffset StartedAtUtc)
{
    public void Validate()
    {
        if (Dataset is null || Dataset.Length > 64 || ContractId is null || ContractId.Length > 256
            || RecordKind is null || RecordKind.Length > 64)
            throw new InvalidDataException("Worker in-flight record diagnostic bounds are invalid.");
    }
    public static DatasetWorkerRecordProgress From(TickAggregationRecordProgress value) => new(
        DatasetWorkerDiagnostics.Bound(value.Dataset, 64), DatasetWorkerDiagnostics.Bound(value.ContractId, 256),
        DatasetWorkerDiagnostics.Bound(value.RecordKind, 64), value.PublisherId, value.InstrumentId,
        value.SourceSequence, value.StartedAtUtc);
    public TickAggregationRecordProgress ToProgress() => new(Dataset, ContractId, RecordKind,
        PublisherId, InstrumentId, SourceSequence, StartedAtUtc);
}
