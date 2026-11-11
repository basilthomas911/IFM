namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

/// <summary>A caller-qualified single-country calendar range with bounded monthly fan-out.</summary>
public sealed record EconomicCalendarDateRange
{
    public EconomicCalendarDateRange(DateTime startDate, DateTime endDate, string countryCode)
    {
        StartDateUtc = MarketDataImportBatch.NormalizeCalendarTimestamp(startDate);
        EndDateUtc = MarketDataImportBatch.NormalizeCalendarTimestamp(endDate);
        CountryCode = countryCode;
        if (!IsEmpty)
            ToPageRequest().Validate();
    }

    public DateTime StartDateUtc { get; }
    public DateTime EndDateUtc { get; }
    public string CountryCode { get; }
    public bool IsEmpty => EndDateUtc < StartDateUtc;

    public EconomicCalendarPageRequest ToPageRequest() => new()
    {
        StartDateUtc = StartDateUtc,
        EndDateUtc = EndDateUtc,
        CountryCodes = [CountryCode],
        PageSize = EconomicCalendarQueryLimits.MaximumPageSize
    };
}
