using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Extensions
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
