using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Identifies the first two eligible contracts in one futures term structure.</summary>
public readonly record struct FuturesTermStructureContracts(
    FuturesContractV3ReadModel Front,
    FuturesContractV3ReadModel Back)
{
    /// <summary>Gets whether both ordered contracts form a valid term-structure pair.</summary>
    public bool IsValid => Front.IsValid && Back.IsValid
        && string.Equals(Front.Symbol, Back.Symbol, StringComparison.Ordinal)
        && Front.LastTradeDate < Back.LastTradeDate;
}
