using TomasAI.IFM.Application.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Databento.Historical;

/// <summary>
/// Resolves CME-style futures value dates using America/New_York timezone rules, holidays, and early closes.
/// </summary>
public sealed class CmeFuturesMarketSessionCalendar :
    IMarketSessionCalendar,
    IFuturesExchangeBusinessCalendar
{
    readonly TimeZoneInfo _marketTimeZone;
    readonly HashSet<DateOnly> _holidays;
    readonly IReadOnlyDictionary<DateOnly, TimeOnly> _earlyCloses;

    /// <summary>Initializes the market session calendar.</summary>
    /// <param name="holidays">Configured non-trading value dates.</param>
    /// <param name="earlyCloses">Configured local early-close times by value date.</param>
    public CmeFuturesMarketSessionCalendar(
        IEnumerable<DateOnly>? holidays = null,
        IReadOnlyDictionary<DateOnly, TimeOnly>? earlyCloses = null)
    {
        _marketTimeZone = ResolveNewYorkTimeZone();
        _holidays = holidays?.ToHashSet() ?? [];
        _earlyCloses = earlyCloses ?? new Dictionary<DateOnly, TimeOnly>();
    }

    /// <inheritdoc/>
    public DateOnly GetValueDate(DateTimeOffset exchangeTimestampUtc)
    {
        if (exchangeTimestampUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Exchange timestamp must be UTC.", nameof(exchangeTimestampUtc));
        var local = TimeZoneInfo.ConvertTime(exchangeTimestampUtc, _marketTimeZone);
        var date = DateOnly.FromDateTime(local.DateTime);
        return TimeOnly.FromDateTime(local.DateTime) >= new TimeOnly(18, 0)
            ? date.AddDays(1)
            : date;
    }

    /// <inheritdoc/>
    public MarketSessionBounds GetSession(DateOnly valueDate)
    {
        if (!IsTradingDate(valueDate))
            throw new ArgumentException($"{valueDate:yyyy-MM-dd} is not a configured trading date.", nameof(valueDate));
        var startLocal = valueDate.AddDays(-1).ToDateTime(new TimeOnly(18, 0), DateTimeKind.Unspecified);
        var close = _earlyCloses.TryGetValue(valueDate, out var earlyClose)
            ? earlyClose
            : new TimeOnly(17, 0);
        var endLocal = valueDate.ToDateTime(close, DateTimeKind.Unspecified);
        return new(
            valueDate,
            TimeZoneInfo.ConvertTimeToUtc(startLocal, _marketTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(endLocal, _marketTimeZone));
    }

    /// <inheritdoc/>
    public bool IsTradingDate(DateOnly valueDate) =>
        valueDate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
        && !_holidays.Contains(valueDate);

    public bool IsBusinessDay(DateOnly valueDate) => IsTradingDate(valueDate);

    public DateOnly PreviousBusinessDay(DateOnly valueDate)
    {
        if (valueDate == default)
            throw new ArgumentOutOfRangeException(nameof(valueDate));
        var candidate = valueDate.AddDays(-1);
        while (!IsBusinessDay(candidate))
            candidate = candidate.AddDays(-1);
        return candidate;
    }

    public DateOnly NextBusinessDay(DateOnly valueDate)
    {
        if (valueDate == default)
            throw new ArgumentOutOfRangeException(nameof(valueDate));
        var candidate = valueDate.AddDays(1);
        while (!IsBusinessDay(candidate))
            candidate = candidate.AddDays(1);
        return candidate;
    }

    public DateOnly GetPreparationDate(DateOnly effectiveValueDate) =>
        PreviousBusinessDay(effectiveValueDate);

    static TimeZoneInfo ResolveNewYorkTimeZone()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        throw new TimeZoneNotFoundException("The New York market timezone is unavailable.");
    }
}
