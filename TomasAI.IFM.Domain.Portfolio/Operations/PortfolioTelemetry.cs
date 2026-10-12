using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Operations;

public static class PortfolioTelemetry
{
    public const string InstrumentationName = "TomasAI.IFM.Domain.Portfolio";
    public static readonly ActivitySource ActivitySource = new(InstrumentationName);
    public static readonly Meter Meter = new(InstrumentationName);
    public static readonly Counter<long> AuthorizationChecks = Meter.CreateCounter<long>("portfolio.authorization.checks");
    public static readonly Counter<long> CommandOutcomes = Meter.CreateCounter<long>("portfolio.command.outcomes");
    public static readonly Histogram<double> QueryDuration = Meter.CreateHistogram<double>("portfolio.query.duration", "ms");

    public static Activity? StartRequest(string kind, string verb, Guid correlationId, ICommand? command = null)
    {
        var activity = ActivitySource.StartActivity($"portfolio.{kind}", ActivityKind.Consumer);
        activity?.SetTag("portfolio.operation", verb);
        activity?.SetTag("correlation.id", correlationId.ToString("N"));
        if (command is not null)
        {
            activity?.SetTag("command.id", command.CommandId.ToString("N"));
            activity?.SetTag("portfolio.entity", command.Subject.EntityId);
        }
        return activity;
    }
}
