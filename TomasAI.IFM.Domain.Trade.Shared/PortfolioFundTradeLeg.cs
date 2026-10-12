using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Compact destination stored in a realtime contract-to-position route bucket.</summary>
[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct PortfolioFundTradeLeg(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId,
    [property: Key(3)] int TradeId,
    [property: Key(4)] Guid StrategyPositionId,
    [property: Key(5)] Guid TradeLegId,
    [property: Key(6)] TradeStrategyKind TradeType,
    [property: Key(7)] long Generation);
