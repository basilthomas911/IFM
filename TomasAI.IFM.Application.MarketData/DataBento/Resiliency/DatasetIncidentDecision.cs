using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public readonly record struct DatasetIncidentDecision(
    DatasetRecoveryAction Action,
    DatasetIncidentSnapshot Snapshot);
