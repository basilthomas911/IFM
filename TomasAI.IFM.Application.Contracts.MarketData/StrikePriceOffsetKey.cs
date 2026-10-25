using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Shared
{
    public class StrikePriceOffsetKey
    {
        private readonly MarketDirectionType _marketTrend;
        private readonly MarketVolatilityType _marketVolatility;

        public StrikePriceOffsetKey(MarketDirectionType marketTrend, MarketVolatilityType marketVolatility)
        {
            _marketTrend = marketTrend;
            _marketVolatility = marketVolatility;
        }

        public MarketDirectionType MarketTrend => _marketTrend;
        public MarketVolatilityType MarketVolatility => _marketVolatility;
    }
}
