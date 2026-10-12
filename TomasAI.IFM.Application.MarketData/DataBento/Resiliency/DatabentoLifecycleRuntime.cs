using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Adapts the existing epoch API to the watchdog's exclusive lifecycle boundary.</summary>
public sealed class DatabentoLifecycleRuntime(
    DatabentoMarketDataApi marketDataApi,
    IDatabentoContractAuthority contractAuthority,
    IDatabentoContractRegistrationRegistry registrations,
    DatabentoWatchdogOptions options,
    TimeProvider timeProvider) : IDatabentoLifecycleRuntime
{
    readonly System.Threading.Lock _generationSync = new();
    Guid _generation;
    Guid Generation { get { lock (_generationSync) return _generation; } }
    public DateOnly? ActiveValueDate => marketDataApi.ActiveValueDate;

    public async Task PrepareContractsAsync(DateOnly valueDate, CancellationToken cancellationToken)
        => _ = await contractAuthority.ReconcileAsync(valueDate, nameof(DatabentoLifecycleRuntime), cancellationToken)
            .ConfigureAwait(false);

    public async Task StartAsync(DateOnly valueDate, CancellationToken cancellationToken)
    {
        await marketDataApi.StartAsync(valueDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        lock (_generationSync) _generation = Guid.CreateVersion7(timeProvider.GetUtcNow());
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (marketDataApi.ActiveValueDate is { } valueDate)
            await marketDataApi.StopAsync(valueDate).ConfigureAwait(false);
    }

    public Task<DatabentoDatasetResetResult> ResetDatasetAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken) =>
        marketDataApi.ResetDatasetAsync(request, cancellationToken);

    public ValueTask<DatabentoBulkWatchdogSnapshot> GetWatchdogSnapshotAsync(
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var health = marketDataApi.GetHealth();
        if (!DatabentoNativeWatchdog.TryRead(out var native, out var nativeFailure))
            return ValueTask.FromResult(new DatabentoBulkWatchdogSnapshot
            {
                Complete = false,
                NativeBackend = options.NativeBackend,
                NativeAbiVersion = 3,
                NativeGeneration = Generation,
                ObservedOnUtc = timeProvider.GetUtcNow().UtcDateTime,
                Feeds = [],
                FailureDetail = nativeFailure
            });
        var registrationSnapshot = registrations.Snapshot();
        var epoch = health.Epoch;
        var feeds = native.Feeds
            .Select(feed => Map(feed, native.ObservedMonotonicNanoseconds,
                epoch, registrationSnapshot))
            .ToArray();
        // Complete describes registry enumeration, not operational health. Individual feed and
        // managed-worker fields below decide readiness and preserve optional-feed isolation.
        var complete = native.Feeds.Count == feeds.Length;
        return ValueTask.FromResult(new DatabentoBulkWatchdogSnapshot
        {
            Complete = complete,
            NativeBackend = options.NativeBackend,
            NativeAbiVersion = 3,
            NativeGeneration = Generation,
            ObservedOnUtc = timeProvider.GetUtcNow().UtcDateTime,
            Feeds = feeds,
            FailureDetail = complete ? string.Empty : "The native registry snapshot was incomplete."
        });
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    DatabentoFeedWatchdogStatus Map(DatabentoNativeFeedWatchdogStatus native,
        ulong observedMonotonicNanoseconds, DatabentoMarketDataEpochHealth? epoch,
        IReadOnlyList<DatabentoContractRegistration> all)
    {
        var members = all.Where(registration => string.Equals(
            registration.Dataset, native.Dataset, StringComparison.Ordinal)).ToArray();
        var roles = members.Select(Role).Where(role => role.HasValue).Select(role => role!.Value).Distinct().ToArray();
        var isCore = roles.Length != 0;
        var contractStatuses = epoch?.ContractStatuses ?? [];
        var memberIds = members.Select(member => member.DomainContractId).ToHashSet(StringComparer.Ordinal);
        var datasetHealth = epoch?.DatasetFeedStatuses?.FirstOrDefault(status =>
            string.Equals(status.Dataset, native.Dataset, StringComparison.Ordinal));
        var managedReady = epoch is { Running: true, LastPriceStoreActive: true }
            && memberIds.Count != 0
            && contractStatuses.Where(status => memberIds.Contains(status.ContractId)).ToArray() is { Length: > 0 } statuses
            && statuses.All(status => status.ServiceRunning && status.ContractConfigured && status.ContractRunning);
        var major = native.MajorStatus switch
        {
            1 => DatabentoMajorStatus.Up,
            2 => DatabentoMajorStatus.Resetting,
            _ => DatabentoMajorStatus.Down
        };
        var up = major == DatabentoMajorStatus.Up && managedReady;
        return new DatabentoFeedWatchdogStatus
        {
            FeedInstanceId = native.FeedInstanceId,
            GenerationId = datasetHealth?.GenerationId ?? Generation,
            Dataset = native.Dataset,
            FeedKind = native.FeedKind == 2 ? "OptionChain" : "Ticker",
            Criticality = isCore
                ? DatabentoFeedCriticality.Core : DatabentoFeedCriticality.Optional,
            MajorStatus = up ? DatabentoMajorStatus.Up : major,
            NativeState = native.State.ToString(),
            TerminalStatus = (int)native.TerminalStatus,
            ProducerAlive = native.ProducerAlive,
            AggregationWorkerRunning = managedReady,
            TransportRunning = native.ProducerAlive,
            ExpectedSubscriptions = checked((int)native.ExpectedSubscriptions),
            ReceivedSubscriptions = checked((int)native.ReceivedSubscriptions),
            HeartbeatCount = native.HeartbeatCount,
            ProviderMessageCount = native.ProviderMessageCount,
            LastHeartbeatAge = Age(observedMonotonicNanoseconds, native.LastHeartbeatMonotonicNanoseconds, major),
            LastProviderMessageAge = Age(observedMonotonicNanoseconds, native.LastProviderMessageMonotonicNanoseconds, major),
            RecordsProduced = native.RecordsProduced,
            RecordsConsumed = native.RecordsConsumed,
            RingCapacity = native.RingCapacityRecords,
            RingUsed = native.RingUsedRecords,
            RingHighWater = native.RingHighWaterRecords,
            RingOverruns = native.RingOverruns,
            BatchesPublished = datasetHealth?.Health.BatchesPublished ?? 0,
            ChannelFullCount = datasetHealth?.Health.ChannelFullCount ?? 0,
            PoolMissCount = datasetHealth?.Health.PoolMissCount ?? 0,
            ChannelBatchCount = datasetHealth?.Health.ChannelBatchCount ?? 0,
            ChannelBatchCapacity = datasetHealth?.Health.ChannelBatchCapacity ?? 0,
            FailureDetail = native.FailureDetail,
            ContractRoles = roles,
            ContractIds = members.Select(registration => registration.DomainContractId).ToArray(),
            DrainDiagnostics = datasetHealth is { Dataset: not null }
                ? datasetHealth.Value.Health.DrainDiagnostics
                : null,
            AggregationMetrics = datasetHealth is { Dataset: not null }
                ? datasetHealth.Value.AggregationMetrics
                : null
        };
    }

    static TimeSpan Age(ulong observed, ulong last, DatabentoMajorStatus status)
    {
        if (last == 0) return status == DatabentoMajorStatus.Up ? TimeSpan.Zero : TimeSpan.MaxValue;
        return TimeSpan.FromTicks(checked((long)Math.Min((observed - Math.Min(observed, last)) / 100, (ulong)long.MaxValue)));
    }

    static DatabentoContractRole? Role(DatabentoContractRegistration registration)
    {
        if (registration.AssetTypeId != TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures
            || !registration.Rollover) return null;
        if (string.Equals(registration.RootSymbol, "ES", StringComparison.OrdinalIgnoreCase))
            return DatabentoContractRole.EsQuarterly;
        if (!string.Equals(registration.RootSymbol, "VX", StringComparison.OrdinalIgnoreCase)) return null;
        return registration.OnTheRun ? DatabentoContractRole.VxFrontMonth : DatabentoContractRole.VxSecondMonth;
    }
}
