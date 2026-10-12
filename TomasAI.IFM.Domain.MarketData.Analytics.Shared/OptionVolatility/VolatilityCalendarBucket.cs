using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record VolatilityCalendarBucket(int Year, int Month)
{
    public static VolatilityCalendarBucket From(DateOnly date) => new(date.Year, date.Month);
    public int Value => checked(Year * 100 + Month);
}
