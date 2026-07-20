using System.Collections.Generic;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.MarketDataAnalytics
{
    public static class FuturesMDIMap
    {
        static readonly Dictionary<FuturesMDIId, FuturesMDIItem> _mdiMap;

        static FuturesMDIMap()
        {
            _mdiMap = new(){
                { new(TradeType.ShortIronCondor, 100), new(TradeType.ShortIronCondor, 100, 0.8) },
                { new(TradeType.ShortIronCondor, 90), new(TradeType.ShortIronCondor, 90,0.79) },
                { new(TradeType.ShortIronCondor, 80), new(TradeType.ShortIronCondor, 80, 0.78) },
                { new(TradeType.ShortIronCondor, 70), new(TradeType.ShortIronCondor, 70, 0.77) },
                { new(TradeType.ShortIronCondor, 60), new(TradeType.ShortIronCondor, 60, 0.76) },
                { new(TradeType.ShortIronCondor, 50), new(TradeType.ShortIronCondor, 50, 0.75) },
                { new(TradeType.ShortIronCondor, 40), new(TradeType.ShortIronCondor, 40, 0.74) },
                { new(TradeType.ShortIronCondor, 30), new(TradeType.ShortIronCondor, 30, 0.73) },
                { new(TradeType.ShortIronCondor, 20), new(TradeType.ShortIronCondor, 20, 0.72) },
                { new(TradeType.ShortIronCondor, 10), new(TradeType.ShortIronCondor, 10, 0.71) },
                { new(TradeType.ShortIronCondor, 0), new(TradeType.ShortIronCondor, 0, 0.70) },

                { new(TradeType.LongIronCondor, 100), new(TradeType.LongIronCondor, 100, 0.62) },
                { new(TradeType.LongIronCondor, 90), new(TradeType.LongIronCondor, 90,0.61) },
                { new(TradeType.LongIronCondor, 80), new(TradeType.LongIronCondor, 80, 0.60) },
                { new(TradeType.LongIronCondor, 70), new(TradeType.LongIronCondor, 70, 0.59) },
                { new(TradeType.LongIronCondor, 60), new(TradeType.LongIronCondor, 60, 0.58) },
                { new(TradeType.LongIronCondor, 50), new(TradeType.LongIronCondor, 50, 0.57) },
                { new(TradeType.LongIronCondor, 40), new(TradeType.LongIronCondor, 40, 0.56) },
                { new(TradeType.LongIronCondor, 30), new(TradeType.LongIronCondor, 30, 0.55) },
                { new(TradeType.LongIronCondor, 20), new(TradeType.LongIronCondor, 20, 0.54) },
                { new(TradeType.LongIronCondor, 10), new(TradeType.LongIronCondor, 10, 0.53) },
                { new(TradeType.LongIronCondor, 0), new(TradeType.LongIronCondor, 0, 0.52) },

            };
        }

        public static double MDIWatermarkLimit => 0.05;
        public static double MDIWatermarkTrailingLimit => MDIWatermarkLimit+0.01;

        public static FuturesMDIItem Get(TradeType tradeType, double mdi)
            => mdi switch
            {
                _ when mdi >= 100 =>  Get(tradeType, 100), //0.80,
                _ when mdi >= 90 => Get(tradeType, 90), //0.79,
                _ when mdi >= 80 => Get(tradeType, 80), //0.78,
                _ when mdi >= 70 => Get(tradeType, 70), //0.77,
                _ when mdi >= 60 => Get(tradeType, 60), //0.76
                _ when mdi >= 50 => Get(tradeType, 50), //0.75,
                _ when mdi >= 40 => Get(tradeType, 40), //0.74,
                _ when mdi >= 30 => Get(tradeType, 30), //0.73,
                _ when mdi >= 20 => Get(tradeType, 20), //0.72,
                _ when mdi >= 10 => Get(tradeType, 10), //0.71,
                _ => Get(tradeType, 0)
            };
        
        static FuturesMDIItem Get(TradeType tradeType, int mdi)
        {
            var key = new FuturesMDIId(tradeType, mdi);
            return _mdiMap.ContainsKey(key) 
                ? _mdiMap[key] 
                : default;  
         }
     }
 }

