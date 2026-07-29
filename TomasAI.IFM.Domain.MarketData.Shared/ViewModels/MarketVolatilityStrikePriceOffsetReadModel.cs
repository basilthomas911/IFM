using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels
{
    public record MarketVolatilityStrikePriceOffsetReadModel(
        string Symbol,
        MarketDirectionType MarketTrend,
        MarketVolatilityType MarketVolatility,
        decimal StrikePriceOffset)
    {
    }
}
