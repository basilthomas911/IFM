using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class TickDataSyncTypeExtensions
    {
        public static string ToStringFast(this TickDataSyncType value) => value switch
        {
            TickDataSyncType.FunctionCall => nameof(TickDataSyncType.FunctionCall),
            TickDataSyncType.EventProducer => nameof(TickDataSyncType.EventProducer),
            _ => value.ToString()
        };
    }
}
