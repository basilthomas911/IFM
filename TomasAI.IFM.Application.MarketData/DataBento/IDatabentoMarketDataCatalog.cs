using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento;

public interface IDatabentoMarketDataCatalog
{
    bool TryGetMarketInstrumentId(string contractId, out uint marketInstrumentId)
    {
        marketInstrumentId = 0;
        return false;
    }
    FuturesContractV3ReadModel? FindFutures(string contractId);
    FuturesOptionContractReadModel? FindFuturesOption(string contractId);
    string? FindOptionUnderlying(string futuresOptionContractId);
    Task<FuturesOptionContractReadModel[]> GetOptionChainAsync(
        string futuresContractId,
        DateOnly maturityDate);
    Task<FuturesOptionContractReadModel[]> GetOptionChainBySymbolAsync(
        string underlyingSymbol,
        DateOnly maturityDate) =>
        throw new NotSupportedException("Underlying-symbol option-chain discovery is unavailable.");
    Task<OptionContractExpiryReadModel[]> DiscoverOptionContractExpiriesAsync(
        string underlyingSymbol,
        DateOnly fromExpiry,
        DateOnly throughExpiry,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Option-expiry discovery is unavailable.");
    Task<FuturesOptionContractReadModel[]> GetOptionChainByRootAsync(
        string underlyingSymbol,
        string providerRoot,
        DateOnly maturityDate) =>
        throw new NotSupportedException("Provider-root option-chain discovery is unavailable.");
    Task<FuturesOptionContractReadModel[]> GetOptionChainByRootAsync(
        string underlyingSymbol, string providerRoot, DateOnly fromMaturityDate,
        DateOnly throughMaturityDate, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Provider-root option-chain range discovery is unavailable.");
}
