using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels
{
    [MessagePackObject(true)]
    public record FuturesTradeSignalMetricsLLMReadModel(
            string ContractId,
            DateOnly ValueDate,
            TradeTimePeriodType TimePeriod,
            long SequenceId,
            DateTime Timestamp,
             MarketDirectionType MarketDirection,
            MarketVolatilityType MarketVolatility,
            PriceDirectionType PriceDirection,
            PriceVolatilityType PriceVolatility,
            double MarketDirectionIndicator,
            DateTime CreatedOn ,
            string CreatedBy)
    {
        public FuturesTradeSignalId Id => new(ContractId, ValueDate, TimePeriod, SequenceId);
    }
}
