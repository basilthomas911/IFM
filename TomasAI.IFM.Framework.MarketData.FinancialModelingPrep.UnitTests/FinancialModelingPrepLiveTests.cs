namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests;

public sealed class FinancialModelingPrepLiveTests
{
    [FmpLiveFact]
    [Trait("Category", "Live")]
    public async Task Treasury_endpoint_matches_the_normalized_contract()
    {
        var provider = new FinancialModelingPrepTreasuryCurve(
            new HttpClient(),
            new FinancialModelingPrepOptions());
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await provider.GetLatestAsync(todayUtc);

        Assert.NotNull(result);
        Assert.True(result.ValueDate <= todayUtc);
        Assert.Equal(12, result.Rates.Count);
    }

    [FmpLiveFact]
    [Trait("Category", "Live")]
    public async Task Economic_calendar_endpoint_matches_the_normalized_contract()
    {
        var provider = new FinancialModelingPrepEconomicCalendar(
            new HttpClient(),
            new FinancialModelingPrepOptions());
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromInclusive = todayUtc.AddDays(-7);

        var results = await provider.GetAsync(fromInclusive, todayUtc);

        Assert.NotEmpty(results);
        Assert.All(results, entry =>
        {
            var eventDate = DateOnly.FromDateTime(entry.EventTimeUtc.UtcDateTime);
            Assert.InRange(eventDate, fromInclusive, todayUtc);
            Assert.Equal(TimeSpan.Zero, entry.EventTimeUtc.Offset);
            Assert.False(string.IsNullOrWhiteSpace(entry.CountryCode));
            Assert.False(string.IsNullOrWhiteSpace(entry.EventName));
        });
    }
}

public sealed class FmpLiveFactAttribute : FactAttribute
{
    public FmpLiveFactAttribute()
    {
        var explicitlyEnabled = string.Equals(
            Environment.GetEnvironmentVariable("IFM_FMP_LIVE_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var hasCredential = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(FinancialModelingPrepOptions.DefaultApiKeyEnvironmentVariable));

        if (!explicitlyEnabled || !hasCredential)
        {
            Skip = "Set IFM_FMP_LIVE_TESTS=true and FMP_API_KEY to run the opt-in FMP live contract tests.";
        }
    }
}
