using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

[MessagePackObject]
public sealed record EconomicCalendarPageRequest
{
    [Key(0)] public DateTime StartDateUtc { get; init; }
    [Key(1)] public DateTime EndDateUtc { get; init; }
    [Key(2)] public string[] CountryCodes { get; init; } = [];
    [Key(3)] public int PageSize { get; init; } = 100;
    [Key(4)] public string? ContinuationToken { get; init; }

    public void Validate()
    {
        if (StartDateUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("StartDateUtc must be UTC.", nameof(StartDateUtc));
        if (EndDateUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("EndDateUtc must be UTC.", nameof(EndDateUtc));
        if (EndDateUtc < StartDateUtc)
            throw new ArgumentOutOfRangeException(nameof(EndDateUtc), "EndDateUtc must not precede StartDateUtc.");
        if (CountryCodes is null || CountryCodes.Length == 0)
            throw new ArgumentException("At least one country code is required.", nameof(CountryCodes));
        if (CountryCodes.Length > EconomicCalendarQueryLimits.MaximumCountryCodes)
            throw new ArgumentOutOfRangeException(nameof(CountryCodes));
        if (CountryCodes.Any(static code => string.IsNullOrWhiteSpace(code) || code.Length > 8 || code.Any(char.IsControl)))
            throw new ArgumentException("Country codes must be non-empty, at most eight characters, and contain no control characters.", nameof(CountryCodes));
        if (PageSize is < 1 or > EconomicCalendarQueryLimits.MaximumPageSize)
            throw new ArgumentOutOfRangeException(nameof(PageSize));
        if (ContinuationToken?.Length > EconomicCalendarQueryLimits.MaximumContinuationTokenLength
            || ContinuationToken?.Any(char.IsControl) == true)
            throw new ArgumentOutOfRangeException(nameof(ContinuationToken));

        var startMonth = new DateTime(StartDateUtc.Year, StartDateUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endMonth = new DateTime(EndDateUtc.Year, EndDateUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var months = ((endMonth.Year - startMonth.Year) * 12) + endMonth.Month - startMonth.Month + 1;
        if (months > EconomicCalendarQueryLimits.MaximumRangeMonths)
            throw new ArgumentOutOfRangeException(nameof(EndDateUtc));
        var countries = CountryCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (months * countries > EconomicCalendarQueryLimits.MaximumPartitions)
            throw new ArgumentOutOfRangeException(nameof(CountryCodes), "The requested country/month fan-out exceeds the partition limit.");
    }
}
