using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatasetIncidentSnapshot
{
    public required string Dataset { get; init; }
    public required DateOnly ValueDate { get; init; }
    public Guid IncidentId { get; init; }
    public Guid GenerationId { get; init; }
    public bool IsOpen { get; init; }
    public bool ProcessReplacementLatched { get; init; }
    public int CooperativeAttempts { get; init; }
    public int ProcessReplacements { get; init; }
    public TimeSpan UnhealthyDuration { get; init; }
    public TimeSpan HealthyDuration { get; init; }
    public FuturesMarketState? PolicySession { get; init; }
    public TimeSpan PolicyUnhealthyDuration { get; init; }
    public TimeSpan ReplacementBackoffRemaining { get; init; }
    public IReadOnlyList<TimeSpan> ReplacementFailureAges { get; init; } = [];
    public DatabentoDatasetFailureReason FailureReason { get; init; }
    public DatasetRecoveryAction LastAction { get; init; }
    public DateTime ObservedOnUtc { get; init; }
}
