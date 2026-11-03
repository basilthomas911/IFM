using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class TradePositionChangeSourceTypeExtensions
    {

        extension(TradePositionChangeSourceType value)
        {
            public string ToStringFast() => value switch
            {
                TradePositionChangeSourceType.PutCreditSpreadLeg => nameof(TradePositionChangeSourceType.PutCreditSpreadLeg),
                TradePositionChangeSourceType.CallCreditSpreadLeg => nameof(TradePositionChangeSourceType.CallCreditSpreadLeg),
                TradePositionChangeSourceType.SpreadDistributionStatistics => nameof(TradePositionChangeSourceType.SpreadDistributionStatistics),
                TradePositionChangeSourceType.None => nameof(TradePositionChangeSourceType.None),
                _ => value.ToString()
            };
        }
    }
}
