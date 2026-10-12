using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

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
