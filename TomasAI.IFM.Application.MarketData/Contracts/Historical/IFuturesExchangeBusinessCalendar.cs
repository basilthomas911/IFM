using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>
/// Deterministic exchange-business-day operations used by futures rollover.
/// Implementations include weekends and explicitly configured exchange closures;
/// they never infer preparation dates with calendar-day subtraction.
/// </summary>
public interface IFuturesExchangeBusinessCalendar
{
    bool IsBusinessDay(DateOnly valueDate);
    DateOnly PreviousBusinessDay(DateOnly valueDate);
    DateOnly NextBusinessDay(DateOnly valueDate);
    DateOnly GetPreparationDate(DateOnly effectiveValueDate);
}
