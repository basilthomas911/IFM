using TomasAI.IFM.Shared.Trade;
using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{
   public record FuturesMDIItem(
       TradeType TradeType,
       int MDI,
       double ForwardLossRateLimit )
    {
        public FuturesMDIId Id => new (TradeType, MDI);
    }
}
