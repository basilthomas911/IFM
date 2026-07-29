using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;

/// <summary>
/// Parameters for generating a futures RSI signal command, including the futures EOD data, time period, and period length.
/// </summary>
public record GenerateFuturesRsiSignalParameter : ICommandParameter
{
    public FuturesEodDataV2ReadModel FuturesEodData { get; init; }
    public TimeFrameType TimePeriod { get; init; }
    public int PeriodLength { get; init; } 
    public int ErrorCode { get; init; }

    public GenerateFuturesRsiSignalParameter(
        FuturesEodDataV2ReadModel futuresEodData,
        TimeFrameType timePeriod,
        int periodLength,
        int errorCode)
    {
        FuturesEodData = futuresEodData;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        ErrorCode = errorCode;
    }
}
    
