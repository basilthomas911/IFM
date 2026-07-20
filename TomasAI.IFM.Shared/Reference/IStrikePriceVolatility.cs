using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Reference
{
    public interface IStrikePriceVolatility
    {
        StrikePriceVolatilityId Id { get; }
        string Symbol { get; }
        TradeType TradeType { get; }
        MarketDirectionType MarketTrend { get; }
        MarketVolatilityType MarketVolatility { get; }
        PriceDirectionType MarketVolatilityTrend { get; }
        int Delta { get; }
        int StrikePriceOffset { get; }
    }
}
