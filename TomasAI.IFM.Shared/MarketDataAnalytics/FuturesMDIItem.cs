using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.MarketDataAnalytics
{
   public record FuturesMDIItem(
       TradeType TradeType,
       int MDI,
       double ForwardLossRateLimit )
    {
        public FuturesMDIId Id => new (TradeType, MDI);
    }
}
