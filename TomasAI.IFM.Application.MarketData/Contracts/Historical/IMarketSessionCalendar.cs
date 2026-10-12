using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Resolves futures value dates and exact session boundaries without fixed UTC offsets.</summary>
public interface IMarketSessionCalendar
{
    /// <summary>Gets the value date containing a UTC exchange timestamp.</summary>
    DateOnly GetValueDate(DateTimeOffset exchangeTimestampUtc);

    /// <summary>Gets the exact UTC session boundaries for a futures value date.</summary>
    MarketSessionBounds GetSession(DateOnly valueDate);

    /// <summary>Gets whether the supplied value date is a trading session.</summary>
    bool IsTradingDate(DateOnly valueDate);
}
