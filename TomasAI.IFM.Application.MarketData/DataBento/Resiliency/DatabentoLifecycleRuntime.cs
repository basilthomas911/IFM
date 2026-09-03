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
    readonly object _generationSync = new();
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
                Complete = false, NativeBackend = options.NativeBackend, NativeAbiVersion = 3,
                NativeGeneration = Generation, ObservedOnUtc = timeProvider.GetUtcNow().UtcDateTime,
                Feeds = [], FailureDetail = nativeFailure
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
            Dataset = native.Dataset, FeedKind = native.FeedKind == 2 ? "OptionChain" : "Ticker", Criticality = isCore
                ? DatabentoFeedCriticality.Core : DatabentoFeedCriticality.Optional,
            MajorStatus = up ? DatabentoMajorStatus.Up : major,
            NativeState = native.State.ToString(), TerminalStatus = (int)native.TerminalStatus,
            ProducerAlive = native.ProducerAlive, AggregationWorkerRunning = managedReady,
            TransportRunning = native.ProducerAlive, ExpectedSubscriptions = checked((int)native.ExpectedSubscriptions),
            ReceivedSubscriptions = checked((int)native.ReceivedSubscriptions),
            HeartbeatCount = native.HeartbeatCount, ProviderMessageCount = native.ProviderMessageCount,
            LastHeartbeatAge = Age(observedMonotonicNanoseconds, native.LastHeartbeatMonotonicNanoseconds, major),
            LastProviderMessageAge = Age(observedMonotonicNanoseconds, native.LastProviderMessageMonotonicNanoseconds, major),
            RecordsProduced = native.RecordsProduced, RecordsConsumed = native.RecordsConsumed,
            RingCapacity = native.RingCapacityRecords, RingUsed = native.RingUsedRecords,
            RingHighWater = native.RingHighWaterRecords, RingOverruns = native.RingOverruns,
            BatchesPublished = datasetHealth?.Health.BatchesPublished ?? 0,
            ChannelFullCount = datasetHealth?.Health.ChannelFullCount ?? 0,
            PoolMissCount = datasetHealth?.Health.PoolMissCount ?? 0,
            ChannelBatchCount = datasetHealth?.Health.ChannelBatchCount ?? 0,
            ChannelBatchCapacity = datasetHealth?.Health.ChannelBatchCapacity ?? 0,
            FailureDetail = native.FailureDetail, ContractRoles = roles,
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

/// <summary>Deterministic store for tests and non-persistent composition; production uses PostgreSQL.</summary>
public sealed class InMemoryMarketDataServiceStore : IMarketDataServiceStore
{
    readonly object _sync = new();
    readonly Dictionary<DatabentoContractRole, FuturesRolloverContractAssignment> _assignments = [];
    readonly List<DatabentoWatchdogObservation> _observations = [];
    long _nextObservationId;

    public Task<FuturesRolloverContractAssignment?> GetAssignmentAsync(DatabentoContractRole role,
        CancellationToken cancellationToken = default)
    { lock (_sync) return Task.FromResult(_assignments.GetValueOrDefault(role)); }

    public Task<IReadOnlyList<FuturesRolloverContractAssignment>> ListAssignmentsAsync(CancellationToken cancellationToken = default)
    { lock (_sync) return Task.FromResult<IReadOnlyList<FuturesRolloverContractAssignment>>([.. _assignments.Values.OrderBy(x => x.ContractRole)]); }

    public Task<FuturesRolloverContractAssignment> UpsertAssignmentAsync(FuturesRolloverContractAssignment assignment,
        long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        Validate(assignment);
        lock (_sync)
        {
            var current = _assignments.GetValueOrDefault(assignment.ContractRole);
            if ((current?.RowVersion ?? 0) != expectedRowVersion)
                throw new InvalidOperationException("The current-contract assignment row version changed.");
            var saved = assignment with { RowVersion = checked(expectedRowVersion + 1) };
            _assignments[assignment.ContractRole] = saved;
            ValidateSet(_assignments.Values);
            return Task.FromResult(saved);
        }
    }

