using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{
    public record FuturesMDIId(
        TradeType TradeType,
        int MDI )
    {
    }
}
