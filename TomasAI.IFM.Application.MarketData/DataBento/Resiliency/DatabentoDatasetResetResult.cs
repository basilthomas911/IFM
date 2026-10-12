using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoDatasetResetResult(
    string Dataset,
    Guid PreviousGenerationId,
    Guid GenerationId,
    bool Succeeded,
    string Detail);
