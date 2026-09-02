using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>Deterministically identifies the closed-window rollover preparation target.</summary>
public static class FuturesRolloverPreparationPolicy
{
    public static readonly TimeOnly WindowOpensAt = new(17, 0);
    public static readonly TimeOnly WindowClosesAt = new(18, 0);

    public static bool TryResolveTargetValueDate(
        DateTimeOffset now,
        IFuturesExchangeBusinessCalendar calendar,
        out DateOnly targetValueDate)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        var eastern = TimeZoneInfo.ConvertTime(now, FuturesTradingValueDate.MarketTimeZone);
        var localDate = DateOnly.FromDateTime(eastern.DateTime);
        var localTime = TimeOnly.FromDateTime(eastern.DateTime);
        if (localTime < WindowOpensAt
            || localTime >= WindowClosesAt
            || !calendar.IsBusinessDay(localDate))
        {
            targetValueDate = default;
            return false;
        }

        targetValueDate = calendar.NextBusinessDay(localDate);
        return calendar.GetPreparationDate(targetValueDate) == localDate;
    }

    public static bool IsDue(DateOnly requestedValueDate, DateOnly? nextRolloverDate) =>
        nextRolloverDate is null || requestedValueDate >= nextRolloverDate.Value;
}
