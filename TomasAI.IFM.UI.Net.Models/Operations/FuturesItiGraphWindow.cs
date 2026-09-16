using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.UI.Net.Models.Operations;

/// <summary>Defines the exact UTC interval displayed by a Strategy ITI graph.</summary>
public readonly record struct FuturesItiGraphWindow(DateTime StartUtc, DateTime EndUtc)
{
    /// <summary>Resolves the graph interval ending at the supplied current instant.</summary>
    public static FuturesItiGraphWindow Resolve(
        DateTimeOffset currentUtc,
        TimeFrameType timeFrame)
    {
        if (currentUtc == default)
            throw new ArgumentOutOfRangeException(nameof(currentUtc));

        var endUtc = currentUtc.UtcDateTime;
        if (timeFrame == TimeFrameType.Daily)
            return new(endUtc.AddHours(-8), endUtc);

        var easternNow = EasternTime.FromUtc(currentUtc).DateTime;
        var easternStart = timeFrame switch
        {
            TimeFrameType.Weekly => easternNow.Date.AddDays(
                -(((int)easternNow.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7)),
            TimeFrameType.Monthly => new DateTime(
                easternNow.Year,
                easternNow.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Unspecified),
            _ => throw new ArgumentOutOfRangeException(
                nameof(timeFrame),
                timeFrame,
                "Strategy ITI graphs support only Daily, Weekly, and Monthly timeframes.")
        };

        return new(EasternTime.ToUtc(easternStart), endUtc);
    }

    /// <summary>Returns whether a backend UTC timestamp falls inside the inclusive graph interval.</summary>
    public bool Contains(DateTime occurredOnUtc)
    {
        var normalized = occurredOnUtc.Kind == DateTimeKind.Utc
            ? occurredOnUtc
            : DateTime.SpecifyKind(occurredOnUtc, DateTimeKind.Utc);
        return normalized >= StartUtc && normalized <= EndUtc;
    }
}
