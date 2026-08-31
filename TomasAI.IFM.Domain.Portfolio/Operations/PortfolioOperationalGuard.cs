using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Operations;

public sealed class PortfolioOperationalOptions
{
    public const string SectionName = "Portfolio:Operations";
    public bool Enabled { get; init; } = true;
    public bool QueriesEnabled { get; init; } = true;
    public bool MutationsEnabled { get; init; } = true;
    public bool AuthorizationRequired { get; init; } = true;

    public PortfolioOperationalOptions Validate()
    {
        if (Enabled && !QueriesEnabled && !MutationsEnabled)
            throw new InvalidOperationException("At least one Portfolio operational path must be enabled when Portfolio is enabled.");
        return this;
    }
}

public interface IPortfolioOperationalGuard
{
    PortfolioAccessContext Demand(PortfolioOperation operation, IPortfolioRequestMetadata request, bool mutation);
    PortfolioOperationalOptions Options { get; }
}

public sealed class PortfolioOperationalGuard(PortfolioOperationalOptions options) : IPortfolioOperationalGuard
{
    public PortfolioOperationalOptions Options { get; } = (options ?? throw new ArgumentNullException(nameof(options))).Validate();

    public PortfolioAccessContext Demand(PortfolioOperation operation, IPortfolioRequestMetadata request, bool mutation)
    {
        ArgumentNullException.ThrowIfNull(request);
        var outcome = "allowed";
        try
        {
            if (!Options.Enabled || mutation && !Options.MutationsEnabled || !mutation && !Options.QueriesEnabled)
            {
                outcome = "disabled";
                throw new PortfolioOperationalException("The requested Portfolio path is disabled by the operator rollback switch.");
            }

            var access = request.Access ?? new PortfolioAccessContext();
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

public sealed class PortfolioAuthorizationException(string message) : UnauthorizedAccessException(message);
public sealed class PortfolioOperationalException(string message) : InvalidOperationException(message);

public static class PortfolioTelemetry
{
    public const string InstrumentationName = "TomasAI.IFM.Domain.Portfolio";
    public static readonly ActivitySource ActivitySource = new(InstrumentationName);
    public static readonly Meter Meter = new(InstrumentationName);
    public static readonly Counter<long> AuthorizationChecks = Meter.CreateCounter<long>("portfolio.authorization.checks");
    public static readonly Counter<long> CommandOutcomes = Meter.CreateCounter<long>("portfolio.command.outcomes");
    public static readonly Histogram<double> QueryDuration = Meter.CreateHistogram<double>("portfolio.query.duration", "ms");

    public static Activity? StartRequest(string kind, string verb, IPortfolioRequestMetadata request)
    {
        var activity = ActivitySource.StartActivity($"portfolio.{kind}", ActivityKind.Consumer);
        activity?.SetTag("portfolio.operation", verb);
        activity?.SetTag("correlation.id", request.CorrelationId.ToString("N"));
        if (request is ICommand command)
        {
            activity?.SetTag("command.id", command.CommandId.ToString("N"));
            activity?.SetTag("portfolio.entity", command.Subject.EntityId);
        }
        return activity;
    }
}
