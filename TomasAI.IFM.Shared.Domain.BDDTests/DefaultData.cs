using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Reference.ViewModels;

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
