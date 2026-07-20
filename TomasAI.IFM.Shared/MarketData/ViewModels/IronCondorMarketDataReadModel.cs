using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketData.ViewModels
{
    public record IronCondorMarketDataReadModel(
        FuturesContractV2ReadModel UnderlyingContract,
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
