using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;

/// <summary>
/// Defines the calendar identity and deterministic child-command identity used by
/// the Daily-to-Weekly/Monthly ITI completion hierarchy.
/// </summary>
internal static class FuturesItiSignalTimeFrame
{
    internal static readonly TimeFrameType[] DerivedPeriods =
    [
        TimeFrameType.Weekly,
        TimeFrameType.Monthly
    ];

    /// <summary>Returns the first calendar date represented by an ITI timeframe stream.</summary>
    internal static DateOnly GetCalendarBucketStart(DateOnly valueDate, TimeFrameType period) =>
        period switch
        {
            TimeFrameType.Daily => valueDate,
            TimeFrameType.Weekly => valueDate.AddDays(-(((int)valueDate.DayOfWeek + 6) % 7)),
            TimeFrameType.Monthly => new DateOnly(valueDate.Year, valueDate.Month, 1),
            _ => throw new ArgumentOutOfRangeException(
                nameof(period),
                period,
                "Only Daily, Weekly, and Monthly ITI timeframes are supported.")
        };

    /// <summary>
    /// Creates the stable identity of a child timeframe command from the persisted
    /// Daily completion. Redelivery therefore resolves to the same command audit entry.
    /// </summary>
    internal static Guid CreateDerivedCommandId(
        FuturesItiSignalGeneratedCompleteEvent source,
        TimeFrameType targetPeriod)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (targetPeriod is not (TimeFrameType.Weekly or TimeFrameType.Monthly))
            throw new ArgumentOutOfRangeException(nameof(targetPeriod), targetPeriod, "A derived ITI period is required.");

        var identity =
            $"{source.Id:N}|{source.CommandId:N}|{source.EntityId.Format()}|{targetPeriod}|GenerateFuturesItiSignal";
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
    }
}
