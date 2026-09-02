using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels
{
    public record IronCondorMarketDataReadModel(
        FuturesContractV3ReadModel UnderlyingContract,
        FuturesOptionContractReadModel ShortPutOptionContract,
        FuturesOptionContractReadModel LongPutOptionContract,
        FuturesOptionContractReadModel ShortCallOptionContract,
        FuturesOptionContractReadModel LongCallOptionContract,
        double RiskFreeRate,
        int TradingDays 
       )
    {
    }
}
