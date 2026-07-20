using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public record StopStreamingFuturesOptionTickDataRequest(
        Guid CommandId)
    {
    }

    public record StopStreamingFuturesOptionTickDataResponse(
        Guid commandId,
        bool Response)
    {
    }
}
