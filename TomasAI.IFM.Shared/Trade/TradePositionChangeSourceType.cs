using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum TradePositionChangeSourceType
    {
        PutCreditSpreadLeg,
        CallCreditSpreadLeg,
        SpreadDistributionStatistics,
        None
    }

    public static class TradePositionChangeSourceTypeExtensions
    {
        public static string ToStringFast(this TradePositionChangeSourceType value) => value switch
        {
            TradePositionChangeSourceType.PutCreditSpreadLeg => nameof(TradePositionChangeSourceType.PutCreditSpreadLeg),
            TradePositionChangeSourceType.CallCreditSpreadLeg => nameof(TradePositionChangeSourceType.CallCreditSpreadLeg),
            TradePositionChangeSourceType.SpreadDistributionStatistics => nameof(TradePositionChangeSourceType.SpreadDistributionStatistics),
            TradePositionChangeSourceType.None => nameof(TradePositionChangeSourceType.None),
            _ => value.ToString()
        };
    }
}
