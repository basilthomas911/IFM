namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests;

public sealed class FinancialModelingPrepEconomicCalendarTests
{
    [Fact]
    public async Task Get_maps_utc_nullable_and_supplemental_fields_and_filters_country()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            const string payload = """
                [
                  {
                    "date":"2026-08-14T08:30:00-04:00","country":" us ","event":" CPI Release ",
                    "actual":2.9,"estimate":"3.0%","previous":null,"impact":"High","unit":"%",
                    "change":-0.1,"changePercentage":"-3.33%"
                  },
                  {
                    "date":"2026-08-14T10:00:00Z","country":"CA","event":"Filtered Event",
                    "actual":"","estimate":null,"previous":"1.0"
                  }
                ]
                """;
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json(payload)));
            var provider = new FinancialModelingPrepEconomicCalendar(
                new HttpClient(handler),
                options,
                new FixedTimeProvider(new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero)));

            var rows = await provider.GetAsync(
                new DateOnly(2026, 8, 14),
                new DateOnly(2026, 8, 14),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "us" });

            var row = Assert.Single(rows);
            Assert.Equal(new DateTimeOffset(2026, 8, 14, 12, 30, 0, TimeSpan.Zero), row.EventTimeUtc);
            Assert.Equal("US", row.CountryCode);
            Assert.Equal("CPI Release", row.EventName);
            Assert.Equal("2.9", row.Actual);
            Assert.Equal("3.0%", row.Forecast);
            Assert.Null(row.Previous);
            Assert.Equal("High", row.Impact);
            Assert.Equal("%", row.Unit);
            Assert.Equal("-0.1", row.Change);
            Assert.Equal("-3.33%", row.ChangePercentage);
            Assert.Equal("FinancialModelingPrep", row.Source);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Invalid_country_filter_fails_before_http()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json("[]")));
            var provider = new FinancialModelingPrepEconomicCalendar(new HttpClient(handler), options);

            await Assert.ThrowsAsync<FinancialModelingPrepValidationException>(() =>
                provider.GetAsync(
                    new DateOnly(2026, 8, 14),
                    new DateOnly(2026, 8, 14),
                    new HashSet<string> { "United States" }));
            Assert.Empty(handler.Requests);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Conflicting_duplicate_logical_events_fail()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            const string payload = """
                [
                  {"date":"2026-08-14T12:30:00Z","country":"US","event":"CPI","actual":"2.9"},
                  {"date":"2026-08-14T12:30:00Z","country":"US","event":"CPI","actual":"3.0"}
                ]
                """;
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json(payload)));
            var provider = new FinancialModelingPrepEconomicCalendar(new HttpClient(handler), options);

            await Assert.ThrowsAsync<FinancialModelingPrepContractException>(() =>
                provider.GetAsync(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 14)));
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Inverted_range_fails_before_http()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json("[]")));
            var provider = new FinancialModelingPrepEconomicCalendar(new HttpClient(handler), options);

            await Assert.ThrowsAsync<FinancialModelingPrepValidationException>(() =>
                provider.GetAsync(new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 14)));
            Assert.Empty(handler.Requests);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }
}
