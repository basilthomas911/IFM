using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests;

public sealed class FinancialModelingPrepResilienceAndRegistrationTests
{
    [Fact]
    public async Task Transient_status_is_retried_for_safe_get()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        options.MaximumRetryAttempts = 1;
        try
        {
            var attempts = 0;
            var handler = new RecordingHandler((_, _) =>
            {
                attempts++;
                return Task.FromResult(attempts == 1
                    ? RecordingHandler.Json("{}", HttpStatusCode.ServiceUnavailable)
                    : RecordingHandler.Json("[]"));
            });
            var provider = new FinancialModelingPrepTreasuryCurve(new HttpClient(handler), options);

            var rows = await provider.GetRangeAsync(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 14));

            Assert.Empty(rows);
            Assert.Equal(2, attempts);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Rate_limit_has_a_distinct_failure_type()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            var handler = new RecordingHandler((_, _) => Task.FromResult(
                RecordingHandler.Json("{}", HttpStatusCode.TooManyRequests)));
            var provider = new FinancialModelingPrepTreasuryCurve(new HttpClient(handler), options);

            await Assert.ThrowsAsync<FinancialModelingPrepRateLimitException>(() =>
                provider.GetRangeAsync(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 14)));
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }

    [Fact]
    public async Task Malformed_json_has_a_distinct_contract_failure()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            var handler = new RecordingHandler((_, _) => Task.FromResult(RecordingHandler.Json("not-json")));
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
    public void Registration_exposes_both_provider_neutral_contracts_as_singletons()
    {
        var options = FmpTestOptions.Create(out var environmentVariable);
        try
        {
            var services = new ServiceCollection();
            services.AddFinancialModelingPrepMarketData(configured =>
            {
                configured.ApiKeyEnvironmentVariable = options.ApiKeyEnvironmentVariable;
                configured.MaximumRetryAttempts = 0;
            });
            using var provider = services.BuildServiceProvider();

            Assert.Same(
                provider.GetRequiredService<ITreasuryCurve>(),
                provider.GetRequiredService<ITreasuryCurve>());
            Assert.Same(
                provider.GetRequiredService<IEconomicCalendar>(),
                provider.GetRequiredService<IEconomicCalendar>());
            Assert.IsType<FinancialModelingPrepTreasuryCurve>(provider.GetRequiredService<ITreasuryCurve>());
            Assert.IsType<FinancialModelingPrepEconomicCalendar>(provider.GetRequiredService<IEconomicCalendar>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }
    }
}
