namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests;

public sealed class FinancialModelingPrepLiveTests
{
    [FmpLiveFact]
    [Trait("Category", "Live")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public async Task Treasury_endpoint_matches_the_normalized_contract()
    {
        var provider = new FinancialModelingPrepTreasuryCurve(
            new HttpClient(),
            new FinancialModelingPrepOptions());
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await (provider.GetLatestAsync(todayUtc)).ConfigureAwait(true);

        Assert.NotNull(result);
        Assert.True(result.ValueDate <= todayUtc);
        Assert.Equal(12, result.Rates.Count);
    }

    [FmpLiveFact]
    [Trait("Category", "Live")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public async Task Economic_calendar_endpoint_matches_the_normalized_contract()
    {
        var provider = new FinancialModelingPrepEconomicCalendar(
            new HttpClient(),
            new FinancialModelingPrepOptions());
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromInclusive = todayUtc.AddDays(-7);

        var results = await (provider.GetAsync(fromInclusive, todayUtc)).ConfigureAwait(true);

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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]

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
