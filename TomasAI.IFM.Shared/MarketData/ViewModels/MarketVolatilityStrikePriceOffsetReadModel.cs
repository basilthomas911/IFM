using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketData.ViewModels
{
    public record MarketVolatilityStrikePriceOffsetReadModel(
        string Symbol,
        MarketDirectionType MarketTrend,
        MarketVolatilityType MarketVolatility,
        decimal StrikePriceOffset)
    {
    }
}
