using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels
{
    [MessagePackObject(true)]
    public record FuturesTradeSignalMetricsLLMReadModel(
            string ContractId,
            DateOnly ValueDate,
            TimeFrameType TimePeriod,
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
