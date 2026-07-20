using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Extensions
{
    public static class TradeTypeLimitReadModelExtension
    {
        public static TradeTypeLimitReadModel? Get(this TradeTypeLimitReadModel[] tradeTypeLimits, TradeType tradeType)
           => tradeTypeLimits
               .Where(e => e.TradeType == tradeType)
               .SingleOrDefault();

        public static void Set(this TradeTypeLimitReadModel[] tradeTypeLimits, TradeType tradeType, TradeTypeLimitReadModel tradeTypeLimit)
        {
            for(var index = 0; index < tradeTypeLimits.Length; index++)
            {
                if (tradeTypeLimits[index].TradeType == tradeType)
                {
                    tradeTypeLimits[index] = tradeTypeLimit;
                    break;
                }
            }
        }
    }
}
