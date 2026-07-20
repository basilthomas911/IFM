using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public record StopStreamingFuturesTickDataRequest(
        Guid commandId)
    {
    }

    public record StopStreamingFuturesTickDataResponse(
        Guid commandId,
        bool Response)
    {
    }
}
