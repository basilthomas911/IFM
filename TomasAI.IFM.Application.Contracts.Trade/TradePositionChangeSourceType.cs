using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{
    public enum TradePositionChangeSourceType
    {
        PutCreditSpreadLeg,
        CallCreditSpreadLeg,
        SpreadDistributionStatistics,
        None
    }
}
