using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

public sealed record MarketDataOperationsHealthSnapshot
{
    public required long Revision { get; init; }
    public required DateTime ObservedOnUtc { get; init; }
    public required MarketDataOperationsStatus OverallStatus { get; init; }
    public required IReadOnlyDictionary<MarketDataOperationStage, MarketDataStageHealth> Stages { get; init; }
    public required IReadOnlyDictionary<string, DatasetIncidentSnapshot> DatasetIncidents { get; init; }
    public long RejectedStaleGenerationPublications { get; init; }
}
