using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to generate a daily futures RSI signal.
/// </summary>
/// <param name="FuturesEodData">The end-of-day futures data used as input for daily RSI signal generation.</param>
/// <param name="TimePeriod">The time period for which to generate the RSI signal.</param>
/// <param name="PeriodLength">The length of the period for RSI calculation.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record GenerateFuturesRsiDailySignalParameter(FuturesEodDataV2ReadModel FuturesEodData, TradeTimePeriodType TimePeriod, int PeriodLength, int ErrorCode)
    : ICommandParameter;
