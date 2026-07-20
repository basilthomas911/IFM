using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public record StartStreamingFuturesOptionTickDataRequest(
        Guid CommandId,
        DateOnly valueDate,
        DateTime MaturityDate,
        FuturesOptionContractReadModel Contract,
        double RiskFreeRate)
    {
    }
}
