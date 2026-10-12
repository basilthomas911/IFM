using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

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
