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

        extension(TradeTypeLimitReadModel[] tradeTypeLimits)
        {
            public TradeTypeLimitReadModel? Get(TradeType tradeType)
               => tradeTypeLimits
                   .SingleOrDefault(e => e.TradeType == tradeType);

            public void Set(TradeType tradeType, TradeTypeLimitReadModel tradeTypeLimit)
            {
                ArgumentNullException.ThrowIfNull(tradeTypeLimits);
                for (var index = 0; index < tradeTypeLimits.Length; index++)
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
}
