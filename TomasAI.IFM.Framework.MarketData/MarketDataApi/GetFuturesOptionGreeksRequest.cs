using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Framework.MarketData.MarketDataApi
{
    public record GetFuturesOptionGreeksRequest(
        Guid commandId,
        DateOnly valueDate, 
        DateTime MaturityDate, 
        FuturesOptionContractReadModel OptionContract, 
        double OptionPrice, 
        double FuturesPrice, 
        double RiskFreeRate)
    {
    }
}
