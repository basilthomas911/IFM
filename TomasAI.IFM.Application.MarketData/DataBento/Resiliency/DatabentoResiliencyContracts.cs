using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public enum DatabentoContractRole { EsQuarterly = 1, VxFrontMonth = 2, VxSecondMonth = 3 }
public enum DatabentoFeedCriticality { Core = 1, Optional = 2 }
public enum DatabentoMajorStatus { Up = 1, Resetting = 2, Down = 3 }
public enum DatabentoDisplayHealth { Green = 1, Yellow = 2, Orange = 3, Red = 4, Inactive = 5 }
public enum DatabentoLifecycleState { ScheduledStopped = 1, Starting = 2, Healthy = 3, Degraded = 4, Failed = 5, Resetting = 6 }
public enum DatabentoOperationReason
{
    InitialStartup = 1, ScheduledSessionStart = 2, WatchdogPoll = 3,
    AutomaticRecovery = 4, ManualReset = 5, ValueDateRollover = 6,
    RequestedStop = 7, ApplicationShutdown = 8
}

public sealed record FuturesRolloverContractAssignment
{
    public required DatabentoContractRole ContractRole { get; init; }
    public required string RootSymbol { get; init; }
    public required string ContractId { get; init; }
    public required string Description { get; init; }
    public required string LocalSymbol { get; init; }
    public required string SecurityType { get; init; }
    public required string Currency { get; init; }
    public required string Exchange { get; init; }
    public required string Multiplier { get; init; }
    public required DateOnly LastTradeDate { get; init; }
    public required DateOnly NextRolloverDate { get; init; }
    public required string SourceContractHash { get; init; }
    public long RowVersion { get; init; } = 1;
    public required DateTime CreatedOnUtc { get; init; }
    public required string CreatedBy { get; init; }
    public required DateTime UpdatedOnUtc { get; init; }
    public required string UpdatedBy { get; init; }
}

public sealed record DatabentoFeedWatchdogStatus
{
    public required ulong FeedInstanceId { get; init; }
    public required Guid GenerationId { get; init; }
    public required string Dataset { get; init; }
    public required string FeedKind { get; init; }
    public required DatabentoFeedCriticality Criticality { get; init; }
    public required DatabentoMajorStatus MajorStatus { get; init; }
    public required string NativeState { get; init; }
    public required int TerminalStatus { get; init; }
    public required bool ProducerAlive { get; init; }
    public required bool AggregationWorkerRunning { get; init; }
    public required bool TransportRunning { get; init; }
    public required int ExpectedSubscriptions { get; init; }
    public required int ReceivedSubscriptions { get; init; }
    public required ulong HeartbeatCount { get; init; }
    public required ulong ProviderMessageCount { get; init; }
    public required TimeSpan LastHeartbeatAge { get; init; }
    public required TimeSpan LastProviderMessageAge { get; init; }
    public required ulong RecordsProduced { get; init; }
    public required ulong RecordsConsumed { get; init; }
    public required ulong RingCapacity { get; init; }
    public required ulong RingUsed { get; init; }
    public required ulong RingHighWater { get; init; }
    public required ulong RingOverruns { get; init; }
    public required string FailureDetail { get; init; }
    public IReadOnlyList<DatabentoContractRole> ContractRoles { get; init; } = [];
    public IReadOnlyList<string> ContractIds { get; init; } = [];
}

public sealed record DatabentoBulkWatchdogSnapshot
{
    public required bool Complete { get; init; }
    public required string NativeBackend { get; init; }
    public required int NativeAbiVersion { get; init; }
    public required Guid NativeGeneration { get; init; }
    public required DateTime ObservedOnUtc { get; init; }
    public required IReadOnlyList<DatabentoFeedWatchdogStatus> Feeds { get; init; }
    public string FailureDetail { get; init; } = string.Empty;
}

public sealed record DatabentoWatchdogObservation
{
    public long WatchdogStatusLogId { get; init; }
    public required Guid ObservationId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required DateOnly ValueDate { get; init; }
    public required DateTime ObservedOnUtc { get; init; }
    public required DatabentoOperationReason OperationReason { get; init; }
    public required DatabentoMajorStatus MajorStatus { get; init; }
    public required DatabentoDisplayHealth DisplayHealth { get; init; }
    public required bool CoreContractsReady { get; init; }
    public required int RecoveryAttempt { get; init; }
    public required string NativeBackend { get; init; }
    public required int NativeAbiVersion { get; init; }
    public required Guid NativeGeneration { get; init; }
    public string FailureStage { get; init; } = string.Empty;
    public string FailureDetail { get; init; } = string.Empty;
    public required IReadOnlyList<DatabentoFeedWatchdogStatus> FeedStatusDetails { get; init; }
    public long RowVersion { get; init; } = 1;
}

