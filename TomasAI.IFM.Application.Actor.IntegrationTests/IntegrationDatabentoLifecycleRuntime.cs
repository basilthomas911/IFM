using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.Actor.IntegrationTests;

/// <summary>
/// Adapts the managed deterministic feed used by integration tests to the production watchdog contract.
/// The real application epoch and contract authority remain active; only the unavailable native-process
/// diagnostics boundary is synthesized from the reconciled registration snapshot.
/// </summary>
public sealed class IntegrationDatabentoLifecycleRuntime(
    DatabentoMarketDataApi marketDataApi,
    IDatabentoContractAuthority contractAuthority,
    IDatabentoContractRegistrationRegistry registrations,
    TimeProvider timeProvider) : IDatabentoLifecycleRuntime
{
    Guid generation;

    /// <inheritdoc />
    public DateOnly? ActiveValueDate => marketDataApi.ActiveValueDate;

    /// <inheritdoc />
    public async Task PrepareContractsAsync(DateOnly valueDate, CancellationToken cancellationToken)
        => _ = await contractAuthority.ReconcileAsync(
            valueDate,
            nameof(IntegrationDatabentoLifecycleRuntime),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task StartAsync(DateOnly valueDate, CancellationToken cancellationToken)
    {
        await marketDataApi.StartAsync(valueDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        generation = Guid.CreateVersion7(timeProvider.GetUtcNow());
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (marketDataApi.ActiveValueDate is { } valueDate)
            await marketDataApi.StopAsync(valueDate).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<DatabentoDatasetResetResult> ResetDatasetAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken)
        => marketDataApi.ResetDatasetAsync(request, cancellationToken);

    /// <inheritdoc />
    public ValueTask<DatabentoBulkWatchdogSnapshot> GetWatchdogSnapshotAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var currentGeneration = generation == Guid.Empty
            ? Guid.CreateVersion7(timeProvider.GetUtcNow())
            : generation;
        var feeds = registrations.Snapshot()
            .GroupBy(static registration => registration.Dataset ?? "GLBX.MDP3", StringComparer.Ordinal)
            .Select((group, index) => CreateFeed(group.Key, group.ToArray(), currentGeneration, checked((ulong)index + 1)))
            .ToArray();
        return ValueTask.FromResult(new DatabentoBulkWatchdogSnapshot
        {
            Complete = true,
            NativeBackend = "IntegrationManaged",
            NativeAbiVersion = 3,
            NativeGeneration = currentGeneration,
            ObservedOnUtc = timeProvider.GetUtcNow().UtcDateTime,
            Feeds = feeds
        });
    }

    static DatabentoFeedWatchdogStatus CreateFeed(
        string dataset,
        IReadOnlyList<DatabentoContractRegistration> contracts,
        Guid generationId,
        ulong feedInstanceId)
    {
        var roles = contracts
            .Select(Role)
            .OfType<DatabentoContractRole>()
            .Distinct()
            .ToArray();
        return new DatabentoFeedWatchdogStatus
        {
            FeedInstanceId = feedInstanceId,
            GenerationId = generationId,
            Dataset = dataset,
            FeedKind = "Ticker",
            Criticality = roles.Length == 0 ? DatabentoFeedCriticality.Optional : DatabentoFeedCriticality.Core,
            MajorStatus = DatabentoMajorStatus.Up,
            NativeState = "ManagedIntegration",
            TerminalStatus = 0,
            ProducerAlive = true,
            AggregationWorkerRunning = true,
            TransportRunning = true,
            ExpectedSubscriptions = contracts.Count,
            ReceivedSubscriptions = contracts.Count,
            HeartbeatCount = 1,
            ProviderMessageCount = 1,
            LastHeartbeatAge = TimeSpan.Zero,
            LastProviderMessageAge = TimeSpan.Zero,
            RecordsProduced = 0,
            RecordsConsumed = 0,
            RingCapacity = 1,
            RingUsed = 0,
            RingHighWater = 0,
            RingOverruns = 0,
            BatchesPublished = 0,
            ChannelFullCount = 0,
            PoolMissCount = 0,
            ChannelBatchCount = 0,
            ChannelBatchCapacity = 1,
            FailureDetail = string.Empty,
            ContractRoles = roles,
            ContractIds = contracts.Select(static contract => contract.DomainContractId).ToArray()
        };
    }

    static DatabentoContractRole? Role(DatabentoContractRegistration registration)
    {
        if (registration.AssetTypeId != AssetTypeId.Futures || !registration.Rollover)
            return null;
        return registration.RootSymbol?.ToUpperInvariant() switch
        {
            "ES" => DatabentoContractRole.EsQuarterly,
            "VX" => registration.OnTheRun
                ? DatabentoContractRole.VxFrontMonth
                : DatabentoContractRole.VxSecondMonth,
            _ => null
        };
    }
}
