using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoDatasetResetRequest(
    string Dataset,
    Guid ExpectedGenerationId,
    DateOnly ValueDate,
    DatabentoDatasetFailureReason Reason,
    TimeSpan TeardownTimeout,
    TimeSpan QualificationTimeout,
    Guid CorrelationId);
