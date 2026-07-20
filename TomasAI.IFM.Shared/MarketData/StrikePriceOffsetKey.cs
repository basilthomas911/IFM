using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketData
{
    public class StrikePriceOffsetKey
    {
        private MarketDirectionType _marketTrend;
        private MarketVolatilityType _marketVolatility;

        public StrikePriceOffsetKey(MarketDirectionType marketTrend, MarketVolatilityType marketVolatility)
        {
            _marketTrend = marketTrend;
            _marketVolatility = marketVolatility;
        }

        public MarketDirectionType MarketTrend => _marketTrend;
        public MarketVolatilityType MarketVolatility => _marketVolatility;
    }
}
