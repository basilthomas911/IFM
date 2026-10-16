using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716", Scope = "namespace", Target = "~N:TomasAI.IFM.Domain.MarketData.Shared", Justification = "Preserve the published domain namespace.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716", Scope = "namespace", Target = "~N:TomasAI.IFM.Domain.MarketData.Shared.Calendar", Justification = "Preserve the published domain namespace.")]
namespace TomasAI.IFM.Domain.MarketData.Shared.Calendar;

/// <summary>Defines the inclusive trading-date estimate used by market-data queries.</summary>
public static class TradingCalendar
{
    /// <summary>Counts weekdays excluding the supplied market holidays.</summary>
    /// <param name="startDate">The inclusive first date.</param>
    /// <param name="endDate">The inclusive last date.</param>
    /// <param name="holidays">The holidays for the requested currency.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of qualifying dates; zero for an inverted range.</returns>
    public static int Count(DateOnly startDate, DateOnly endDate,
        IEnumerable<MarketHolidayReadModel> holidays, CancellationToken cancellationToken = default)
        => Enumerate(startDate, endDate, holidays, cancellationToken).Count();

    /// <summary>Returns weekdays excluding the supplied market holidays.</summary>
    /// <param name="startDate">The inclusive first date.</param>
    /// <param name="endDate">The inclusive last date.</param>
    /// <param name="holidays">The holidays for the requested currency.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The qualifying dates in ascending order; empty for an inverted range.</returns>
    public static DateOnly[] Dates(DateOnly startDate, DateOnly endDate,
        IEnumerable<MarketHolidayReadModel> holidays, CancellationToken cancellationToken = default)
        => Enumerate(startDate, endDate, holidays, cancellationToken).ToArray();

    static IEnumerable<DateOnly> Enumerate(DateOnly startDate, DateOnly endDate,
        IEnumerable<MarketHolidayReadModel> holidays, CancellationToken cancellationToken)
    {
        var excluded = holidays.Select(holiday => holiday.HolidayDate).ToHashSet();
        for (var date = startDate; date <= endDate;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !excluded.Contains(date))
                yield return date;
            if (date == endDate) break;
            date = date.AddDays(1);
        }
    }
}
