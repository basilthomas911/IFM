using System.Net;
using System.Xml;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class UsTreasuryCurveTests(ITestOutputHelper output)
{
    static readonly DateOnly Date = new(2026, 9, 4);
    static string Row(string date = "2026-09-04", string rate = "3.85") =>
        $"<entry><content><m:properties><d:NEW_DATE>{date}T00:00:00</d:NEW_DATE><d:BC_1MONTH>{rate}</d:BC_1MONTH><d:BC_2MONTH>3.89</d:BC_2MONTH><d:BC_3MONTH>3.92</d:BC_3MONTH></m:properties></content></entry>";
    static string Feed(string rows) => "<feed xmlns=\"http://www.w3.org/2005/Atom\" xmlns:d=\"http://schemas.microsoft.com/ado/2007/08/dataservices\" xmlns:m=\"http://schemas.microsoft.com/ado/2007/08/dataservices/metadata\">" + rows + "</feed>";
    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Interlocked.Increment(ref Calls); return send(request, token); }
    }
    static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task Official_fields_keep_units_source_date_and_cached_retrieval_time()
    {
        var clock = new Clock();
        using var handler = new Handler((r, _) =>
        {
            Assert.Equal("home.treasury.gov", r.RequestUri!.Host);
            Assert.Contains("data=daily_treasury_yield_curve&field_tdr_date_value_month=202609", r.RequestUri.Query);
            return Task.FromResult(Response(Feed(Row())));
        });
        using var client = new HttpClient(handler); using var source = new UsTreasuryCurve(client, clock);
        var first = Assert.Single(await source.GetRangeAsync(Date, Date));
        clock.Now = clock.Now.AddSeconds(20);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => source.GetRangeAsync(Date, Date)));
        var again = Assert.Single(await source.GetRangeAsync(Date, Date));
        Assert.Equal(1, handler.Calls); Assert.Equal(first.RetrievedAtUtc, again.RetrievedAtUtc);
        Assert.Equal("USTreasury", first.Source); Assert.Equal(Date, first.ValueDate);
        Assert.Equal(3.85m, first.Rates[0].RatePercent); Assert.Equal(.0385m, first.Rates[0].DecimalRate);
        Assert.False(first.TryGetRate(TreasuryTenor.SixMonth, out _));
        var rate = source.GetContinuouslyCompoundedAnnualRate(first, TreasuryTenor.OneMonth, UsTreasuryCurve.ConversionPolicy);
        Assert.True(rate.Succeeded); Assert.Equal(2 * Math.Log(1 + .0385 / 2), rate.Value!.AnnualContinuousRate, 14);
        Assert.False(source.GetContinuouslyCompoundedAnnualRate(first, TreasuryTenor.OneMonth,
            UsTreasuryCurve.ConversionPolicy with { Source = "FinancialModelingPrep" }).Succeeded);
        clock.Now = clock.Now.AddMinutes(1);
        Assert.True(Assert.Single(await source.GetRangeAsync(Date, Date)).RetrievedAtUtc > first.RetrievedAtUtc);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("date")] [InlineData("number")] [InlineData("conflict")]
    [InlineData("field")] [InlineData("root")] [InlineData("pagination")]
    public async Task Malformed_or_ambiguous_payloads_are_rejected(string kind)
    {
        var xml = kind switch
        {
            "date" => Feed(Row("2026-08-31")), "number" => Feed(Row(rate: "NaN")),
            "conflict" => Feed(Row() + Row(rate: "4.00")),
            "field" => Feed(Row().Replace("</m:properties>", "<d:BC_1MONTH>4</d:BC_1MONTH></m:properties>")),
            "root" => "<html>Error</html>", _ => Feed("<link rel=\"next\" href=\"ignored\"/>" + Row())
        };
        using var handler = new Handler((_, _) => Task.FromResult(Response(xml)));
        using var client = new HttpClient(handler); using var source = new UsTreasuryCurve(client);
        await Assert.ThrowsAsync<InvalidDataException>(() => source.GetRangeAsync(Date, Date));
    }

    [Fact]
    public async Task Dtd_and_oversize_responses_are_rejected()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Response("<!DOCTYPE feed [<!ENTITY x 'unsafe'>]>" + Feed(Row()))));
        using var client = new HttpClient(handler); using var source = new UsTreasuryCurve(client);
        await Assert.ThrowsAsync<XmlException>(() => source.GetRangeAsync(Date, Date));
        using var largeHandler = new Handler((_, _) => Task.FromResult(Response(new string('x', 1_048_577))));
        using var largeClient = new HttpClient(largeHandler); using var large = new UsTreasuryCurve(largeClient);
        await Assert.ThrowsAsync<HttpRequestException>(() => large.GetRangeAsync(Date, Date));
    }

    [Fact]
    public async Task Null_is_missing_and_published_zero_is_a_real_rate()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Response(Feed(Row(rate: "0").Replace(
            "<d:BC_2MONTH>3.89</d:BC_2MONTH>", "<d:BC_2MONTH m:null=\"true\"/>")))));
        using var client = new HttpClient(handler); using var source = new UsTreasuryCurve(client);
        var row = Assert.Single(await source.GetRangeAsync(Date, Date));
        Assert.True(row.TryGetRate(TreasuryTenor.OneMonth, out var zero)); Assert.Equal(0m, zero.RatePercent);
        Assert.False(row.TryGetRate(TreasuryTenor.TwoMonth, out _));
        Assert.False(source.GetContinuouslyCompoundedAnnualRate(row, TreasuryTenor.TwoMonth, UsTreasuryCurve.ConversionPolicy).Succeeded);
    }

    [Fact]
    public async Task Cancellation_propagates_and_latest_does_not_read_future_dates()
    {
        using var handler = new Handler((r, _) => Task.FromResult(Response(Feed(r.RequestUri!.Query.EndsWith("202608")
            ? Row("2026-08-31") : Row() + Row("2026-09-08")))));
        using var client = new HttpClient(handler); using var source = new UsTreasuryCurve(client);
        Assert.Equal(Date, (await source.GetLatestAsync(Date))!.ValueDate);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.GetLatestAsync(Date, cancelled.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => source.GetRangeAsync(Date.AddDays(-366), Date));
    }

    [Theory]
    [InlineData("2026-09-07T23:00:00Z", "2026-09-04")]
    [InlineData("2026-09-08T21:59:59Z", "2026-09-04")]
    [InlineData("2026-09-08T22:00:00Z", "2026-09-08")]
    [InlineData("2026-11-02T22:59:59Z", "2026-10-30")]
    [InlineData("2026-11-02T23:00:00Z", "2026-11-02")]
    [InlineData("2026-07-03T23:00:00Z", "2026-07-02")]
    [InlineData("2026-04-03T23:00:00Z", "2026-04-03")]
    public void Publication_policy_handles_full_holidays_early_closes_and_dst(string at, string required)
        => Assert.Equal(DateOnly.Parse(required), UsTreasuryPublicationCalendar.Default2026.RequiredValueDate(DateTimeOffset.Parse(at)));

    [Fact]
    public async Task Outage_reuses_only_a_still_fresh_observation_and_fails_after_next_deadline()
    {
        var clock = new Clock(); var unavailable = false;
        using var handler = new Handler((r, _) => unavailable
            ? throw new HttpRequestException("offline")
            : Task.FromResult(Response(Feed(r.RequestUri!.Query.EndsWith("202608") ? Row("2026-08-31") : Row()))));
        using var client = new HttpClient(handler); using var source = new UsTreasuryCurve(client, clock);
        var pricing = new TreasuryPricingProvider(source, clock);
        var policy = UsTreasuryPublicationCalendar.Default2026;
        Assert.True((await pricing.GetAsync(clock.Now, 0, policy, UsTreasuryCurve.ConversionPolicy, default)).Succeeded);
        unavailable = true; clock.Now = clock.Now.AddMinutes(2);
        Assert.True((await pricing.GetAsync(clock.Now, 0, policy, UsTreasuryCurve.ConversionPolicy, default)).Succeeded);
        clock.Now = new(2026, 9, 8, 22, 0, 0, TimeSpan.Zero);
        Assert.Equal("TreasuryStale", (await pricing.GetAsync(clock.Now, 0, policy, UsTreasuryCurve.ConversionPolicy, default)).Error);
        Assert.Throws<ArgumentException>(() => policy.RequiredValueDate(new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [LiveTreasuryFact]
    public async Task Live_official_feed_has_qualified_short_tenors()
    {
        using var client = new HttpClient(); using var source = new UsTreasuryCurve(client);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
        var row = await source.GetLatestAsync(today); Assert.NotNull(row);
        Assert.Equal("USTreasury", row.Source); Assert.Equal(12, row.Rates.Count);
        var now = DateTimeOffset.UtcNow;
        Assert.True(row.ValueDate >= UsTreasuryPublicationCalendar.Default2026.RequiredValueDate(now));
        foreach (var tenor in new[] { TreasuryTenor.OneMonth, TreasuryTenor.TwoMonth, TreasuryTenor.ThreeMonth })
        {
            var rate = source.GetContinuouslyCompoundedAnnualRate(row, tenor, UsTreasuryCurve.ConversionPolicy);
            Assert.True(rate.Succeeded, rate.Error);
            output.WriteLine($"{row.Source} {row.ValueDate:yyyy-MM-dd} {tenor}: {rate.Value!.RatePercent}% -> {rate.Value.AnnualContinuousRate:R}; observed {row.RetrievedAtUtc:O}; digest {rate.Value.CurveDigest}");
        }
    }
}

public sealed class LiveTreasuryFactAttribute : FactAttribute
{
    public LiveTreasuryFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_LIVE_TREASURY") != "1") Skip = "Set IFM_LIVE_TREASURY=1 to verify the official public endpoint.";
    }
}
