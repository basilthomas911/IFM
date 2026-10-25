using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public readonly record struct FuturesMarketHealthSnapshot(bool Running, bool Healthy, string Generation,
    DateOnly? ValueDate, DateTimeOffset ObservedAtUtc, long Sequence);
