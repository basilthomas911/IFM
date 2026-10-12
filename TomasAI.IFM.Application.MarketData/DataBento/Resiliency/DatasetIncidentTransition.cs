using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatasetIncidentTransition(
    Guid TransitionId,
    Guid CorrelationId,
    DatasetIncidentSnapshot Snapshot,
    long RowVersion = 0);
