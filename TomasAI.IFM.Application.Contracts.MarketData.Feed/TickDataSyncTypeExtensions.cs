using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class TickDataSyncTypeExtensions
    {

        extension(TickDataSyncType value)
        {
            public string ToStringFast() => value switch
            {
                TickDataSyncType.FunctionCall => nameof(TickDataSyncType.FunctionCall),
                TickDataSyncType.EventProducer => nameof(TickDataSyncType.EventProducer),
                _ => value.ToString()
            };
        }
    }
}
