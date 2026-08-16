using System.Net;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests;

public sealed class FinancialModelingPrepTreasuryCurveTests
{
    private const string CompleteRow = """
        {
          "date":"2026-08-14",
          "month1":4.31,"month2":4.32,"month3":4.33,"month6":4.34,
          "year1":4.35,"year2":4.36,"year3":4.37,"year5":4.38,
          "year7":4.39,"year10":4.40,"year20":4.41,"year30":4.42
        }
        """;

    [Fact]
    public async Task GetRange_maps_every_tenor_and_keeps_secret_out_of_uri()
    {
        const string secret = "never-put-this-in-a-uri";
        var options = FmpTestOptions.Create(out var environmentVariable, secret);
        try
        {
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json($"[{CompleteRow}]")));
            var provider = new FinancialModelingPrepTreasuryCurve(
                new HttpClient(handler),
                options,
                new FixedTimeProvider(new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero)));

            var rows = await provider.GetRangeAsync(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 14));

            var row = Assert.Single(rows);
            Assert.Equal(12, row.Rates.Count);
            Assert.Equal(4.31m, row.Rates.Single(rate => rate.Tenor == TreasuryTenor.OneMonth).RatePercent);
            Assert.Equal(4.42m, row.Rates.Single(rate => rate.Tenor == TreasuryTenor.ThirtyYear).RatePercent);
            Assert.Equal("US", row.CountryCode);
            Assert.Equal("USD", row.CurrencyCode);
            Assert.Equal("FinancialModelingPrep", row.Source);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(secret, request.ApiKeyHeader);
            Assert.DoesNotContain(secret, request.Uri, StringComparison.Ordinal);
            Assert.DoesNotContain("apikey", request.Uri, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("from=2026-08-14&to=2026-08-14", request.Uri, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task GetLatest_returns_newest_curve_not_after_as_of_date()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            var older = CompleteRow.Replace("2026-08-14", "2026-08-12", StringComparison.Ordinal);
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json($"[{CompleteRow},{older}]")));
            var provider = new FinancialModelingPrepTreasuryCurve(new HttpClient(handler), options);

            var result = await provider.GetLatestAsync(new DateOnly(2026, 8, 14));

            Assert.NotNull(result);
            Assert.Equal(new DateOnly(2026, 8, 14), result.ValueDate);
            Assert.EndsWith("to=2026-08-14", Assert.Single(handler.Requests).Uri, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task GetRange_chunks_and_deduplicates_identical_rows()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        options.MaximumProviderWindowDays = 2;
        try
        {
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json($"[{CompleteRow}]")));
            var provider = new FinancialModelingPrepTreasuryCurve(new HttpClient(handler), options);

            var rows = await provider.GetRangeAsync(new DateOnly(2026, 8, 13), new DateOnly(2026, 8, 15));

            Assert.Single(rows);
            Assert.Equal(2, handler.Requests.Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Missing_treasury_maturity_fails_instead_of_becoming_zero()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            var incomplete = CompleteRow.Replace("\"month2\":4.32,", string.Empty, StringComparison.Ordinal);
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json($"[{incomplete}]")));
            var provider = new FinancialModelingPrepTreasuryCurve(new HttpClient(handler), options);

            var exception = await Assert.ThrowsAsync<FinancialModelingPrepContractException>(() =>
                provider.GetRangeAsync(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 14)));

            Assert.Contains("month2", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Authentication_failure_is_typed_and_redacts_secret()
    {
        const string secret = "redact-me";
        var options = FmpTestOptions.Create(out var environmentVariable, secret);
        try
        {
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json("{}", HttpStatusCode.Unauthorized)));
            var provider = new FinancialModelingPrepTreasuryCurve(new HttpClient(handler), options);

            var exception = await Assert.ThrowsAsync<FinancialModelingPrepAuthenticationException>(() =>
                provider.GetRangeAsync(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 14)));

            Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(secret, Assert.Single(handler.Requests).Uri, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Oversized_response_fails_without_deserializing()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        options.MaximumResponseBytes = 16;
        try
        {
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json($"[{CompleteRow}]")));
            var provider = new FinancialModelingPrepTreasuryCurve(new HttpClient(handler), options);

            await Assert.ThrowsAsync<FinancialModelingPrepResponseTooLargeException>(() =>
                provider.GetRangeAsync(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 14)));
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Caller_cancellation_is_preserved()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            var handler = new RecordingHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return RecordingHandler.Json("[]");
            });
            var provider = new FinancialModelingPrepTreasuryCurve(new HttpClient(handler), options);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                provider.GetRangeAsync(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 14), cancellation.Token));
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }
}