public sealed record DatabentoLifecycleSnapshot
{
    public required DatabentoLifecycleState State { get; init; }
    public required long StateRevision { get; init; }
    public required DateOnly? ValueDate { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid NativeGeneration { get; init; }
    public required int RecoveryAttempt { get; init; }
    public required string Reason { get; init; }
    public required DateTime ChangedOnUtc { get; init; }
    public DateTime? AttemptStartedOnUtc { get; init; }
    public DateTime? AttemptCompletedOnUtc { get; init; }
    public DateTime? NextRetryOnUtc { get; init; }
    public DatabentoWatchdogObservation? LastObservation { get; init; }
    public bool CoreReady => State is DatabentoLifecycleState.Healthy or DatabentoLifecycleState.Degraded;
}

public sealed record DatabentoWatchdogOptions
{
    public bool Enabled { get; init; } = true;
    public string NativeBackend { get; init; } = "Cpp";
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan AttemptTwoDelay { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan AttemptThreeDelay { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan PersistenceRetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan YellowFreshnessAge { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan RedFreshnessAge { get; init; } = TimeSpan.FromMinutes(15);

    public DatabentoWatchdogOptions Validate()
    {
        if (NativeBackend is not ("Cpp" or "Rust"))
            throw new InvalidOperationException("Databento native backend must be explicitly configured as Cpp or Rust.");
        if (PollInterval <= TimeSpan.Zero || ProbeTimeout <= TimeSpan.Zero
            || AttemptTwoDelay < TimeSpan.Zero || AttemptThreeDelay < TimeSpan.Zero
            || PersistenceRetryDelay < TimeSpan.Zero
            || YellowFreshnessAge <= TimeSpan.Zero || RedFreshnessAge <= YellowFreshnessAge)
            throw new InvalidOperationException("Databento watchdog intervals and freshness boundaries are invalid.");
        return this;
    }
}

public interface IMarketDataServiceStore
{
    Task<FuturesRolloverContractAssignment?> GetAssignmentAsync(DatabentoContractRole role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ListAssignmentsAsync(CancellationToken cancellationToken = default);
    Task<FuturesRolloverContractAssignment> UpsertAssignmentAsync(FuturesRolloverContractAssignment assignment, long expectedRowVersion, CancellationToken cancellationToken = default);
    Task DeleteAssignmentAsync(DatabentoContractRole role, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReplaceVxAssignmentsAsync(FuturesRolloverContractAssignment front, FuturesRolloverContractAssignment second, long expectedFrontVersion, long expectedSecondVersion, CancellationToken cancellationToken = default);
    Task<DatabentoWatchdogObservation> AppendObservationAsync(DatabentoWatchdogObservation observation, CancellationToken cancellationToken = default);
    Task<DatabentoWatchdogObservation> UpdateObservationAsync(DatabentoWatchdogObservation observation, long expectedRowVersion, string changedBy, CancellationToken cancellationToken = default);
    Task<DatabentoWatchdogObservation?> GetObservationAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DatabentoWatchdogObservation>> ListObservationsAsync(DateOnly? valueDate = null, DatabentoMajorStatus? status = null, int pageSize = 100, CancellationToken cancellationToken = default);
    Task DeleteObservationAsync(long id, long expectedRowVersion, string deletedBy, CancellationToken cancellationToken = default);
}

public interface IDatabentoLifecycleRuntime
{
    DateOnly? ActiveValueDate { get; }
    Task PrepareContractsAsync(DateOnly valueDate, CancellationToken cancellationToken);
    Task StartAsync(DateOnly valueDate, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    ValueTask<DatabentoBulkWatchdogSnapshot> GetWatchdogSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

public interface IMarketDataLifecycleRequests
{
    DatabentoLifecycleSnapshot Current { get; }
    Task StartAsync(DateOnly valueDate, Func<Guid, int, string, Task>? errorMessageHandler = null, CancellationToken cancellationToken = default);
    Task StopAsync(DateOnly valueDate, CancellationToken cancellationToken = default);
    Task ResetAsync(DateOnly valueDate, Guid correlationId, CancellationToken cancellationToken = default);
    Task ProbeAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(Guid correlationId, CancellationToken cancellationToken = default);
}

public interface IDatabentoWatchdogPublisher
{
    ValueTask PublishAsync(DatabentoWatchdogObservation observation, CancellationToken cancellationToken);
}

public sealed class NullDatabentoWatchdogPublisher : IDatabentoWatchdogPublisher
{
    public ValueTask PublishAsync(DatabentoWatchdogObservation observation, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
