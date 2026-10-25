using TomasAI.IFM.Shared.MarketData.Diagnostics;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

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
    public ulong BatchesPublished { get; init; }
    public ulong ChannelFullCount { get; init; }
    public ulong PoolMissCount { get; init; }
    public int ChannelBatchCount { get; init; }
    public int ChannelBatchCapacity { get; init; }
    public required string FailureDetail { get; init; }
    public DatabentoDatasetState DatasetState { get; init; } = DatabentoDatasetState.Up;
    public DatabentoDatasetFailureReason FailureReason { get; init; }
    public DateTime? SuspectSinceUtc { get; init; }
    public IReadOnlyList<DatabentoContractRole> ContractRoles { get; init; } = [];
    public IReadOnlyList<string> ContractIds { get; init; } = [];
    public FeedDrainDiagnostics? DrainDiagnostics { get; init; }
    public TickAggregationMetricsSnapshot? AggregationMetrics { get; init; }
}
