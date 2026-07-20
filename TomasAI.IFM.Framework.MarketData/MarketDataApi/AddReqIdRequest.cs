using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public record AddReqIdRequest(
        string ContractId)
    {
    }

    public record AddReqIdResponse(
        string ContractId,
        int Response)
    {
    }

}
