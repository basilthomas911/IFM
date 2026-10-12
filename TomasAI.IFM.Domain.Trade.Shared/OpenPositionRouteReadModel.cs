using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Associates a market contract with its persisted open-position route.</summary>
public sealed record OpenPositionRouteReadModel(
    string ContractId,
    PortfolioFundTradeLeg Route);
