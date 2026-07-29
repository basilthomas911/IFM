using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared
{
    public record FuturesMDIId(
        TradeType TradeType,
        int MDI )
    {
    }
}
