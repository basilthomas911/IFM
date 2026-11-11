using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Query;

/// <summary>Qualifies trade-plan request bounds before opening a database connection.</summary>
internal static class TradePlanRequestQualification
{
    internal static void RequireScope(StrategyPositionId positionId, TradeStrategyKind strategy, DateOnly valueDate)
    {
        if (!positionId.IsValid || valueDate == default || strategy is not
            (TradeStrategyKind.IronCondor or TradeStrategyKind.VerticalSpread or TradeStrategyKind.FuturesOutright))
            throw new ArgumentException("Valid Trade Plan identity, strategy, and value date are required.", nameof(positionId));
    }

    internal static void RequirePositionDate(StrategyPositionId positionId, DateOnly valueDate)
    {
        if (!positionId.IsValid || valueDate == default)
            throw new ArgumentException("Valid strategy-position identity and value date are required.", nameof(valueDate));
    }

    internal static void RequireDate(DateOnly valueDate)
    {
        if (valueDate == default) throw new ArgumentException("A value date is required.", nameof(valueDate));
    }

    internal static void RequirePageSize(int pageSize)
    {
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    internal static void RequireDailySpreads(StrategyPositionId positionId, IReadOnlyDictionary<DateOnly, DateTime> dayEndsUtc)
    {
        foreach (var day in dayEndsUtc)
        {
            RequireScope(positionId, TradeStrategyKind.IronCondor, day.Key);
            if (day.Value.Kind != DateTimeKind.Utc)
                throw new ArgumentException("EOD chart boundaries must be UTC.", nameof(dayEndsUtc));
        }
    }

    internal static void RequireChart(StrategyPositionId positionId, DateOnly firstValueDate, DateOnly lastValueDate,
        DateTime startUtc, DateTime endUtc)
    {
        RequireScope(positionId, TradeStrategyKind.IronCondor, firstValueDate);
        if (lastValueDate < firstValueDate || startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc
            || endUtc < startUtc || endUtc - startUtc > TimeSpan.FromHours(25))
            throw new ArgumentException("Chart history requires ordered trading dates and a UTC range of at most one trading session.");
    }
}
