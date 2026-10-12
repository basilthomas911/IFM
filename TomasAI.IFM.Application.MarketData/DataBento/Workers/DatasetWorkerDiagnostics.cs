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
    [Key(31)] public long ManagedAllocatedBytes { get; init; }
    [Key(32)] public long ManagedHeapBytes { get; init; }
    [Key(33)] public int Gen0Collections { get; init; }
    [Key(34)] public int Gen1Collections { get; init; }
    [Key(35)] public int Gen2Collections { get; init; }
    [Key(36)] public long GcPauseTicks { get; init; }
    [Key(37)] public ulong OptionRecordsProduced { get; init; }
    [Key(38)] public ulong OptionRecordsConsumed { get; init; }
    [Key(39)] public ulong OptionRingUsed { get; init; }
    [Key(40)] public ulong OptionRingOverruns { get; init; }

    [IgnoreMember]
    public bool Operational => Complete && NativeMajorStatus == 1
        && TerminalStatus == 0 && ProducerAlive && AggregationRunning && TransportReady
        && ReceivedSubscriptions >= ExpectedSubscriptions && RingOverruns == 0;

    /// <summary>
    /// Databento transport and local drain qualification, independent of aggregation and
    /// downstream publication. Live-session record progress is checked separately over time.
    /// </summary>
    [IgnoreMember]
    public bool DatabentoLocallyReady => Complete && FeedInstanceId != 0
        && NativeMajorStatus == 1 && NativeState == FeedState.Running
        && TerminalStatus == 0 && ProducerAlive && TransportReady
        && ExpectedSubscriptions > 0 && ReceivedSubscriptions >= ExpectedSubscriptions
        && RingOverruns == 0 && Drain is not null;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Dataset) || Dataset.Length > 64
            || GenerationId == Guid.Empty || ObservedOnUtc == default
            || FailureDetail is null || FailureDetail.Length > 4096
            || LastHeartbeatAgeTicks < 0 || LastProviderMessageAgeTicks < 0
            || ExpectedSubscriptions < 0 || ReceivedSubscriptions < 0
            || ChannelBatchCount < 0 || ChannelBatchCapacity < 0
            || ManagedAllocatedBytes < 0 || ManagedHeapBytes < 0 || Gen0Collections < 0
            || Gen1Collections < 0 || Gen2Collections < 0 || GcPauseTicks < 0
            || Complete && (FeedInstanceId == 0 || Drain is null || Aggregation is null))
            throw new InvalidDataException($"Dataset worker diagnostic identity or bounds are invalid: " +
                $"expected={ExpectedSubscriptions}, received={ReceivedSubscriptions}, channel={ChannelBatchCount}/{ChannelBatchCapacity}, " +
                $"heartbeatAge={LastHeartbeatAgeTicks}, providerAge={LastProviderMessageAgeTicks}, " +
                $"allocated={ManagedAllocatedBytes}, heap={ManagedHeapBytes}, gc={Gen0Collections}/{Gen1Collections}/{Gen2Collections}, pause={GcPauseTicks}, " +
                $"complete={Complete}, feed={FeedInstanceId}, drain={Drain is not null}, aggregation={Aggregation is not null}.");
        Drain?.Validate();
        Aggregation?.Validate();
    }

    public static DatasetWorkerDiagnostics Unavailable(string dataset, Guid generation, string detail,
        DateTime observedOnUtc) 
    {
        ArgumentNullException.ThrowIfNull(detail);
        return new()
        {
            Dataset = dataset,
            GenerationId = generation,
            ObservedOnUtc = observedOnUtc,
            LastHeartbeatAgeTicks = long.MaxValue,
            LastProviderMessageAgeTicks = long.MaxValue,
            FailureDetail = Bound(detail, 4096)
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public static DatasetWorkerDiagnostics Capture(DatasetSubscriptionManifest manifest,
        DatabentoMarketDataEpochHealth epoch, DatabentoNativeWatchdogSnapshot? native,
        string failureDetail, DateTime observedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var managed = epoch.DatasetFeedStatuses?.Where(value => string.Equals(value.Dataset,manifest.Dataset,StringComparison.Ordinal)).ToArray() ?? [];
        if (managed.Length != 1 || managed[0].GenerationId == Guid.Empty)
            throw new InvalidOperationException("The worker has no unique managed dataset generation.");
        var generation = managed[0].GenerationId;
        var feeds = native?.Feeds.Where(value => string.Equals(value.Dataset,manifest.Dataset,StringComparison.Ordinal) && value.FeedKind == 1).ToArray() ?? [];
        if (feeds.Length != 1 || managed[0].Health.DrainDiagnostics is null)
            return Unavailable(manifest.Dataset, generation,
                string.IsNullOrEmpty(failureDetail) ? "Native or managed dataset diagnostics are incomplete." : failureDetail,
                observedOnUtc);
        var feed = feeds[0];
        var health = managed[0].Health;
        var statuses = epoch.ContractStatuses ?? [];
        var managedReady = epoch.Running && epoch.LastPriceStoreActive
            && manifest.Contracts.All(contract => statuses.Any(status => string.Equals(status.ContractId,contract.DomainContractId,StringComparison.Ordinal)
                && status.ServiceRunning && status.ContractConfigured && status.ContractRunning));
        return new()
        {
            Dataset = manifest.Dataset,
            GenerationId = generation,
            ObservedOnUtc = observedOnUtc,
            Complete = true,
            FeedInstanceId = feed.FeedInstanceId,
            NativeMajorStatus = feed.MajorStatus,
            NativeState = feed.State,
            TerminalStatus = (int)feed.TerminalStatus,
            ProducerAlive = feed.ProducerAlive,
            AggregationRunning = managedReady,
            TransportReady = health.TransportReady,
            ExpectedSubscriptions = checked((int)feed.ExpectedSubscriptions),
            ReceivedSubscriptions = checked((int)feed.ReceivedSubscriptions),
            HeartbeatCount = feed.HeartbeatCount,
            ProviderMessageCount = feed.ProviderMessageCount,
            LastHeartbeatAgeTicks = Age(native!.ObservedMonotonicNanoseconds, feed.LastHeartbeatMonotonicNanoseconds),
            LastProviderMessageAgeTicks = Age(native.ObservedMonotonicNanoseconds, feed.LastProviderMessageMonotonicNanoseconds),
            RecordsProduced = feed.RecordsProduced,
            RecordsConsumed = feed.RecordsConsumed,
            RingCapacity = feed.RingCapacityRecords,
            RingUsed = feed.RingUsedRecords,
            RingHighWater = feed.RingHighWaterRecords,
            RingOverruns = feed.RingOverruns,
            BatchesPublished = health.BatchesPublished,
            ChannelFullCount = health.ChannelFullCount,
            PoolMissCount = health.PoolMissCount,
            ChannelBatchCount = health.ChannelBatchCount,
            ChannelBatchCapacity = health.ChannelBatchCapacity,
            FailureDetail = Bound(string.IsNullOrEmpty(feed.FailureDetail) ? health.Warning ?? string.Empty : feed.FailureDetail, 4096),
            Drain = DatasetWorkerDrainDiagnostics.From(health.DrainDiagnostics!),
            Aggregation = DatasetWorkerAggregationDiagnostics.From(managed[0].AggregationMetrics),
            ManagedAllocatedBytes = GC.GetTotalAllocatedBytes(),
            ManagedHeapBytes = GC.GetTotalMemory(false),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            GcPauseTicks = GC.GetTotalPauseDuration().Ticks,
            OptionRecordsProduced = native.Feeds.Where(x => string.Equals(x.Dataset,manifest.Dataset,StringComparison.Ordinal) && x.FeedKind == 2).Aggregate(0UL, (sum, x) => sum + x.RecordsProduced),
            OptionRecordsConsumed = native.Feeds.Where(x => string.Equals(x.Dataset,manifest.Dataset,StringComparison.Ordinal) && x.FeedKind == 2).Aggregate(0UL, (sum, x) => sum + x.RecordsConsumed),
            OptionRingUsed = native.Feeds.Where(x => string.Equals(x.Dataset,manifest.Dataset,StringComparison.Ordinal) && x.FeedKind == 2).Aggregate(0UL, (sum, x) => sum + x.RingUsedRecords),
            OptionRingOverruns = native.Feeds.Where(x => string.Equals(x.Dataset,manifest.Dataset,StringComparison.Ordinal) && x.FeedKind == 2).Aggregate(0UL, (sum, x) => sum + x.RingOverruns)
        };
    }

    public DatabentoFeedWatchdogStatus ToWatchdogStatus(IReadOnlyList<DatabentoContractRegistration> contracts,
        bool processHealthy)
    {
        Validate();
        var roles = contracts.Select(Role).OfType<DatabentoContractRole>().Distinct().ToArray();
        return new()
        {
            FeedInstanceId = FeedInstanceId,
            GenerationId = GenerationId,
            Dataset = Dataset,
            FeedKind = "Ticker",
            Criticality = roles.Length == 0 ? DatabentoFeedCriticality.Optional : DatabentoFeedCriticality.Core,
            MajorStatus = !Complete || !processHealthy ? DatabentoMajorStatus.Down : NativeMajorStatus switch
            {
                1 => DatabentoMajorStatus.Up,
                2 => DatabentoMajorStatus.Resetting,
                _ => DatabentoMajorStatus.Down
            },
            NativeState = Complete ? NativeState.ToString() : "DiagnosticsUnavailable",
            TerminalStatus = TerminalStatus,
            ProducerAlive = Complete && processHealthy && ProducerAlive,
            AggregationWorkerRunning = Complete && processHealthy && AggregationRunning,
            TransportRunning = Complete && processHealthy && TransportReady,
            ExpectedSubscriptions = ExpectedSubscriptions,
            ReceivedSubscriptions = ReceivedSubscriptions,
            HeartbeatCount = HeartbeatCount,
            ProviderMessageCount = ProviderMessageCount,
            LastHeartbeatAge = TimeSpan.FromTicks(LastHeartbeatAgeTicks),
            LastProviderMessageAge = TimeSpan.FromTicks(LastProviderMessageAgeTicks),
            RecordsProduced = RecordsProduced,
            RecordsConsumed = RecordsConsumed,
            RingCapacity = RingCapacity,
            RingUsed = RingUsed,
            RingHighWater = RingHighWater,
            RingOverruns = RingOverruns,
            BatchesPublished = BatchesPublished,
            ChannelFullCount = ChannelFullCount,
            PoolMissCount = PoolMissCount,
            ChannelBatchCount = ChannelBatchCount,
            ChannelBatchCapacity = ChannelBatchCapacity,
            FailureDetail = FailureDetail,
            ContractRoles = roles,
            ContractIds = contracts.Select(contract => contract.DomainContractId).ToArray(),
            DrainDiagnostics = Drain?.ToDiagnostics(),
            AggregationMetrics = Aggregation?.ToMetrics()
        };
    }

    static long Age(ulong observed, ulong last) => last == 0 ? long.MaxValue
        : checked((long)Math.Min((observed - Math.Min(observed, last)) / 100, (ulong)long.MaxValue));
    internal static string Bound(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
    static DatabentoContractRole? Role(DatabentoContractRegistration value) => (!value.Rollover) switch { true => null, false => value.RootSymbol?.ToUpperInvariant() switch
        {
            "ES" => DatabentoContractRole.EsQuarterly,
            "VX" => value.OnTheRun ? DatabentoContractRole.VxFrontMonth : DatabentoContractRole.VxSecondMonth,
            _ => null
        } };
}
