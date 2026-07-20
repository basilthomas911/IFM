using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels
{
    public record FuturesTrendIndexReadModel (
            MarketDirectionType MarketDirection,
            MarketVolatilityType MarketVolatility,
            PriceDirectionType PriceDirection,
            PriceVolatilityType PriceVolatility)
    {
    }
}
