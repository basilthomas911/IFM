using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Domain.Portfolio.Operations;

namespace TomasAI.IFM.Application.Api.Server;

public sealed class PortfolioOperationalHealthCheck(IPortfolioOperationalGuard guard) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = guard.Options;
        IReadOnlyDictionary<string, object> data = new Dictionary<string, object>
        {
            ["enabled"] = options.Enabled,
            ["queriesEnabled"] = options.QueriesEnabled,
            ["mutationsEnabled"] = options.MutationsEnabled,
            ["authorizationRequired"] = options.AuthorizationRequired,
        };
        var description = options.Enabled
            ? "Portfolio operational controls are configured."
            : "Portfolio is intentionally disabled by the operator rollback switch.";
        return Task.FromResult(HealthCheckResult.Healthy(description, data));
    }
}
