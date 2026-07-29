using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels
{
    public record FuturesTrendIndexReadModel (
            MarketDirectionType MarketDirection,
            MarketVolatilityType MarketVolatility,
            PriceDirectionType PriceDirection,
            PriceVolatilityType PriceVolatility)
    {
    }
}
