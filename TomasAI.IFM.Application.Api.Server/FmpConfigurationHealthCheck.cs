using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

namespace TomasAI.IFM.Application.Api.Server;

public sealed class FmpConfigurationHealthCheck(FinancialModelingPrepOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("FMP market data is disabled."));

        var configured = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(options.ApiKeyEnvironmentVariable));
        return Task.FromResult(configured
            ? HealthCheckResult.Healthy("FMP credentials are configured.")
            : HealthCheckResult.Unhealthy(
                $"FMP environment variable '{options.ApiKeyEnvironmentVariable}' is not set."));
    }
}
