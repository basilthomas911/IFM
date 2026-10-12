using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public sealed record DatabentoHardAttemptResult(
    bool Succeeded,
    bool SafeToRetry,
    string Stage,
    Exception? Failure,
    IReadOnlyList<DatasetWorkerProcessSnapshot> Workers)
{
    public Exception? CleanupFailure { get; init; }
}
