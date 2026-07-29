using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Shared.Domain.BDDTests;

public static class DefaultData
{
    public static StrikePriceVolatilityReadModel StrikePriceVolatility => new StrikePriceVolatilityReadModel(
       symbol: "ES",
       tradeType: TradeType.ShortIronCondor,
       marketTrend: MarketDirectionType.Up,
       marketVolatility: MarketVolatilityType.Normal,
       marketVolatilityTrend: PriceDirectionType.Rising,
       delta: 11,
       strikePriceOffset: 2
       );

    public static StrikePriceVolatilityReadModel StrikePriceVolatilityChanged => new StrikePriceVolatilityReadModel(
       symbol: "ES",
       tradeType: TradeType.ShortIronCondor,
       marketTrend: MarketDirectionType.Down,
       marketVolatility: MarketVolatilityType.Normal,
       marketVolatilityTrend: PriceDirectionType.Rising,
       delta: 12,
       strikePriceOffset: 2);
}
