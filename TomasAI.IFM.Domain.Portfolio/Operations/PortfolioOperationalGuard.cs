using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Operations;

public sealed class PortfolioOperationalGuard(PortfolioOperationalOptions options) : IPortfolioOperationalGuard
{
    public PortfolioOperationalOptions Options { get; } = (options ?? throw new ArgumentNullException(nameof(options))).Validate();

    public PortfolioAccessContext Demand(PortfolioOperation operation, PortfolioAccessContext access, bool mutation)
    {
        ArgumentNullException.ThrowIfNull(access);
        var outcome = "allowed";
        try
        {
            if (!Options.Enabled || mutation && !Options.MutationsEnabled || !mutation && !Options.QueriesEnabled)
            {
                outcome = "disabled";
                throw new PortfolioOperationalException("The requested Portfolio path is disabled by the operator rollback switch.");
            }


            if (Options.AuthorizationRequired)
            {
                if (string.IsNullOrWhiteSpace(access.Principal))
                {
                    outcome = "unauthorized";
                    throw new PortfolioAuthorizationException("An authenticated Portfolio principal is required.");
                }
                var roles = access.Roles
                    .Where(static role => !string.IsNullOrWhiteSpace(role))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!PortfolioOperationalPolicy.IsAuthorized(operation, roles))
                {
                    outcome = "unauthorized";
                    throw new PortfolioAuthorizationException($"Principal '{access.Principal}' is not authorized for Portfolio operation '{operation}'.");
                }
            }
            return access;
        }
        finally
        {
            PortfolioTelemetry.AuthorizationChecks.Add(1,
                new KeyValuePair<string, object?>("portfolio.operation", operation.ToString()),
                new KeyValuePair<string, object?>("portfolio.outcome", outcome));
        }
    }
}
