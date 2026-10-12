using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoDatasetEvaluation(
    string Dataset,
    Guid GenerationId,
    DatabentoDatasetState State,
    DatabentoDatasetFailureReason Reason,
    DateTime? SuspectSinceUtc,
    string Detail);
