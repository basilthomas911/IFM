using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public record GetFuturesOptionContractRequest(
        Guid CommandId,
        FuturesOptionContractReadModel QueryForContract)
    {
    }
}