    public Task DeleteAssignmentAsync(DatabentoContractRole role, long expectedRowVersion, string deletedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedBy);
        lock (_sync)
        {
            if (!_assignments.TryGetValue(role, out var current) || current.RowVersion != expectedRowVersion)
                throw new InvalidOperationException("The current-contract assignment row version changed.");
            _assignments.Remove(role);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReplaceVxAssignmentsAsync(
        FuturesRolloverContractAssignment front, FuturesRolloverContractAssignment second,
        long expectedFrontVersion, long expectedSecondVersion, CancellationToken cancellationToken = default)
    {
        if (front.ContractRole != DatabentoContractRole.VxFrontMonth || second.ContractRole != DatabentoContractRole.VxSecondMonth)
            throw new ArgumentException("The coupled replacement requires front and second VX roles.");
        Validate(front); Validate(second);
        lock (_sync)
        {
            var currentFront = _assignments.GetValueOrDefault(DatabentoContractRole.VxFrontMonth);
            var currentSecond = _assignments.GetValueOrDefault(DatabentoContractRole.VxSecondMonth);
            if ((currentFront?.RowVersion ?? 0) != expectedFrontVersion || (currentSecond?.RowVersion ?? 0) != expectedSecondVersion)
                throw new InvalidOperationException("A VX assignment row version changed.");
            var savedFront = front with { RowVersion = checked(expectedFrontVersion + 1) };
            var savedSecond = second with { RowVersion = checked(expectedSecondVersion + 1) };
            ValidateSet(_assignments.Values.Where(x => x.ContractRole is not (DatabentoContractRole.VxFrontMonth or DatabentoContractRole.VxSecondMonth))
                .Append(savedFront).Append(savedSecond));
            _assignments[front.ContractRole] = savedFront; _assignments[second.ContractRole] = savedSecond;
            return Task.FromResult<IReadOnlyList<FuturesRolloverContractAssignment>>([savedFront, savedSecond]);
        }
    }

    public Task<DatabentoWatchdogObservation> AppendObservationAsync(DatabentoWatchdogObservation observation,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_observations.Any(x => x.ObservationId == observation.ObservationId))
                return Task.FromResult(_observations.Single(x => x.ObservationId == observation.ObservationId));
            var saved = observation with { WatchdogStatusLogId = checked(++_nextObservationId), RowVersion = 1 };
            _observations.Add(saved); return Task.FromResult(saved);
        }
    }

    public Task<DatabentoWatchdogObservation?> GetObservationAsync(long id, CancellationToken cancellationToken = default)
    { lock (_sync) return Task.FromResult(_observations.SingleOrDefault(x => x.WatchdogStatusLogId == id)); }

    public Task<DatabentoWatchdogObservation> UpdateObservationAsync(DatabentoWatchdogObservation observation,
        long expectedRowVersion, string changedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);
        lock (_sync)
        {
            var index = _observations.FindIndex(x => x.WatchdogStatusLogId == observation.WatchdogStatusLogId);
            if (index < 0 || _observations[index].RowVersion != expectedRowVersion)
                throw new InvalidOperationException("The watchdog observation row version changed.");
            var saved = observation with { RowVersion = checked(expectedRowVersion + 1) };
            _observations[index] = saved;
            return Task.FromResult(saved);
        }
    }

    public Task<IReadOnlyList<DatabentoWatchdogObservation>> ListObservationsAsync(DateOnly? valueDate = null,
        DatabentoMajorStatus? status = null, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
        lock (_sync) return Task.FromResult<IReadOnlyList<DatabentoWatchdogObservation>>([.. _observations
            .Where(x => valueDate is null || x.ValueDate == valueDate)
            .Where(x => status is null || x.MajorStatus == status)
            .OrderByDescending(x => x.ObservedOnUtc).Take(pageSize)]);
    }

    public Task DeleteObservationAsync(long id, long expectedRowVersion, string deletedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deletedBy);
        lock (_sync)
        {
            var index = _observations.FindIndex(x => x.WatchdogStatusLogId == id);
            if (index < 0 || _observations[index].RowVersion != expectedRowVersion)
                throw new InvalidOperationException("The watchdog observation row version changed.");
            _observations.RemoveAt(index);
        }
        return Task.CompletedTask;
    }

    static void Validate(FuturesRolloverContractAssignment value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.ContractId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.UpdatedBy);
        var expected = value.ContractRole == DatabentoContractRole.EsQuarterly ? "ES" : "VX";
        if (!string.Equals(value.RootSymbol, expected, StringComparison.Ordinal))
            throw new ArgumentException($"Role {value.ContractRole} requires root {expected}.");
        if (value.LastTradeDate == default || value.RowVersion < 0) throw new ArgumentException("Assignment metadata is invalid.");
    }

    static void ValidateSet(IEnumerable<FuturesRolloverContractAssignment> values)
    {
        var set = values.ToArray();
        if (set.Select(x => x.ContractId).Distinct(StringComparer.Ordinal).Count() != set.Length)
            throw new InvalidOperationException("Active contract IDs must be unique.");
        var front = set.SingleOrDefault(x => x.ContractRole == DatabentoContractRole.VxFrontMonth);
        var second = set.SingleOrDefault(x => x.ContractRole == DatabentoContractRole.VxSecondMonth);
        if (front is not null && second is not null && second.LastTradeDate <= front.LastTradeDate)
            throw new InvalidOperationException("The second VX contract must mature after the front contract.");
    }
}
