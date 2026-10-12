using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve this existing persisted CLR type identity and acronym spelling.")]
[MessagePackObject(true)]
public record FuturesTradeSignalLLMReadModel(
        string ContractId,
        DateOnly ValueDate,
        TimeFrameType TimePeriod,
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
        DateTime CreatedOn,
        string CreatedBy)
{
    public FuturesTradeSignalId Id => new(ContractId, ValueDate, TimePeriod, SequenceId);
}
