using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public record FuturesTickDataStreamingParameterId(
        int RequestId,
        DateOnly valueDate)
    {
    }
}
