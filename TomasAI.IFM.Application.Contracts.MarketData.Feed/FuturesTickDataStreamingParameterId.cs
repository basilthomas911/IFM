using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{
    public record FuturesTickDataStreamingParameterId(
        int RequestId,
        DateOnly valueDate)
    {
    }
}
