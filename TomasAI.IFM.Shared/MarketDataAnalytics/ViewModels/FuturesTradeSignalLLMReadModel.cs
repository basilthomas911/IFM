using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

[MessagePackObject(true)]
public record FuturesTradeSignalLLMReadModel  (
        string ContractId,
        DateOnly ValueDate,
        TradeTimePeriodType TimePeriod,
        long SequenceId,
        DateTime Timestamp,
        double OpenPrice,
        double HighPrice,
        double LowPrice,
        double ClosePrice,
        int Volume,
        double DailyPercentChange,
        double DailyStdDev,
        double UpperBand,
        double Mean,
        double LowerBand,
        double PriceVolatility,
        DateTime CreatedOn ,
        string CreatedBy)
{
    public FuturesTradeSignalId Id => new(ContractId, ValueDate, TimePeriod, SequenceId);
}
