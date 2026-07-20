using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public record GetReqIdRequest(
        string ContractId)
    {
    }

    public record GetReqIdResponse(
        string ContractId,
        int Response)
    {
    }

}
