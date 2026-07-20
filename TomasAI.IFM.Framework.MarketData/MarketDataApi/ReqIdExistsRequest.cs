using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public record ReqIdExistsRequest(
        int RequestId)
    {
    }

    public record ReqIdExistsResponse(
        int RequestId,
        bool Response)
    {
    }

}
