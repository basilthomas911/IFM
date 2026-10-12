using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

public sealed record MarketDataStageHealth
{
    public required MarketDataOperationStage Stage { get; init; }
    public required MarketDataOperationsStatus Status { get; init; }
    public long Received { get; init; }
    public long Completed { get; init; }
    public long Failed { get; init; }
    public long Coalesced { get; init; }
    public DateTime? LastObservedUtc { get; init; }
    public DateTime? LastSucceededUtc { get; init; }
    public DateTime? LastFailedUtc { get; init; }
    public DateTime? MarketDataAsOfUtc { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public TimeSpan MaximumLatency { get; init; }
    public Guid LastDiagnosticId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
