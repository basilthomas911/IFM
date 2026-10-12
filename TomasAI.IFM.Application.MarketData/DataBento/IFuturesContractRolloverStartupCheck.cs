using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// Refreshes and validates the provider-backed futures rollover set required
/// before a value-date market-data generation can be admitted.
/// </summary>
public interface IFuturesContractRolloverStartupCheck
{
    /// <summary>
    /// Refreshes due contracts and returns the validated durable rollover pointers.
    /// </summary>
    Task<IReadOnlyCollection<FuturesContractRolloverReadModel>> ExecuteAsync(
        DateOnly valueDate,
        CancellationToken cancellationToken = default);
}
