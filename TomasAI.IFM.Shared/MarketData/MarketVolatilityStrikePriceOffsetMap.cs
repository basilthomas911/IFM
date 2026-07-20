using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData
{
    public class MarketVolatilityStrikePriceOffsetMap
    {
        private Dictionary<StrikePriceOffsetKey, MarketVolatilityStrikePriceOffsetReadModel> _strikePriceOffsetMap;

        public MarketVolatilityStrikePriceOffsetMap(ICollection<MarketVolatilityStrikePriceOffsetReadModel> strikePriceOffsets)
        {
            _strikePriceOffsetMap = new Dictionary<StrikePriceOffsetKey, MarketVolatilityStrikePriceOffsetReadModel>();
            foreach (var e in strikePriceOffsets)
                _strikePriceOffsetMap.Add(new StrikePriceOffsetKey(e.MarketTrend, e.MarketVolatility), e);
        }

        public bool Exists(StrikePriceOffsetKey key) => _strikePriceOffsetMap.ContainsKey(key);
        
        public decimal this[MarketDirectionType marketTrend, MarketVolatilityType marketVolatility]
        {
            get
            {
                var key = new StrikePriceOffsetKey(marketTrend, marketVolatility);
                if (!Exists(key)) return 0.0m;
                return _strikePriceOffsetMap[key].StrikePriceOffset;
            }
        }
    }
}
