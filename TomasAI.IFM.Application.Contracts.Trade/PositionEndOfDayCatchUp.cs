using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies the previous session that can still be sealed in the current position stream.</summary>
public static class PositionEndOfDayCatchUp
{
    public static DateOnly? RequiredValueDate(StrategyPositionSnapshot? position, DateOnly nextValueDate)
    {
        if (position is not { IsOpen: true } || nextValueDate == default)
            return null;

        var currentDate = position.ValueDate == default && position.AsOfUtc != default
            ? FuturesTradingValueDate.GetOperational(new DateTimeOffset(position.AsOfUtc))
            : position.ValueDate;
        if (currentDate == default || currentDate >= nextValueDate)
            return null;

        var previousDate = nextValueDate.DayOfWeek == DayOfWeek.Monday
            ? nextValueDate.AddDays(-3)
            : nextValueDate.AddDays(-1);
        if (currentDate > previousDate || position.LatestSealedValueDate >= previousDate
            || (position.Phase == StrategyPositionPhase.EndOfDay && currentDate == previousDate))
            return null;

        return previousDate;
    }
}
