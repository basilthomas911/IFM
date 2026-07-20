using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.MarketDataAnalytics
{
    public record FuturesMDIId(
        TradeType TradeType,
        int MDI )
    {
    }
}
