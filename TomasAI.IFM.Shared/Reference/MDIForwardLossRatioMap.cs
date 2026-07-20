using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Reference
{
    public class MDIForwardLossRatioMap
    {
        public const double MDIWatermarkLimit = 0.05;
        public const double MDIWatermarkTrailingLimit = MDIWatermarkLimit + 0.01;

        Dictionary<MDIForwardLossRatioId, MDIForwardLossRatioReadModel> _mdiMap;

        public void Load(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios)
        {
            _mdiMap = new();
            foreach (var e in mdiForwardLossRatios)
                _mdiMap.Add(e.Id, e);
        }
     
        public  MDIForwardLossRatioReadModel Get(double mdi, IntrinsicTimeTrendType trendDirection, TradeType tradeType)
            => mdi switch  {
                _ when mdi >= 100 => Get(100, trendDirection, tradeType), //0.80,
                _ when mdi >= 90 => Get(90, trendDirection, tradeType), //0.79,
                _ when mdi >= 80 => Get(80, trendDirection, tradeType), //0.78,
                _ when mdi >= 70 => Get(70, trendDirection, tradeType), //0.77,
                _ when mdi >= 60 => Get(60, trendDirection, tradeType), //0.76
                _ when mdi >= 50 => Get(50, trendDirection, tradeType), //0.75,
                _ when mdi >= 40 => Get(40, trendDirection, tradeType), //0.74,
                _ when mdi >= 30 => Get(30, trendDirection, tradeType), //0.73,
                _ when mdi >= 20 => Get(20, trendDirection, tradeType), //0.72,
                _ when mdi >= 10 => Get(10, trendDirection, tradeType), //0.71,
                _ => Get(0, trendDirection, tradeType)
            };

        MDIForwardLossRatioReadModel Get(int mdi,  IntrinsicTimeTrendType trendDirection,  TradeType tradeType)
        {
            var key = new MDIForwardLossRatioId(mdi, trendDirection, tradeType);
            return _mdiMap.ContainsKey(key)
                ? _mdiMap[key]
                : default;
        }
    }

   
}
